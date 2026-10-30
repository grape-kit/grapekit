# Formaty motorlarını indirir: apps\Formaty\engine-bin\<mimari>\ (uygulama çıktısında engines\ klasörü olur)
#   FFmpeg (LGPL): ffmpeg.exe, ffprobe.exe ve DLL'ler — BtbN/FFmpeg-Builds 'lgpl-shared' (GPL bileşen içermez), ffplay atılır
#   PDFium (Apache-2/BSD): pdfium.dll — bblanchon/pdfium-binaries
#   powershell -File tools\get-engines.ps1 [-Archs x64,arm64] [-Force]
param([string[]]$Archs = @('x64', 'arm64'), [switch]$Force)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$base = Join-Path $root 'apps\Formaty\engine-bin'
$map = @{ x64 = 'win64'; arm64 = 'winarm64' }
foreach ($arch in $Archs) {
    $dst = Join-Path $base $arch
    if ((Test-Path (Join-Path $dst 'ffmpeg.exe')) -and -not $Force) { Write-Host "${arch}: zaten var"; continue }
    $name = "ffmpeg-master-latest-$($map[$arch])-lgpl-shared"
    $zip = Join-Path $env:TEMP "$name.zip"
    Write-Host "${arch}: indiriliyor ($name)"
    Invoke-WebRequest -UseBasicParsing "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/$name.zip" -OutFile $zip
    $tmp = Join-Path $env:TEMP "$name-x"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive $zip $tmp
    if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
    New-Item -ItemType Directory -Force $dst | Out-Null
    $bin = Join-Path $tmp "$name\bin"
    Get-ChildItem $bin | Where-Object { $_.Name -ne 'ffplay.exe' } | Copy-Item -Destination $dst
    Copy-Item (Join-Path $tmp "$name\LICENSE.txt") (Join-Path $dst 'FFMPEG-LICENSE.txt') -ErrorAction SilentlyContinue
    Remove-Item $tmp -Recurse -Force; Remove-Item $zip -Force
    $mb = [math]::Round(((Get-ChildItem $dst | Measure-Object Length -Sum).Sum) / 1MB, 1)
    Write-Host "${arch}: hazır ($mb MB)"
}

# PDFium
foreach ($arch in $Archs) {
    $dst = Join-Path $base $arch
    if ((Test-Path (Join-Path $dst 'pdfium.dll')) -and -not $Force) { Write-Host "${arch}: pdfium zaten var"; continue }
    $tgz = Join-Path $env:TEMP "pdfium-win-$arch.tgz"
    Invoke-WebRequest -UseBasicParsing "https://github.com/bblanchon/pdfium-binaries/releases/latest/download/pdfium-win-$arch.tgz" -OutFile $tgz
    $tmp = Join-Path $env:TEMP "pdfium-$arch-x"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    New-Item -ItemType Directory $tmp | Out-Null
    & (Join-Path $env:SystemRoot 'System32\tar.exe') -xzf $tgz -C $tmp
    New-Item -ItemType Directory -Force $dst | Out-Null
    Copy-Item (Join-Path $tmp 'bin\pdfium.dll') $dst
    Copy-Item (Join-Path $tmp 'LICENSE') (Join-Path $dst 'PDFIUM-LICENSE.txt') -ErrorAction SilentlyContinue
    Remove-Item $tmp -Recurse -Force; Remove-Item $tgz -Force
    Write-Host "${arch}: pdfium hazır"
}
