# Uji klik layar secara headless memakai MariaDB sementara berisi struktur Khanza tanpa data.
#
#   powershell -ExecutionPolicy Bypass -File tools\klik-headless.ps1
#
# 1. Tulis tes sementara src\SimrsGramedika.Tests\ZzSementaraKlikTests.cs (contoh di log handover 15).
# 2. Jalankan skrip ini. Koneksi dibaca tes lewat DatabaseSettings.Load(), gambar disimpan ke $env:ZZ_SHOTS.
# 3. Hapus tes sementara dan src\SimrsGramedika.Tests\TestResults sesudahnya; jangan di-commit.
param(
    [string]$MariaDb = (Join-Path $env:TEMP 'simrs-mariadb\mariadb-11.4.8-winx64'),
    [int]$Port = 33188,
    [string]$Gambar = (Join-Path $env:TEMP 'simrs-klik-gambar')
)

$ErrorActionPreference = 'Continue'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$bin = Join-Path $MariaDb 'bin'
if (-not (Test-Path "$bin\mariadbd.exe")) {
    throw "bin\mariadbd.exe tidak ada di $MariaDb. Jalankan tools\uji-fixture.ps1 sekali, atau beri -MariaDb <folder>."
}
# Port harus bebas, supaya skrip tidak pernah menulis ke server lain.
try { $cek = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port); $cek.Start(); $cek.Stop() }
catch { throw "Port $Port sedang dipakai. Matikan prosesnya atau pilih -Port lain." }

$kerja = Join-Path $env:TEMP ('simrs-klik-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$kerja\config" | Out-Null
$schema = Get-Content docs\audit\schema_sik.sql -Raw -Encoding UTF8
$sql = "CREATE DATABASE simrs_klik;`nUSE simrs_klik;`nSET FOREIGN_KEY_CHECKS=0;`n"
$sudah = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($c in [regex]::Matches($schema, '(?s)CREATE TABLE `\w+` \(.*?ENGINE=[^;]*;')) {
    if ($sudah.Add([regex]::Match($c.Value, 'CREATE TABLE `(\w+)`').Groups[1].Value)) { $sql += $c.Value + "`n" }
}
[IO.File]::WriteAllText("$kerja\skema.sql", $sql, (New-Object Text.UTF8Encoding $false))

$server = $null
try {
    & "$bin\mariadb-install-db.exe" "--datadir=$kerja\data" | Out-Null
    $server = Start-Process "$bin\mariadbd.exe" -ArgumentList '--no-defaults', "--datadir=`"$kerja\data`"", "--port=$Port",
        '--bind-address=127.0.0.1', '--console' -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput "$kerja\server.out" -RedirectStandardError "$kerja\server.err"
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        & "$bin\mariadb-admin.exe" --no-defaults --host=127.0.0.1 "--port=$Port" --user=root ping 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
    }
    cmd /c "`"$bin\mariadb.exe`" --no-defaults --host=127.0.0.1 --port=$Port --user=root < `"$kerja\skema.sql`""
    if ($LASTEXITCODE -ne 0) { throw 'gagal memuat skema' }
    # Variabel SIMRS_* lain dibuang dan folder konfigurasi dikosongkan, agar profil koneksi tersimpan tidak terpakai.
    Get-ChildItem Env:SIMRS_* | ForEach-Object { Remove-Item "Env:$($_.Name)" }
    $env:SIMRS_CONFIG_DIR = "$kerja\config"
    $env:SIMRS_DB_HOST = '127.0.0.1'; $env:SIMRS_DB_PORT = "$Port"; $env:SIMRS_DB_NAME = 'simrs_klik'; $env:SIMRS_DB_USER = 'root'
    $env:ZZ_SHOTS = $Gambar
    $env:MSBuildEnableWorkloadResolver = 'false'
    dotnet test src\SimrsGramedika.Tests\SimrsGramedika.Tests.csproj -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false `
        -nologo -v minimal --blame-hang-timeout 120s --filter 'FullyQualifiedName~ZzSementaraKlikTests' |
        Select-String ' error |\[FAIL\]|Error Message|Exception|Passed!|Failed!|No test matches|at Simrs'
    "Gambar: $Gambar"
}
finally {
    Get-ChildItem Env:SIMRS_*, Env:ZZ_* -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item "Env:$($_.Name)" }
    if ($server) {
        & "$bin\mariadb-admin.exe" --no-defaults --host=127.0.0.1 "--port=$Port" --user=root shutdown 2>$null
        if (-not $server.WaitForExit(15000)) { $server.Kill() }
    }
    try { [IO.Directory]::Delete($kerja, $true) } catch { "Folder sementara belum terhapus: $kerja" }
}
