# Caps Lock Göstergesi (Caps Lock Indicator)

Caps Lock, Num Lock ve Scroll Lock durumunu ekranda, seste ve sesli okumayla bildiren, pil dostu bir Windows uygulaması.
Windows 10 (1809+) ve Windows 11; x64, ARM64 ve x86.

[grapekit.com](https://grapekit.com/uygulama/caps-lock-gostergesi) · [English](https://grapekit.com/en/app/caps-lock-indicator)

## Özellikler

| Bölüm | Neler var |
|---|---|
| Genel | Üç tuşun canlı durumu, izlenecek tuşlar, duraklatma (15 dk / 1 saat / süresiz), Windows ile başlatma, verimlilik modu |
| Görünüm | Tema (sistem / açık / koyu), dil, canlı kart önizlemesi, renk, boyut, saydamlık, kalıcı rozet |
| Ekran bildirimi | 9 konumlu seçici, ekranda kalma süresi (0,5–10 sn) |
| Ses ve sesli okuma | 6 ses paketi veya kendi .wav dosyanız, ses düzeyi, Windows sesiyle sesli okuma |
| Yanlış basma koruması | Basılı tut / çift bas modları, "kapatmak için tek basış", ipucu kartı |
| Şifre uyarısı | Büyük harf açıkken şifre alanına girilince alanın altında uyarı |
| Kısayol tuşu | Ayarlanabilir genel kısayol (varsayılan Ctrl+Alt+C) ile duraklat / sürdür |
| Sessiz saatler | Zaman aralığı, yalnızca sesler veya tüm bildirimler, tam ekranda sessiz |
| İstatistikler | Günlük sayılar ve 14 günlük grafik |

## Performans ve pil

- Klavye kancası ayrı, yüksek öncelikli bir iş parçacığında, **olay tabanlı** çalışır; boştayken hiçbir zamanlayıcı yoktur.
- Animasyon zamanlayıcısı yalnızca kart görünürken çalışır.
- Şifre alanı sorgusu yalnızca büyük harf **açıkken** yapılır.
- Windows 11 "Verimlilik modu" (EcoQoS) desteklenir; klavye kancası bundan muaf tutulur.
- Ayarlar penceresi kapanınca bellek işletim sistemine geri verilir; ses iş parçacığı boşta kalınca kapanır.

## Derleme

```powershell
dotnet build -c Release
```

Kurulum dosyası ve yayın için depo kökündeki README'ye bakın (`tools/build-app.ps1 -App caps-lock`).

## Dosyalar

| Dosya | Görev |
|---|---|
| `KeyboardMonitor.cs` | Klavye kancası, yanlış basma koruması |
| `FocusWatcher.cs` | Şifre alanı algılama |
| `TrayApp.cs` | Durum, bildirim, tepsi, duraklatma, güncelleme |
| `SettingsForm.cs` | Ayarlar penceresi |
| `Osd.cs`, `Render.cs` | Ekran kartı, rozet, uyarı balonu, simgeler |
| `Sounds.cs`, `Speech.cs` | Ses paketleri, sesli okuma |
| `AppSettings.cs`, `Stats.cs`, `Loc.cs` | Ayarlar, istatistik, arayüz metinleri |
| `AutoStart.cs`, `Hotkey.cs` | Windows ile başlatma, kısayol |
