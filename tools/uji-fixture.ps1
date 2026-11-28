# Menjalankan tes fixture pada MariaDB sementara berisi data sintetis, lalu seluruh tes.
#
#   powershell -ExecutionPolicy Bypass -File tools\uji-fixture.ps1
#   powershell -ExecutionPolicy Bypass -File tools\uji-fixture.ps1 -MariaDb C:\alat\mariadb-11.4.8-winx64
#
# MariaDB portabel (folder berisi bin\mariadbd.exe). Tanpa -MariaDb, skrip mengekstrak
# dist\mariadb-test-11.4.8.zip ke folder TEMP. Server hanya mendengar di 127.0.0.1, memakai
# data baru setiap kali, dan dimatikan serta dihapus setelah selesai.
param(
    [string]$MariaDb = '',
    [int]$Port = 33177
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo

if (-not $MariaDb) {
    $MariaDb = Join-Path $env:TEMP 'simrs-mariadb\mariadb-11.4.8-winx64'
    if (-not (Test-Path "$MariaDb\bin\mariadbd.exe")) {
        $zip = Join-Path $repo 'dist\mariadb-test-11.4.8.zip'
        if (-not (Test-Path $zip)) { throw 'MariaDB tidak ditemukan. Unduh paket ZIP MariaDB 11.4 untuk Windows lalu beri -MariaDb <folder>.' }
        Expand-Archive $zip -DestinationPath (Join-Path $env:TEMP 'simrs-mariadb') -Force
    }
}
$bin = Join-Path $MariaDb 'bin'
if (-not (Test-Path "$bin\mariadbd.exe")) { throw "bin\mariadbd.exe tidak ada di $MariaDb" }

# Port harus bebas, supaya skrip tidak pernah menulis ke server lain.
try { $cek = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port); $cek.Start(); $cek.Stop() }
catch { throw "Port $Port sedang dipakai. Matikan prosesnya atau pilih -Port lain." }

$kerja = Join-Path $env:TEMP ('simrs-fixture-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$kerja\config" | Out-Null

$schema = Get-Content docs\audit\schema_sik.sql -Raw -Encoding UTF8
$relasi = ',\s*CONSTRAINT [^\r\n]*?(?=,?\r?\n)'

# Satu database fixture berisi tabel terpilih dari skema Khanza, tanpa data.
function New-SqlFixture([string]$Nama, [string[]]$Tabel, [switch]$TanpaRelasi) {
    $sql = "DROP DATABASE IF EXISTS $Nama;`nCREATE DATABASE $Nama;`nUSE $Nama;`nSET FOREIGN_KEY_CHECKS=0;`n"
    # ranap_gabung dibaca oleh cek tagihan dan pencarian kamar, jadi selalu disertakan.
    foreach ($t in @($Tabel) + 'ranap_gabung' | Select-Object -Unique) {
        $c = [regex]::Match($schema, '(?s)CREATE TABLE `' + $t + '` \(.*?ENGINE=[^;]*;')
        if (-not $c.Success) { throw "tabel $t tidak ditemukan di schema_sik.sql" }
        $ddl = if ($TanpaRelasi) { $c.Value -replace $relasi, '' } else { $c.Value }
        $sql += $ddl + "`n"
    }
    $sql
}

function Import-Sql([string]$Sql, [string]$Nama) {
    $berkas = Join-Path $kerja "$Nama.sql"
    [IO.File]::WriteAllText($berkas, $Sql, (New-Object Text.UTF8Encoding $false))
    cmd /c "`"$bin\mariadb.exe`" --no-defaults --host=127.0.0.1 --port=$Port --user=root < `"$berkas`""
    if ($LASTEXITCODE -ne 0) { throw "gagal memuat $Nama" }
}

$sql = New-SqlFixture simrs_registrasi_fixture ('pasien reg_periksa kamar_inap billing catatan_pasien set_validasi_catatan mutasi_berkas dokter pasien_mati rujuk rawat_inap_dr periksa_lab periksa_radiologi operasi detail_pemberian_obat databarang suratsakit kelurahan kecamatan kabupaten pegawai sidikjari' -split ' ')
$sql += New-SqlFixture simrs_resep_fixture ('pasien dokter poliklinik penjab reg_periksa billing resep_obat resep_dokter resep_dokter_racikan bangsal set_depo_ralan set_lokasi set_embalase set_harga_obat_ralan kodesatuan databarang data_batch gudangbarang riwayat_barang_medis detail_pemberian_obat aturan_pakai set_akun_ralan rekening jurnal detailjurnal metode_racik resep_dokter_racikan_detail obat_racikan detail_obat_racikan set_depo_ranap set_harga_obat_ranap kamar kamar_inap set_akun_ranap returpasien' -split ' ')
$sql += New-SqlFixture simrs_penunjang_fixture ('pasien reg_periksa kamar_inap kamar billing periksa_lab detail_periksa_lab detail_periksa_labpa detail_periksa_labpa_gambar saran_kesan_lab periksa_radiologi hasil_radiologi gambar_radiologi beri_bhp_radiologi permintaan_lab permintaan_pemeriksaan_lab permintaan_detail_permintaan_lab permintaan_labpa permintaan_pemeriksaan_labpa permintaan_radiologi permintaan_pemeriksaan_radiologi antrilabpk2 antrilabpa2 antriradiologi2 set_akun_ralan set_akun_ranap rekening jurnal detailjurnal' -split ' ') -TanpaRelasi
$sql += New-SqlFixture simrs_resep_pulang_fixture ('pasien dokter bangsal kamar reg_periksa kamar_inap databarang data_batch gudangbarang permintaan_resep_pulang detail_permintaan_resep_pulang resep_pulang riwayat_barang_medis billing set_harga_obat_ranap' -split ' ') -TanpaRelasi
$sql += ((Get-Content docs\migrations\20261125_serah_resep_pulang.sql -Raw -Encoding UTF8) -replace $relasi, '') + "`n"
$sql += New-SqlFixture simrs_formklinis_fixture ('pasien reg_periksa petugas dokter pegawai sidikjari setting catatan_keperawatan_ralan catatan_observasi_igd skrining_gizi penilaian_lanjutan_resiko_jatuh_dewasa penilaian_medis_ralan checklist_kriteria_masuk_hcu checklist_kriteria_keluar_hcu checklist_kriteria_masuk_icu checklist_kriteria_keluar_icu checklist_kriteria_masuk_nicu checklist_kriteria_keluar_nicu checklist_kriteria_masuk_picu checklist_kriteria_keluar_picu checklist_kriteria_masuk_isolasi checklist_kriteria_keluar_isolasi catatan_observasi_ranap catatan_observasi_ranap_kebidanan catatan_observasi_ranap_postpartum catatan_observasi_bayi catatan_observasi_chbp catatan_observasi_induksi_persalinan catatan_observasi_ruang_ok catatan_observasi_restrain_nonfarma catatan_observasi_ventilator catatan_observasi_hemodialisa catatan_cairan_hemodialisa catatan_keseimbangan_cairan catatan_cek_gds follow_up_dbd pemantauan_pews_dewasa pemantauan_pews_anak pemantauan_ews_neonatus pemantauan_meows_obstetri monitoring_reaksi_tranfusi monitoring_tolac intervensi_nyeri_farmakologi intervensi_nyeri_nonfarmakologi monitoring_asuhan_gizi skrining_gizi_kehamilan penilaian_lanjutan_resiko_jatuh_anak penilaian_lanjutan_resiko_jatuh_geriatri penilaian_lanjutan_resiko_jatuh_lansia penilaian_lanjutan_resiko_jatuh_psikiatri penilaian_lanjutan_skrining_fungsional penilaian_risiko_dekubitus penilaian_risiko_jatuh_neonatus skrining_adiksi_nikotin skrining_anemia skrining_curb65 skrining_diabetes_melitus skrining_frailty_syndrome skrining_hipertensi skrining_indra_pendengaran skrining_instrumen_acrs skrining_instrumen_amt skrining_instrumen_esat skrining_instrumen_sdq skrining_kanker_kolorektal skrining_kekerasan_pada_perempuan skrining_kesehatan_gigi_mulut_balita skrining_kesehatan_gigi_mulut_dewasa skrining_kesehatan_gigi_mulut_lansia skrining_kesehatan_gigi_mulut_remaja skrining_kesehatan_penglihatan mpp_skrining skrining_nutrisi_anak skrining_nutrisi_dewasa skrining_nutrisi_lansia skrining_obesitas skrining_puma skrining_pneumonia_severity_index skrining_risiko_kanker_paru skrining_risiko_kanker_payudara skrining_risiko_kanker_serviks skrining_instrumen_srq skrining_tbc skrining_tolac skrining_thalassemia' -split ' ') -TanpaRelasi

$sql += New-SqlFixture simrs_tagihan_fixture ('billing nota_inap' -split ' ') -TanpaRelasi

# AlurPenunjangRalanTests memakai nama database yang sama dengan tes jurnal, tetapi butuh skema penuh yang masih kosong.
$penuh = "DROP DATABASE IF EXISTS simrs_penunjang_fixture;`nCREATE DATABASE simrs_penunjang_fixture;`nUSE simrs_penunjang_fixture;`nSET FOREIGN_KEY_CHECKS=0;`n"
$sudah = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($c in [regex]::Matches($schema, '(?s)CREATE TABLE `\w+` \(.*?ENGINE=[^;]*;')) {
    $nama = [regex]::Match($c.Value, 'CREATE TABLE `(\w+)`').Groups[1].Value
    if ($sudah.Add($nama)) { $penuh += ($c.Value -replace $relasi, '') + "`n" }  # nama berulang berasal dari skema bridging
}

# Suite lain mensyaratkan skema penuh berelasi yang masih kosong, satu database per suite.
$lain = [ordered]@{
    ANTREAN = 'NomorAntreanTests'; DIAGNOSA = 'DiagnosaRalanTests'; KASIR_FARMASI = 'PelunasanPenjualanTests'
    PASIEN = 'PasienBaruTests'; PULANG = 'PulangRanapTests'; RALAN = 'TindakanRalanInputTests'
    RANAP = 'BillingRanapTests'; RUJUKAN = 'RujukanInternalTests'; TINDAKAN_RANAP = 'TindakanRanapInputTests'
    KUNCI = 'KunciTanggalTests'; GABUNG = 'RawatGabungTests'; OPERASI = 'OperasiTests'
    PIUTANG = 'PiutangPasienTests'; BIAYA = 'KasirPenyesuaianTests'; ORDER_RANAP = 'OrderRanapTests'
    BATAL = 'KasirBatalNotaTests'; BAYI = 'KelahiranBayiTests'; PEMBELIAN = 'PembelianTests'; RETUR_BELI = 'ReturBeliTests'
}
$tabelPenuh = New-Object 'System.Collections.Generic.HashSet[string]'
$ddlPenuh = ''
foreach ($c in [regex]::Matches($schema, '(?s)CREATE TABLE `\w+` \(.*?ENGINE=[^;]*;')) {
    if ($tabelPenuh.Add([regex]::Match($c.Value, 'CREATE TABLE `(\w+)`').Groups[1].Value)) { $ddlPenuh += $c.Value + "`n" }
}
$sqlLain = ''
foreach ($n in $lain.Keys) {
    $db = "simrs_$($n.ToLower())_fixture"
    $sqlLain += "DROP DATABASE IF EXISTS $db;`nCREATE DATABASE $db;`nUSE $db;`nSET FOREIGN_KEY_CHECKS=0;`n" + $ddlPenuh
}

$server = $null
$gagal = @()
$configLama = $env:SIMRS_CONFIG_DIR
try {
    $ErrorActionPreference = 'Continue'
    & "$bin\mariadb-install-db.exe" "--datadir=$kerja\data" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'mariadb-install-db gagal' }
    $server = Start-Process "$bin\mariadbd.exe" -ArgumentList '--no-defaults', "--datadir=`"$kerja\data`"", "--port=$Port", '--bind-address=127.0.0.1', '--console' -PassThru -WindowStyle Hidden -RedirectStandardOutput "$kerja\server.out" -RedirectStandardError "$kerja\server.err"
    $siap = $false
    for ($i = 0; $i -lt 60 -and -not $siap; $i++) {
        Start-Sleep -Milliseconds 500
        & "$bin\mariadb-admin.exe" --no-defaults --host=127.0.0.1 "--port=$Port" --user=root ping 2>$null | Out-Null
        $siap = $LASTEXITCODE -eq 0
    }
    if (-not $siap) { Get-Content "$kerja\server.err" -Tail 20; throw 'MariaDB tidak siap' }
    Import-Sql $sql 'fixture'

    # Tes tidak boleh membaca profil koneksi pengguna yang tersimpan di komputer ini.
    Get-ChildItem Env:SIMRS_* | ForEach-Object { Remove-Item "Env:$($_.Name)" }
    $env:SIMRS_CONFIG_DIR = "$kerja\config"
    $env:MSBuildEnableWorkloadResolver = 'false'
    $umum = '-m:1', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false', '-nologo', '-v', 'minimal', '--blame-hang-timeout', '180s'
    $proyek = 'src\SimrsGramedika.Tests\SimrsGramedika.Tests.csproj'
    $saring = ' error |\[FAIL\]|Failed |Error Message|Expected|Actual|Exception|Passed!|Failed!|at Simrs'

    foreach ($n in 'REGISTRASI', 'RESEP', 'PENUNJANG', 'RESEP_PULANG', 'FORMKLINIS', 'TAGIHAN') { Set-Item "Env:SIMRS_${n}_FIXTURE_PORT" "$Port" }
    $kelas = 'RegistrasiMenuTests', 'ValidasiResepTests', 'JurnalPenunjangTests', 'ResepPulangTests', 'FormKlinisTests', 'TagihanKunjunganTests'
    '== fixture'
    dotnet test $proyek @umum --filter (($kelas | ForEach-Object { "FullyQualifiedName~$_" }) -join '|') | Select-String $saring
    if ($LASTEXITCODE -ne 0) { $gagal += 'fixture' }

    '== alur penunjang rawat jalan'
    Import-Sql $penuh 'penuh'
    dotnet test $proyek @umum --filter 'FullyQualifiedName~AlurPenunjangRalanTests' | Select-String $saring
    if ($LASTEXITCODE -ne 0) { $gagal += 'alur' }

    '== fixture skema penuh'
    Get-ChildItem Env:SIMRS_*_FIXTURE_PORT | ForEach-Object { Remove-Item "Env:$($_.Name)" }
    Import-Sql $sqlLain 'lain'
    foreach ($n in $lain.Keys) { Set-Item "Env:SIMRS_${n}_FIXTURE_PORT" "$Port" }
    dotnet test $proyek @umum --filter (($lain.Values | ForEach-Object { "FullyQualifiedName~$_" }) -join '|') | Select-String $saring
    if ($LASTEXITCODE -ne 0) { $gagal += 'skema-penuh' }

    '== seluruh tes tanpa database'
    Get-ChildItem Env:SIMRS_*_FIXTURE_PORT | ForEach-Object { Remove-Item "Env:$($_.Name)" }
    dotnet test $proyek @umum | Select-String $saring
    if ($LASTEXITCODE -ne 0) { $gagal += 'penuh' }
}
finally {
    $ErrorActionPreference = 'Continue'
    Get-ChildItem Env:SIMRS_* -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item "Env:$($_.Name)" }
    if ($configLama) { $env:SIMRS_CONFIG_DIR = $configLama }
    if ($server) {
        & "$bin\mariadb-admin.exe" --no-defaults --host=127.0.0.1 "--port=$Port" --user=root shutdown 2>$null
        if (-not $server.WaitForExit(15000)) { $server.Kill() }
    }
    # Remove-Item di PowerShell 5.1 gagal pada nama pendek TEMP seperti "NAMA~1".
    try { [IO.Directory]::Delete($kerja, $true) } catch { "Folder sementara belum terhapus: $kerja" }
}

if ($gagal) { "GAGAL: $($gagal -join ', ')"; exit 1 }
'SEMUA LULUS'
