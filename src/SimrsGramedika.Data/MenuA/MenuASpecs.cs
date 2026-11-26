using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.MenuA;

public static class MenuASpecs
{
    private const string Rawat = "JOIN reg_periksa r ON r.no_rawat=x.no_rawat JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis";
    private const string Petugas = "SELECT nip AS kode,nama FROM petugas ORDER BY nama";
    private static readonly (string, string)[] Pasien = [("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien")];

    private static MasterField NoRawat(bool key = true) => new("no_rawat", "No. Rawat", MaxLen: 17, Key: key, Required: true);
    private static MasterField Nip() => new("nip", "Petugas", FieldKind.Lookup, LookupSql: Petugas, Required: true);
    private static MasterField Tanggal(bool key = false) => new("tanggal", "Tanggal", FieldKind.Date, Key: key, Required: true);
    private static MasterField Jam(string column, string label, bool key = false) => new(column, label, FieldKind.Time, Key: key, Required: true)
        { DefaultFactory = () => DateTime.Now.TimeOfDay };
    private static MasterField Pilihan(string column, string label, params string[] choices) =>
        new(column, label, FieldKind.Choice, Choices: choices, Required: true);
    private static MasterField YaTidak(string column, string label) => Pilihan(column, label, "Tidak", "Ya");

    private static MasterSpec RawatSpec(string title, string table, MasterField[] fields, string period,
        string joins, string extra, string search, (string, string)[] display) =>
        new(title, table, fields, $"""
            SELECT x.*, r.no_rkm_medis,p.nm_pasien {extra}
            FROM {table} x {Rawat} {joins}
            WHERE {period} BETWEEN @p1 AND @p2
            AND CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien {search}) LIKE @p0
            ORDER BY {period} DESC,x.no_rawat LIMIT 1000
            """, [.. Pasien, .. display]) { Period = PeriodFilter.DateRange };

    public static readonly MasterSpec PermintaanRanap = RawatSpec("Permintaan Rawat Inap", "permintaan_ranap",
        [NoRawat(), Tanggal(), new("kd_kamar", "Kamar", FieldKind.Lookup, LookupSql:
            "SELECT k.kd_kamar AS kode,CONCAT(b.nm_bangsal,' / ',k.status) AS nama FROM kamar k JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal ORDER BY b.nm_bangsal,k.kd_kamar", Required: true),
         new("diagnosa", "Diagnosis", MaxLen: 50, Required: true), new("alasan", "Alasan Rawat Inap", MaxLen: 50),
         new("catatan", "Catatan", FieldKind.Memo, 150)],
        "x.tanggal", "JOIN kamar k ON k.kd_kamar=x.kd_kamar JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal",
        ",b.nm_bangsal, IF(EXISTS(SELECT 1 FROM kamar_inap i WHERE i.no_rawat=x.no_rawat),'Sudah masuk','Menunggu') AS status_masuk",
        ",b.nm_bangsal,x.diagnosa",
        [("tanggal", "Tanggal"), ("kd_kamar", "Kamar"), ("nm_bangsal", "Bangsal"), ("status_masuk", "Status"), ("diagnosa", "Diagnosis"),
         ("alasan", "Alasan"), ("catatan", "Catatan")]) with
        { BeforeSave = MenuAWrites.PesanKamarAsync, BeforeDelete = MenuAWrites.BatalPesanKamarAsync };

    public static readonly MasterSpec JadwalOperasi = RawatSpec("Jadwal Operasi", "booking_operasi",
        [NoRawat(), new("kode_paket", "Paket Operasi", FieldKind.Lookup, LookupSql:
            "SELECT kode_paket AS kode,nm_perawatan AS nama FROM paket_operasi ORDER BY nm_perawatan", Key: true),
         Tanggal(key: true), Jam("jam_mulai", "Jam Mulai", key: true) with { DefaultFactory = null, Default = TimeSpan.Zero },
         Jam("jam_selesai", "Jam Selesai") with { DefaultFactory = null, Default = TimeSpan.Zero },
         Pilihan("status", "Status", "Menunggu", "Proses Operasi", "Selesai"),
         new("kd_dokter", "Operator", FieldKind.Lookup, LookupSql: "SELECT kd_dokter AS kode,nm_dokter AS nama FROM dokter ORDER BY nm_dokter", Required: true),
         new("kd_ruang_ok", "Ruang Operasi", FieldKind.Lookup, LookupSql: "SELECT kd_ruang_ok AS kode,nm_ruang_ok AS nama FROM ruang_ok ORDER BY nm_ruang_ok", Required: true)],
        "x.tanggal", """
            JOIN paket_operasi o ON o.kode_paket=x.kode_paket LEFT JOIN dokter d ON d.kd_dokter=x.kd_dokter
            JOIN ruang_ok ruang ON ruang.kd_ruang_ok=x.kd_ruang_ok
            """, ",o.nm_perawatan,d.nm_dokter,ruang.nm_ruang_ok", ",o.nm_perawatan,d.nm_dokter,ruang.nm_ruang_ok,x.status",
        [("tanggal", "Tanggal"), ("jam_mulai", "Mulai"), ("jam_selesai", "Selesai"), ("nm_perawatan", "Operasi"),
         ("nm_dokter", "Operator"), ("nm_ruang_ok", "Ruang"), ("status", "Status")]) with
        { BeforeSave = MenuAWrites.CekJadwalOperasiAsync, BeforeDelete = MenuAWrites.CekHapusDenganBillingAsync };

    public static readonly MasterSpec DietPasien = RawatSpec("Diet Pasien", "detail_beri_diet",
        [NoRawat(), new("kd_kamar", "Kamar", FieldKind.Lookup, LookupSql:
            "SELECT k.kd_kamar AS kode,b.nm_bangsal AS nama FROM kamar k JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal ORDER BY b.nm_bangsal,k.kd_kamar", Key: true),
         Tanggal(key: true), new("waktu", "Waktu Makan", FieldKind.Lookup, LookupSql:
            "SELECT waktu AS kode,COALESCE(jam,'') AS nama FROM jam_diet_pasien ORDER BY waktu", Key: true),
         new("kd_diet", "Jenis Diet", FieldKind.Lookup, LookupSql: "SELECT kd_diet AS kode,nama_diet AS nama FROM diet ORDER BY nama_diet", Key: true)],
        "x.tanggal", "JOIN diet d ON d.kd_diet=x.kd_diet JOIN kamar k ON k.kd_kamar=x.kd_kamar JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal",
        ",d.nama_diet,b.nm_bangsal", ",d.nama_diet,b.nm_bangsal,x.waktu",
        [("tanggal", "Tanggal"), ("kd_kamar", "Kamar"), ("nm_bangsal", "Bangsal"), ("waktu", "Waktu Makan"), ("nama_diet", "Diet")]) with
        { CanEdit = false, BeforeSave = MenuAWrites.CekDietAsync };

    public static readonly MasterSpec SkriningRawatJalan = new("Skrining Rawat Jalan", "skrining_rawat_jalan",
        [Tanggal(key: true), Jam("jam", "Jam", key: true), new("no_rkm_medis", "No. RM", MaxLen: 15, Key: true),
         YaTidak("geriatri", "Geriatri"), Pilihan("kesadaran", "Kesadaran", "Sadar penuh", "Tampak mengantuk/gelisah bicara tidak jelas", "Tidak sadar", "Batuk > 2 minggu"),
         Pilihan("pernapasan", "Pernapasan", "Nafas normal", "Tampak sesak", "Tidak bernafas"),
         Pilihan("nyeri_dada", "Nyeri Dada", "Tidak ada", "Ada (Tingkat sedang)", "Nyeri dada kiri tembus punggung"),
         Pilihan("skala_nyeri", "Skala Nyeri", "Tidak sakit", "Sedikit sakit", "Agak mengganggu", "Mengganggu aktivitas", "Sangat mengganggu", "Tak tertahankan"),
         Pilihan("batuk", "Batuk", "Tidak", "Ya < 2 minggu", "Ya > 2 minggu"), YaTidak("risiko_jatuh", "Risiko Jatuh"),
         Pilihan("keputusan", "Keputusan", "Sesuai antrian", "IGD"), Nip()],
        """
        SELECT x.*,p.nm_pasien,t.nama AS petugas FROM skrining_rawat_jalan x
        JOIN pasien p ON p.no_rkm_medis=x.no_rkm_medis LEFT JOIN petugas t ON t.nip=x.nip
        WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rkm_medis,p.nm_pasien,t.nama,x.keputusan) LIKE @p0
        ORDER BY x.tanggal DESC,x.jam DESC LIMIT 1000
        """, [("tanggal", "Tanggal"), ("jam", "Jam"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("geriatri", "Geriatri"),
              ("kesadaran", "Kesadaran"), ("pernapasan", "Pernapasan"), ("nyeri_dada", "Nyeri Dada"), ("skala_nyeri", "Skala Nyeri"),
              ("batuk", "Batuk"), ("risiko_jatuh", "Risiko Jatuh"), ("keputusan", "Keputusan"), ("petugas", "Petugas")])
        { Period = PeriodFilter.DateRange, CanEdit = false };

    public static readonly MasterSpec DeteksiCorona = RawatSpec("Deteksi Dini Corona", "deteksi_dini_corona",
        [NoRawat(), Tanggal(), Nip(), YaTidak("gejala_demam", "Demam"), YaTidak("gejala_batuk", "Batuk"), YaTidak("gejala_sesak", "Sesak"),
         new("gejala_tanggal_pertama", "Tanggal Gejala Pertama", FieldKind.Date), new("gejala_riwayat_sakit", "Riwayat Sakit", MaxLen: 50),
         new("gejala_riwayat_periksa", "Riwayat Pemeriksaan", MaxLen: 50), YaTidak("faktor_riwayat_perjalanan", "Riwayat Perjalanan"),
         new("faktor_asal_daerah", "Asal Daerah", MaxLen: 50), new("faktor_tanggal_kedatangan", "Tanggal Kedatangan", FieldKind.Date, Required: true),
         YaTidak("faktor_paparan_kontakpositif", "Kontak Positif"), YaTidak("faktor_paparan_kontakpdp", "Kontak PDP"),
         YaTidak("faktor_paparan_faskespositif", "Paparan Faskes Positif"), YaTidak("faktor_paparan_perjalananln", "Perjalanan Luar Negeri"),
         YaTidak("faktor_paparan_pasarhewan", "Paparan Pasar Hewan"),
         Pilihan("kesimpulan", "Kesimpulan", "Bukan ketiganya", "ODP", "PDP", "OTG"),
         Pilihan("tindak_lanjut", "Tindak Lanjut", "Rawat Jalan", "Rawat Inap", "Rujuk")],
        "x.tanggal", "LEFT JOIN petugas t ON t.nip=x.nip", ",t.nama AS petugas", ",t.nama,x.kesimpulan,x.tindak_lanjut",
        [("tanggal", "Tanggal"), ("gejala_demam", "Demam"), ("gejala_batuk", "Batuk"), ("gejala_sesak", "Sesak"),
         ("kesimpulan", "Kesimpulan"), ("tindak_lanjut", "Tindak Lanjut"), ("petugas", "Petugas")]);

    public static readonly MasterSpec PerkiraanBiaya = RawatSpec("Perkiraan Biaya Ranap", "perkiraan_biaya_ranap",
        [NoRawat(), new("kd_penyakit", "Diagnosis", FieldKind.Lookup, LookupSql: "SELECT kd_penyakit AS kode,nm_penyakit AS nama FROM penyakit ORDER BY nm_penyakit", Required: true),
         new("tarif", "Perkiraan Biaya (Rp)", FieldKind.Number, Required: true)],
        "r.tgl_registrasi", "JOIN penyakit d ON d.kd_penyakit=x.kd_penyakit", ",r.tgl_registrasi,d.nm_penyakit", ",x.kd_penyakit,d.nm_penyakit",
        [("tgl_registrasi", "Registrasi"), ("kd_penyakit", "ICD-10"), ("nm_penyakit", "Diagnosis"), ("tarif", "Perkiraan Biaya")]) with
        { BeforeSave = MenuAWrites.CekPerkiraanBiayaAsync };

    public static readonly MasterSpec PermintaanKfr = RawatSpec("Permintaan Layanan Program KFR", "layanan_kedokteran_fisik_rehabilitasi",
        [NoRawat()],
        "x.tanggal", "JOIN dokter d ON d.kd_dokter=x.kd_dokter",
        ",d.nm_dokter,(SELECT COUNT(*) FROM layanan_program_kfr l WHERE l.no_rawat_layanan=x.no_rawat) AS sesi",
        ",d.nm_dokter,x.diagnosa_medis,x.status_program",
        [("tanggal", "Tanggal"), ("nm_dokter", "Dokter"), ("diagnosa_medis", "Diagnosis Medis"), ("tatalaksana", "Permintaan Terapi"),
         ("status_program", "Status Program"), ("sesi", "Sesi")]) with
        {
            CanInsert = false, CanEdit = false, CanDelete = false,
            Actions =
            [
                new("Selesaikan Program", "UPDATE layanan_kedokteran_fisik_rehabilitasi SET status_program='Sudah Selesai' WHERE no_rawat=@p0",
                    "Selesaikan program pasien terpilih?"),
                new("Buka Kembali Program", "UPDATE layanan_kedokteran_fisik_rehabilitasi SET status_program='Belum Selesai' WHERE no_rawat=@p0",
                    "Aktifkan kembali program pasien terpilih?")
            ]
        };

    public static readonly MasterSpec SesiKfr = RawatSpec("Layanan Program KFR", "layanan_program_kfr",
        [new("no_rawat_layanan", "No. Rawat Permintaan", MaxLen: 17, Required: true), NoRawat(),
         new("tanggal", "Tanggal Layanan", FieldKind.DateTime, Required: true), Nip(), new("program", "Program", MaxLen: 50, Required: true)],
        "x.tanggal", "JOIN petugas t ON t.nip=x.nip", ",t.nama AS petugas", ",x.no_rawat_layanan,x.program,t.nama",
        [("no_rawat_layanan", "No. Permintaan"), ("tanggal", "Tanggal Layanan"), ("program", "Program"), ("petugas", "Petugas")]) with
        { BeforeSave = MenuAWrites.CekSesiKfrAsync };

    public static readonly MasterSpec Deposit = RawatSpec("Deposit Pasien", "deposit",
        [new("no_deposit", "No. Deposit", Key: true, AutoKey: true), NoRawat(false),
         new("tgl_deposit", "Tanggal Deposit", FieldKind.DateTime, Required: true),
         new("nama_bayar", "Akun Bayar", FieldKind.Lookup, LookupSql: "SELECT nama_bayar AS kode,CONCAT(COALESCE(ppn,0),'% admin') AS nama FROM akun_bayar ORDER BY nama_bayar", Required: true),
         new("besar_deposit", "Deposit (Rp)", FieldKind.Number, Required: true), Nip(), new("keterangan", "Keterangan", MaxLen: 70)],
        "x.tgl_deposit", "JOIN petugas t ON t.nip=x.nip",
        ",t.nama AS petugas,x.besar_deposit+x.besarppn AS total", ",x.no_deposit,x.nama_bayar,t.nama",
        [("no_deposit", "No. Deposit"), ("tgl_deposit", "Tanggal"), ("nama_bayar", "Akun Bayar"), ("besar_deposit", "Deposit"),
         ("besarppn", "PPN/Admin"), ("total", "Dibayar"), ("petugas", "Petugas"), ("keterangan", "Keterangan")]) with
        {
            CanEdit = false,
            AutoKeySql = MenuAWrites.NomorDepositSql, AutoKeyLen = 4, AutoKeyPrefix = "DP{0:yyyyMMdd}", AutoKeyDateField = "tgl_deposit",
            BeforeSave = MenuAWrites.PostDepositAsync, BeforeDelete = MenuAWrites.BalikDepositAsync, ComputedColumns = ["besarppn"]
        };

    public static readonly MasterSpec ResepPulang = RawatSpec("Resep Pulang", "resep_pulang",
        [NoRawat(), new("kode_brng", "Obat", FieldKind.Lookup, LookupSql: "SELECT kode_brng AS kode,nama_brng AS nama FROM databarang ORDER BY nama_brng", Key: true),
         new("jml_barang", "Jumlah", FieldKind.Number, Required: true), new("harga", "Harga Satuan (Rp)", FieldKind.Number, Required: true),
         new("dosis", "Aturan Pakai", MaxLen: 150, Required: true), Tanggal(key: true), Jam("jam", "Jam", key: true),
         new("kd_bangsal", "Depo", FieldKind.Lookup, LookupSql: "SELECT kd_bangsal AS kode,nm_bangsal AS nama FROM bangsal ORDER BY nm_bangsal", Required: true),
         new("no_batch", "No. Batch", MaxLen: 20, Key: true, AllowEmptyKey: true), new("no_faktur", "No. Faktur", MaxLen: 20, Key: true, AllowEmptyKey: true)],
        "x.tanggal", "JOIN databarang b ON b.kode_brng=x.kode_brng JOIN bangsal d ON d.kd_bangsal=x.kd_bangsal",
        ",b.nama_brng,d.nm_bangsal,CAST(x.jml_barang AS CHAR) AS jumlah_tampil", ",b.nama_brng,d.nm_bangsal,x.no_batch",
        [("tanggal", "Tanggal"), ("jam", "Jam"), ("nama_brng", "Obat"), ("jumlah_tampil", "Jumlah"), ("dosis", "Aturan Pakai"),
         ("harga", "Harga"), ("total", "Total"), ("nm_bangsal", "Depo"), ("no_batch", "No. Batch"), ("no_faktur", "No. Faktur")]) with
        { CanEdit = false, BeforeSave = MenuAWrites.KeluarkanResepAsync, BeforeDelete = MenuAWrites.KembalikanResepAsync, ComputedColumns = ["total"] };

    public static readonly IReadOnlyDictionary<string, MasterSpec> Tiles = new Dictionary<string, MasterSpec>
    {
        ["btnPermintaanRanap"] = PermintaanRanap,
        ["btnJadwalOperasi"] = JadwalOperasi,
        ["btnPermintaanLayananProgramKFR"] = PermintaanKfr,
        ["btnResepPulang"] = ResepPulang,
        ["btnDiet"] = DietPasien,
        ["btnSkriningRawatJalan"] = SkriningRawatJalan,
        ["btnDeteksiDiniCorona"] = DeteksiCorona,
        ["btnDeposit"] = Deposit,
        ["btnPerkiraanBiayaRanap"] = PerkiraanBiaya,
    };
}
