using System.Globalization;

namespace SimrsGramedika.Data.RekamMedis;

/// <summary>
/// Lembar observasi dan pemantauan berulang (RMDataCatatanObservasi*.java dan sejenisnya): satu baris per waktu.
/// Label, panjang isian, isian wajib dan pilihan diambil dari layar Java.
/// </summary>
internal static class FormObservasi
{
    private static readonly string[] YaTidak = ["Ya", "Tidak"];

    private static IsianKlinis T(string kolom, string label, int maks, bool wajib = false, string? bagian = null) =>
        new(kolom, label, maks, Wajib: wajib, Bagian: bagian);

    private static FormKlinis Buat(string tile, string judul, string tabel, IsianKlinis[] isian, string? izin = null,
        PemilikForm pemilik = PemilikForm.Petugas) =>
        new(tile, judul, tabel, izin ?? tabel, pemilik, isian)
        {
            KolomTanggal = "tgl_perawatan", KolomJam = "jam_rawat",
            Kelompok = FormKlinisKatalog.Observasi, DaftarLengkap = true,
        };

    /// <summary>Tanda vital yang sama pada lembar observasi rawat inap.</summary>
    private static IsianKlinis[] Vital() =>
    [
        T("gcs", "GCS (E,V,M)", 10), T("td", "TD (mmHg)", 8), T("hr", "HR (x/menit)", 5),
        T("rr", "RR (x/menit)", 5), T("suhu", "Suhu (°C)", 5), T("spo2", "SpO2 (%)", 3),
    ];

    private static readonly FormKlinis Ranap = Buat(
        "btnCatatanObservasiRanap", "Catatan Observasi Rawat Inap", "catatan_observasi_ranap", Vital());

    private static readonly FormKlinis Kebidanan = Buat(
        "btnCatatanObservasiRanapKebidanan", "Catatan Observasi Rawat Inap Kebidanan", "catatan_observasi_ranap_kebidanan",
        [.. Vital(), T("kontraksi", "Kontraksi/HIS", 15), T("bjj", "BJJ", 5), T("ppv", "PPV", 15), T("vt", "VT", 30)]);

    private static readonly FormKlinis PostPartum = Buat(
        "btnCatatanObservasiRanapPostPartum", "Catatan Observasi Rawat Inap Post Partum/Post SC", "catatan_observasi_ranap_postpartum",
        [.. Vital(), T("tfu", "TFU", 15), T("kontraksi", "Kontraksi", 15), T("perdarahan", "Perdarahan", 15), T("keterangan", "Keterangan", 30)]);

    private static readonly FormKlinis Bayi = Buat(
        "btnCatatanObservasiBayi", "Catatan Observasi Rawat Bayi", "catatan_observasi_bayi",
        [
            .. Vital(), T("nch", "NCH", 70), T("ikterik_status", "Ikterik", 30), T("retraksi_dada", "Retraksi Dada", 30),
            T("ogt_residu", "OGT Residu", 30), T("asi_jumlah", "ASI", 30), T("pasi_jumlah", "PASI", 30),
            T("bak_status", "BAK", 30), T("bab_status", "BAB", 30),
        ]);

    private static readonly FormKlinis Chbp = Buat(
        "btnCatatanObservasiCHBP", "Catatan Observasi CHBP", "catatan_observasi_chbp",
        [
            T("td", "TD (mmHg)", 8), T("hr", "Nadi (x/menit)", 5), T("suhu", "Suhu Rectal (°C)", 5), T("djj", "DJJ", 5),
            T("his", "HIS", 20), T("ppv", "PPV", 10), T("keterangan", "Keterangan", 50),
        ]);

    private static readonly FormKlinis InduksiPersalinan = Buat(
        "btnCatatanObservasiInduksiPersalinan", "Catatan Observasi Induksi Persalinan", "catatan_observasi_induksi_persalinan",
        [
            T("obat", "Obat", 50), T("cairan", "Cairan", 50), T("dosis", "Dosis", 10), T("his", "HIS", 50), T("djj", "DJJ", 5),
            T("keterangan", "Keterangan", 50),
        ]);

    private static readonly FormKlinis RuangOperasi = Buat(
        "btnCatatanObservasiRuangOperasi", "Catatan Observasi Ruang Operasi", "catatan_observasi_ruang_ok",
        [.. Vital(), T("keterangan", "Keterangan", 100)]);

    private static IsianKlinis Restrain(string kolom, string label, string? bagian = null) =>
        new(kolom, label, Pilihan: YaTidak, Bagian: bagian, Awal: "Tidak");

    private static readonly FormKlinis RestrainNonFarmakologi = Buat(
        "btnCatatanObservasiRestrainNonFramakologi", "Catatan Observasi Restrain Nonfarmakologi", "catatan_observasi_restrain_nonfarma",
        [
            Restrain("tangan_kiri", "Pergelangan Tangan Kiri", "Lokasi Pemasangan Restrain"),
            Restrain("tangan_kanan", "Pergelangan Tangan Kanan"), Restrain("badan", "Badan"),
            Restrain("kaki_kiri", "Pergelangan Kaki Kiri"), Restrain("kaki_kanan", "Pergelangan Kaki Kanan"),
            Restrain("edema", "Edema", "Observasi Lokasi Pemasangan Restrain"), Restrain("iritasi", "Iritasi"),
            Restrain("sirkulasi", "Sirkulasi"), T("kondisi_keterangan", "Kondisi", 100),
        ]);

    private static readonly FormKlinis Ventilator = Buat(
        "btnCatatanObservasiVentilator", "Catatan Observasi & Pemasangan Ventilator", "catatan_observasi_ventilator",
        [
            new("mode", "Mode", Pilihan:
                ["CPAP", "Nasal IMV", "IMV", "SIMV", "A/C Atau SIPPV", "PSV", "Volume Guarantee", "HFO", "HFO + IMV"]),
            T("vt", "VT", 5), T("pakar", "Pakar", 30), T("rr", "RR", 5), T("reefps", "REEF/PS", 5), T("ee", "EE", 5),
            T("keterangan", "Keterangan", 200),
        ]);

    private static readonly FormKlinis Hemodialisa = Buat(
        "btnCatatanObservasiHemodialisa", "Catatan Observasi Hemodialisa", "catatan_observasi_hemodialisa",
        [
            T("qb", "QB", 10), T("qd", "QD", 8), T("tekanan_arteri", "Arteri", 5), T("tekanan_vena", "Vena", 5), T("tmp", "TMP", 5),
            T("ufr", "UFR", 5), T("tensi", "Tensi", 8), T("nadi", "Nadi", 6), T("suhu", "Suhu", 5), T("spo2", "SpO2", 5),
            T("tindakan", "Tindakan", 100), T("ufg", "UFG", 10), T("barcode_hf", "Barcode HF", 50),
        ]);

    private static readonly FormKlinis CairanHemodialisa = Buat(
        "btnCatatanCairanHemodialisa", "Catatan Cairan Hemodialisa", "catatan_cairan_hemodialisa",
        [
            T("minum", "Minum", 10), T("infus", "Infus", 10), T("tranfusi", "Transfusi", 10), T("sisa_priming", "Sisa Priming", 10),
            T("wash_out", "Wash Out", 10), T("urine", "Urine", 10), T("pendarahan", "Pendarahan", 10), T("muntah", "Muntah", 10),
            T("keterangan", "Keterangan", 100),
        ]);

    private static readonly (string Kolom, string Label)[] Masuk = [("infus", "Infus"), ("tranfusi", "Tranfusi"), ("minum", "Minum")];

    private static readonly (string Kolom, string Label)[] Keluar =
        [("urine", "Urine/Feses"), ("drain", "Drain"), ("ngt", "NGT/Oral"), ("iwl", "IWL")];

    private static bool Angka(string? teks, out decimal nilai) =>
        decimal.TryParse(teks, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out nilai);

    private static IsianKlinis Volume((string Kolom, string Label) c, string? bagian) =>
        new(c.Kolom, c.Label, 4, Wajib: true, Bagian: bagian, Awal: "0");

    private static readonly FormKlinis KeseimbanganCairan = Buat(
        "btnCatatanKeseimbanganCairan", "Keseimbangan Cairan", "catatan_keseimbangan_cairan",
        [
            .. Masuk.Select((c, i) => Volume(c, i == 0 ? "Input" : null)),
            .. Keluar.Select((c, i) => Volume(c, i == 0 ? "Output" : null)),
            new("keseimbangan", "Keseimbangan Cairan", 4, Otomatis: true, Bagian: "Hasil"),
            T("keterangan", "Keterangan", 200),
        ], izin: "balance_cairan") with
    {
        // Rumus Khanza: jumlah input dikurangi jumlah output.
        Hitung = isi =>
        {
            var total = 0m;
            foreach (var (daftar, tanda) in new[] { (Masuk, 1), (Keluar, -1) })
                foreach (var (kolom, _) in daftar)
                {
                    var teks = isi.TryGetValue(kolom, out var v) ? v.Trim() : "";
                    if (teks.Length == 0) continue;
                    if (!Angka(teks, out var nilai)) { isi["keseimbangan"] = ""; return; }
                    total += tanda * nilai;
                }
            isi["keseimbangan"] = total.ToString("0.####", CultureInfo.InvariantCulture);
        },
        Periksa = isi =>
        {
            foreach (var (kolom, label) in Masuk.Concat(Keluar))
                if (!Angka(isi[kolom], out _)) return $"{label} harus angka (desimal memakai titik).";
            var hasil = isi["keseimbangan"];
            return hasil.Length > 4
                ? $"Keseimbangan cairan {hasil} lebih dari 4 karakter, sedangkan kolom tabel Khanza hanya memuat 4."
                : null;
        },
    };

    private static readonly FormKlinis CekGds = Buat(
        "btnCatatanCekGDS", "Catatan Cek GDS & Pemberian Insulin / Obat Gula", "catatan_cek_gds",
        [T("gdp", "GDS", 5), T("insulin", "Insulin", 30), T("obat_gula", "Obat Gula", 30)]);

    private static readonly FormKlinis FollowUpDbd = Buat(
        "btnDataFollowUpDBD", "Follow Up DBD", "follow_up_dbd",
        [
            T("hemoglobin", "Hemoglobin", 5), T("hematokrit", "Hematokrit", 5), T("leokosit", "Leokosit", 7),
            T("trombosit", "Trombosit", 10), T("terapi_cairan", "Terapi Cairan", 100),
        ]);

    private static readonly FormKlinis ReaksiTranfusi = Buat(
        "btnMonitoringReaksiTranfusi", "Monitoring Reaksi Tranfusi", "monitoring_reaksi_tranfusi",
        [
            T("produk_darah", "Produk/Jenis Darah", 40), T("no_kantong", "No.Kantong", 20), T("lokasi_insersi", "Lokasi Insersi", 40),
            T("td", "TD (mmHg)", 8), T("hr", "HR (x/menit)", 5), T("rr", "RR (x/menit)", 5), T("suhu", "Suhu (°C)", 5),
            T("jenis_reaksi_alergi", "Alergi", 70), T("keterangan", "Keterangan", 70),
        ]);

    private static readonly FormKlinis Tolac = Buat(
        "btnMonitoringTOLAC", "Monitoring Selama TOLAC", "monitoring_tolac",
        [
            T("his_frekuensi", "His (x/10 menit)", 2, wajib: true), T("his_durasi_detik", "Durasi His (detik)", 5, wajib: true),
            T("pembukaan_cm", "Pembukaan (cm)", 2, wajib: true), T("td", "TD (mmHg)", 7, wajib: true),
            T("nadi", "Nadi (x/menit)", 5, wajib: true), T("djj", "DJJ (x/menit)", 5, wajib: true),
            new("tanda_bahaya", "Tanda Bahaya", Pilihan: ["Tidak Ada", "Ada"]),
            T("keterangan", "Keterangan Tanda Bahaya", 255),
        ], pemilik: PemilikForm.Pegawai) with
    {
        KolomPemilikKhusus = "nip", // kolomnya nip, tetapi merujuk pegawai.nik
        Periksa = isi => isi["tanda_bahaya"] == "Ada" && isi["keterangan"].Length == 0
            ? "Keterangan Tanda Bahaya wajib diisi." : null,
    };

    private static readonly FormKlinis NyeriFarmakologi = Buat(
        "btnIntervensiNyeriFarmakologi", "Intervensi Nyeri Farmakologi", "intervensi_nyeri_farmakologi",
        [T("nama_obat", "Obat", 100, wajib: true), T("dosis_efek", "Dosis & Efek", 100, wajib: true), T("rute", "Rute", 30, wajib: true)]);

    private static readonly FormKlinis NyeriNonFarmakologi = Buat(
        "btnIntervensiNyeriNonFarmakologi", "Intervensi Nyeri Non Farmakologi", "intervensi_nyeri_nonfarmakologi",
        [T("intervensi", "Intervensi", 200, wajib: true)]);

    /// <summary>Kunci tabelnya datetime tunggal, bukan tanggal dan jam terpisah.</summary>
    private static readonly FormKlinis AsuhanGizi = Buat(
        "btnMonitoringAsuhanGizi", "Monitoring & Evaluasi Asuhan Gizi", "monitoring_asuhan_gizi",
        [T("monitoring", "Monitoring", 500, wajib: true), T("evaluasi", "Evaluasi", 500, wajib: true)]) with
    { KolomTanggal = "tanggal", KolomJam = null };

    // Properti, bukan field, agar tidak bergantung pada urutan inisialisasi.
    public static IReadOnlyList<FormKlinis> Semua =>
    [
        Ranap, Kebidanan, PostPartum, Bayi, Chbp, InduksiPersalinan, RuangOperasi, RestrainNonFarmakologi, Ventilator,
        Hemodialisa, CairanHemodialisa, KeseimbanganCairan, CekGds, FollowUpDbd, ReaksiTranfusi, Tolac,
        NyeriFarmakologi, NyeriNonFarmakologi, AsuhanGizi,
    ];
}
