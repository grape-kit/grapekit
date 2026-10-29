# Bir Grapekit uygulamasının Microsoft Store paketini (.msixupload) üretir.
#   powershell -File tools\build-msix.ps1 -App keep-awake [-Version 1.0.1] [-Archs x64,arm64] [-TestSign]
# Gerekenler: Windows SDK (makeappx, makepri), Node (görseller için, tools\node_modules).
# Kimlik bilgileri app.json "store" bölümünden okunur (Partner Center > Ürün > Ürün kimliği).
# Çıktı: dist\<id>\store\<Exe>_<sürüm>.msixupload  →  Partner Center > Gönderim > Paketler'e sürükleyin.
param(
    [Parameter(Mandatory)][string]$App,
    [string]$Version = "",
    [string[]]$Archs = @(),
    # Partner Center'daki kimlik henüz yoksa deneme için geçici kimlik (ör. Grapekit.Deneme)
    [string]$Identity = "",
    # Yerelde kurup denemek için kendinden imzalı sertifikayla imzala (Store'a gönderirken gerekmez)
    [switch]$TestSign
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$appDir = Get-ChildItem (Join-Path $root 'apps') -Directory | Where-Object {
    (Test-Path (Join-Path $_.FullName 'app.json')) -and ((Get-Content (Join-Path $_.FullName 'app.json') -Raw -Encoding UTF8 | ConvertFrom-Json).id -eq $App)
} | Select-Object -First 1
if (-not $appDir) { throw "Uygulama bulunamadı: $App" }
$meta = Get-Content (Join-Path $appDir.FullName 'app.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($Identity) { $meta.store.identity = $Identity }
if (-not $meta.store -or -not $meta.store.identity -or $meta.store.identity -like '*DOLDUR*') {
    throw "app.json içindeki store.identity / store.publisher değerlerini Partner Center > Ürün kimliği sayfasından doldurun."
}
$proj = Join-Path $appDir.FullName $meta.project
if (-not $Version) { $Version = ([xml](Get-Content $proj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
if (-not $Archs -or $Archs.Count -eq 0) { $Archs = $meta.archs }
Write-Host "== $($meta.name.tr) $Version — Microsoft Store paketi ($($Archs -join ', ')) ==" -ForegroundColor Cyan

$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } | Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdkBin) { throw "Windows SDK bulunamadı (makeappx.exe): winget install Microsoft.WindowsSDK.10.0.26100" }
$makeappx = Join-Path $sdkBin.FullName 'x64\makeappx.exe'
$makepri = Join-Path $sdkBin.FullName 'x64\makepri.exe'
$signtool = Join-Path $sdkBin.FullName 'x64\signtool.exe'

# Görseller (her seferinde simgeden yeniden üretilir)
$assets = Join-Path $appDir.FullName 'packaging\Assets'
& node (Join-Path $PSScriptRoot 'make-store-assets.mjs') (Join-Path $appDir.FullName 'brand') $assets
if ($LASTEXITCODE -ne 0) { throw "Store görselleri üretilemedi" }

$out = Join-Path $root "dist\$($meta.id)\store"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory $out | Out-Null
$msixVer = "$Version.0"
$template = Get-Content (Join-Path $root 'installer\AppxManifest.xml') -Raw -Encoding UTF8
# Uzantılar: tepsi uygulamalarında "Windows ile başlat"; dosya türü olanlarda ilişkilendirme ve Gezgin komutları
$ext = @()
if ($meta.kind -ne 'desktop') {
    $ext += '        <desktop:Extension Category="windows.startupTask" Executable="$EXE$.exe" EntryPoint="Windows.FullTrustApplication">'
    $ext += '          <desktop:StartupTask TaskId="$EXE$Startup" Enabled="true" DisplayName="$NAME$" />'
    $ext += '        </desktop:Extension>'
}
if ($meta.fileTypes) {
    $types = ($meta.fileTypes | ForEach-Object { "              <uap:FileType>$_</uap:FileType>" }) -join "`n"
    $ext += '        <uap3:Extension Category="windows.fileTypeAssociation">'
    $ext += '          <uap3:FileTypeAssociation Name="markdown">'
    $ext += '            <uap:DisplayName>Markdown</uap:DisplayName>'
    $ext += '            <uap:Logo>Assets\StoreLogo.png</uap:Logo>'
    $ext += '            <uap:SupportedFileTypes>'
    $ext += $types
    $ext += '            </uap:SupportedFileTypes>'
    $ext += '            <uap2:SupportedVerbs>'
    $ext += '              <uap3:Verb Id="open" Parameters="&quot;%1&quot;">Open</uap3:Verb>'
    $ext += '              <uap3:Verb Id="topdf" Parameters="--to-pdf &quot;%1&quot;">Convert to PDF</uap3:Verb>'
    $ext += '            </uap2:SupportedVerbs>'
    $ext += '          </uap3:FileTypeAssociation>'
    $ext += '        </uap3:Extension>'
}
$template = $template.Replace('$EXTENSIONS$', ($ext -join "`n"))
$esc = { param($s) [Security.SecurityElement]::Escape([string]$s) }
$packages = @()

foreach ($arch in $Archs) {
    Write-Host "`n-- ${arch}: derleniyor" -ForegroundColor Yellow
    $layout = Join-Path $out "layout\$arch"
    dotnet publish $proj -c Release -r "win-$arch" --self-contained true `
        -p:PublishReadyToRun=true -p:Version=$Version -p:DebugType=none -p:DebugSymbols=false -o $layout
    if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız ($arch)" }
    Copy-Item $assets (Join-Path $layout 'Assets') -Recurse -Force

    $manifest = $template.Replace('$IDENTITY$', $meta.store.identity).Replace('$PUBLISHER$', $meta.store.publisher).
        Replace('$PUBLISHER_NAME$', (& $esc $meta.store.publisherName)).Replace('$VERSION$', $msixVer).Replace('$ARCH$', $arch).
        Replace('$NAME$', (& $esc $meta.store.displayName)).Replace('$DESCRIPTION$', (& $esc $meta.store.description)).Replace('$EXE$', $meta.exe)
    [IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

    $cfg = Join-Path $out "priconfig-$arch.xml"
    & $makepri createconfig /cf $cfg /dq tr-TR_en-US /pv 10.0.0 /o | Out-Null
    & $makepri new /pr $layout /cf $cfg /of (Join-Path $layout 'resources.pri') /mn (Join-Path $layout 'AppxManifest.xml') /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri başarısız ($arch)" }

    $msix = Join-Path $out "bundle\$($meta.exe)_$($Version)_$arch.msix"
    New-Item -ItemType Directory -Force (Split-Path $msix) | Out-Null
    & $makeappx pack /d $layout /p $msix /o /h SHA256 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makeappx başarısız ($arch)" }
    $packages += $msix
}

Write-Host "`n-- Paket grubu (msixbundle) ve Store yükleme dosyası (msixupload)" -ForegroundColor Yellow
$bundle = Join-Path $out "$($meta.exe)_$Version.msixbundle"
& $makeappx bundle /d (Join-Path $out 'bundle') /p $bundle /bv $msixVer /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle başarısız" }

if ($TestSign) {
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $meta.store.publisher } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $meta.store.publisher -KeyUsage DigitalSignature `
            -FriendlyName "Grapekit MSIX test" -CertStoreLocation Cert:\CurrentUser\My `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    }
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $bundle | Out-Null
    Export-Certificate -Cert $cert -FilePath (Join-Path $out 'test-sertifika.cer') | Out-Null
    Write-Host "Test imzası atıldı. Kurmadan önce test-sertifika.cer dosyasını yönetici olarak 'Güvenilen Kişiler' deposuna ekleyin."
}

# .msixupload = msixbundle'ı içeren zip (Partner Center bu biçimi önerir)
$upload = Join-Path $out "$($meta.exe)_$Version.msixupload"
Compress-Archive -Path $bundle -DestinationPath "$upload.zip" -Force
Move-Item "$upload.zip" $upload -Force

Remove-Item (Join-Path $out 'layout'), (Join-Path $out 'bundle') -Recurse -Force
Get-ChildItem $out -Filter 'priconfig-*.xml' | Remove-Item
Write-Host "`nHazır:" -ForegroundColor Green
Get-ChildItem $out -File | ForEach-Object { "{0,-48} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB) }
