<#
  Membuat installer SIMRS Gramedika:
    dist\SIMRS-Gramedika-Setup-<versi>-win-x64.exe   (Inno Setup)
    dist\simrs-gramedika_<versi>_amd64.deb           (Debian/Ubuntu)

  Kebutuhan: .NET SDK 10, Inno Setup 6 (winget install JRSoftware.InnoSetup).
  Keduanya self-contained: komputer tujuan tidak perlu memasang .NET.

  Pemakaian:  powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 [-Version 1.0.0] [-Only win|deb]
#>
param(
    [string]$Version = "",
    [ValidateSet("", "win", "deb")][string]$Only = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\SimrsKhanza.UI\SimrsKhanza.UI.csproj"
$dist = Join-Path $root "dist"
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = "C:\Program Files\dotnet\dotnet.exe" }

if (-not $Version) { $Version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
if (-not $Version) { $Version = "1.0.0" }
New-Item -ItemType Directory -Force $dist | Out-Null

function Publish($rid) {
    $out = Join-Path $root "publish\$rid"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    Write-Host "== dotnet publish $rid ($Version)" -ForegroundColor Cyan
    & $dotnet publish $proj -c Release -r $rid --self-contained true -o $out `
        -p:Version=$Version -p:DebugType=none -p:DebugSymbols=false -nologo -v q | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "publish $rid gagal" }
    return $out
}

if ($Only -ne "deb") {
    $win = Publish "win-x64"
    Rename-Item (Join-Path $win "SimrsKhanza.UI.exe") "SIMRS Gramedika.exe"
    $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw "Inno Setup 6 tidak ditemukan. Pasang dengan: winget install JRSoftware.InnoSetup" }
    Write-Host "== Inno Setup" -ForegroundColor Cyan
    & $iscc /Q "/DAppVersion=$Version" "/DSourceDir=$win" "/DOutputDir=$dist" (Join-Path $PSScriptRoot "windows\SimrsGramedika.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup gagal" }
}

if ($Only -ne "win") {
    $linux = Publish "linux-x64"
    Write-Host "== .deb" -ForegroundColor Cyan
    $deb = Join-Path $dist "simrs-gramedika_${Version}_amd64.deb"
    & $dotnet run --project (Join-Path $PSScriptRoot "DebBuilder\DebBuilder.csproj") -c Release -- $linux (Join-Path $PSScriptRoot "linux") $deb $Version
    if ($LASTEXITCODE -ne 0) { throw "pembuatan .deb gagal" }
}

Write-Host "== Selesai:" -ForegroundColor Green
Get-ChildItem $dist | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
