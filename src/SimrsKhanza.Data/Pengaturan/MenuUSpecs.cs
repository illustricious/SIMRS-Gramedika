using System.Globalization;
using SimrsKhanza.Data.Master;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.Pengaturan;

public static class MenuUSpecs
{
    private const string Dokter = "SELECT kd_dokter AS kode,nm_dokter AS nama FROM dokter ORDER BY nm_dokter";
    private const string Penjab = "SELECT kd_pj AS kode,png_jawab AS nama FROM penjab ORDER BY png_jawab";
    private const string Bangsal = "SELECT kd_bangsal AS kode,nm_bangsal AS nama FROM bangsal ORDER BY nm_bangsal";
    private const string Kamar = "SELECT k.kd_kamar AS kode,b.nm_bangsal AS nama FROM kamar k JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal ORDER BY b.nm_bangsal,k.kd_kamar";
    private const string Poli = "SELECT kd_poli AS kode,nm_poli AS nama FROM poliklinik ORDER BY nm_poli";
    private const string Obat = "SELECT kode_brng AS kode,nama_brng AS nama FROM databarang ORDER BY nama_brng";
    private const string Tindakan = "SELECT kd_jenis_prw AS kode,nm_perawatan AS nama FROM jns_perawatan ORDER BY nm_perawatan";
    private static readonly string[] Kelas = ["Kelas 1", "Kelas 2", "Kelas 3", "Kelas Utama", "Kelas VIP", "Kelas VVIP"];

    private static MasterField Lookup(string column, string label, string sql, bool key = false, bool required = true) =>
        new(column, label, FieldKind.Lookup, LookupSql: sql, Key: key, Required: required);

    private static MasterField Text(string column, string label, int length, bool key = false) =>
        new(column, label, MaxLen: length, Key: key, Required: true);

    private static MasterField Number(string column, string label) => new(column, label, FieldKind.Number);
    private static MasterField Integer(string column, string label) => new(column, label, FieldKind.Integer);

    private static MasterField Option(string column, string label, params string[] choices) =>
        new(column, label, FieldKind.Choice, Choices: choices, Required: true);

    private static MasterField Toggle(string column, string label) => Option(column, label, "No", "Yes");

    private static MasterSpec Single(string title, string table, params MasterField[] fields) =>
        new(title, table, fields, $"SELECT * FROM {table} LIMIT 2", [])
        {
            Singleton = true, CanDelete = false
        };

    private static MasterSpec List(string title, string table, MasterField[] fields, string joins,
        string names, string search, params (string Column, string Header)[] labels) =>
        new(title, table, fields, $"""
            SELECT x.* {names} FROM {table} x {joins}
            WHERE CONCAT_WS(' ',{search}) LIKE @p0 ORDER BY {search.Split(',')[0]} LIMIT 1000
            """, labels)
        { BeforeSave = ValidasiAngkaAsync };

    private static Task ValidasiAngkaAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        foreach (var (column, value) in values)
        {
            if (value is double n && (!double.IsFinite(n) || n < (column == "hargajual" ? -100 : 0)))
                throw new ArgumentException("Masukkan angka biaya atau persentase yang valid.");
            if (value is long count && (count < 0 || count > int.MaxValue))
                throw new ArgumentException("Masukkan jumlah yang valid.");
        }
        return Task.CompletedTask;
    }

    private static MasterSpec Depo(bool ranap)
    {
        var unit = ranap ? "kd_bangsal" : "kd_poli";
        var depo = ranap ? "kd_depo" : "kd_bangsal";
        var join = ranap ? "bangsal" : "poliklinik";
        var name = ranap ? "nm_bangsal" : "nm_poli";
        return List(ranap ? "Depo Rawat Inap" : "Depo Rawat Jalan", ranap ? "set_depo_ranap" : "set_depo_ralan",
            [Lookup(unit, ranap ? "Bangsal" : "Poliklinik", ranap ? Bangsal : Poli, key: true),
             Lookup(depo, "Depo", Bangsal, key: true)],
            $"JOIN {join} u ON u.{unit}=x.{unit} JOIN bangsal d ON d.kd_bangsal=x.{depo}",
            $",u.{name} AS unit,d.nm_bangsal AS depo", $"u.{name},d.nm_bangsal,x.{unit},x.{depo}",
            (unit, "Kode Unit"), ("unit", "Unit"), (depo, "Kode Depo"), ("depo", "Depo"));
    }

    private static MasterSpec HargaObat(bool ranap) =>
        List(ranap ? "Set Obat Ranap" : "Set Obat Ralan", ranap ? "set_harga_obat_ranap" : "set_harga_obat_ralan",
            [Lookup("kd_pj", "Cara Bayar", Penjab, key: true),
             .. (ranap ? new[] { Option("kelas", "Kelas", Kelas) with { Key = true } } : []),
             Number("hargajual", "Kenaikan Harga (%)")],
            "JOIN penjab p ON p.kd_pj=x.kd_pj", ",p.png_jawab", "p.png_jawab,x.kd_pj",
            [("kd_pj", "Kode"), ("png_jawab", "Cara Bayar"),
             .. (ranap ? new (string, string)[] { ("kelas", "Kelas") } : []),
             ("hargajual", "Kenaikan (%)")]);

    private static MasterSpec Biaya(bool harian) =>
        List(harian ? "Biaya Harian" : "Biaya Masuk Sekali", harian ? "biaya_harian" : "biaya_sekali",
            [Lookup("kd_kamar", "Kamar", Kamar, key: true), Text("nama_biaya", "Nama Biaya", 50, true),
             Number("besar_biaya", "Besaran (Rp)"), .. (harian ? new[] { Integer("jml", "Jumlah") with { Default = 1L } } : [])],
            "JOIN kamar k ON k.kd_kamar=x.kd_kamar JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal", ",b.nm_bangsal",
            "x.kd_kamar,b.nm_bangsal,x.nama_biaya",
            [("kd_kamar", "Kamar"), ("nm_bangsal", "Bangsal"), ("nama_biaya", "Biaya"), ("besar_biaya", "Besaran"),
             .. (harian ? new (string, string)[] { ("jml", "Jumlah") } : [])]);

    private static MasterSpec OtoRalan(string title, string table, bool dokter) =>
        List(title, table,
            [.. (dokter ? new[] { Lookup("kd_dokter", "Dokter", Dokter, key: true) } : []),
             Lookup("kd_jenis_prw", "Tindakan", Tindakan, key: true), Lookup("kd_pj", "Cara Bayar", Penjab, key: true)],
            "JOIN jns_perawatan j ON j.kd_jenis_prw=x.kd_jenis_prw JOIN penjab p ON p.kd_pj=x.kd_pj"
                + (dokter ? " JOIN dokter d ON d.kd_dokter=x.kd_dokter" : ""),
            ",j.nm_perawatan,p.png_jawab" + (dokter ? ",d.nm_dokter" : ""),
            "j.nm_perawatan,p.png_jawab" + (dokter ? ",d.nm_dokter" : ""),
            [.. (dokter ? new (string, string)[] { ("nm_dokter", "Dokter") } : []),
             ("nm_perawatan", "Tindakan"), ("png_jawab", "Cara Bayar")]);

    private static readonly (string Column, string Label)[] Margin =
    [
        ("ralan","Rawat Jalan"), ("kelas1","Kelas 1"), ("kelas2","Kelas 2"), ("kelas3","Kelas 3"),
        ("utama","Utama"), ("vip","VIP"), ("vvip","VVIP"), ("beliluar","Beli Luar"),
        ("jualbebas","Jual Bebas"), ("karyawan","Karyawan")
    ];

    private static MasterField[] MarginFields() => Margin.Select(m => Number(m.Column, m.Label + " (%)")).ToArray();

    private static MasterSpec MarginLookup(bool perBarang)
    {
        var key = perBarang ? "kode_brng" : "kdjns";
        var lookup = perBarang ? Obat : "SELECT kdjns AS kode,nama FROM jenis ORDER BY nama";
        var name = perBarang ? "nama_brng" : "nama";
        var join = perBarang ? "databarang" : "jenis";
        return List(perBarang ? "Margin Per Barang" : "Margin Per Jenis", perBarang ? "setpenjualanperbarang" : "setpenjualan",
            [Lookup(key, perBarang ? "Barang" : "Jenis", lookup, key: true), .. MarginFields()],
            $"JOIN {join} j ON j.{key}=x.{key}", $",j.{name} AS nama", $"j.{name},x.{key}",
            [(key, "Kode"), ("nama", "Nama"), .. Margin]);
    }

    private static MasterField[] Flags(params (string Column, string Label)[] values) =>
        values.Select(v => Toggle(v.Column, v.Label)).ToArray();

    private static MasterSpec LayananBilling(string title, string table) => List(title, table,
        [Text("nama_service", "Layanan", 100, true), Number("besar", "Besaran (%)"),
         .. Flags(("laborat","Laboratorium"), ("radiologi","Radiologi"), ("operasi","Operasi"), ("obat","Obat"),
            ("ranap_dokter","Dokter Ranap"), ("ranap_paramedis","Paramedis Ranap"), ("ralan_dokter","Dokter Ralan"),
            ("ralan_paramedis","Paramedis Ralan"), ("tambahan","Tambahan"), ("potongan","Potongan"),
            ("kamar","Kamar"), ("registrasi","Registrasi"), ("harian","Harian"), ("retur_Obat","Retur Obat"), ("resep_Pulang","Resep Pulang"))],
        "", "", "x.nama_service", ("nama_service", "Layanan"), ("besar", "Besaran (%)"));

    private static MasterSpec Nota() => Single("Nota dan Kuitansi", "set_nota",
        [.. new (string Column, string Label)[]
         {
             ("notaralan", "Nota Ralan"), ("kwitansiralan", "Kuitansi Ralan"), ("nota1ranap", "Nota 1 Ranap"),
             ("nota2ranap", "Nota 2 Ranap"), ("kwitansiranap", "Kuitansi Ranap"), ("notaapotek", "Nota Apotek"),
             ("notalabrad", "Nota Lab/Radiologi"), ("notatoko", "Nota Toko"), ("notalabkesling", "Nota Lab Kesling"),
             ("kwitansilabkesling", "Kuitansi Lab Kesling")
         }.Select(v => new MasterField(v.Column, v.Label, MaxLen: 11)),
         .. Flags(("cetaknotasimpanralan","Cetak Setelah Simpan Ralan"), ("cetaknotasimpanranap","Cetak Setelah Simpan Ranap"),
             ("rinciandokterralan","Rincian Dokter Ralan"), ("rinciandokterranap","Rincian Dokter Ranap"),
             ("centangdokterralan","Centang Dokter Ralan"), ("centangdokterranap","Centang Dokter Ranap"),
             ("tampilkan_administrasi_di_billingranap","Administrasi Billing Ranap"), ("rincianoperasi","Rincian Operasi"),
             ("tampilkan_ppnobat_ralan","PPN Obat Ralan"), ("tampilkan_ppnobat_ranap","PPN Obat Ranap"),
             ("tampilkan_tombol_nota_ralan","Tombol Nota Ralan"), ("tampilkan_tombol_nota_ranap","Tombol Nota Ranap"),
             ("verifikasi_penjualan_di_kasir","Verifikasi Penjualan di Kasir"), ("verifikasi_penyerahan_darah_di_kasir","Verifikasi Penyerahan Darah"),
             ("cetaknotasimpanpenjualan","Cetak Setelah Simpan Penjualan"), ("tampilkan_tombol_nota_penjualan","Tombol Nota Penjualan"),
             ("centangobatralan","Centang Obat Ralan"), ("centangobatranap","Centang Obat Ranap"),
             ("cetaknotasimpantoko","Cetak Setelah Simpan Toko"), ("tampilkan_tombol_nota_toko","Tombol Nota Toko"),
             ("cetaknotasimpanlabkesling","Cetak Setelah Simpan Lab Kesling"), ("tampilkan_tombol_nota_labkesling","Tombol Nota Lab Kesling"))]);

    private static MasterSpec Kelengkapan() => Single("Kelengkapan Data Pasien", "set_kelengkapan_data_pasien",
        new (string Column, string Label)[]
        {
            ("no_ktp","NIK"), ("tmp_lahir","Tempat Lahir"), ("nm_ibu","Nama Ibu"), ("alamat","Alamat"),
            ("pekerjaan","Pekerjaan"), ("no_tlp","No. Telepon"), ("umur","Umur"), ("namakeluarga","Nama Keluarga"),
            ("no_peserta","No. Peserta"), ("kelurahan","Kelurahan"), ("kecamatan","Kecamatan"), ("kabupaten","Kabupaten"),
            ("pekerjaanpj","Pekerjaan PJ"), ("alamatpj","Alamat PJ"), ("kelurahanpj","Kelurahan PJ"), ("kecamatanpj","Kecamatan PJ"),
            ("kabupatenpj","Kabupaten PJ"), ("propinsi","Provinsi"), ("propinsipj","Provinsi PJ")
        }.SelectMany(v => new[] { Toggle(v.Column, v.Label + " Wajib"), Integer("p_" + v.Column, v.Label + " Panjang Minimal") }).ToArray())
        with
        {
            BeforeSave = (_, values, _) =>
            {
                if (values.Values.OfType<long>().Any(v => v is < 0 or > 127))
                    throw new ArgumentException("Panjang minimal harus dalam rentang 0–127.");
                return Task.CompletedTask;
            }
        };

    private static Task ValidasiJamDietAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        if (!TimeOnly.TryParseExact((string?)values["jam"], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException("Gunakan format jam HH:mm.");
        return Task.CompletedTask;
    }

    private static async Task CekHapusRuangAsync(Db db, Dictionary<string, object?> keys)
    {
        await db.FirstAsync("SELECT kd_ruang_ok FROM ruang_ok WHERE kd_ruang_ok=@p0 FOR UPDATE", keys["kd_ruang_ok"]);
        if (await db.CountAsync("SELECT COUNT(*) FROM booking_operasi WHERE kd_ruang_ok=@p0", keys["kd_ruang_ok"]) > 0)
            throw new InvalidOperationException("Ruang masih dipakai dalam jadwal operasi.");
    }

    public static readonly IReadOnlyDictionary<string, MasterSpec[]> Tiles = new Dictionary<string, MasterSpec[]>
    {
        ["btnSetPenjab"] = [Single("Set P.J. Unit Penunjang", "set_pjlab",
            Lookup("kd_dokterlab", "Lab Patologi Klinis", Dokter), Lookup("kd_dokterrad", "Radiologi", Dokter),
            Lookup("kd_dokterhemodialisa", "Hemodialisa", Dokter), Lookup("kd_dokterutd", "UTD", Dokter),
            Lookup("kd_dokterlabpa", "Lab Patologi Anatomi", Dokter), Lookup("kd_dokterlabmb", "Lab Mikrobiologi", Dokter))],
        ["btnSetupOtoLokasi"] = [Single("Lokasi Stok Utama", "set_lokasi",
            Lookup("kd_bangsal", "Lokasi", Bangsal), Option("asal_stok", "Sumber Stok", "Gunakan Stok Utama Obat", "Gunakan Stok Bangsal")),
            Depo(false), Depo(true)],
        ["btnSetupJamInap"] = [Single("Set Kamar Inap", "set_jam_minimal",
            Integer("lamajam","Jam Minimal"), Toggle("hariawal","Tagih Hari Awal"), Number("feeperujuk","Fee Perujuk"),
            Toggle("diagnosaakhir","Aktifkan Diagnosis Akhir"), Integer("bayi","Persentase Tarif Kamar Bayi"),
            Toggle("aktifkan_hapus_data_salah","Aktifkan Hapus Data Salah"),
            Toggle("kamar_inap_kasir_ralan","Kamar Inap di Kasir Ralan"), Toggle("ubah_status_kamar","Ubah Status Kamar"))
            with { BeforeSave = ValidasiAngkaAsync }],
        ["btnSetHargaKamar"] = [List("Set Harga Kamar", "set_harga_kamar",
            [Lookup("kd_kamar","Kamar",Kamar,key: true), Lookup("kd_pj","Cara Bayar",Penjab,key: true), Number("tarif","Tarif (Rp)")],
            "JOIN kamar k ON k.kd_kamar=x.kd_kamar JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal JOIN penjab p ON p.kd_pj=x.kd_pj",
            ",b.nm_bangsal,p.png_jawab", "x.kd_kamar,b.nm_bangsal,p.png_jawab",
            ("kd_kamar","Kamar"), ("nm_bangsal","Bangsal"), ("png_jawab","Cara Bayar"), ("tarif","Tarif"))
            with { Hint = "Tarif dipakai saat pasien masuk atau pindah kamar. Periode yang sudah tercatat memakai tarif tersimpan." }],
        ["btnSetupEmbalase"] = [Single("Set Embalase & Tuslah", "set_embalase",
            Number("embalase_per_obat","Embalase per Obat (Rp)"), Number("tuslah_per_obat","Tuslah per Obat (Rp)"))
            with { BeforeSave = ValidasiAngkaAsync }],
        ["btnSetupHarga"] = [Single("Mode Harga Obat", "set_harga_obat",
            Option("setharga","Pengaturan Harga","Umum","Per Jenis","Per Barang"), Option("hargadasar","Harga Dasar","Harga Beli","Harga Diskon"),
            Toggle("ppn","Gunakan PPN")), Single("Margin Umum", "setpenjualanumum", MarginFields())
            with { BeforeSave = ValidasiAngkaAsync }, MarginLookup(false), MarginLookup(true)],
        ["btnSetObatRalan"] = [HargaObat(false)],
        ["btnSetObatRanap"] = [HargaObat(true)],
        ["btnSetupTarif"] = [Single("Set Penggunaan Tarif", "set_tarif",
            Flags(("poli_ralan","Poli Ralan"), ("cara_bayar_ralan","Cara Bayar Ralan"), ("ruang_ranap","Ruang Ranap"),
                ("cara_bayar_ranap","Cara Bayar Ranap"), ("cara_bayar_lab","Cara Bayar Lab"), ("cara_bayar_radiologi","Cara Bayar Radiologi"),
                ("cara_bayar_operasi","Cara Bayar Operasi"), ("kelas_ranap","Kelas Ranap"), ("kelas_lab","Kelas Lab"),
                ("kelas_radiologi","Kelas Radiologi"), ("kelas_operasi","Kelas Operasi"), ("cara_bayar_resep","Cara Bayar Resep")))],
        ["btnSetOtoRalan"] = [OtoRalan("Tindakan Dokter", "set_otomatis_tindakan_ralan", true),
            OtoRalan("Tindakan Petugas", "set_otomatis_tindakan_ralan_petugas", false),
            OtoRalan("Tindakan Dokter dan Petugas", "set_otomatis_tindakan_ralan_dokterpetugas", true)],
        ["btnSetBiayaHarian"] = [Biaya(true)],
        ["btnSetBiayaMasukSekali"] = [Biaya(false)],
        ["btnSetupRM"] = [Single("Nomor RM Terakhir", "set_no_rkm_medis", Text("no_rkm_medis","No. RM Terakhir",15)),
            Single("Pengurutan Nomor RM", "set_urut_no_rkm_medis",
                Option("urutan","Urutan","Straight","Middle","Terminal"), Toggle("tahun","Sertakan Tahun"), Toggle("bulan","Sertakan Bulan"),
                Option("posisi_tahun_bulan","Posisi Tahun/Bulan","Depan","Belakang")),
            Single("Alamat Pasien", "set_alamat_pasien", new[] { "kelurahan","kecamatan","kabupaten","propinsi" }
                .Select(c => Option(c, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(c), "false","true")).ToArray()),
            Kelengkapan(),
            Single("Validasi Registrasi", "set_validasi_registrasi", Toggle("wajib_closing_kasir","Wajib Closing Kasir")),
            Single("Validasi Catatan Pasien", "set_validasi_catatan", Toggle("tampilkan_catatan","Tampilkan Catatan")),
            Single("Data TNI/Polri", "set_tni_polri", Toggle("tampilkan_tni_polri","Tampilkan TNI/Polri"))],
        ["btnSetupNota"] = [Nota(), LayananBilling("Service Ranap", "set_service_ranap"),
            LayananBilling("Service Ranap Piutang", "set_service_ranap_piutang")],
        ["btnClosingKasir"] = [List("Closing Kasir", "closing_kasir",
            [Option("shift","Shift","Pagi","Siang","Sore","Malam") with { Key = true },
             new("jam_masuk","Jam Masuk",FieldKind.Time,Required: true), new("jam_pulang","Jam Pulang",FieldKind.Time,Required: true)],
            "", "", "x.shift", ("shift","Shift"), ("jam_masuk","Masuk"), ("jam_pulang","Pulang"))],
        ["btnKeterlambatanPresensi"] = [Single("Set Keterlambatan Presensi", "set_keterlambatan",
            Integer("toleransi","Toleransi (Menit)"), Integer("terlambat1","Terlambat 1 (Menit)"), Integer("terlambat2","Terlambat 2 (Menit)"))
            with
            {
                BeforeSave = async (db, values, keys) =>
                {
                    await ValidasiAngkaAsync(db, values, keys);
                    if ((long)values["toleransi"]! > (long)values["terlambat1"]! || (long)values["terlambat1"]! > (long)values["terlambat2"]!)
                        throw new ArgumentException("Urutkan batas dari toleransi hingga Terlambat 2.");
                }
            }],
        ["btnSetInputParsial"] = [List("Set Input Parsial", "set_input_parsial",
            [Lookup("kd_pj","Cara Bayar",Penjab,key: true)], "JOIN penjab p ON p.kd_pj=x.kd_pj", ",p.png_jawab", "p.png_jawab,x.kd_pj",
            ("kd_pj","Kode"), ("png_jawab","Cara Bayar"))],
        ["btnSetHargaToko"] = [Single("Set Harga Toko", "tokosetharga", Number("distributor","Distributor (%)"),
            Number("grosir","Grosir (%)"), Number("retail","Retail (%)")) with { BeforeSave = ValidasiAngkaAsync }],
        ["btnJamDietPasien"] = [List("Jam Diet Pasien", "jam_diet_pasien",
            [Option("waktu","Waktu","Pagi","Pagi2","Pagi3","Siang","Siang2","Siang3","Sore","Sore2","Sore3","Malam","Malam2","Malam3") with { Key = true },
             Text("jam","Jam (HH:mm)",5)], "", "", "x.waktu", ("waktu","Waktu Makan"), ("jam","Jam")) with { BeforeSave = ValidasiJamDietAsync }],
        ["btnRuangOperasi"] = [List("Ruang Operasi", "ruang_ok",
            [Text("kd_ruang_ok","Kode Ruang",3,true), Text("nm_ruang_ok","Nama Ruang",50)], "", "", "x.nm_ruang_ok,x.kd_ruang_ok",
            ("kd_ruang_ok","Kode"), ("nm_ruang_ok","Nama Ruang")) with { BeforeDelete = CekHapusRuangAsync }],
        ["btnSetResepPerCaraBayar"] = [List("Set Resep Per Cara Bayar", "set_resep_per_cara_bayar",
            [Lookup("kd_pj","Cara Bayar",Penjab,key: true), Lookup("kode_brng","Obat",Obat,key: true)],
            "JOIN penjab p ON p.kd_pj=x.kd_pj JOIN databarang b ON b.kode_brng=x.kode_brng", ",p.png_jawab,b.nama_brng",
            "p.png_jawab,b.nama_brng,x.kd_pj,x.kode_brng", ("kd_pj","Kode Cara Bayar"), ("png_jawab","Cara Bayar"),
            ("kode_brng","Kode Obat"), ("nama_brng","Obat"))]
    };
}
