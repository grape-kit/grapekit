; Grapekit ortak kurulum betiği (Inno Setup 6). Tüm uygulamalar aynı sihirbazı kullanır.
; tools/build-app.ps1 tarafından uygulamanın app.json bilgileriyle çağrılır:
;   ISCC.exe /DAppId=caps-lock /DAppName="..." /DAppExe=CapsGosterge /DAppGuid={...} /DAppVersion=1.1.0 /DArch=x64 /DSourceDir=... /DOutDir=... grapekit.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef AppNameEn
  #define AppNameEn AppName
#endif
#ifndef AppNameDe
  #define AppNameDe AppNameEn
#endif
#ifndef AppMutex
  #define AppMutex AppExe + ".Tekil"
#endif

#define Publisher "Grapekit"
#define Website "https://grapekit.com"

[Setup]
AppId={{#AppGuid}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL={#Website}
AppSupportURL={#PageUrl}
AppUpdatesURL={#PageUrl}
AppCopyright=© 2026 Grapekit
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#Publisher}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
; Kullanıcı başına kurulum: yönetici izni gerekmez
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\Grapekit\{#AppExe}
DefaultGroupName=Grapekit
DisableProgramGroupPage=yes
DisableDirPage=auto
DisableWelcomePage=no
OutputDir={#OutDir}
OutputBaseFilename={#AppExe}-Setup-{#AppVersion}-{#Arch}
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\{#AppExe}.exe
UninstallDisplayName={#AppName}
WizardImageFile={#SourcePath}\wizard-large.bmp,{#SourcePath}\wizard-large-2x.bmp
WizardSmallImageFile={#AppSmallImage},{#AppSmallImage2x}
WizardImageStretch=yes
WizardImageBackColor=$330B1A
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
WizardStyle=modern
WizardSizePercent=110
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
AppMutex={#AppMutex}
ShowLanguageDialog=auto
#if Arch == "x64"
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible
#elif Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#endif

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german";  MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
turkish.LaunchApp={#AppName} uygulamasını başlat
english.LaunchApp=Start {#AppNameEn}
german.LaunchApp={#AppNameDe} starten
turkish.DesktopIcon=Masaüstü kısayolu oluştur
english.DesktopIcon=Create a desktop shortcut
german.DesktopIcon=Desktop-Verknüpfung erstellen

[Messages]
; Standart sihirbaz metinlerinin Grapekit sürümü
turkish.WelcomeLabel2=Bu sihirbaz bilgisayarınıza [name/ver] uygulamasını kuracak.%n%nGrapekit: Windows için küçük, kullanışlı ve ücretsiz araçlar.%n%nDevam etmeden önce diğer uygulamaları kapatmanız önerilir.
english.WelcomeLabel2=This will install [name/ver] on your computer.%n%nGrapekit: small, handy and free tools for Windows.%n%nIt is recommended that you close all other applications before continuing.
german.WelcomeLabel2=Dieser Assistent installiert [name/ver] auf Ihrem Computer.%n%nGrapekit: kleine, praktische und kostenlose Tools für Windows.%n%nBitte schließen Sie alle anderen Anwendungen, bevor Sie fortfahren.
turkish.FinishedLabel=[name] kuruldu. Uygulama sistem tepsisinde çalışır.%n%nGrapekit'i tercih ettiğiniz için teşekkürler!
english.FinishedLabel=[name] has been installed. The app runs in the system tray.%n%nThank you for choosing Grapekit!
german.FinishedLabel=[name] wurde installiert. Die App läuft im Infobereich.%n%nDanke, dass Sie Grapekit nutzen!

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}.exe"; Tasks: desktopicon

[Registry]
; Uygulama "Windows ile başlat" kaydını kendisi yönetir; kaldırırken temizle.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppExe}"; Flags: uninsdeletevalue dontcreatekey

[Run]
; Sessiz güncellemeden sonra programı arka planda yeniden başlat
; runasoriginaluser: kurulum yönetici izniyle çalışsa da program normal kullanıcı olarak başlar
Filename: "{app}\{#AppExe}.exe"; Parameters: "--autostart"; Flags: nowait skipifnotsilent runasoriginaluser
Filename: "{app}\{#AppExe}.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}.exe"; Flags: runhidden; RunOnceId: "StopApp"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\{#AppExe}"
