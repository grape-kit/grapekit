using System.Runtime.InteropServices;

namespace CapsGosterge;

/// <summary>
/// Kilit tuşlarını izler ve "yanlışlıkla basma korumasını" uygular.
/// Kendi iş parçacığında, olay tabanlı çalışır: boştayken hiç CPU kullanmaz,
/// arayüz meşgul olsa bile yazma gecikmesine yol açmaz.
/// Gizlilik: yalnızca Caps/Num/Scroll Lock tuşlarına bakılır; diğer tuşlar hiçbir şekilde işlenmez/kaydedilmez.
/// </summary>
internal sealed class KeyboardMonitor : IDisposable
{
    static readonly IntPtr Magic = new(0x43415053); // "CAPS" – kendi gönderdiğimiz tuşları tanımak için
    const uint MsgInject = Native.WM_APP + 1;

    readonly SynchronizationContext _ui;
    readonly Thread _thread;
    readonly Native.LowLevelProc _proc;
    readonly ManualResetEventSlim _ready = new();
    uint _threadId;
    IntPtr _hook;

    // Arayüzden ayarlanan değerler (kanca iş parçacığı okur)
    public volatile int Protection;
    public volatile int HoldMs = 500;
    public volatile int DoubleMs = 400;
    public volatile bool SinglePressTurnsOff = true;
    public volatile bool CapsOn;

    /// <summary>Bir kilit tuşu bırakıldı (durum değişmiş olabilir). Arayüz iş parçacığında çağrılır.</summary>
    public event Action<int>? LockKeyReleased;
    /// <summary>Koruma bir basışı engelledi: 1 = basılı tutulmadı, 2 = ikinci basış gelmedi.</summary>
    public event Action<int>? Blocked;

    // Kanca iş parçacığı durumu
    bool _capsDown, _swallowing, _fired;
    long _lastTap;
    IntPtr _holdTimer, _doubleTimer;

    public KeyboardMonitor(SynchronizationContext ui)
    {
        _ui = ui;
        _proc = HookProc;
        _thread = new Thread(Run) { IsBackground = true, Name = "Klavye kancası", Priority = ThreadPriority.Highest };
        _thread.Start();
        _ready.Wait(3000);
    }

    public bool IsActive => _hook != IntPtr.Zero;

    void Run()
    {
        _threadId = Native.GetCurrentThreadId();
        Native.ExemptCurrentThreadFromThrottling();
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        _ready.Set();

        while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == Native.WM_TIMER && msg.hwnd == IntPtr.Zero)
                OnTimer(msg.wParam);
            else if (msg.message == MsgInject)
                InjectCaps();
            else
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }
        }

        if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vk = Marshal.ReadInt32(lParam);
            if (vk is Native.VK_CAPITAL or Native.VK_NUMLOCK or Native.VK_SCROLL)
            {
                var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                int m = (int)wParam;
                bool down = m is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                bool ours = k.dwExtraInfo == Magic;

                if (vk == Native.VK_CAPITAL && !ours && Protection != 0 && HandleProtected(down))
                    return 1; // tuşu yut

                if (!down) _ui.Post(_ => LockKeyReleased?.Invoke(vk), null);
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <returns>true ise tuş yutulur (sisteme ulaşmaz)</returns>
    bool HandleProtected(bool down)
    {
        if (down)
        {
            if (_capsDown) return _swallowing; // basılı tutulurken gelen tekrarlar
            _capsDown = true;
            _fired = false;

            if (SinglePressTurnsOff && CapsOn) { _swallowing = false; return false; }
            _swallowing = true;

            if (Protection == 1)
            {
                _holdTimer = Native.SetTimer(IntPtr.Zero, IntPtr.Zero, (uint)HoldMs, IntPtr.Zero);
            }
            else
            {
                long now = Environment.TickCount64;
                if (_lastTap != 0 && now - _lastTap <= DoubleMs)
                {
                    _lastTap = 0;
                    _fired = true;
                    Kill(ref _doubleTimer);
                    Native.PostThreadMessage(_threadId, MsgInject, IntPtr.Zero, IntPtr.Zero);
                }
                else
                {
                    _lastTap = now;
                }
            }
            return true;
        }

        bool swallowed = _swallowing;
        _capsDown = false;
        _swallowing = false;
        if (!swallowed) return false;

        if (Protection == 1)
        {
            Kill(ref _holdTimer);
            if (!_fired) Notify(1);
        }
        else if (!_fired && _lastTap != 0)
        {
            // İkinci basış gelmezse kısa bir süre sonra ipucu göster
            Kill(ref _doubleTimer);
            _doubleTimer = Native.SetTimer(IntPtr.Zero, IntPtr.Zero, (uint)DoubleMs, IntPtr.Zero);
        }
        return true;
    }

    void OnTimer(IntPtr id)
    {
        if (id == _holdTimer)
        {
            Kill(ref _holdTimer);
            if (_capsDown && _swallowing && !_fired)
            {
                _fired = true;
                InjectCaps();
            }
        }
        else if (id == _doubleTimer)
        {
            Kill(ref _doubleTimer);
            if (_lastTap != 0 && !_capsDown)
            {
                _lastTap = 0;
                Notify(2);
            }
        }
        else
        {
            Native.KillTimer(IntPtr.Zero, id);
        }
    }

    static void Kill(ref IntPtr timer)
    {
        if (timer != IntPtr.Zero) Native.KillTimer(IntPtr.Zero, timer);
        timer = IntPtr.Zero;
    }

    void Notify(int kind) => _ui.Post(_ => Blocked?.Invoke(kind), null);

    static void InjectCaps()
    {
        var inputs = new Native.INPUT[2];
        for (int i = 0; i < 2; i++)
        {
            inputs[i].type = Native.INPUT_KEYBOARD;
            inputs[i].u.ki = new Native.KEYBDINPUT
            {
                wVk = Native.VK_CAPITAL,
                wScan = 0x3A,
                dwFlags = i == 0 ? 0 : Native.KEYEVENTF_KEYUP,
                dwExtraInfo = Magic,
            };
        }
        Native.SendInput(2, inputs, Marshal.SizeOf<Native.INPUT>());
    }

    public void Dispose()
    {
        if (_threadId != 0) Native.PostThreadMessage(_threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _ready.Dispose();
    }
}
