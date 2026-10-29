using System.Runtime.InteropServices;
using System.Text;

namespace CapsGosterge;

/// <summary>
/// Odaklanılan alanın bir şifre alanı olup olmadığını izler.
/// Pil dostu: yalnızca büyük harf AÇIKKEN çalışır (odak olayları + kısa aralıklı kontrol);
/// büyük harf kapalıyken hiçbir sorgu yapılmaz.
/// Gizlilik: alanın içeriği asla okunmaz, sadece "şifre alanı mı" ve ekrandaki konumu öğrenilir.
///
/// Tarayıcılar (Chrome, Edge, Firefox) sayfa içi alanları yalnızca UI Automation ile bildirdiği için
/// öncelikle UI Automation kullanılır; klasik Win32 kutuları ve MSAA yedek olarak kontrol edilir.
/// </summary>
internal sealed class FocusWatcher : IDisposable
{
    const uint MsgArm = Native.WM_APP + 1;
    const uint PollMs = 400;

    readonly SynchronizationContext _ui;
    readonly Thread _thread;
    readonly Native.WinEventProc _proc;
    readonly ManualResetEventSlim _ready = new();
    uint _threadId;
    IntPtr _hook, _hookFg, _pollTimer;
    IUIAutomation? _uia;

    volatile bool _armed;
    IntPtr _hwnd;
    int _obj, _child;
    Rectangle? _last;

    /// <summary>Şifre alanına girildi (alanın ekran konumuyla) ya da çıkıldı (null).</summary>
    public event Action<Rectangle?>? PasswordFocusChanged;

    public FocusWatcher(SynchronizationContext ui)
    {
        _ui = ui;
        _proc = OnWinEvent;
        _thread = new Thread(Run) { IsBackground = true, Name = "Odak izleyici" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait(3000);
    }

    /// <summary>Büyük harf açıkken true: yalnızca o zaman alan sorgulanır.</summary>
    public void SetArmed(bool armed)
    {
        _armed = armed;
        if (_threadId != 0) Native.PostThreadMessage(_threadId, MsgArm, IntPtr.Zero, IntPtr.Zero);
    }

    void Run()
    {
        _threadId = Native.GetCurrentThreadId();
        _ready.Set();

        while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == MsgArm) OnArmChanged();
            else if (msg.message == Native.WM_TIMER && msg.hwnd == IntPtr.Zero && msg.wParam == _pollTimer) Evaluate();
            else
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }
        }
        Disarm();
        if (_uia != null) Marshal.ReleaseComObject(_uia);
    }

    void OnArmChanged()
    {
        if (_armed)
        {
            // Olay kancası ve kısa aralıklı kontrol yalnızca büyük harf açıkken var
            if (_hook == IntPtr.Zero)
                _hook = Native.SetWinEventHook(Native.EVENT_OBJECT_FOCUS, Native.EVENT_OBJECT_FOCUS, IntPtr.Zero, _proc,
                    0, 0, Native.WINEVENT_OUTOFCONTEXT);
            if (_hookFg == IntPtr.Zero)
                _hookFg = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc,
                    0, 0, Native.WINEVENT_OUTOFCONTEXT);
            if (_pollTimer == IntPtr.Zero)
                _pollTimer = Native.SetTimer(IntPtr.Zero, IntPtr.Zero, PollMs, IntPtr.Zero);
            Evaluate();
        }
        else
        {
            Disarm();
            Report(null);
        }
    }

    void Disarm()
    {
        if (_hook != IntPtr.Zero) Native.UnhookWinEvent(_hook);
        if (_hookFg != IntPtr.Zero) Native.UnhookWinEvent(_hookFg);
        _hookFg = IntPtr.Zero;
        if (_pollTimer != IntPtr.Zero) Native.KillTimer(IntPtr.Zero, _pollTimer);
        _hook = IntPtr.Zero;
        _pollTimer = IntPtr.Zero;
    }

    void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (evt != Native.EVENT_OBJECT_FOCUS && evt != Native.EVENT_SYSTEM_FOREGROUND) return;
        if (evt == Native.EVENT_OBJECT_FOCUS) { _hwnd = hwnd; _obj = idObject; _child = idChild; }
        if (_armed) Evaluate();
    }

    void Evaluate() => Report(_armed ? Query() : null);

    /// <summary>Yalnızca durum ya da konum değişince arayüze bildir.</summary>
    void Report(Rectangle? rect)
    {
        if (rect == _last) return;
        _last = rect;
        _ui.Post(_ => PasswordFocusChanged?.Invoke(rect), null);
    }

    Rectangle? Query() => QueryUia() ?? QueryWin32() ?? QueryMsaa();

    // ── UI Automation (tarayıcılar, modern uygulamalar, Win32)

    const int UIA_BoundingRectanglePropertyId = 30001, UIA_IsPasswordPropertyId = 30019;

    Rectangle? QueryUia()
    {
        try
        {
            _uia ??= (IUIAutomation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("ff48dba4-60ef-4201-aa87-54103eef594e"))!)!;
            if (_uia.GetFocusedElement(out var el) != 0 || el == null) return null;
            try
            {
                if (el.GetCurrentPropertyValue(UIA_IsPasswordPropertyId, out var pw) != 0 || pw is not true) return null;
                if (el.GetCurrentPropertyValue(UIA_BoundingRectanglePropertyId, out var r) == 0 && r is double[] { Length: 4 } d && d[2] > 0)
                    return new Rectangle((int)d[0], (int)d[1], (int)d[2], (int)d[3]);
                return null;
            }
            finally { Marshal.ReleaseComObject(el); }
        }
        catch { return null; }
    }

    // ── Klasik Win32 şifre kutusu

    Rectangle? QueryWin32()
    {
        if (_hwnd == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(64);
            Native.GetClassName(_hwnd, sb, sb.Capacity);
            if (sb.ToString().Contains("Edit", StringComparison.OrdinalIgnoreCase)
                && (Native.GetWindowLong(_hwnd, Native.GWL_STYLE) & Native.ES_PASSWORD) != 0
                && Native.GetWindowRect(_hwnd, out var wr))
                return Rectangle.FromLTRB(wr.Left, wr.Top, wr.Right, wr.Bottom);
        }
        catch { }
        return null;
    }

    // ── MSAA (eski uygulamalar için yedek)

    Rectangle? QueryMsaa()
    {
        if (_hwnd == IntPtr.Zero) return null;
        try
        {
            if (Native.AccessibleObjectFromEvent(_hwnd, _obj, _child, out var acc, out var child) != 0 || acc == null)
                return null;
            try
            {
                if (acc.get_accState(child) is int state && (state & Native.STATE_SYSTEM_PROTECTED) != 0)
                {
                    acc.accLocation(out int l, out int t, out int w, out int h, child);
                    if (w > 0 && h > 0) return new Rectangle(l, t, w, h);
                }
            }
            finally
            {
                if (Marshal.IsComObject(acc)) Marshal.ReleaseComObject(acc);
            }
        }
        catch { }
        return null;
    }

    public void Dispose()
    {
        if (_threadId != 0) Native.PostThreadMessage(_threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _ready.Dispose();
    }

    // ── UI Automation COM arayüzleri (yalnızca kullanılan yöntemler; sıra vtable düzenine uyar)

    [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IUIAutomation
    {
        void _CompareElements();
        void _CompareRuntimeIds();
        void _GetRootElement();
        void _ElementFromHandle();
        void _ElementFromPoint();
        [PreserveSig] int GetFocusedElement(out IUIAutomationElement? element);
    }

    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IUIAutomationElement
    {
        void _SetFocus();
        void _GetRuntimeId();
        void _FindFirst();
        void _FindAll();
        void _FindFirstBuildCache();
        void _FindAllBuildCache();
        void _BuildUpdatedCache();
        [PreserveSig] int GetCurrentPropertyValue(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object? retVal);
    }
}
