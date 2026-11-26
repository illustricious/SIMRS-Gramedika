using SimrsGramedika.Data.Master;
using static SimrsGramedika.Data.Master.FieldKind;
using static SimrsGramedika.Data.Master.MasterLookups;

namespace SimrsGramedika.Data.Kepegawaian;

/// <summary>
/// Menu [C] "Presensi, Manajemen &amp; Penggajian Pegawai" as data: each legacy tile (by its frmUtama
/// button name) maps to a <see cref="MasterSpec"/> (list + form) or a <see cref="ReportSpec"/>, on the
/// same tables the Java app uses (kepegawaian/*.java, K3RS*, DlgAudit*, SKP*).
/// </summary>
public static class MenuCSpecs
{
    private const string Berkas = "Path berkas di server web Khanza (mis. pages/berkas/xxx.pdf).";

    private static MasterField Pegawai(string col = "id", string label = "Pegawai", bool key = true) =>
        new(col, label, Lookup, LookupSql: col == "id" ? PegawaiId : PegawaiNik, Key: key, Required: true);

    private static MasterField Ya(string col, string label) => new(col, label, Choice, Choices: YaTidakNa);

    private static readonly string[] Bulan = Enumerable.Range(1, 12).Select(i => i.ToString("00")).ToArray();
    private static readonly string[] JenisKelamin = ["L", "P"];
    private static readonly string[] GolDarah = ["A", "B", "O", "AB", "-"];
    private static readonly string[] StatusNikah = ["BELUM MENIKAH", "MENIKAH", "JANDA", "DUDHA", "JOMBLO"];
    private static readonly string[] Status01 = ["1", "0"];

    // ---------------------------------------------------------------- Petugas / Dokter

    private static readonly MasterSpec Petugas = new(
        "Petugas", "petugas",
        [
            Pegawai("nip", "NIP (Pegawai)"),
            new("nama", "Nama", MaxLen: 50, Required: true),
            new("jk", "J.K.", Choice, Choices: JenisKelamin),
            new("tmp_lahir", "Tmp.Lahir", MaxLen: 20),
            new("tgl_lahir", "Tgl.Lahir", Date),
            new("gol_darah", "Gol.Darah", Choice, Choices: GolDarah),
            new("agama", "Agama", MaxLen: 12),
            new("stts_nikah", "Stts.Nikah", Choice, Choices: StatusNikah),
            new("alamat", "Alamat", MaxLen: 60),
            new("kd_jbtn", "Jabatan", Lookup, LookupSql: "SELECT kd_jbtn AS kode, nm_jbtn AS nama FROM jabatan ORDER BY nm_jbtn"),
            new("no_telp", "No.Telp", MaxLen: 13),
            new("email", "Email", MaxLen: 70),
            new("status", "Status (1=Aktif)", Choice, Choices: Status01),
        ],
        """
        SELECT t.*, j.nm_jbtn FROM petugas t LEFT JOIN jabatan j ON j.kd_jbtn=t.kd_jbtn
        WHERE CONCAT_WS(' ',t.nip,t.nama,t.alamat,j.nm_jbtn,t.no_telp,t.email) LIKE @p0 ORDER BY t.status DESC, t.nama
        """,
        [("nip", "NIP"), ("nama", "Nama"), ("jk", "J.K."), ("tmp_lahir", "Tmp.Lahir"), ("tgl_lahir", "Tgl.Lahir"),
         ("gol_darah", "G.D."), ("agama", "Agama"), ("stts_nikah", "Stts.Nikah"), ("alamat", "Alamat"), ("nm_jbtn", "Jabatan"),
         ("no_telp", "No.Telp"), ("email", "Email"), ("status", "Status")])
    { Hint = "NIP harus sudah terdaftar di data pegawai. Status 0 = tidak aktif (cara Khanza lama \"menghapus\" petugas)." };

    private static readonly MasterSpec Dokter = new(
        "Dokter", "dokter",
        [
            Pegawai("kd_dokter", "Kode Dokter (Pegawai)"),
            new("nm_dokter", "Nama Dokter", MaxLen: 50, Required: true),
            new("jk", "J.K.", Choice, Choices: JenisKelamin),
            new("tmp_lahir", "Tmp.Lahir", MaxLen: 20),
            new("tgl_lahir", "Tgl.Lahir", Date),
            new("gol_drh", "Gol.Darah", Choice, Choices: GolDarah),
            new("agama", "Agama", MaxLen: 12),
            new("almt_tgl", "Alamat", MaxLen: 60),
            new("no_telp", "No.Telp", MaxLen: 13),
            new("email", "Email", MaxLen: 70),
            new("stts_nikah", "Stts.Nikah", Choice, Choices: StatusNikah),
            new("kd_sps", "Spesialis", Lookup, LookupSql: "SELECT kd_sps AS kode, nm_sps AS nama FROM spesialis ORDER BY nm_sps"),
            new("alumni", "Alumni", MaxLen: 60),
            new("no_ijn_praktek", "No.Ijin Praktek", MaxLen: 120),
            new("status", "Status (1=Aktif)", Choice, Choices: Status01),
        ],
        """
        SELECT d.*, s.nm_sps FROM dokter d LEFT JOIN spesialis s ON s.kd_sps=d.kd_sps
        WHERE CONCAT_WS(' ',d.kd_dokter,d.nm_dokter,s.nm_sps,d.almt_tgl,d.no_telp,d.alumni,d.no_ijn_praktek) LIKE @p0 ORDER BY d.status DESC, d.nm_dokter
        """,
        [("kd_dokter", "Kode"), ("nm_dokter", "Nama Dokter"), ("jk", "J.K."), ("tmp_lahir", "Tmp.Lahir"), ("tgl_lahir", "Tgl.Lahir"),
         ("gol_drh", "G.D."), ("agama", "Agama"), ("almt_tgl", "Alamat"), ("no_telp", "No.Telp"), ("email", "Email"),
         ("stts_nikah", "Stts.Nikah"), ("nm_sps", "Spesialis"), ("alumni", "Alumni"), ("no_ijn_praktek", "No.Ijin Praktek"), ("status", "Status")])
    { Hint = "Kode dokter = NIK pegawai. Status 0 = tidak aktif." };

    // ---------------------------------------------------------------- Presensi

    private const string PegawaiJoin = "JOIN pegawai p ON p.id=x.id LEFT JOIN departemen dp ON dp.dep_id=p.departemen";

    private static readonly MasterSpec Barcode = new(
        "Barcode Presensi", "barcode",
        [Pegawai(), new("barcode", "Barcode", MaxLen: 25, Required: true)],
        $"SELECT x.*, p.nik, p.nama, dp.nama AS departemen FROM barcode x {PegawaiJoin} WHERE CONCAT_WS(' ',p.nik,p.nama,x.barcode,dp.nama) LIKE @p0 ORDER BY p.nama",
        [("id", "ID"), ("nik", "NIK"), ("nama", "Nama"), ("departemen", "Departemen"), ("barcode", "Barcode")])
    { Hint = "Kode barcode/kartu yang dipindai pegawai di mesin presensi." };

    private static readonly MasterSpec SidikJari = new(
        "Sidik Jari", "sidikjari", [Pegawai()],
        $"SELECT x.id, p.nik, p.nama, dp.nama AS departemen, 'Terdaftar' AS status FROM sidikjari x {PegawaiJoin} WHERE CONCAT_WS(' ',p.nik,p.nama,dp.nama) LIKE @p0 ORDER BY p.nama",
        [("id", "ID"), ("nik", "NIK"), ("nama", "Nama"), ("departemen", "Departemen"), ("status", "Sidik Jari")])
    {
        CanInsert = false, CanEdit = false,
        Hint = "Perekaman sidik jari tetap memakai alat fingerprint di aplikasi presensi. Di sini bisa melihat siapa yang sudah terdaftar dan menghapus data agar bisa direkam ulang.",
    };

    private static readonly MasterSpec JamPresensi = new(
        "Jam Presensi", "jam_masuk",
        [new("shift", "Shift", Choice, Choices: Shift, Key: true), new("jam_masuk", "Jam Masuk", Time, Required: true), new("jam_pulang", "Jam Pulang", Time, Required: true)],
        "SELECT * FROM jam_masuk WHERE shift LIKE @p0 ORDER BY shift",
        [("shift", "Shift"), ("jam_masuk", "Jam Masuk"), ("jam_pulang", "Jam Pulang")]);

    private static MasterSpec Jadwal(string title, string table) => new(
        title, table,
        [
            Pegawai(),
            new("tahun", "Tahun", Integer, Key: true) { Default = (long)System.DateTime.Today.Year },
            new("bulan", "Bulan", Choice, Choices: Bulan, Key: true) { Default = System.DateTime.Today.Month.ToString("00") },
            .. Enumerable.Range(1, 31).Select(d => new MasterField($"h{d}", $"Tgl {d}", Choice, Choices: ["", .. Shift])),
        ],
        $"""
        SELECT x.*, p.nik, p.nama, dp.nama AS departemen FROM {table} x {PegawaiJoin}
        WHERE x.tahun=YEAR(@p1) AND x.bulan=LPAD(MONTH(@p1),2,'0') AND CONCAT_WS(' ',p.nik,p.nama,dp.nama) LIKE @p0 ORDER BY dp.nama, p.nama
        """,
        [("nik", "NIK"), ("nama", "Nama"), ("departemen", "Departemen"), ("tahun", "Tahun"), ("bulan", "Bln"),
         .. Enumerable.Range(1, 31).Select(d => ($"h{d}", d.ToString()))])
    { Period = PeriodFilter.Month, Hint = "Satu baris per pegawai per bulan; kosongkan tanggal yang libur." };

    private static MasterSpec RekapPresensi(string title, PeriodFilter period) => new(
        title, "rekap_presensi",
        [
            Pegawai(),
            new("jam_datang", "Jam Datang", FieldKind.DateTime, Key: true),
            new("shift", "Shift", Choice, Choices: Shift),
            new("jam_pulang", "Jam Pulang", FieldKind.DateTime),
            new("status", "Status", Choice, Choices: StatusPresensi),
            new("keterlambatan", "Keterlambatan", MaxLen: 20),
            new("durasi", "Durasi", MaxLen: 20),
            new("keterangan", "Catatan", MaxLen: 100),
        ],
        $"""
        SELECT x.id, x.shift, x.jam_datang, x.jam_pulang, x.status, x.keterlambatan, x.durasi, x.keterangan, p.nik, p.nama, dp.nama AS departemen
        FROM rekap_presensi x {PegawaiJoin}
        WHERE x.jam_datang BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',p.nik,p.nama,dp.nama,x.shift,x.status,x.keterangan) LIKE @p0
        ORDER BY x.jam_datang DESC
        """,
        [("nik", "NIK"), ("nama", "Nama"), ("departemen", "Departemen"), ("shift", "Shift"), ("jam_datang", "Jam Datang"),
         ("jam_pulang", "Jam Pulang"), ("status", "Status"), ("keterlambatan", "Keterlambatan"), ("durasi", "Durasi"), ("keterangan", "Catatan")])
    {
        Period = period, CanInsert = false,
        Hint = "Presensi masuk dari mesin/aplikasi presensi; di sini bisa dikoreksi (jam pulang, status, catatan) atau dihapus.",
    };

    private static readonly MasterSpec TemporaryPresensi = new(
        "Temporary Presensi", "temporary_presensi",
        [
            Pegawai(),
            new("shift", "Shift", Choice, Choices: Shift),
            new("jam_datang", "Jam Datang", FieldKind.DateTime),
            new("status", "Status", Choice, Choices: StatusPresensi),
            new("keterlambatan", "Keterlambatan", MaxLen: 20),
        ],
        $"""
        SELECT x.id, x.shift, x.jam_datang, x.status, x.keterlambatan, TIMEDIFF(NOW(),x.jam_datang) AS lama, p.nik, p.nama, dp.nama AS departemen
        FROM temporary_presensi x {PegawaiJoin} WHERE CONCAT_WS(' ',p.nik,p.nama,dp.nama,x.shift) LIKE @p0 ORDER BY x.jam_datang
        """,
        [("id", "ID"), ("nik", "NIK"), ("nama", "Nama"), ("departemen", "Departemen"), ("shift", "Shift"), ("jam_datang", "Jam Datang"),
         ("lama", "Lama Bekerja"), ("status", "Status"), ("keterlambatan", "Keterlambatan")])
    {
        CanInsert = false,
        Hint = "Pegawai yang sudah presensi datang tetapi belum pulang. \"Proses Pulang\" memindahkannya ke rekap presensi dengan jam pulang sekarang.",
        Actions =
        [
            new("🏁 Proses Pulang",
                """
                INSERT INTO rekap_presensi (id,shift,jam_datang,jam_pulang,status,keterlambatan,durasi,keterangan,photo)
                SELECT id,shift,jam_datang,NOW(),status,keterlambatan,TIMEDIFF(NOW(),jam_datang),'',photo FROM temporary_presensi WHERE id=@p0;
                DELETE FROM temporary_presensi WHERE id=@p0;
                """,
                "Pulangkan pegawai terpilih sekarang (jam pulang = saat ini)?"),
        ],
    };

    private static readonly MasterSpec PengajuanCuti = new(
        "Pengajuan Cuti", "pengajuan_cuti",
        [
            new("no_pengajuan", "No.Pengajuan", Key: true, AutoKey: true),
            new("tanggal", "Tgl.Pengajuan", Date),
            Pegawai("nik", "Pegawai", key: false),
            new("urgensi", "Urgensi", Choice, Choices: ["Tahunan", "Besar", "Sakit", "Bersalin", "Alasan Penting", "Keterangan Lainnya"]),
            new("tanggal_awal", "Mulai Cuti", Date),
            new("tanggal_akhir", "Sampai", Date),
            new("jumlah", "Jumlah Hari", Integer) { Default = 1L },
            new("alamat", "Alamat Selama Cuti", MaxLen: 100),
            new("kepentingan", "Kepentingan", MaxLen: 70),
            Pegawai("nik_pj", "Penanggung Jawab", key: false),
            new("status", "Status", Choice, Choices: ["Proses Pengajuan", "Disetujui", "Ditolak"]),
        ],
        """
        SELECT c.*, p.nama, pj.nama AS nama_pj FROM pengajuan_cuti c JOIN pegawai p ON p.nik=c.nik LEFT JOIN pegawai pj ON pj.nik=c.nik_pj
        WHERE c.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',c.no_pengajuan,c.nik,p.nama,c.urgensi,c.kepentingan,c.status,pj.nama) LIKE @p0
        ORDER BY c.tanggal DESC, c.no_pengajuan DESC
        """,
        [("no_pengajuan", "No.Pengajuan"), ("tanggal", "Tanggal"), ("nik", "NIK"), ("nama", "Nama"), ("urgensi", "Urgensi"),
         ("tanggal_awal", "Mulai"), ("tanggal_akhir", "Sampai"), ("jumlah", "Jml"), ("alamat", "Alamat"), ("kepentingan", "Kepentingan"),
         ("nama_pj", "P.J."), ("status", "Status")],
        "SELECT IFNULL(MAX(CONVERT(RIGHT(no_pengajuan,3),SIGNED)),0) FROM pengajuan_cuti WHERE tanggal=@p0", 3)
    { Period = PeriodFilter.DateRange, AutoKeyPrefix = "PC{0:yyyyMMdd}", AutoKeyDateField = "tanggal" };

    // ---------------------------------------------------------------- Berkas & riwayat kepegawaian

    private static readonly MasterSpec MasterBerkas = new(
        "Master Berkas Pegawai", "master_berkas_pegawai",
        [
            new("kode", "Kode", MaxLen: 10, Key: true),
            new("kategori", "Kategori", Choice, Choices: ["Tenaga klinis Dokter Umum", "Tenaga klinis Dokter Spesialis", "Tenaga klinis Perawat dan Bidan", "Tenaga klinis Profesi Lain", "Tenaga Non Klinis"]),
            new("nama_berkas", "Nama Berkas", Memo, MaxLen: 300, Required: true),
            new("no_urut", "No.Urut", Integer),
        ],
        "SELECT * FROM master_berkas_pegawai WHERE CONCAT_WS(' ',kode,kategori,nama_berkas) LIKE @p0 ORDER BY kategori, no_urut",
        [("kode", "Kode"), ("kategori", "Kategori"), ("nama_berkas", "Nama Berkas"), ("no_urut", "No.Urut")]);

    private static readonly MasterSpec BerkasPegawai = new(
        "Berkas Kepegawaian", "berkas_pegawai",
        [
            Pegawai("nik"),
            new("kode_berkas", "Jenis Berkas", Lookup, Key: true,
                LookupSql: "SELECT kode, CONCAT(nama_berkas,' (',kategori,')') AS nama FROM master_berkas_pegawai ORDER BY kategori, no_urut"),
            new("tgl_uploud", "Tgl.Upload", Date, Key: true),
            new("berkas", "Berkas", MaxLen: 500, Key: true),
        ],
        """
        SELECT b.*, p.nama, m.nama_berkas, m.kategori FROM berkas_pegawai b JOIN pegawai p ON p.nik=b.nik
        JOIN master_berkas_pegawai m ON m.kode=b.kode_berkas
        WHERE CONCAT_WS(' ',b.nik,p.nama,m.nama_berkas,m.kategori,b.berkas) LIKE @p0 ORDER BY p.nama, m.no_urut
        """,
        [("nik", "NIK"), ("nama", "Nama"), ("kategori", "Kategori"), ("nama_berkas", "Berkas"), ("tgl_uploud", "Tgl.Upload"), ("berkas", "File")])
    { Hint = Berkas };

    private static MasterSpec Riwayat(string title, string table, MasterField[] fields, string cari, (string, string)[] display) => new(
        title, table, [Pegawai(), .. fields, new MasterField("berkas", "Berkas", MaxLen: 500)],
        $"SELECT x.*, p.nik, p.nama FROM {table} x JOIN pegawai p ON p.id=x.id WHERE CONCAT_WS(' ',p.nik,p.nama,{cari}) LIKE @p0 ORDER BY p.nama",
        [("nik", "NIK"), ("nama", "Nama"), .. display, ("berkas", "Berkas")])
    { Hint = Berkas };

    private static readonly MasterSpec RiwayatJabatan = Riwayat("Riwayat Jabatan", "riwayat_jabatan",
        [
            new("jabatan", "Jabatan", MaxLen: 50, Key: true),
            new("tmt_pangkat", "TMT Pangkat", Date), new("tmt_pangkat_yad", "TMT Pangkat YAD", Date),
            new("pejabat_penetap", "Pejabat Penetap", MaxLen: 50), new("nomor_sk", "Nomor SK", MaxLen: 25), new("tgl_sk", "Tgl.SK", Date),
            new("dasar_peraturan", "Dasar Peraturan", MaxLen: 50), new("masa_kerja", "Masa Kerja (Thn)", Integer), new("bln_kerja", "Masa Kerja (Bln)", Integer),
        ],
        "x.jabatan,x.nomor_sk,x.pejabat_penetap",
        [("jabatan", "Jabatan"), ("tmt_pangkat", "TMT"), ("tmt_pangkat_yad", "TMT YAD"), ("pejabat_penetap", "Pejabat"), ("nomor_sk", "No.SK"),
         ("tgl_sk", "Tgl.SK"), ("dasar_peraturan", "Dasar"), ("masa_kerja", "Thn"), ("bln_kerja", "Bln")]);

    private static readonly MasterSpec RiwayatPendidikan = Riwayat("Riwayat Pendidikan", "riwayat_pendidikan",
        [
            new("pendidikan", "Pendidikan", Choice, Key: true, Choices: ["SD", "SMP", "SMA", "SMK", "D I", "D II", "D III", "D IV", "S1", "S2", "S3", "Post Doctor"]),
            new("sekolah", "Sekolah/PT", MaxLen: 50, Key: true), new("jurusan", "Jurusan", MaxLen: 40),
            new("thn_lulus", "Tahun Lulus", Integer) { Default = (long)System.DateTime.Today.Year },
            new("kepala", "Kepala/Rektor", MaxLen: 50),
            new("pendanaan", "Pendanaan", Choice, Choices: ["Biaya Sendiri", "Biaya Instansi Sendiri", "Lembaga Swasta Kerjasama", "Lembaga Swasta Kompetisi", "Lembaga Pemerintah Kerjasama", "Lembaga Pemerintah Kompetisi", "Lembaga Internasional"]),
            new("keterangan", "Keterangan", MaxLen: 50), new("status", "Status", MaxLen: 40),
        ],
        "x.pendidikan,x.sekolah,x.jurusan",
        [("pendidikan", "Pendidikan"), ("sekolah", "Sekolah"), ("jurusan", "Jurusan"), ("thn_lulus", "Lulus"), ("kepala", "Kepala"),
         ("pendanaan", "Pendanaan"), ("keterangan", "Ket"), ("status", "Status")]);

    private static readonly MasterSpec RiwayatNaikGaji = Riwayat("Riwayat Naik Gaji", "riwayat_naik_gaji",
        [
            new("pangkatjabatan", "Pangkat/Jabatan", MaxLen: 50, Key: true), new("gapok", "Gaji Pokok", Number, Key: true),
            new("tmt_berkala", "TMT Berkala", Date), new("tmt_berkala_yad", "TMT Berkala YAD", Date),
            new("no_sk", "No.SK", MaxLen: 25), new("tgl_sk", "Tgl.SK", Date),
            new("masa_kerja", "Masa Kerja (Thn)", Integer), new("bulan_kerja", "Masa Kerja (Bln)", Integer),
        ],
        "x.pangkatjabatan,x.no_sk",
        [("pangkatjabatan", "Pangkat/Jabatan"), ("gapok", "Gapok"), ("tmt_berkala", "TMT"), ("tmt_berkala_yad", "TMT YAD"), ("no_sk", "No.SK"),
         ("tgl_sk", "Tgl.SK"), ("masa_kerja", "Thn"), ("bulan_kerja", "Bln")]);

    private static readonly MasterSpec KegiatanIlmiah = Riwayat("Kegiatan Ilmiah & Pelatihan", "riwayat_seminar",
        [
            new("nama_seminar", "Nama Kegiatan", MaxLen: 50, Key: true), new("mulai", "Mulai", Date, Key: true), new("selesai", "Selesai", Date),
            new("tingkat", "Tingkat", Choice, Choices: ["Local", "Regional", "Nasional", "Internasional"]),
            new("jenis", "Jenis", Choice, Choices: ["WORKSHOP", "SIMPOSIUM", "SEMINAR", "FGD", "PELATIHAN", "LAINNYA"]),
            new("peranan", "Peranan", MaxLen: 40), new("penyelengara", "Penyelenggara", MaxLen: 50), new("tempat", "Tempat", MaxLen: 50),
        ],
        "x.nama_seminar,x.jenis,x.penyelengara,x.tempat",
        [("nama_seminar", "Kegiatan"), ("tingkat", "Tingkat"), ("jenis", "Jenis"), ("peranan", "Peranan"), ("mulai", "Mulai"), ("selesai", "Selesai"),
         ("penyelengara", "Penyelenggara"), ("tempat", "Tempat")]);

    private static readonly MasterSpec RiwayatPenghargaan = Riwayat("Riwayat Penghargaan", "riwayat_penghargaan",
        [
            new("nama_penghargaan", "Nama Penghargaan", MaxLen: 60, Key: true), new("tanggal", "Tanggal", Date, Key: true),
            new("jenis", "Jenis", MaxLen: 30), new("instansi", "Instansi", MaxLen: 40), new("pejabat_pemberi", "Pejabat Pemberi", MaxLen: 40),
        ],
        "x.nama_penghargaan,x.jenis,x.instansi",
        [("jenis", "Jenis"), ("nama_penghargaan", "Penghargaan"), ("tanggal", "Tanggal"), ("instansi", "Instansi"), ("pejabat_pemberi", "Pemberi")]);

    private static readonly MasterSpec RiwayatSuratPeringatan = Riwayat("Riwayat Surat Peringatan", "riwayat_surat_peringatan",
        [new("nama_peringatan", "Nama Peringatan", MaxLen: 60, Key: true), new("tanggal", "Tanggal", Date, Key: true), new("jenis", "Jenis", MaxLen: 30)],
        "x.nama_peringatan,x.jenis",
        [("jenis", "Jenis"), ("nama_peringatan", "Peringatan"), ("tanggal", "Tanggal")]);

    private static readonly MasterSpec RiwayatPenelitian = Riwayat("Riwayat Penelitian", "riwayat_penelitian",
        [
            new("judul_penelitian", "Judul Penelitian", MaxLen: 60, Key: true),
            new("tahun", "Tahun", Integer, Key: true) { Default = (long)System.DateTime.Today.Year },
            new("jenis_penelitian", "Jenis", MaxLen: 30), new("peranan", "Peranan", MaxLen: 30), new("judul_jurnal", "Judul Jurnal", MaxLen: 60),
            new("biaya_penelitian", "Biaya", Number), new("asal_dana", "Asal Dana", MaxLen: 30),
        ],
        "x.judul_penelitian,x.jenis_penelitian,x.judul_jurnal",
        [("jenis_penelitian", "Jenis"), ("peranan", "Peranan"), ("judul_penelitian", "Judul"), ("judul_jurnal", "Jurnal"), ("tahun", "Tahun"),
         ("biaya_penelitian", "Biaya"), ("asal_dana", "Dana")]);

    private static readonly MasterSpec Sms = new(
        "SMS Gateway — Pesan Masuk", "sms",
        [new("id_pesan", "ID", Integer, Key: true), new("no_hp", "No.HP", MaxLen: 15), new("tgl_sms", "Tanggal", FieldKind.DateTime), new("sms_masuk", "Pesan", Memo, MaxLen: 255)],
        "SELECT * FROM sms WHERE tgl_sms BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',sms_masuk,no_hp,pdu_pesan) LIKE @p0 ORDER BY id_pesan DESC",
        [("id_pesan", "ID"), ("tgl_sms", "Tanggal"), ("no_hp", "No.HP"), ("sms_masuk", "Pesan"), ("id_gateway", "Gateway")])
    {
        Period = PeriodFilter.DateRange, CanInsert = false, CanEdit = false,
        Hint = "Pesan yang diterima layanan SMS Khanza (modem GSM). Pengiriman SMS tetap lewat layanan SMS di komputer modem.",
    };

    // ---------------------------------------------------------------- K3RS

    private sealed record K3(string Title, string Table, string Code, string Name);

    private static readonly K3[] K3Masters =
    [
        new("Jenis Cidera K3", "k3rs_jenis_cidera", "kode_cidera", "jenis_cidera"),
        new("Penyebab Kecelakaan K3", "k3rs_penyebab", "kode_penyebab", "penyebab_kecelakaan"),
        new("Jenis Luka K3", "k3rs_jenis_luka", "kode_luka", "jenis_luka"),
        new("Lokasi Kejadian K3", "k3rs_lokasi_kejadian", "kode_lokasi", "lokasi_kejadian"),
        new("Dampak Cidera K3", "k3rs_dampak_cidera", "kode_dampak", "dampak_cidera"),
        new("Jenis Pekerjaan K3", "k3rs_jenis_pekerjaan", "kode_pekerjaan", "jenis_pekerjaan"),
        new("Bagian Tubuh K3", "k3rs_bagian_tubuh", "kode_bagian", "bagian_tubuh"),
    ];

    private static MasterSpec K3Master(K3 k) => new(
        k.Title, k.Table, [new(k.Code, "Kode", MaxLen: 5, Key: true), new(k.Name, "Nama", MaxLen: 150, Required: true)],
        $"SELECT * FROM {k.Table} WHERE CONCAT_WS(' ',{k.Code},{k.Name}) LIKE @p0 ORDER BY {k.Code}",
        [(k.Code, "Kode"), (k.Name, k.Title.Replace(" K3", ""))]);

    private static string K3Lookup(K3 k) => $"SELECT {k.Code} AS kode, {k.Name} AS nama FROM {k.Table} ORDER BY {k.Code}";

    private static ReportSpec K3PerTahun(K3 k) => new(
        k.Title + " Per Tahun",
        $"""
        SELECT m.{k.Code} AS kode, m.{k.Name} AS nama,
        {string.Join(",\n", Enumerable.Range(1, 12).Select(b => $"IFNULL(SUM(MONTH(e.tgl_insiden)={b}),0) AS b{b}"))},
        COUNT(e.no_k3rs) AS total
        FROM {k.Table} m LEFT JOIN k3rs_peristiwa e ON e.{k.Code}=m.{k.Code} AND YEAR(e.tgl_insiden)=@p1
        WHERE CONCAT_WS(' ',m.{k.Code},m.{k.Name}) LIKE @p0
        GROUP BY m.{k.Code}, m.{k.Name} ORDER BY m.{k.Code}
        """,
        [("kode", "Kode"), ("nama", k.Title.Replace(" K3", "")),
         .. new[] { "Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des" }.Select((m, i) => ($"b{i + 1}", m)),
         ("total", "Total")],
        ReportPeriod.Year, "Jumlah peristiwa K3 per bulan pada tahun terpilih (berdasarkan tanggal insiden).");

    private static readonly MasterSpec PeristiwaK3 = new(
        "Peristiwa K3", "k3rs_peristiwa",
        [
            new("no_k3rs", "No.Laporan", Key: true, AutoKey: true),
            new("tgl_pelaporan", "Tgl.Pelaporan", Date), new("waktu_pelaporan", "Jam Pelaporan", Time) { Default = TimeSpan.FromHours(System.DateTime.Now.Hour) },
            new("tgl_insiden", "Tgl.Insiden", Date), new("waktu_insiden", "Jam Insiden", Time) { Default = TimeSpan.FromHours(System.DateTime.Now.Hour) },
            Pegawai("nik", "Korban", key: false),
            new("kode_pekerjaan", "Jenis Pekerjaan", Lookup, LookupSql: K3Lookup(K3Masters[5]), Required: true),
            new("kode_lokasi", "Lokasi Kejadian", Lookup, LookupSql: K3Lookup(K3Masters[3]), Required: true),
            new("kronologi_kejadian", "Kronologi", Memo, MaxLen: 300),
            new("kode_penyebab", "Penyebab", Lookup, LookupSql: K3Lookup(K3Masters[1]), Required: true),
            new("kategori_cidera", "Kategori Cidera", Choice, Choices: ["Ringan", "Sedang", "Berat", "Fatal"]),
            new("kode_cidera", "Jenis Cidera", Lookup, LookupSql: K3Lookup(K3Masters[0]), Required: true),
            new("kode_luka", "Jenis Luka", Lookup, LookupSql: K3Lookup(K3Masters[2]), Required: true),
            new("kode_bagian", "Bagian Tubuh", Lookup, LookupSql: K3Lookup(K3Masters[6]), Required: true),
            new("lt", "Lost Time (hari)", Integer),
            new("penyebab_langsung_kondisi", "Penyebab Langsung: Kondisi", MaxLen: 100),
            new("penyebab_langsung_tindakan", "Penyebab Langsung: Tindakan", MaxLen: 100),
            new("penyebab_tidak_langsung_pribadi", "Penyebab Tdk Langsung: Pribadi", MaxLen: 100),
            new("penyebab_tidak_langsung_pekerjaan", "Penyebab Tdk Langsung: Pekerjaan", MaxLen: 100),
            new("barang_bukti", "Barang Bukti", Choice, Choices: YaTidak),
            new("kode_dampak", "Dampak Cidera", Lookup, LookupSql: K3Lookup(K3Masters[4]), Required: true),
            Pegawai("nik_pelapor", "Pelapor", key: false),
            new("perbaikan_jenis_tindakan", "Jenis Tindakan", Choice, Choices: ["Tindakan Perbaikan", "Tindakan Pencegahan"]),
            new("perbaikan_rencana_tindakan", "Rencana Tindakan", Memo, MaxLen: 200),
            new("perbaikan_target", "Target", Date),
            new("perbaikan_wewenang", "Wewenang", MaxLen: 100),
            Pegawai("nik_timk3", "Tim K3", key: false),
            new("catatan", "Catatan", Memo, MaxLen: 200),
        ],
        """
        SELECT e.*, p.nama AS korban, l.lokasi_kejadian, c.jenis_cidera, pl.nama AS pelapor
        FROM k3rs_peristiwa e JOIN pegawai p ON p.nik=e.nik JOIN k3rs_lokasi_kejadian l ON l.kode_lokasi=e.kode_lokasi
        JOIN k3rs_jenis_cidera c ON c.kode_cidera=e.kode_cidera LEFT JOIN pegawai pl ON pl.nik=e.nik_pelapor
        WHERE e.tgl_insiden BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',e.no_k3rs,p.nama,l.lokasi_kejadian,c.jenis_cidera,e.kronologi_kejadian) LIKE @p0
        ORDER BY e.tgl_insiden DESC, e.no_k3rs DESC
        """,
        [("no_k3rs", "No.Laporan"), ("tgl_insiden", "Tgl.Insiden"), ("waktu_insiden", "Jam"), ("korban", "Korban"), ("lokasi_kejadian", "Lokasi"),
         ("kategori_cidera", "Kategori"), ("jenis_cidera", "Cidera"), ("lt", "LT"), ("kronologi_kejadian", "Kronologi"), ("pelapor", "Pelapor")],
        "SELECT IFNULL(MAX(CONVERT(RIGHT(no_k3rs,4),SIGNED)),0) FROM k3rs_peristiwa WHERE tgl_pelaporan=@p0", 4)
    { Period = PeriodFilter.DateRange, AutoKeyPrefix = "K3/{0:yyyy}/{0:MM}/{0:dd}/", AutoKeyDateField = "tgl_pelaporan" };

    // ---------------------------------------------------------------- Audit PPI

    private enum AuditBy { Ruang, Pegawai, None }

    private static MasterSpec Audit(string title, string table, AuditBy by, params (string Col, string Label)[] items)
    {
        var subject = by switch
        {
            AuditBy.Ruang => new[] { new MasterField("id_ruang", "Ruang/Unit", Lookup, LookupSql: RuangAudit, Key: true, Required: true) },
            AuditBy.Pegawai => [Pegawai("nik", "Dokter/Paramedis")],
            _ => [],
        };
        var join = by switch
        {
            AuditBy.Ruang => "JOIN ruang_audit_kepatuhan s ON s.id_ruang=a.id_ruang",
            AuditBy.Pegawai => "JOIN pegawai s ON s.nik=a.nik",
            _ => "",
        };
        var cari = by switch { AuditBy.Ruang => "a.id_ruang,s.nama_ruang", AuditBy.Pegawai => "a.nik,s.nama,s.jbtn", _ => "''" };
        (string, string)[] who = by switch
        {
            AuditBy.Ruang => [("id_ruang", "ID Ruang"), ("nama_ruang", "Ruang/Unit")],
            AuditBy.Pegawai => [("nik", "NIP"), ("nama", "Dokter/Paramedis"), ("jbtn", "Jabatan")],
            _ => [],
        };
        var sel = by switch { AuditBy.Ruang => ", s.nama_ruang", AuditBy.Pegawai => ", s.nama, s.jbtn", _ => "" };
        var cols = items.Select(i => i.Col).ToArray();
        return new MasterSpec(
            title, table,
            [new("tanggal", "Tanggal Audit", FieldKind.DateTime, Key: true), .. subject, .. items.Select(i => Ya(i.Col, i.Label))],
            $"SELECT a.*{sel}, {ScoreSql("a", cols)} AS nilai FROM {table} a {join} WHERE a.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',{cari}) LIKE @p0 ORDER BY a.tanggal DESC",
            [("tanggal", "Tanggal Audit"), .. who, .. items.Select((i, n) => (i.Col, $"{n + 1}")), ("nilai", "Ttl.Nilai(%)")])
        {
            Period = PeriodFilter.DateRange, ScoreOf = cols,
            Hint = "Kolom 1, 2, 3… = butir audit sesuai urutan di formulir. Nilai = jumlah \"Ya\" ÷ butir yang dinilai (NA tidak dihitung).",
        };
    }

    private static (string, string)[] Numbered(params string[] labels) =>
        labels.Select((l, i) => ($"audit{i + 1}", $"{i + 1}. {l}")).ToArray();

    private static (string, string)[] Named(params (string Col, string Label)[] items) =>
        items.Select((x, i) => (x.Col, $"{i + 1}. {x.Label}")).ToArray();

    private static readonly MasterSpec RuangAuditKepatuhan = new(
        "Ruang/Unit Audit Kepatuhan", "ruang_audit_kepatuhan",
        [new("id_ruang", "ID Ruang", MaxLen: 5, Key: true), new("nama_ruang", "Nama Ruang/Unit", MaxLen: 40, Required: true)],
        "SELECT * FROM ruang_audit_kepatuhan WHERE CONCAT_WS(' ',id_ruang,nama_ruang) LIKE @p0 ORDER BY id_ruang",
        [("id_ruang", "ID Ruang"), ("nama_ruang", "Ruang/Unit")]);

    private static readonly MasterSpec AuditKepatuhanApd = new(
        "Audit Kepatuhan APD", "audit_kepatuhan_apd",
        [
            new("tanggal", "Tanggal Audit", FieldKind.DateTime, Key: true), Pegawai("nik", "Dokter/Paramedis"),
            new("tindakan", "Tindakan", MaxLen: 50, Key: true),
            Ya("topi", "Topi"), Ya("masker", "Masker"), Ya("kacamata", "Kacamata"), Ya("sarungtangan", "Sarung Tangan"), Ya("apron", "Apron"), Ya("sepatu", "Sepatu"),
        ],
        $"""
        SELECT a.*, s.nama, s.jbtn, {ScoreSql("a", ["topi", "masker", "kacamata", "sarungtangan", "apron", "sepatu"])} AS nilai
        FROM audit_kepatuhan_apd a JOIN pegawai s ON s.nik=a.nik
        WHERE a.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',a.nik,s.nama,s.jbtn,a.tindakan) LIKE @p0 ORDER BY a.tanggal DESC
        """,
        [("tanggal", "Tanggal Audit"), ("tindakan", "Tindakan"), ("nik", "NIP"), ("nama", "Dokter/Paramedis"), ("jbtn", "Jabatan"),
         ("topi", "Topi"), ("masker", "Masker"), ("kacamata", "Kacamata"), ("sarungtangan", "Sarung Tangan"), ("apron", "Apron"), ("sepatu", "Sepatu"),
         ("nilai", "Ttl.Nilai(%)")])
    { Period = PeriodFilter.DateRange, ScoreOf = ["topi", "masker", "kacamata", "sarungtangan", "apron", "sepatu"] };

    // ---------------------------------------------------------------- SKP

    private static readonly MasterSpec SkpKategori = new(
        "Kategori Pengkajian SKP", "skp_kategori_penilaian",
        [
            new("kode_kategori", "Kode", MaxLen: 5, Key: true), new("nama_kategori", "Nama Kategori", MaxLen: 100, Required: true),
            new("sasaran", "Sasaran", Choice, Choices: ["1", "2", "3", "4", "5", "6"]),
        ],
        "SELECT * FROM skp_kategori_penilaian WHERE CONCAT_WS(' ',kode_kategori,nama_kategori,sasaran) LIKE @p0 ORDER BY sasaran, kode_kategori",
        [("kode_kategori", "Kode"), ("nama_kategori", "Kategori"), ("sasaran", "Sasaran")])
    { Hint = SkpSasaran.Hint };

    private static readonly MasterSpec SkpKriteria = new(
        "Kriteria Pengkajian SKP", "skp_kriteria_penilaian",
        [
            new("kode_kriteria", "Kode", MaxLen: 10, Key: true), new("nama_kriteria", "Kriteria", Memo, MaxLen: 150, Required: true),
            new("kode_kategori", "Kategori", Lookup, Required: true,
                LookupSql: "SELECT kode_kategori AS kode, CONCAT('Sasaran ',sasaran,' - ',nama_kategori) AS nama FROM skp_kategori_penilaian ORDER BY sasaran, kode_kategori"),
        ],
        """
        SELECT k.*, c.nama_kategori, c.sasaran FROM skp_kriteria_penilaian k JOIN skp_kategori_penilaian c ON c.kode_kategori=k.kode_kategori
        WHERE CONCAT_WS(' ',k.kode_kriteria,k.nama_kriteria,c.nama_kategori) LIKE @p0 ORDER BY c.sasaran, k.kode_kategori, k.kode_kriteria
        """,
        [("kode_kriteria", "Kode"), ("nama_kriteria", "Kriteria"), ("kode_kategori", "Kd Kategori"), ("nama_kategori", "Kategori"), ("sasaran", "Sasaran")]);

    // ---------------------------------------------------------------- reports

    private const string HadirJoin = """
        FROM pegawai p JOIN departemen dp ON dp.dep_id=p.departemen
        LEFT JOIN rekap_presensi r ON r.id=p.id AND r.jam_datang BETWEEN @p1 AND @p2
        WHERE p.stts_aktif<>'KELUAR' AND CONCAT_WS(' ',p.nik,p.nama,dp.nama) LIKE @p0
        """;

    private static readonly ReportSpec RekapKehadiran = new(
        "Rekap Kehadiran",
        $"""
        SELECT p.nik, p.nama, dp.nama AS departemen, COUNT(r.id) AS hadir,
        IFNULL(SUM(r.shift LIKE '%Pagi%'),0) AS pagi, IFNULL(SUM(r.shift LIKE '%Siang%'),0) AS siang, IFNULL(SUM(r.shift LIKE '%Malam%'),0) AS malam,
        IFNULL(SUM(r.status LIKE 'Tepat Waktu%'),0) AS tepat, IFNULL(SUM(r.status LIKE 'Terlambat Toleransi%'),0) AS toleransi,
        IFNULL(SUM(r.status IN ('Terlambat I','Terlambat I & PSW')),0) AS t1, IFNULL(SUM(r.status IN ('Terlambat II','Terlambat II & PSW')),0) AS t2,
        IFNULL(SUM(r.status LIKE '%PSW'),0) AS psw,
        SEC_TO_TIME(IFNULL(SUM(TIME_TO_SEC(r.durasi)),0)) AS durasi,
        p.wajibmasuk, ROUND(COUNT(r.id)*100/NULLIF(p.wajibmasuk,0),2) AS persen
        {HadirJoin}
        GROUP BY p.id, p.nik, p.nama, dp.nama, p.wajibmasuk ORDER BY dp.nama, p.nama
        """,
        [("nik", "NIP"), ("nama", "Nama"), ("departemen", "Departemen"), ("hadir", "Kehadiran"), ("pagi", "Pagi"), ("siang", "Siang"), ("malam", "Malam"),
         ("tepat", "Tepat Waktu"), ("toleransi", "Toleransi"), ("t1", "Terlambat I"), ("t2", "Terlambat II"), ("psw", "PSW"),
         ("durasi", "Durasi"), ("wajibmasuk", "Wajib Masuk"), ("persen", "% Hadir")],
        ReportPeriod.DateRange, "Rekap presensi per pegawai untuk periode terpilih (dari rekap_presensi).");

    private static readonly ReportSpec RekapKehadiran2 = new(
        "Rekap Kehadiran 2",
        $"""
        SELECT p.id, p.nik, p.nama, p.pendidikan, dp.nama AS departemen,
        {string.Join(",\n", Enumerable.Range(1, 31).Select(d => $"MAX(IF(DAY(r.jam_datang)={d},r.shift,NULL)) AS d{d}"))},
        COUNT(r.id) AS jumlah
        {HadirJoin}
        GROUP BY p.id, p.nik, p.nama, p.pendidikan, dp.nama ORDER BY dp.nama, p.nama
        """,
        [("nik", "NIP"), ("nama", "Nama"), ("pendidikan", "Pendidikan"), ("departemen", "Departemen"),
         .. Enumerable.Range(1, 31).Select(d => ($"d{d}", d.ToString())), ("jumlah", "Jml")],
        ReportPeriod.Month, "Shift yang dijalani tiap pegawai per tanggal pada bulan terpilih.");

    // ---------------------------------------------------------------- catalog

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = BuildMasters();
    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = BuildReports();

    private static Dictionary<string, MasterSpec> BuildMasters()
    {
        var m = new Dictionary<string, MasterSpec>
        {
            ["btnPegawai"] = Petugas,
            ["btnDokter"] = Dokter,
            ["btnBarcode"] = Barcode,
            ["btnSidikJari"] = SidikJari,
            ["btnJamPresensi"] = JamPresensi,
            ["btnJadwalPegawai"] = Jadwal("Jadwal Pegawai", "jadwal_pegawai"),
            ["btnJadwalTambahan"] = Jadwal("Jadwal Tambahan", "jadwal_tambahan"),
            ["btnRekapHarian"] = RekapPresensi("Presensi Harian", PeriodFilter.DateRange),
            ["btnRekapBulanan"] = RekapPresensi("Presensi Bulanan", PeriodFilter.Month),
            ["btnTemporaryPresensi"] = TemporaryPresensi,
            ["btnPengajuanCuti"] = PengajuanCuti,
            ["btnMasterBerkasPegawai"] = MasterBerkas,
            ["btnBerkasPegawai"] = BerkasPegawai,
            ["btnRiwayatJabatan"] = RiwayatJabatan,
            ["btnRiwayatPendidikan"] = RiwayatPendidikan,
            ["btnRiwayatNaikGaji"] = RiwayatNaikGaji,
            ["btnKegiatanIlmiah"] = KegiatanIlmiah,
            ["btnRiwayatPenghargaan"] = RiwayatPenghargaan,
            ["btnRiwayatSuratPeringatan"] = RiwayatSuratPeringatan,
            ["btnRiwayatPenelitian"] = RiwayatPenelitian,
            ["btnSMS"] = Sms,
            ["btnPeristiwaK3"] = PeristiwaK3,
            ["btnRuangAuditKepatuhan"] = RuangAuditKepatuhan,
            ["btnAuditKepatuhanAPD"] = AuditKepatuhanApd,
            ["btnSKPKategoriPenilaian"] = SkpKategori,
            ["btnSKPKriteriaPenilaian"] = SkpKriteria,

            ["btnAuditCuciTanganMedis"] = Audit("Audit Cuci Tangan Medis", "audit_cuci_tangan_medis", AuditBy.Pegawai, Named(
                ("sebelum_menyentuh_pasien", "Sebelum Menyentuh Pasien"), ("sebelum_tehnik_aseptik", "Sebelum Tehnik Aseptik"),
                ("setelah_terpapar_cairan_tubuh_pasien", "Setelah Terpapar Cairan Tubuh Pasien"), ("setelah_kontak_dengan_pasien", "Setelah Kontak Dengan Pasien"),
                ("setelah_kontak_dengan_lingkungan_pasien", "Setelah Kontak Dengan Lingkungan Pasien"))),
            ["btnAuditPembuanganLimbah"] = Audit("Audit Pembuangan Limbah", "audit_pembuangan_limbah", AuditBy.Ruang, Named(
                ("pemisahan_limbah_oleh_penghasil_limbah", "Pemisahan Limbah Dilakukan Segera Oleh Penghasil Limbah"),
                ("limbah_infeksius_dimasukkan_kantong_kuning", "Limbah Infeksius Dimasukkan Ke Kantong Plastik Kuning"),
                ("limbah_noninfeksius_dimasukkan_kantong_hitam", "Limbah Non Infeksius Dimasukkan Ke Kantong Plastik Hitam"),
                ("limbah_tigaperempat_diikat", "Limbah 3/4 Penuh Diikat"),
                ("limbah_segera_dibawa_kepembuangan_sementara", "Limbah Segera Dibawa Ke Tempat Pembuangan Sementara"),
                ("kotak_sampah_dalam_kondisi_bersih", "Kotak Sampah Dalam Kondisi Bersih"),
                ("pembersihan_tempat_sampah_dengan_desinfekten", "Pembersihan Tempat Sampah Menggunakan Disinfektan"),
                ("pembersihan_penampungan_sementara_dengan_desinfekten", "Pembersihan Penampungan Sementara Menggunakan Disinfektan"))),
            ["btnAuditPembuanganBendaTajam"] = Audit("Audit Pembuangan Benda Tajam & Jarum", "audit_pembuangan_benda_tajam", AuditBy.Ruang, Named(
                ("setiap_injeksi_needle_langsung_dimasukkan_safety_box", "Setiap Injeksi, Needle Langsung Dimasukkan Safety Box"),
                ("setiap_pemasangan_iv_canula_langsung_dimasukkan_safety_box", "Setiap Pemasangan IV, Canula Mandrain Dimasukkan Safety Box"),
                ("setiap_benda_tajam_jarum_dimasukkan_safety_box", "Setiap Benda Tajam/Jarum Dimasukkan Safety Box"),
                ("safety_box_tigaperempat_diganti", "Safety Box Tidak Lebih Dari 3/4 Sudah Diganti"),
                ("safety_box_keadaan_bersih", "Safety Box Dalam Keadaan Bersih"),
                ("saftey_box_tertutup_setelah_digunakan", "Safety Box Tertutup Setelah Digunakan"))),
            ["btnAuditPenangananDarah"] = Audit("Audit Penanganan Darah", "audit_penanganan_darah", AuditBy.Ruang, Named(
                ("menggunakan_apd_waktu_membuang_darah", "Menggunakan APD Waktu Membuang Darah / Komponen Darah"),
                ("komponen_darah_tidak_ada_dilantai", "Komponen Darah Tidak Ada Di Lantai"),
                ("membuang_darah_pada_tempat_ditentukan", "Membuang Darah Pada Tempat Yang Ditentukan"),
                ("pembersihan_areal_tumbahan_darah", "Pembersihan Areal Tumpahan Darah Dengan Clorin / Spill Kit"),
                ("apd_dibuang_di_limbah_infeksius", "APD Yang Digunakan Dibuang Di Limbah Infeksius"),
                ("melakukan_kebersihan_tangan_setelah_prosedur", "Melakukan Kebersihan Tangan Setelah Prosedur"))),
            ["btnAuditPengelolaanLinenKotor"] = Audit("Audit Pengelolaan Linen Kotor", "audit_pengelolaan_linen_kotor", AuditBy.Ruang, Numbered(
                "Petugas ruangan segera mengambil linen kotor ke bak setelah pasien pulang",
                "Petugas laundry mengambil linen kotor ke bak linen kotor dengan cara yang benar",
                "Petugas ruangan menempatkan linen kotor sesuai wadah yang ditentukan",
                "Petugas memasukkan linen kotor ke bak linen kotor dengan benar",
                "Bak penampung linen kotor tersedia dengan baik di ruangan",
                "Plastik untuk linen kotor tersedia dengan baik di ruangan",
                "Petugas laundry mengambil linen kotor sesuai jadwal",
                "Tersedia troli khusus linen infeksius dan non infeksius")),
            ["btnAuditPenempatanPasien"] = Audit("Audit Penempatan Pasien", "audit_penempatan_pasien", AuditBy.Ruang, Numbered(
                "Petugas melakukan kebersihan tangan",
                "Petugas menggunakan APD sesuai pola transmisi infeksi pasien",
                "Pasien ditempatkan sesuai pola transmisi infeksi (kontak, udara, droplet)",
                "Pasien suspek penularan udara ditempatkan di ruang isolasi ventilasi negatif",
                "Penunggu diberi informasi pintu ruang isolasi harus selalu tertutup",
                "Konsultasi dengan Komite PPI untuk pasien yang dapat disatukan dalam 1 ruangan",
                "Petugas memberi edukasi sesuai jenis transmisi",
                "Petugas segera melepaskan APD",
                "Petugas melakukan kebersihan tangan setelah kontak pasien")),
            ["btnAuditKamarJenazah"] = Audit("Audit Kamar Jenazah", "audit_kamar_jenazah", AuditBy.None, Numbered(
                "Tersedia tempat limbah infeksius dan non infeksius",
                "Tersedia tempat linen infeksius dan non infeksius",
                "Tersedia APD lengkap (masker, sarung tangan, apron, sepatu boot, kacamata, tutup kepala)",
                "Petugas menggunakan APD sesuai indikasi",
                "Tersedia wastafel cuci tangan lengkap dengan tisu, tempat sampah dan sabun",
                "Tersedia handrub",
                "Tersedia peralatan memandikan & perawatan jenazah",
                "Keranda bersih, tidak berdebu dan dibersihkan setiap selesai digunakan")),
            ["btnAuditBundleIADP"] = Audit("Audit Bundle IADP", "audit_bundle_iadp", AuditBy.Pegawai, Named(
                ("handhygiene", "Handhygiene"), ("apd", "APD"), ("skin_antiseptik", "Skin Antiseptik"), ("lokasi_iv", "Lokasi IV"), ("perawatan_rutin", "Perawatan Rutin"))),
            ["btnAuditBundleIDO"] = Audit("Audit Bundle IDO", "audit_bundle_ido", AuditBy.Ruang, Named(
                ("pencukuran_rambut", "Pencukuran Rambut Yang Mengganggu Jalannya Operasi"), ("antibiotik", "Antibiotik"),
                ("temperature", "Temperatur (Suhu Pasien)"), ("sugar", "Sugar (Gula Darah Pasien)"))),
            ["btnAuditFasilitasKebersihanTangan"] = Audit("Audit Fasilitas Kebersihan Tangan", "audit_fasilitas_kebersihan_tangan", AuditBy.Ruang, Numbered(
                "Tersedia wastafel dan berfungsi dengan baik", "Tersedia sabun cair di setiap wastafel", "Tersedia tisu kertas di setiap wastafel",
                "Tersedia cairan antiseptik cuci tangan di wastafel", "Wastafel bebas dari peralatan yang tidak tepat", "Fasilitas tempat cuci tangan bersih",
                "Ada tempat sampah di bawah wastafel", "Tersedia handrub di setiap ruangan", "Tersedia poster kebersihan tangan")),
            ["btnAuditFasilitasAPD"] = Audit("Audit Fasilitas APD", "audit_fasilitas_apd", AuditBy.Ruang, Numbered(
                "Ada kebijakan pembuangan limbah rumah sakit",
                "Tersedia masker di ruang perawatan, intensif, ruang tindakan sesuai kebutuhan",
                "Tersedia sarung tangan non steril & steril sesuai kebutuhan",
                "Tersedia topi di ruang intensif & ruang tindakan sesuai kebutuhan",
                "Tersedia gaun di ruangan, intensif, ruang tindakan sesuai kebutuhan",
                "Tersedia sepatu boot (sepatu penutup)")),
            ["btnAuditPembuanganLimbahCairInfeksius"] = Audit("Audit Pembuangan Limbah Cair Infeksius", "audit_pembuangan_limbah_cair_infeksius", AuditBy.Ruang, Numbered(
                "Kebersihan tangan sebelum membuang limbah cair infeksius",
                "Memakai APD minimal sarung tangan & masker sebelum membuang",
                "Limbah cair infeksius dibuang di spoelhoek atau WC ruang perawatan",
                "Limbah cair infeksius disiram air mengalir yang banyak",
                "APD dilepas sesuai prosedur setelah pembuangan",
                "Kebersihan tangan sesuai prosedur setelah pembuangan")),
            ["btnAuditSterilisasiAlat"] = Audit("Audit Sterilisasi Alat", "audit_sterilisasi_alat", AuditBy.Ruang, Numbered(
                "Petugas memakai APD (masker, sarung tangan, gaun) lengkap", "Perendaman alat sampai seluruh permukaan",
                "Perendaman dengan cairan enzymatic 10-15 menit", "Alat dibersihkan dengan sikat lembut sampai sela-sela",
                "Alat dibilas air mengalir setelah direndam enzymatic", "Petugas mengeringkan alat yang telah dibersihkan",
                "Petugas melepaskan APD yang sudah dipakai", "Petugas melakukan kebersihan tangan",
                "Pengepakan dan pelabelan alat", "Alat siap steril dibawa ke ruang steril",
                "Alat steril disimpan di lemari tertutup ±60 cm dari lantai, tidak tercampur barang lain")),
            ["btnAuditBundleISK"] = Audit("Audit Bundle ISK", "audit_bundle_isk", AuditBy.Ruang, Named(
                ("pemasangan_sesuai_indikasi", "Pemasangan sesuai indikasi"), ("hand_hygiene", "Hand Hygiene"),
                ("menggunakan_apd_yang_tepat", "Menggunakan APD yang tepat"), ("pemasangan_menggunakan_alat_steril", "Pemasangan menggunakan alat steril"),
                ("segera_dilepas_setelah_tidak_diperlukan", "Segera dilepas setelah tidak diperlukan"),
                ("pengisian_balon_sesuai_petunjuk", "Pengisian balon sesuai petunjuk produk (20 ml)"),
                ("fiksasi_kateter_dengan_plester", "Fiksasi kateter dengan plester"),
                ("urinebag_menggantung_tidak_menyentuh_lantai", "Urine bag menggantung tidak menyentuh lantai"))),
            ["btnAuditBundlePLABSI"] = Audit("Audit Bundle PLABSI", "audit_bundle_plabsi", AuditBy.Ruang, Named(
                ("sebelum_melakukan_hand_hygiene", "Hand hygiene & 5 moment (sebelum)"), ("menggunakan_apd_lengkap", "APD lengkap & sarung tangan steril"),
                ("lokasi_pemasangan_sesuai", "Lokasi pemasangan sesuai"), ("alat_yang_digunakan_steril", "Alat yang digunakan steril"),
                ("pembersihan_kulit", "Pembersihan kulit dengan chlorhexidine 2%/4% atau alkohol 70%"),
                ("setelah_melakukan_hand_hygiene", "Hand hygiene & 5 moment (sesudah)"), ("perawatan_dressing_infus", "Perawatan dressing infus jika kotor/basah"),
                ("spoit_yang_digunakan_disposible", "Spoit yang digunakan disposable"),
                ("memberi_tanggal_dan_jam_pemasangan_infus", "Memberi tanggal & jam pemasangan infus"), ("set_infus_setiap_72jam", "Set infus diganti setiap 72 jam"))),
            ["btnAuditBundleVAP"] = Audit("Audit Bundle VAP", "audit_bundle_vap", AuditBy.Ruang, Named(
                ("posisi_kepala", "Posisi kepala 30° s/d 45°"), ("pengkajian_setiap_hari", "Pengkajian harian sedasi & ekstubasi"),
                ("hand_hygiene", "Hand Hygiene"), ("oral_hygiene", "Oral Hygiene secara rutin"), ("suction_manajemen_sekresi", "Suction / Manajemen Sekresi"),
                ("profilaksis_peptic_ulcer", "Profilaksis Peptic Ulcer"), ("dvt_profiklasisi", "DVT Profilaksis jika indikasi"),
                ("penggunaan_apd_sesuai", "Penggunaan APD yang sesuai"))),
        };

        string[] masterKeys = ["btnJenisCideraK3", "btnPenyebabKecelakaanK3", "btnJenisLukaK3", "btnLokasiKejadianK3", "btnDampakCideraK3", "btnJenisPekerjaanK3", "btnBagianTubuhK3"];
        for (var i = 0; i < K3Masters.Length; i++) m[masterKeys[i]] = K3Master(K3Masters[i]);
        return m;
    }

    private static Dictionary<string, ReportSpec> BuildReports()
    {
        var r = new Dictionary<string, ReportSpec>
        {
            ["btnRekapPresensi"] = RekapKehadiran,
            ["btnRekapPresensi2"] = RekapKehadiran2,
        };
        string[] perTahun = ["btnJenisCideraK3PerTahun", "btnPenyebabKecelakaanK3PerTahun", "btnJenisLukaK3PerTahun", "btnLokasiKejadianK3PerTahun",
                             "btnDampakCideraK3PerTahun", "btnJenisPekerjaanK3PerTahun", "btnBagianTubuhK3PerTahun"];
        for (var i = 0; i < K3Masters.Length; i++) r[perTahun[i]] = K3PerTahun(K3Masters[i]);
        return r;
    }
}

/// <summary>The six international patient safety goals the SKP categories are grouped by.</summary>
public static class SkpSasaran
{
    public static readonly string[] Names =
    [
        "Ketepatan identifikasi pasien",
        "Peningkatan komunikasi yang efektif",
        "Peningkatan keamanan obat yang perlu diwaspadai",
        "Kepastian tepat lokasi, tepat prosedur, tepat pasien operasi",
        "Pengurangan risiko infeksi terkait pelayanan kesehatan",
        "Pengurangan risiko pasien jatuh",
    ];

    public static string Hint => "Sasaran: " + string.Join("; ", Names.Select((n, i) => $"{i + 1} = {n}"));
}
