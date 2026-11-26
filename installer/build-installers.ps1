<#
  Builds SIMRS Gramedika packages into dist\ (self-contained: target machines need no .NET):
    win   : SIMRS-Gramedika-Setup-<ver>-win-<arch>.exe (Inno Setup) and SIMRS-Gramedika-<ver>-win-<arch>.msi (WiX v5)
    linux : simrs-gramedika_<ver>_<amd64|arm64>.deb and simrs-gramedika-<ver>-linux-<arch>.tar.gz
  for each architecture in -Arch (x64, arm64).

  .rpm, AppImage and macOS .dmg need Linux/macOS tools and are built by .github/workflows/release.yml.

  Needs: .NET SDK 10; for win also Inno Setup 6 (winget install JRSoftware.InnoSetup). WiX comes from NuGet.
  Usage: powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 [-Version 1.0.0] [-Only all|win|linux] [-Arch x64,arm64]
#>
param(
    # Display version, e.g. 0.1.0-alpha.202611191230 (default: <Version> from the csproj + a timestamp when it is a pre-release).
    [string]$Version = "",
    # Numeric version for MSI ProductVersion / Windows file info (default 0.1.<yy><day of year>).
    [string]$FileVersion = "",
    [ValidateSet("all", "win", "linux")][string]$Only = "all",
    [ValidateSet("x64", "arm64")][string[]]$Arch = @("x64", "arm64"),
    [switch]$WindowsSetupOnly,
    [string]$InnoSetupCompiler = ""
)
$ErrorActionPreference = "Stop"
if ($WindowsSetupOnly -and $Only -ne "win") { throw "-WindowsSetupOnly memerlukan -Only win." }
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src/SimrsGramedika.UI/SimrsGramedika.UI.csproj"
$dist = Join-Path $root "dist"
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = "C:\Program Files\dotnet\dotnet.exe" }

if (-not $Version) {
    $Version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) { $Version = "0.1.0-alpha" }
    if ($Version -match "-") { $Version = "$Version.$(Get-Date -Format yyyyMMddHHmm)" }
}
if (-not $FileVersion) {
    $FileVersion = if ($Version -match '^\d+\.\d+\.\d+$') { $Version } else {
        "0.1.$(Get-Date -Format yy)$((Get-Date).DayOfYear.ToString('000'))"
    }
}
Write-Host "Versi: $Version (file version $FileVersion)" -ForegroundColor Cyan
New-Item -ItemType Directory -Force $dist | Out-Null

function Publish($rid) {
    $out = Join-Path $root "publish/$rid"
    if (Test-Path -LiteralPath $out) {
        $publishRoot = (Resolve-Path -LiteralPath (Join-Path $root "publish")).ProviderPath
        $expected = [IO.Path]::GetFullPath($out)
        $target = (Resolve-Path -LiteralPath $out).ProviderPath
        if ($target -ne $expected -or
            -not $target.StartsWith($publishRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Folder publish tidak valid: $target"
        }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    Write-Host "== dotnet publish $rid ($Version)" -ForegroundColor Cyan
    & $dotnet publish $proj -c Release -r $rid --self-contained true -o $out `
        -p:Version=$Version -p:DebugType=none -p:DebugSymbols=false -m:1 -nr:false -nologo -v q | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "publish $rid gagal" }
    return $out
}

if ($Only -in "all", "win") {
    $iscc = @($InnoSetupCompiler, "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
        Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
    if (-not $iscc) { throw "Inno Setup 6 tidak ditemukan. Pasang dengan: winget install JRSoftware.InnoSetup" }
    foreach ($a in $Arch) {
        $win = Publish "win-$a"
        Rename-Item (Join-Path $win "SimrsGramedika.UI.exe") "SIMRS Gramedika.exe"

        Write-Host "== Inno Setup ($a)" -ForegroundColor Cyan
        & $iscc /Q "/DAppVersion=$Version" "/DFileVersion=$FileVersion" "/DArch=$a" "/DSourceDir=$win" "/DOutputDir=$dist" (Join-Path $PSScriptRoot "windows/SimrsGramedika.iss")
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup $a gagal" }

        if ($WindowsSetupOnly) { continue }

        Write-Host "== MSI ($a)" -ForegroundColor Cyan
        & $dotnet build (Join-Path $PSScriptRoot "msi/SimrsGramedika.wixproj") -c Release -nologo -v q `
            "-p:Platform=$a" "-p:SourceDir=$win" "-p:Version=$FileVersion" "-p:DisplayVersion=$Version" "-p:OutputPath=$dist\" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "MSI $a gagal" }
    }
    if (-not $WindowsSetupOnly) {
        Get-ChildItem $dist -Filter *.wixpdb -ErrorAction SilentlyContinue | Remove-Item -Force
    }
}

if ($Only -in "all", "linux") {
    $builderProj = Join-Path $PSScriptRoot "DebBuilder/DebBuilder.csproj"
    & $dotnet build $builderProj -c Release -nologo -v q -o (Join-Path $root "publish/debbuilder") | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "DebBuilder gagal dibuild" }
    $builder = Join-Path $root "publish/debbuilder/DebBuilder.dll"
    foreach ($a in $Arch) {
        $linux = Publish "linux-$a"
        $debArch = if ($a -eq "arm64") { "arm64" } else { "amd64" }
        Write-Host "== .deb + .tar.gz ($a)" -ForegroundColor Cyan
        & $dotnet $builder $linux (Join-Path $PSScriptRoot "linux") (Join-Path $dist "simrs-gramedika_${Version}_$debArch.deb") $Version $debArch
        if ($LASTEXITCODE -ne 0) { throw ".deb $a gagal" }
        & $dotnet $builder $linux (Join-Path $PSScriptRoot "linux") (Join-Path $dist "simrs-gramedika-$Version-linux-$a.tar.gz") $Version $debArch
        if ($LASTEXITCODE -ne 0) { throw ".tar.gz $a gagal" }
    }
}

Write-Host "== Selesai:" -ForegroundColor Green
Get-ChildItem $dist -File | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
