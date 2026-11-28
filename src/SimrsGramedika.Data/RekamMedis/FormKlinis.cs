namespace SimrsGramedika.Data.RekamMedis;

public enum PemilikForm { Petugas, Dokter, Pegawai }

/// <summary>
/// Satu kolom isian. <paramref name="Otomatis"/> dihitung layanan dari isian lain dan tidak bisa diketik.
/// <paramref name="Awal"/> adalah pilihan pada catatan baru bila bukan pilihan pertama.
/// </summary>
public sealed record IsianKlinis(string Kolom, string Label, int Maks = 0, string[]? Pilihan = null,
    bool Wajib = false, string? Bagian = null, bool Otomatis = false, string? Awal = null);

/// <summary>
/// Definisi satu formulir rekam medis Khanza berpola "satu tabel, banyak catatan per kunjungan":
/// kunci kunjungan + waktu, pemilik catatan, dan kolom isian. Aturan simpan, ubah, hapus dan cetak ada di
/// <see cref="FormKlinisService"/> sehingga formulir baru cukup menambah definisi.
/// </summary>
public sealed record FormKlinis(string Tile, string Judul, string Tabel, string Izin, PemilikForm Pemilik,
    IsianKlinis[] Isian)
{
    /// <summary>Kolom tanggal; bila <see cref="KolomJam"/> kosong kolom ini bertipe datetime.</summary>
    public string KolomTanggal { get; init; } = "tanggal";

    public string? KolomJam { get; init; }

    /// <summary>Kolom tanggalnya bertipe date tanpa jam: satu catatan per tanggal.</summary>
    public bool TanggalSaja { get; init; }

    /// <summary>Kunci tabel hanya no_rawat.</summary>
    public bool SatuPerKunjungan { get; init; }

    /// <summary>Selain Admin Utama hanya boleh mengubah atau menghapus sampai 2 x 24 jam dari waktu catatan.</summary>
    public bool Batas48Jam { get; init; } = true;

    /// <summary>Layar Khanza formulir ini mengunci tanggal bila TANGGALMUNDUR=no.</summary>
    public bool IkutKunciTanggal { get; init; } = true;

    /// <summary>Selain Admin Utama, waktu catatan tidak boleh sebelum waktu registrasi.</summary>
    public bool CekWaktuRegistrasi { get; init; } = true;

    /// <summary>
    /// Data kunjungan yang ikut dibaca <see cref="Hitung"/> dan <see cref="Usul"/> tetapi tidak disimpan: jenis kelamin
    /// ("L"/"P"), umur saat daftar dan satuannya ("Th", "Bl", "Hr").
    /// </summary>
    public const string KunciJk = "@jk", KunciUmur = "@umur", KunciSatuanUmur = "@sttsumur";

    /// <summary>Mengisi kolom otomatis (skor) dari isian lain.</summary>
    public Action<IDictionary<string, string>>? Hitung { get; init; }

    /// <summary>
    /// Isian yang disarankan dari isian lain atau data pasien, tetapi tetap bisa diganti. Layar hanya menerapkannya
    /// saat sarannya berubah; layanan menyimpan apa pun yang tertulis.
    /// </summary>
    public Func<IReadOnlyDictionary<string, string>, Dictionary<string, string>>? Usul { get; init; }

    /// <summary>Penjelasan yang ditampilkan tetapi tidak disimpan.</summary>
    public Func<IReadOnlyDictionary<string, string>, string>? Keterangan { get; init; }

    /// <summary>Aturan antar isian sebelum disimpan; mengembalikan pesan penolakan atau null.</summary>
    public Func<IReadOnlyDictionary<string, string>, string?>? Periksa { get; init; }

    /// <summary>Submenu klik kanan pasien.</summary>
    public string Kelompok { get; init; } = FormKlinisKatalog.Pengkajian;

    /// <summary>Daftar menampilkan semua isian (lembar observasi), bukan beberapa kolom pertama.</summary>
    public bool DaftarLengkap { get; init; }

    /// <summary>Diisi bila nama kolom pemilik tidak mengikuti jenis pemiliknya.</summary>
    public string? KolomPemilikKhusus { get; init; }

    public string KolomPemilik => KolomPemilikKhusus
        ?? Pemilik switch { PemilikForm.Dokter => "kd_dokter", PemilikForm.Pegawai => "nik", _ => "nip" };
    public string SebutanPemilik => Pemilik switch { PemilikForm.Dokter => "dokter", PemilikForm.Pegawai => "pegawai", _ => "petugas" };
}

public static class FormKlinisKatalog
{
    public const string Pengkajian = "Pengkajian & Catatan";
    public const string Observasi = "Observasi & Pemantauan";
    public const string PeringatanDini = "Peringatan Dini (EWS)";
    public const string KriteriaRuang = "Kriteria Ruang Rawat";
    public const string Skrining = "Skrining";
    public const string Risiko = "Risiko Jatuh, Dekubitus & Fungsional";

    private static readonly string[] Organ = ["Normal", "Abnormal", "Tidak Diperiksa"];

    private static string? Ambil(IDictionary<string, string> isi, string kolom) => isi.TryGetValue(kolom, out var v) ? v : null;

    /// <summary>Skala Morse: pilihan dan nilainya, mengikuti RMPenilaianLanjutanRisikoJatuhDewasa.java.</summary>
    private static readonly (string Judul, (string Pilihan, int Nilai)[] Skala)[] Morse =
    [
        ("1. Riwayat Jatuh (1 Tahun Terakhir)", [("Tidak", 0), ("Ya", 25)]),
        ("2. Diagnosis Sekunder (≥ 2 Diagnosis Medis)", [("Tidak", 0), ("Ya", 15)]),
        ("3. Alat Bantu", [("Tidak Ada/Kursi Roda/Perawat/Tirah Baring", 0), ("Tongkat/Alat Penopang", 15), ("Berpegangan Pada Perabot", 30)]),
        ("4. Terpasang Infus", [("Tidak", 0), ("Ya", 20)]),
        ("5. Gaya Berjalan", [("Normal/Tirah Baring/Imobilisasi", 0), ("Lemah", 10), ("Terganggu", 20)]),
        ("6. Status Mental", [("Sadar Akan Kemampuan Diri Sendiri", 0), ("Sering Lupa Akan Keterbatasan Yang Dimiliki", 15)]),
    ];

    private static readonly (string Pilihan, int Skor)[] GiziImt =
        [("IMT > 20/z score > 2", 0), ("IMT 18,5-20/-2 =< z score =< 2", 1), ("IMT < 18,5/z score < -2", 2)];
    private static readonly (string Pilihan, int Skor)[] GiziBb =
        [("BB Hilang < 5%", 0), ("BB Hilang 5 - 10 %", 1), ("BB Hilang > 10 %", 2)];
    private static readonly (string Pilihan, int Skor)[] GiziPenyakit =
        [("Ada asupan nutrisi > 5 hari", 0), ("Tidak ada asupan nutrisi > 5 hari", 2)];

    public static readonly FormKlinis CatatanKeperawatanRalan = new(
        "btnCatatanKeperawatanRalan", "Catatan Keperawatan Rawat Jalan", "catatan_keperawatan_ralan",
        "catatan_keperawatan_ralan", PemilikForm.Petugas,
        [new("uraian", "Uraian", 1000, Wajib: true)])
    { KolomJam = "jam" };

    public static readonly FormKlinis ObservasiIgd = new(
        "btnCatatanObservasiIGD", "Catatan Observasi IGD", "catatan_observasi_igd", "catatan_observasi_igd",
        PemilikForm.Petugas,
        [
            new("gcs", "GCS (E,V,M)", 10), new("td", "TD (mmHg)", 8), new("hr", "HR (x/menit)", 5),
            new("rr", "RR (x/menit)", 5), new("suhu", "Suhu (°C)", 5), new("spo2", "SpO2 (%)", 3),
        ])
    { KolomTanggal = "tgl_perawatan", KolomJam = "jam_rawat", Kelompok = Observasi, DaftarLengkap = true };

    public static readonly FormKlinis SkriningGizi = new(
        "btnSkriningGiziLanjut", "Skrining Gizi Lanjut", "skrining_gizi", "skrining_gizi", PemilikForm.Petugas,
        [
            new("skrining_bb", "BB (Kg)", 5, Wajib: true), new("skrining_tb", "TB (Cm)", 5, Wajib: true),
            new("alergi", "Alergi", 25, Wajib: true),
            new("parameter_imt", "1. Skor IMT / z Score", Pilihan: [.. GiziImt.Select(x => x.Pilihan)]),
            new("skor_imt", "Skor IMT", Otomatis: true),
            new("parameter_bb", "2. Skor Kehilangan BB", Pilihan: [.. GiziBb.Select(x => x.Pilihan)]),
            new("skor_bb", "Skor BB", Otomatis: true),
            new("parameter_penyakit", "3. Skor Efek Penyakit Akut", Pilihan: [.. GiziPenyakit.Select(x => x.Pilihan)]),
            new("skor_penyakit", "Skor Penyakit", Otomatis: true),
            new("skor_total", "Total Skor", Otomatis: true),
            new("parameter_total", "Parameter", 200, Otomatis: true),
        ])
    {
        Hitung = isi =>
        {
            int Skor((string Pilihan, int Skor)[] daftar, string kolom) =>
                daftar.FirstOrDefault(x => x.Pilihan == Ambil(isi, kolom)).Skor;
            var imt = Skor(GiziImt, "parameter_imt");
            var bb = Skor(GiziBb, "parameter_bb");
            var penyakit = Skor(GiziPenyakit, "parameter_penyakit");
            var total = imt + bb + penyakit;
            isi["skor_imt"] = imt.ToString();
            isi["skor_bb"] = bb.ToString();
            isi["skor_penyakit"] = penyakit.ToString();
            isi["skor_total"] = total.ToString();
            isi["parameter_total"] = total switch
            {
                0 => "Beresiko rendah, ulangi 7 hari",
                1 => "Beresiko menengah, monitoring asupan selama 3 hari",
                _ => "Beresiko tinggi, bekerja sama dengan tim dukungan gizi upayakan peningkatan asupan gizi dan memberikan makanan sesuai dengan daya terima",
            };
        },
    };

    public static readonly FormKlinis RisikoJatuhDewasa = new(
        "btnPenilaianRisikoJatuhDewasa", "Penilaian Lanjutan Risiko Jatuh Dewasa", "penilaian_lanjutan_resiko_jatuh_dewasa",
        "penilaian_lanjutan_resiko_jatuh_dewasa", PemilikForm.Petugas,
        [
            .. Morse.SelectMany((m, i) => new IsianKlinis[]
            {
                new($"penilaian_jatuhmorse_skala{i + 1}", m.Judul, Pilihan: [.. m.Skala.Select(x => x.Pilihan)], Bagian: i == 0 ? "Skala Morse" : null),
                new($"penilaian_jatuhmorse_nilai{i + 1}", $"Nilai {i + 1}", Otomatis: true),
            }),
            new("penilaian_jatuhmorse_totalnilai", "Total Nilai", Otomatis: true),
            new("hasil_skrining", "Hasil Skrining", 200, Wajib: true),
            new("saran", "Saran", 200, Wajib: true),
        ])
    {
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        Hitung = isi =>
        {
            var total = 0;
            for (var i = 0; i < Morse.Length; i++)
            {
                var pilihan = Ambil(isi, $"penilaian_jatuhmorse_skala{i + 1}");
                var nilai = Morse[i].Skala.FirstOrDefault(x => x.Pilihan == pilihan).Nilai;
                isi[$"penilaian_jatuhmorse_nilai{i + 1}"] = nilai.ToString();
                total += nilai;
            }
            isi["penilaian_jatuhmorse_totalnilai"] = total.ToString();
        },
        Keterangan = isi => int.TryParse(isi.GetValueOrDefault("penilaian_jatuhmorse_totalnilai"), out var total)
            ? total < 25 ? "Tingkat Resiko : Risiko Rendah (0-24), Tindakan : Intervensi pencegahan risiko jatuh standar"
            : total < 45 ? "Tingkat Resiko : Risiko Sedang (25-44), Tindakan : Intervensi pencegahan risiko jatuh standar"
            : "Tingkat Resiko : Risiko Tinggi (> 45), Tindakan : Intervensi pencegahan risiko jatuh standar dan Intervensi risiko jatuh tinggi"
            : "",
    };

    public static readonly FormKlinis MedisRalan = new(
        "btnPenilaianAwalMedisRalan", "Penilaian Awal Medis Rawat Jalan", "penilaian_medis_ralan",
        "penilaian_awal_medis_ralan", PemilikForm.Dokter,
        [
            new("anamnesis", "Anamnesis", Pilihan: ["Autoanamnesis", "Alloanamnesis"]),
            new("hubungan", "Hubungan", 30),
            new("keluhan_utama", "Keluhan Utama", 2000, Wajib: true, Bagian: "I. Riwayat Kesehatan"),
            new("rps", "Riwayat Penyakit Sekarang", 2000, Wajib: true),
            new("rpd", "Riwayat Penyakit Dahulu", 1000, Wajib: true),
            new("rpk", "Riwayat Penyakit Keluarga", 1000, Wajib: true),
            new("rpo", "Riwayat Penggunaan Obat", 1000, Wajib: true),
            new("alergi", "Riwayat Alergi", 50),
            new("keadaan", "Keadaan Umum", Pilihan: ["Sehat", "Sakit Ringan", "Sakit Sedang", "Sakit Berat"], Bagian: "II. Pemeriksaan Fisik"),
            new("gcs", "GCS (E,V,M)", 10),
            new("kesadaran", "Kesadaran", Pilihan: ["Compos Mentis", "Apatis", "Somnolen", "Sopor", "Koma"]),
            new("td", "TD (mmHg)", 8), new("nadi", "Nadi (x/menit)", 5), new("rr", "RR (x/menit)", 5),
            new("suhu", "Suhu (°C)", 5), new("spo", "SpO2 (%)", 5), new("bb", "BB (Kg)", 5), new("tb", "TB (Cm)", 5),
            new("kepala", "Kepala", Pilihan: Organ), new("gigi", "Gigi & Mulut", Pilihan: Organ), new("tht", "THT", Pilihan: Organ),
            new("thoraks", "Thoraks", Pilihan: Organ), new("abdomen", "Abdomen", Pilihan: Organ),
            new("genital", "Genital & Anus", Pilihan: Organ), new("ekstremitas", "Ekstremitas", Pilihan: Organ),
            new("kulit", "Kulit", Pilihan: Organ),
            new("ket_fisik", "Keterangan Pemeriksaan Fisik", 5000),
            new("ket_lokalis", "Keterangan", 3000, Bagian: "III. Status Lokalis"),
            new("penunjang", "Pemeriksaan Penunjang", 3000, Bagian: "IV. Pemeriksaan Penunjang"),
            new("diagnosis", "Diagnosis / Asesmen", 500, Bagian: "V. Diagnosis/Asesmen"),
            new("tata", "Tatalaksana", 5000, Bagian: "VI. Tatalaksana"),
            new("konsulrujuk", "Konsul / Rujuk", 1000, Bagian: "VII. Konsul/Rujuk"),
        ])
    { SatuPerKunjungan = true };

    public static readonly IReadOnlyList<FormKlinis> Semua =
        [CatatanKeperawatanRalan, ObservasiIgd, SkriningGizi, RisikoJatuhDewasa, MedisRalan, .. FormKriteriaRuang.Semua,
            .. FormObservasi.Semua, .. FormEws.Semua,
            .. FormSkrining.Semua];

    public static FormKlinis? Cari(string? tile) => Semua.FirstOrDefault(f => f.Tile == tile);
}
