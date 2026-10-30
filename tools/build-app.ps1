# Bir Grapekit uygulamasını derler ve kurulum dosyalarını üretir.
#   powershell -File tools\build-app.ps1 -App caps-lock [-Version 1.1.0] [-Archs x64]
# Çıktı: dist\<id>\<Exe>-Setup-<sürüm>-<mimari>.exe ve dist\<id>\update.json (güncelleme bilgisi)
param(
    [Parameter(Mandatory)][string]$App,
    [string]$Version = "",
    [string[]]$Archs = @(),
    [string]$Tag = ""
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$appDir = Get-ChildItem (Join-Path $root 'apps') -Directory | Where-Object {
    (Test-Path (Join-Path $_.FullName 'app.json')) -and ((Get-Content (Join-Path $_.FullName 'app.json') -Raw -Encoding UTF8 | ConvertFrom-Json).id -eq $App)
} | Select-Object -First 1
if (-not $appDir) { throw "Uygulama bulunamadı: $App" }
$meta = Get-Content (Join-Path $appDir.FullName 'app.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$proj = Join-Path $appDir.FullName $meta.project
if (-not $Version) { $Version = ([xml](Get-Content $proj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
if (-not $Archs -or $Archs.Count -eq 0) { $Archs = $meta.archs }
if (-not $Tag) { $Tag = "$($meta.id)-v$Version" }
Write-Host "== $($meta.name.tr) $Version ($($Archs -join ', ')) ==" -ForegroundColor Cyan

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup bulunamadı: winget install JRSoftware.InnoSetup" }

$dist = Join-Path $root "dist\$($meta.id)"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory $dist | Out-Null
$brand = Join-Path $appDir.FullName 'brand'
$page = "https://grapekit.com/uygulama/$($meta.slug.tr)"
$files = [ordered]@{}

# Web arayüzlü uygulamalar: arayüzü dotnet'ten önce derle (MSBuild dosyaları değerlendirme anında görsün)
$web = Join-Path $appDir.FullName 'web'
if ($meta.webview2 -and (Test-Path (Join-Path $web 'package.json'))) {
    Write-Host "`n-- web arayüzü derleniyor" -ForegroundColor Yellow
    Push-Location $web
    try {
        if (-not (Test-Path 'node_modules')) { npm ci --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw "npm ci başarısız" } }
        npm run build; if ($LASTEXITCODE -ne 0) { throw "Arayüz derlenemedi" }
    } finally { Pop-Location }
}

# Ayrı dönüştürme motorları olan uygulamalar (Formaty): FFmpeg ve PDFium'u indir
if ($meta.engines) { & (Join-Path $PSScriptRoot 'get-engines.ps1') -Archs $Archs; if (-not $?) { throw "Motorlar indirilemedi" } }

foreach ($arch in $Archs) {
    Write-Host "`n-- ${arch}: derleniyor" -ForegroundColor Yellow
    $pub = Join-Path $dist "publish\$arch"
    dotnet publish $proj -c Release -r "win-$arch" --self-contained true `
        -p:PublishReadyToRun=true -p:Version=$Version -p:DebugType=none -p:DebugSymbols=false -o $pub
    if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız ($arch)" }

    Write-Host "-- ${arch}: kurulum dosyası" -ForegroundColor Yellow
    # İsteğe bağlı: WebView2 kullanan / tepsi uygulaması olmayan (masaüstü) uygulamalar
    $extra = @()
    if ($meta.webview2) { $extra += "/DWebView2=1" }
    if ($meta.kind -eq 'desktop') { $extra += "/DDesktopApp=1" }
    if ($meta.engines) { $extra += "/DLargeApp=1" }
    & $iscc /Q "/DAppId=$($meta.id)" "/DAppName=$($meta.name.tr)" "/DAppNameEn=$($meta.name.en)" "/DAppNameDe=$($meta.name.de)" `
        "/DAppExe=$($meta.exe)" "/DAppGuid={$($meta.guid)}" "/DAppMutex=$($meta.mutex)" "/DPageUrl=$page" `
        "/DAppIcon=$(Join-Path $brand 'app.ico')" "/DAppSmallImage=$(Join-Path $brand 'wizard-small.bmp')" "/DAppSmallImage2x=$(Join-Path $brand 'wizard-small-2x.bmp')" `
        "/DAppVersion=$Version" "/DArch=$arch" "/DSourceDir=$pub" "/DOutDir=$dist" @extra (Join-Path $root 'installer\grapekit.iss')
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup başarısız ($arch)" }
    $exe = Join-Path $dist "$($meta.exe)-Setup-$Version-$arch.exe"
    $files[$arch] = [ordered]@{
        url = "https://github.com/grape-kit/grapekit/releases/download/$Tag/" + (Split-Path $exe -Leaf)
        sha256 = (Get-FileHash $exe -Algorithm SHA256).Hash
        size = (Get-Item $exe).Length
    }
}
Remove-Item (Join-Path $dist 'publish') -Recurse -Force

$notesFile = Join-Path $appDir.FullName 'CHANGELOG.json'
$notes = if (Test-Path $notesFile) { (Get-Content $notesFile -Raw -Encoding UTF8 | ConvertFrom-Json).$Version } else { $null }
if (-not $notes) { $notes = [ordered]@{ tr = "Yenilikler ve iyileştirmeler."; en = "New features and improvements."; de = "Neue Funktionen und Verbesserungen." } }
$json = [ordered]@{ version = $Version; tag = $Tag; date = (Get-Date -Format "yyyy-MM-dd"); notes = $notes; files = $files }
[IO.File]::WriteAllText((Join-Path $dist 'update.json'), ($json | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding $false))

Write-Host "`nHazır:" -ForegroundColor Green
Get-ChildItem $dist -File | ForEach-Object { "{0,-48} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB) }
