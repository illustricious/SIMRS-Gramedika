using System.Globalization;
using Isi = System.Collections.Generic.IDictionary<string, string>;
using Baca = System.Collections.Generic.IReadOnlyDictionary<string, string>;

namespace SimrsGramedika.Data.RekamMedis;

/// <summary>
/// Skrining dan penilaian risiko, dari RMSkrining*.java, RMDataSkrining*.java dan RMPenilaian*Risiko*.java.
/// Label, pilihan, isian awal, nilai tiap pilihan, ambang kesimpulan dan aturan waktu diambil dari layar Java.
/// Isian "usulan" diisi otomatis tetapi tetap bisa diganti, seperti kotak yang tidak dikunci di Khanza.
/// </summary>
internal static class FormSkrining
{
    private static IsianKlinis T(string kolom, string label, int maks, bool wajib = false, string? bagian = null) =>
        new(kolom, label, maks, Wajib: wajib, Bagian: bagian);

    private static IsianKlinis P(string kolom, string label, string[] pilihan, string? awal = null, string? bagian = null) =>
        new(kolom, label, Pilihan: pilihan, Bagian: bagian, Awal: awal);

    /// <summary>Kolom hitungan.</summary>
    private static IsianKlinis H(string kolom, string label, string? bagian = null) =>
        new(kolom, label, Otomatis: true, Bagian: bagian);

    /// <param name="skor">Nilai tiap pilihan, seurut pilihan kolom sumbernya.</param>
    /// <param name="lanjut">Total dan kesimpulan sesudah nilai per pilihan terisi.</param>
    private static FormKlinis Buat(string tile, string judul, string tabel, string izin, IsianKlinis[] isian,
        PemilikForm pemilik = PemilikForm.Petugas, (string Sumber, string Tujuan, string[] Nilai)[]? skor = null,
        Action<Isi>? lanjut = null) =>
        new(tile, judul, tabel, izin, pemilik, isian)
        {
            Kelompok = FormKlinisKatalog.Skrining,
            Hitung = skor is null && lanjut is null ? null : isi =>
            {
                foreach (var (sumber, tujuan, nilai) in skor ?? [])
                {
                    var n = Array.IndexOf(isian.First(i => i.Kolom == sumber).Pilihan!, Teks(isi, sumber));
                    isi[tujuan] = n < 0 ? "" : nilai[n];
                }
                lanjut?.Invoke(isi);
            },
        };

    private static string Teks(Isi isi, string kolom) => isi.TryGetValue(kolom, out var v) ? v.Trim() : "";

    private static string Teks(Baca isi, string kolom) => isi.TryGetValue(kolom, out var v) ? v.Trim() : "";

    private static string[] Seri(string awalan, int dari, int sampai) =>
        [.. Enumerable.Range(dari, sampai - dari + 1).Select(i => awalan + i)];

    /// <summary>Jumlah kolom nilai; hasilnya kosong bila salah satunya belum terisi angka.</summary>
    private static int? Jumlah(Isi isi, string tujuan, params string[] kolom)
    {
        var total = 0;
        foreach (var k in kolom)
        {
            if (!int.TryParse(Teks(isi, k), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
            {
                isi[tujuan] = "";
                return null;
            }
            total += n;
        }
        isi[tujuan] = total.ToString(CultureInfo.InvariantCulture);
        return total;
    }

    private static bool Angka(string teks, out double nilai) =>
        double.TryParse(teks, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out nilai);

    private static string? WajibAngka(Baca isi, params (string Kolom, string Label)[] daftar) => daftar
        .Where(d => !Angka(Teks(isi, d.Kolom), out var n) || n <= 0)
        .Select(d => $"{d.Label} harus angka lebih dari nol (desimal memakai titik).").FirstOrDefault();

    private static int Umur(Baca isi) => Teks(isi, FormKlinis.KunciSatuanUmur) == "Th"
        && int.TryParse(Teks(isi, FormKlinis.KunciUmur), out var n) ? n : 0;

    private static int UmurDaftar(Isi isi) => int.TryParse(Teks(isi, FormKlinis.KunciUmur), out var n) ? n : 0;

    private static int Umur(Isi isi) => Umur((Baca)new Dictionary<string, string>(isi));

    private static Dictionary<string, string> Satu(string kolom, string nilai) => new() { [kolom] = nilai };

    private static readonly string[] KlasImt =
        ["Berat Badan Kurang", "Berat Badan Normal", "Kelebihan Berat Badan", "Obesitas I", "Obesitas II"];

    /// <summary>IMT satu desimal dan klasifikasinya menurut WHO Asia Pasifik.</summary>
    private static void Imt(Isi isi)
    {
        isi["imt"] = isi["kasifikasi_imt"] = "";
        if (!Angka(Teks(isi, "berat_badan"), out var bb) || !Angka(Teks(isi, "tinggi_badan"), out var tb) || bb <= 0 || tb <= 0) return;
        // Dibulatkan dari nilai biner persisnya seperti BigDecimal di Khanza: 31,2499999... menjadi 31,2.
        var persis = Math.Min(bb / (tb / 100 * (tb / 100)), 999999).ToString("F20", CultureInfo.InvariantCulture);
        var imt = Math.Round(decimal.Parse(persis, CultureInfo.InvariantCulture), 1, MidpointRounding.AwayFromZero);
        var tulis = imt.ToString("0.0", CultureInfo.InvariantCulture);
        if (tulis.Length > 6) return; // kolom imt Khanza selebar 6 karakter
        isi["imt"] = tulis;
        isi["kasifikasi_imt"] = KlasImt[imt < 18.5m ? 0 : imt < 23 ? 1 : imt < 25 ? 2 : imt < 30 ? 3 : 4];
    }

    private static readonly string[] RisikoPinggang = ["Rendah", "Cukup", "Meningkat", "Moderat", "Berat", "Sangat"];

    private static double AmbangPinggang(Isi isi) => Teks(isi, FormKlinis.KunciJk) == "L" ? 90 : 80;

    /// <summary>Risiko naik satu tingkat bila lingkar pinggang mencapai ambang jenis kelaminnya.</summary>
    private static void Pinggang(Isi isi)
    {
        var klas = Array.IndexOf(KlasImt, Teks(isi, "kasifikasi_imt"));
        isi["risiko_lingkar_pinggang"] = klas >= 0 && Angka(Teks(isi, "lingkar_pinggang"), out var lp)
            ? RisikoPinggang[klas + (lp >= AmbangPinggang(isi) ? 1 : 0)] : "";
    }

    private static string? PeriksaImt(Baca isi) => WajibAngka(isi, ("berat_badan", "BB"), ("tinggi_badan", "TB"))
        ?? (Teks(isi, "imt").Length == 0 ? "IMT di luar jangkauan. Periksa BB (Kg) dan TB (Cm)." : null);

    private static string? PeriksaTubuh(Baca isi) => PeriksaImt(isi) ?? WajibAngka(isi, ("lingkar_pinggang", "Lingkar Pinggang"));

    private const string Standar = "Tindakan : Intervensi pencegahan risiko jatuh standar";
    private const string Tinggi = Standar + " dan Intervensi risiko jatuh tinggi";

    private static string Tingkat(Baca isi, string kolomTotal, Func<int, string> teks) =>
        int.TryParse(Teks(isi, kolomTotal), out var total) ? teks(total) : "";

    private static readonly string[] PenyakitFrailty =
    [
        "penyakit_kanker", "penyakit_gagal_jantung", "penyakit_ginjal", "penyakit_nyeri_dada", "penyakit_serangan_jantung",
        "penyakit_stroke", "penyakit_asma", "penyakit_nyeri_sendi", "penyakit_paru_kronis", "penyakit_hipertensi", "penyakit_diabetes",
    ];

    private static readonly string[] KelasSatuPsi =
    [
        "nilai_gagal_jantung", "nilai_penyakit_ginjal", "nilai_penyakit_serebrovaskuler", "nilai_penyakit_hati", "nilai_kanker",
        "nilai_disorentasi_mental", "nilai_td_sistolik", "nilai_nadi", "nilai_frekuensi_napas", "nilai_suhu",
    ];

    private static readonly string[] NilaiPsi =
    [
        .. KelasSatuPsi, "nilai_tinggal_di_panti_jompo", "nilai_ph_darah", "nilai_natrium", "nilai_bun", "nilai_pao", "nilai_glukosa",
        "nilai_efusi_pleura", "nilai_hematokrit",
    ];

    /// <summary>
    /// Satu subskala SDQ dari lima butirnya. Layar Java membandingkan ambang "borderline" dengan total seluruh butir;
    /// di sini keduanya memakai nilai subskala itu sendiri.
    /// </summary>
    private static void Subskala(Isi isi, string nilai, string kesimpulan, Func<int, bool> abnormal, Func<int, bool> ambang,
        params int[] butir)
    {
        var total = Jumlah(isi, nilai, [.. butir.Select(b => $"nilai_sdq{b}")]);
        isi[kesimpulan] = total is not { } n ? "" : abnormal(n) ? "Abnormal" : ambang(n) ? "Ambang/Boderline" : "Normal";
    }

    private static FormKlinis GiziKehamilan => Buat(
        "btnSkriningGiziKehamilan", "Skrining Gizi Kehamilan/Obstetri/Nifas", "skrining_gizi_kehamilan", "skrining_gizi_kehamilan",
        [
            P("parameter1", "1. Apakah asupan makan berkurang karena tidak nafsu makan ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("skor1", "Skor"),
            P("parameter2", "2. Ada gangguan metabolisme : (DM; gangguan fungsi tiroid, infeksi kronis spt : HIV/AIDS, TB, Lupus; dll)", ["Ya", "Tidak"], awal: "Tidak"),
            H("skor2", "Skor"),
            P("parameter3", "3. Ada pertambahan BB yang kurang atau lebih selama kehamilan ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("skor3", "Skor"),
            P("parameter4", "4. Nilai Hb < 10 g/dl atau HCT < 30%", ["Ya", "Tidak"], awal: "Tidak"),
            H("skor4", "Skor"),
            H("nilai_skor", "Total Skor"),
            T("keterangan", "Keterangan", 50),
        ],
        skor: [("parameter1", "skor1", ["1", "0"]),
            ("parameter2", "skor2", ["1", "0"]),
            ("parameter3", "skor3", ["1", "0"]),
            ("parameter4", "skor4", ["1", "0"])],
        lanjut: isi => Jumlah(isi, "nilai_skor", Seri("skor", 1, 4))) with
    {
        Keterangan = isi => Tingkat(isi, "nilai_skor", t => t >= 1 ? "Skor ≥ 1 : dilakukan pengkajian lanjut oleh Dietisien" : ""),
    };

    private static FormKlinis LanjutanRisikoJatuhAnak => Buat(
        "btnPenilaianRisikoJatuhAnak", "Penilaian Lanjutan Risiko Jatuh Anak", "penilaian_lanjutan_resiko_jatuh_anak", "penilaian_lanjutan_resiko_jatuh_anak",
        [
            P("penilaian_humptydumpty_skala1", "1. Umur", ["0 - 3 Tahun", "3 - 7 Tahun", "7 - 13 Tahun", "> 13 Tahun"], bagian: "Skala Humpty Dumpty"),
            H("penilaian_humptydumpty_nilai1", "Nilai"),
            P("penilaian_humptydumpty_skala2", "2. Jenis Kelamin", ["Laki-laki", "Perempuan"]),
            H("penilaian_humptydumpty_nilai2", "Nilai"),
            P("penilaian_humptydumpty_skala3", "3. Diagnosa", ["Kelainan Neurologi", "Perubahan Dalam Oksigen(Masalah Saluran Nafas, Dehidrasi, Anemia, Anoreksia / Sakit Kepala, Dll)", "Kelainan Psikis / Perilaku", "Diagnosa Lain"]),
            H("penilaian_humptydumpty_nilai3", "Nilai"),
            P("penilaian_humptydumpty_skala4", "4. Gangguan Kognitif", ["Tidak Sadar Terhadap Keterbatasan", "Lupa Keterbatasan", "Mengetahui Kemampuan Diri"]),
            H("penilaian_humptydumpty_nilai4", "Nilai"),
            P("penilaian_humptydumpty_skala5", "5. Faktor Lingkungan", ["Riwayat Jatuh Dari Tempat Tidur Saat Bayi/Anak", "Pasien Menggunakan Alat Bantu/Box/Mebel", "Pasien Berada Di Tempat Tidur", "Di Luar Ruang Rawat"]),
            H("penilaian_humptydumpty_nilai5", "Nilai"),
            P("penilaian_humptydumpty_skala6", "6. Efek Obat Penenang/Operasi/Anastesi", ["Dalam 24 Jam", "Dalam 48 Jam", "> 48 Jam"]),
            H("penilaian_humptydumpty_nilai6", "Nilai"),
            P("penilaian_humptydumpty_skala7", "7. Penggunaan Obat", ["Bermacam-macam Obat Yang Digunakan : Obat Sedative (Kecuali Pasien ICU Yang Menggunakan sedasi dan paralisis), Hipnotik, Barbiturat, Fenoti-Azin, Antidepresan, Laksans/Diuretika,Narkotik", "Salah Satu Dari Pengobatan Di Atas", "Pengobatan Lain"]),
            H("penilaian_humptydumpty_nilai7", "Nilai"),
            H("penilaian_humptydumpty_totalnilai", "Total Nilai"),
            T("hasil_skrining", "Hasil Skrining", 200, wajib: true, bagian: "Hasil"),
            T("saran", "Saran", 200, wajib: true),
        ],
        skor: [("penilaian_humptydumpty_skala1", "penilaian_humptydumpty_nilai1", ["4", "3", "2", "1"]),
            ("penilaian_humptydumpty_skala2", "penilaian_humptydumpty_nilai2", ["2", "1"]),
            ("penilaian_humptydumpty_skala3", "penilaian_humptydumpty_nilai3", ["4", "3", "2", "1"]),
            ("penilaian_humptydumpty_skala4", "penilaian_humptydumpty_nilai4", ["3", "2", "1"]),
            ("penilaian_humptydumpty_skala5", "penilaian_humptydumpty_nilai5", ["4", "3", "2", "1"]),
            ("penilaian_humptydumpty_skala6", "penilaian_humptydumpty_nilai6", ["3", "2", "1"]),
            ("penilaian_humptydumpty_skala7", "penilaian_humptydumpty_nilai7", ["3", "2", "1"])],
        lanjut: isi => Jumlah(isi, "penilaian_humptydumpty_totalnilai", Seri("penilaian_humptydumpty_nilai", 1, 7))) with
    {
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        Keterangan = isi => Tingkat(isi, "penilaian_humptydumpty_totalnilai", t => "Tingkat Resiko : " + (t < 12 ? "Risiko Rendah (7-11), " + Standar : "Risiko Tinggi (>= 12), " + Tinggi)),
        Kelompok = FormKlinisKatalog.Risiko,
    };

    private static FormKlinis LanjutanRisikoJatuhGeriatri => Buat(
        "btnPenilaianRisikoJatuhGeriatri", "Penilaian Lanjutan Risiko Jatuh Geriatri", "penilaian_lanjutan_resiko_jatuh_geriatri", "penilaian_lanjutan_resiko_jatuh_geriatri",
        [
            P("penilaian_jatuh_skala1", "1. Gangguan Gaya Berjalan (Diseret, Menghentak, Berayun)", ["Tidak", "Ya"], bagian: "Tingkat Risiko"),
            H("penilaian_jatuh_nilai1", "Nilai"),
            P("penilaian_jatuh_skala2", "2. Pusing/Pingsan Pada Posisi Tegak", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai2", "Nilai"),
            P("penilaian_jatuh_skala3", "3. Kebingungan Setiap Saat", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai3", "Nilai"),
            P("penilaian_jatuh_skala4", "4. Nokturia/Inkontinen", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai4", "Nilai"),
            P("penilaian_jatuh_skala5", "5. Kebingunan Intermiten", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai5", "Nilai"),
            P("penilaian_jatuh_skala6", "6. Kelemahan Umum", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai6", "Nilai"),
            P("penilaian_jatuh_skala7", "7. Obat-obatan Berisiko Tinggi (Diuretic, Narkotik, Sedative, Anti Psikotik, Laksatif, Vasodilator, Antiaritmia, Anti Hipertensi, Anti Diabetik, Anti Depresan, Neuroleptik, NSAID)", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai7", "Nilai"),
            P("penilaian_jatuh_skala8", "8. Riwayat Jatuh Dalam Waktu 12 Bulan", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai8", "Nilai"),
            P("penilaian_jatuh_skala9", "9. Osteoporosis", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai9", "Nilai"),
            P("penilaian_jatuh_skala10", "10. Gangguan Pendengaran Atau Penglihatan", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai10", "Nilai"),
            P("penilaian_jatuh_skala11", "11. Usia 70 Tahun Ke Atas", ["Tidak", "Ya"]),
            H("penilaian_jatuh_nilai11", "Nilai"),
            H("penilaian_jatuh_totalnilai", "Total Nilai"),
            T("hasil_skrining", "Hasil Skrining", 200, wajib: true, bagian: "Hasil"),
            T("saran", "Saran", 200, wajib: true),
        ],
        skor: [("penilaian_jatuh_skala10", "penilaian_jatuh_nilai10", ["0", "1"]),
            ("penilaian_jatuh_skala11", "penilaian_jatuh_nilai11", ["0", "1"]),
            ("penilaian_jatuh_skala1", "penilaian_jatuh_nilai1", ["0", "4"]),
            ("penilaian_jatuh_skala2", "penilaian_jatuh_nilai2", ["0", "3"]),
            ("penilaian_jatuh_skala3", "penilaian_jatuh_nilai3", ["0", "3"]),
            ("penilaian_jatuh_skala4", "penilaian_jatuh_nilai4", ["0", "3"]),
            ("penilaian_jatuh_skala5", "penilaian_jatuh_nilai5", ["0", "2"]),
            ("penilaian_jatuh_skala6", "penilaian_jatuh_nilai6", ["0", "2"]),
            ("penilaian_jatuh_skala7", "penilaian_jatuh_nilai7", ["0", "2"]),
            ("penilaian_jatuh_skala8", "penilaian_jatuh_nilai8", ["0", "2"]),
            ("penilaian_jatuh_skala9", "penilaian_jatuh_nilai9", ["0", "1"])],
        lanjut: isi => Jumlah(isi, "penilaian_jatuh_totalnilai", Seri("penilaian_jatuh_nilai", 1, 11))) with
    {
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        Keterangan = isi => Tingkat(isi, "penilaian_jatuh_totalnilai", t => "Tingkat Resiko : " + (t <= 3 ? "Risiko Rendah (0 - 3), " + Standar : "Risiko Tinggi (>= 4), " + Tinggi)),
        Kelompok = FormKlinisKatalog.Risiko,
    };

    private static FormKlinis LanjutanRisikoJatuhLansia => Buat(
        "btnPenilaianRisikoJatuhLansia", "Penilaian Lanjutan Risiko Jatuh Lansia", "penilaian_lanjutan_resiko_jatuh_lansia", "penilaian_lanjutan_resiko_jatuh_lansia",
        [
            P("penilaian_jatuhmorse_skala1", "1. Riwayat Jatuh", ["Tidak", "Pasien Datang Karena Jatuh", "Pasien Jatuh Dalam 2 Bulan Terakhir"], bagian: "Skala Morse"),
            H("penilaian_jatuhmorse_nilai1", "Nilai"),
            P("penilaian_jatuhmorse_skala2", "2. Status Mental", ["Tidak", "Pasien Delirium", "Pasien Disorientasi", "Pasien Agitasi"]),
            H("penilaian_jatuhmorse_nilai2", "Nilai"),
            P("penilaian_jatuhmorse_skala3", "3. Penglihatan", ["Tidak", "Memakai Kaca Mata", "Penglihatan Kabur", "Memiliki Glukoma/Katarak/Degenerasi Makula"]),
            H("penilaian_jatuhmorse_nilai3", "Nilai"),
            P("penilaian_jatuhmorse_skala4", "4. Kebiasaan Berkemih", ["Tidak", "Prilaku Berkemih/Frekuensi/Urgensi/Incontinensia/Nokturia"]),
            H("penilaian_jatuhmorse_nilai4", "Nilai"),
            P("penilaian_jatuhmorse_skala5", "5. Transfer", ["Mandiri", "Memerlukan Bantuan 1 Orang/Pengawasan", "Memerlukan Bantuan 2 Orang", "Memerlukan Bantuan Total"]),
            H("penilaian_jatuhmorse_nilai5", "Nilai"),
            P("penilaian_jatuhmorse_skala6", "5. Mobilitas", ["Mandiri", "Berjalan Dengan Bantuan 1 Orang", "Menggunakan Kursi Roda", "Imobilisasi"]),
            H("penilaian_jatuhmorse_nilai6", "Nilai"),
            H("penilaian_jatuhmorse_totalnilai", "Total Nilai"),
            T("hasil_skrining", "Hasil Skrining", 200, wajib: true, bagian: "Hasil"),
            T("saran", "Saran", 200, wajib: true),
        ],
        skor: [("penilaian_jatuhmorse_skala1", "penilaian_jatuhmorse_nilai1", ["0", "6", "6"]),
            ("penilaian_jatuhmorse_skala2", "penilaian_jatuhmorse_nilai2", ["0", "14", "14", "14"]),
            ("penilaian_jatuhmorse_skala3", "penilaian_jatuhmorse_nilai3", ["0", "1", "1", "1"]),
            ("penilaian_jatuhmorse_skala4", "penilaian_jatuhmorse_nilai4", ["0", "2"]),
            ("penilaian_jatuhmorse_skala5", "penilaian_jatuhmorse_nilai5", ["0", "1", "2", "3"]),
            ("penilaian_jatuhmorse_skala6", "penilaian_jatuhmorse_nilai6", ["0", "1", "2", "3"])],
        lanjut: isi =>
        {
            // Transfer dan mobilitas dihitung bersama: sampai 3 bernilai 0, lebih dari itu 7.
            if (Jumlah(isi, "penilaian_jatuhmorse_totalnilai", "penilaian_jatuhmorse_nilai5", "penilaian_jatuhmorse_nilai6") is not { } gerak) return;
            if (Jumlah(isi, "penilaian_jatuhmorse_totalnilai", Seri("penilaian_jatuhmorse_nilai", 1, 4)) is { } lain)
                isi["penilaian_jatuhmorse_totalnilai"] = (lain + (gerak <= 3 ? 0 : 7)).ToString(CultureInfo.InvariantCulture);
        }) with
    {
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        Keterangan = isi => Tingkat(isi, "penilaian_jatuhmorse_totalnilai", t => "Tingkat Resiko : " + (t < 6 ? "Risiko Rendah (0-5), " + Standar : t < 17 ? "Risiko Sedang (6-16), " + Standar : "Risiko Tinggi (> 17), " + Tinggi)),
        Kelompok = FormKlinisKatalog.Risiko,
    };

    private static FormKlinis LanjutanRisikoJatuhPsikiatri => Buat(
        "btnPenilaianRisikoJatuhPsikiatri", "Penilaian Lanjutan Risiko Jatuh Psikiatri", "penilaian_lanjutan_resiko_jatuh_psikiatri", "penilaian_lanjutan_resiko_jatuh_psikiatri",
        [
            P("penilaian_jatuhedmonson_skala1", "1. Usia", ["-", "Kurang Dari 50 Th", "50 - 70 Th", "Lebih Dari 70 Th"], bagian: "Skala Edmonson"),
            H("penilaian_jatuhedmonson_nilai1", "Nilai"),
            P("penilaian_jatuhedmonson_skala2", "2. Status Mental", ["-", "Kesadaran/Orientasi Baik Setiap Saat", "Agitasi/Ansietas", "Kadang-kadang Bingung", "Bingung/Disorientasi"]),
            H("penilaian_jatuhedmonson_nilai2", "Nilai"),
            P("penilaian_jatuhedmonson_skala3", "3. Eliminasi", ["-", "Mandiri & Mampu Mengontrol BAB/BAK", "Dower Catheter/Colostomy", "Eliminasi Dengan Bantuan", "Gangguan Eliminasi (Inkontinensia/Nokturia/Frekuensi)", "Inkontinensia Tetapi Mampu Untuk Mobilisasi"]),
            H("penilaian_jatuhedmonson_nilai3", "Nilai"),
            P("penilaian_jatuhedmonson_skala4", "4. Pengobatan", ["-", "Tanpa Obat-obatan", "Obat-obatan Jantung", "Obat-obatan Psikotropika (Termasuk Benzodiazepine & Antidepresan)", "Mendapat Tambahan Obat-obatan & Atau Obat PRN Selama 24 Jam Terakhir"]),
            H("penilaian_jatuhedmonson_nilai4", "Nilai"),
            P("penilaian_jatuhedmonson_skala5", "5. Diagnosa", ["-", "Bipolar/Gangguan Schizoaffective", "Penggunaan Obat-obatan Terlarang, Ketergantungan Alkohol", "Gangguan Depresi Mayor", "Demensia/Delirium"]),
            H("penilaian_jatuhedmonson_nilai5", "Nilai"),
            P("penilaian_jatuhedmonson_skala6", "6. Ambulasi/Keseimbangan", ["-", "Mandiri/Keseimbangan Baik/Imobilisasi", "Dengan Alat Bantu (Kursi Roda, Walker, Dll)", "Vertigo/Kelemahan", "Goyah/Membutuhkan Bantuan & Menyadari Kemampuan", "Goyah Tapi Lupa Keterbatasan"]),
            H("penilaian_jatuhedmonson_nilai6", "Nilai"),
            H("penilaian_jatuhedmonson_totalnilai", "Total Nilai"),
            T("hasil_skrining", "Hasil Skrining", 200, wajib: true, bagian: "Hasil"),
            T("saran", "Saran", 200, wajib: true),
        ],
        skor: [("penilaian_jatuhedmonson_skala1", "penilaian_jatuhedmonson_nilai1", ["0", "8", "10", "26"]),
            ("penilaian_jatuhedmonson_skala2", "penilaian_jatuhedmonson_nilai2", ["0", "4", "12", "13", "14"]),
            ("penilaian_jatuhedmonson_skala3", "penilaian_jatuhedmonson_nilai3", ["0", "8", "12", "10", "12", "12"]),
            ("penilaian_jatuhedmonson_skala4", "penilaian_jatuhedmonson_nilai4", ["0", "10", "10", "8", "12"]),
            ("penilaian_jatuhedmonson_skala5", "penilaian_jatuhedmonson_nilai5", ["0", "10", "8", "10", "12"]),
            ("penilaian_jatuhedmonson_skala6", "penilaian_jatuhedmonson_nilai6", ["0", "7", "8", "10", "8", "15"])],
        lanjut: isi => Jumlah(isi, "penilaian_jatuhedmonson_totalnilai", Seri("penilaian_jatuhedmonson_nilai", 1, 6))) with
    {
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        Keterangan = isi => Tingkat(isi, "penilaian_jatuhedmonson_totalnilai", t => "Tingkat Resiko : " + (t < 90 ? "Tidak Berisiko (0-89), " + Standar : "Risiko Tinggi (> 90), " + Tinggi)),
        Kelompok = FormKlinisKatalog.Risiko,
    };

    private static FormKlinis LanjutanFungsional => Buat(
        "btnPenilaianLanjutanSkriningFungsional", "Penilaian Lanjutan Skrining Fungsional", "penilaian_lanjutan_skrining_fungsional", "penilaian_lanjutan_skrining_fungsional",
        [
            P("penilaian_skrining_skala1", "1. Mengendalikan Rangsang BAB", ["Tak Terkendali/Tak Teratur (Perlu Pencahar)", "Kadang-kadang Tak Terkendali (1x Seminggu)", "Terkendali Teratur"], bagian: "Skala Barthel"),
            H("penilaian_skrining_nilai1", "Nilai"),
            P("penilaian_skrining_skala2", "2. Mengendalikan Rangsang BAK", ["Tak Terkendali/Pakai Kateter", "Kadang-kadang Tak Terkendali (Hanya 1x/24 Jam )", "Mandiri"]),
            H("penilaian_skrining_nilai2", "Nilai"),
            P("penilaian_skrining_skala3", "3. Membersihkan Diri (Seka, Sisir, Sikat Gigi)", ["Butuh Pertolongan Orang Lain", "Mandiri"]),
            H("penilaian_skrining_nilai3", "Nilai"),
            P("penilaian_skrining_skala4", "4. Menggunakan WC (In/Out, Lepas Pakaian/Celana, Siram)", ["Tergantung Pertolongan Orang Lain", "Perlu Pertolongan Pada Beberapa Kegiatan Tetapi Dapat Mengerjakan Sendiri Beberapa Kegiatan Yang Lain", "Mandiri"]),
            H("penilaian_skrining_nilai4", "Nilai"),
            P("penilaian_skrining_skala5", "5. Makan", ["Tidak Mampu", "Perlu Ditolong Memotong Makanan", "Mandiri"]),
            H("penilaian_skrining_nilai5", "Nilai"),
            P("penilaian_skrining_skala6", "6. Berubah Sikap Dari Berbaring Ke Duduk", ["Tidak Mampu", "Perlu Banyak Bantuan Untuk Bisa Duduk (2 Orang)", "Bantuan Minimal 1 Orang", "Mandiri"]),
            H("penilaian_skrining_nilai6", "Nilai"),
            P("penilaian_skrining_skala7", "7. Berpindah/Berjalan", ["Tidak Mampu", "Bisa (Pindah) Dengan Kursi Roda", "Berjalan Dengan Bantuan 1 Orang", "Mandiri"]),
            H("penilaian_skrining_nilai7", "Nilai"),
            P("penilaian_skrining_skala8", "8. Mengenakan Pakaian", ["Tergantung Orang Lain", "Sebagian Dibantu (Misal Mengancing Baju)", "Mandiri"]),
            H("penilaian_skrining_nilai8", "Nilai"),
            P("penilaian_skrining_skala9", "9. Naik Turun Anak Tangga", ["Tidak Mampu", "Butuh Pertolongan", "Mandiri"]),
            H("penilaian_skrining_nilai9", "Nilai"),
            P("penilaian_skrining_skala10", "10. Mandi", ["Tergantung Orang Lain", "Mandiri"]),
            H("penilaian_skrining_nilai10", "Nilai"),
            H("penilaian_skrining_totalnilai", "Total Nilai"),
        ],
        skor: [("penilaian_skrining_skala10", "penilaian_skrining_nilai10", ["0", "1"]),
            ("penilaian_skrining_skala1", "penilaian_skrining_nilai1", ["0", "1", "2"]),
            ("penilaian_skrining_skala2", "penilaian_skrining_nilai2", ["0", "1", "2"]),
            ("penilaian_skrining_skala3", "penilaian_skrining_nilai3", ["0", "1"]),
            ("penilaian_skrining_skala4", "penilaian_skrining_nilai4", ["0", "1", "2"]),
            ("penilaian_skrining_skala5", "penilaian_skrining_nilai5", ["0", "1", "2"]),
            ("penilaian_skrining_skala6", "penilaian_skrining_nilai6", ["0", "1", "2", "3"]),
            ("penilaian_skrining_skala7", "penilaian_skrining_nilai7", ["0", "1", "2", "3"]),
            ("penilaian_skrining_skala8", "penilaian_skrining_nilai8", ["0", "1", "2"]),
            ("penilaian_skrining_skala9", "penilaian_skrining_nilai9", ["0", "1", "2"])],
        lanjut: isi => Jumlah(isi, "penilaian_skrining_totalnilai", Seri("penilaian_skrining_nilai", 1, 10))) with
    {
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        Keterangan = isi => Tingkat(isi, "penilaian_skrining_totalnilai", t => "Keterangan : " + (t <= 4 ? "Ketergantungan Total (0-4)"
            : t < 9 ? "Ketergantungan Berat (5-8)" : t < 12 ? "Ketergantungan Sedang (9-11)" : t < 20 ? "Ketergantungan Ringan (12-19)" : "Mandiri (20)")),
        Kelompok = FormKlinisKatalog.Risiko,
    };

    private static FormKlinis Dekubitus => Buat(
        "btnPenilaianRisikoDekubitus", "Penilaian Risiko Dekubitus", "penilaian_risiko_dekubitus", "penilaian_risiko_dekubitus",
        [
            P("kondisi_fisik", "1. Kondisi Fisik", ["Baik", "Sedang", "Buruk", "Sangat Buruk"], bagian: "Skala Norton"),
            H("kondisi_fisik_nilai", "Nilai"),
            P("status_mental", "2. Status Mental", ["Sadar", "Apatis", "Bingung", "Stupor"]),
            H("status_mental_nilai", "Nilai"),
            P("aktifitas", "3. Aktifitas", ["Jalan Sendiri", "Jalan Dengan Bantuan", "Kursi Roda", "Di Tempat Tidur"]),
            H("aktifitas_nilai", "Nilai"),
            P("mobilitas", "4. Mobilitas", ["Bebas Bergerak", "Agar Terbatas", "Sangat Terbatas", "Tidak Mampu Bergerak"]),
            H("mobilitas_nilai", "Nilai"),
            P("inkontinensia", "5. Inkontinensia", ["Kontinen", "Kadang-kadang Inkontinensia Urine", "Selalu Inkontenesia Urine", "Inkontinensia Alvi Dan Urine"]),
            H("inkontinensia_nilai", "Nilai"),
            H("totalnilai", "Total Nilai"),
            H("kategorinilai", "Kategori"),
        ],
        skor: [("kondisi_fisik", "kondisi_fisik_nilai", ["4", "3", "2", "1"]),
            ("status_mental", "status_mental_nilai", ["4", "3", "2", "1"]),
            ("aktifitas", "aktifitas_nilai", ["4", "3", "2", "1"]),
            ("mobilitas", "mobilitas_nilai", ["4", "3", "2", "1"]),
            ("inkontinensia", "inkontinensia_nilai", ["4", "3", "2", "1"])],
        lanjut: isi =>
        {
            var total = Jumlah(isi, "totalnilai", "kondisi_fisik_nilai", "status_mental_nilai", "aktifitas_nilai", "mobilitas_nilai", "inkontinensia_nilai");
            isi["kategorinilai"] = total switch { null => "", < 12 => "Risiko Tinggi", < 16 => "Risiko Sedang", _ => "Risiko Rendah" };
        }) with
    {
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        Kelompok = FormKlinisKatalog.Risiko,
    };

    private static FormKlinis RisikoJatuhNeonatus => Buat(
        "btnPenilaianRisikoJatuhNeonatus", "Penilaian Lanjutan Risiko Jatuh Neonatus", "penilaian_risiko_jatuh_neonatus", "penilaian_risiko_jatuh_neonatus",
        [
            P("intervensi1", "Pasang Gelang Risiko Jatuh", ["Tidak", "Ya"], awal: "Ya", bagian: "I. Intervensi"),
            P("intervensi2", "Edukasi Orang Tua/Keluarga", ["Tidak", "Ya"], awal: "Ya"),
            P("intervensi3", "Dekatkan Box Bayi Dengan Ibu", ["Tidak", "Ya"], awal: "Ya"),
            P("intervensi4", "Orentasi Ruangan Pada Orang Tua/Keluarga", ["Tidak", "Ya"], awal: "Ya"),
            P("intervensi5", "Pastikan Lantai & Alas Kaki Tidak Licin", ["Tidak", "Ya"], awal: "Ya"),
            P("intervensi6", "Pastikan Selalu Ada Pendamping", ["Tidak", "Ya"], awal: "Ya"),
            P("intervensi7", "Bila Dirawat Dalam Inkubator, Pastikan Semua Jendela Terkunci", ["Tidak", "Ya"], awal: "Ya"),
            P("intervensi8", "Kontrol Rutin Oleh Perawat/Bidan", ["Tidak", "Ya"], awal: "Ya"),
            P("intervensi9", "Pasang Tanda Risiko Jatuh Pada Box/Inkubator", ["Tidak", "Ya"], awal: "Ya"),
            P("edukasi1", "Tempatkan Bayi Pada Tempat Yang Aman", ["Tidak", "Ya"], awal: "Ya", bagian: "II. Edukasi Yang Diberikan"),
            P("edukasi2", "Segera Istirahat Apabila Merasa Lelah & Tempatkan Bayi Pada Boxnya", ["Tidak", "Ya"], awal: "Ya"),
            P("edukasi3", "Cara Membedong Bayi", ["Tidak", "Ya"], awal: "Ya"),
            P("edukasi4", "Libatkan Keluarga Untuk Mendampingi/Segera Panggil Perawat/Bidan Jika Diperlukan", ["Tidak", "Ya"], awal: "Ya"),
            P("edukasi5", "Teknik Menggendong Bayi", ["Tidak", "Ya"], awal: "Ya"),
            P("sasaran1", "Bapak", ["Tidak", "Ya"], bagian: "III. Sasaran Edukasi"),
            P("sasaran2", "Ibu", ["Tidak", "Ya"], awal: "Ya"),
            P("sasaran3", "Keluarga", ["Tidak", "Ya"]),
            P("sasaran4", "Wali Lainnya", ["Tidak", "Ya"]),
            P("evaluasi1", "Memahami & Mampu Menjelaskan Kembali", ["Tidak", "Ya"], awal: "Ya", bagian: "IV. Evaluasi"),
            P("evaluasi2", "Perlu Edukasi Ulang", ["Tidak", "Ya"], awal: "Ya"),
            P("evaluasi3", "Mampu Mendemonstrasikan", ["Tidak", "Ya"], awal: "Ya"),
        ]) with
    {
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        Kelompok = FormKlinisKatalog.Risiko,
    };

    private static FormKlinis AdiksiNikotin => Buat(
        "btnSkriningAdiksiNikotin", "Skrining Adiksi Nikotin (Fagerstrom)", "skrining_adiksi_nikotin", "skrining_adiksi_nikotin",
        [
            P("rokok_dihisap", "1. Berapa banyak rokok yang Anda hisap dalam satu hari ?", ["1-10", "11-20", "21-30", ">=31"], bagian: "I. Anamnesis"),
            H("nilai_rokok_dihisap", "Nilai"),
            P("menyalakan_rokok", "2. Seberapa cepat Anda menyalakan rokok pertama anda setelah terjaga ?", ["Dalam 5 Menit", "6 Hingga 30 Menit", "31 Hingga 60 Menit", "Setelah 60 Menit"]),
            H("nilai_menyalakan_rokok", "Nilai"),
            P("tidak_rela", "3. Rokok mana yang paling Anda tidak relakan untuk di hentikan ?", ["Lainnya", "Rokok Pertama Pada Pagi Hari"]),
            H("nilai_tidak_rela", "Nilai"),
            P("jam_pertama", "4. Apakah Anda merokok lebih banyak dalam jam pertama hari anda dari pada sisa hari Anda ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_jam_pertama", "Nilai"),
            P("rasa_ingin", "5. Apakah Anda kesulitan menahan rasa ingin merokok di tempat yang dilarang seperti bangunan umum, pesawat terbang, atau di tempat kerja ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_rasa_ingin", "Nilai"),
            P("sakit_berat", "6. Apakah Anda masih merokok ketika Anda sakit berat, sehingga Anda harus berbaring dalam sebagian besar waktu Anda ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_sakit_berat", "Nilai"),
            H("nilai_total", "Total Nilai"),
            H("keterangan_hasil_skrining", "Kesimpulan"),
            P("skala_motivasi", "Skala Motivasi", ["1. Saya SUDAH memutuskan TIDAK akan berhenti merokok seumur hidup Saya", "2. Saya TIDAK PERNAH berpikir untuk berhenti merokok. Saya TIDAK PUNYA rencana untuk berhenti", "3. Saya PERNAH berpikir untuk berhentl merokok, tetapi Saya TIDAK PUNYA rencana", "4. TERKADANG Saya berpikir untuk berhenti merokok, tetapi Saya tidak punya rencana", "5. Saya SERING berpikir untuk berhentl merokok, tetapi Saya tidak punya rencana", "6. Saya BERENCANA untuk berhenti merokok dalam 6 bulan ke depan", "7. Saya berencana untuk berhenti merokok dalam 30 hari ke depan", "8. Saya masih merokok, tetapi Saya mau berubah. Saya siap untuk berhenti merokok", "9. Saya sudah berhenti merokok, tetapi Saya khawatir akan merokok kembali, Saya butuh lingkungan tanpa asap rokok", "10. Saya sudah berhenti merokok"], bagian: "II. Motivasi"),
        ],
        skor: [("jam_pertama", "nilai_jam_pertama", ["1", "0"]),
            ("menyalakan_rokok", "nilai_menyalakan_rokok", ["3", "2", "1", "0"]),
            ("rasa_ingin", "nilai_rasa_ingin", ["1", "0"]),
            ("rokok_dihisap", "nilai_rokok_dihisap", ["0", "1", "2", "3"]),
            ("sakit_berat", "nilai_sakit_berat", ["1", "0"]),
            ("tidak_rela", "nilai_tidak_rela", ["0", "1"])],
        lanjut: isi =>
        {
            var total = Jumlah(isi, "nilai_total", "nilai_rokok_dihisap", "nilai_menyalakan_rokok", "nilai_tidak_rela", "nilai_jam_pertama",
                "nilai_rasa_ingin", "nilai_sakit_berat");
            isi["keterangan_hasil_skrining"] = total switch
            { null => "", > 7 => "Ketergantungan Berat", > 4 => "Ketergantungan Sedang", _ => "Ketergantungan Rendah" };
        }) with
    {
        SatuPerKunjungan = true,
    };

    private static FormKlinis Anemia => Buat(
        "btnSkriningAnemia", "Skrining Anemia", "skrining_anemia", "skrining_anemia",
        [
            P("mudah_lelah", "1. Apakah Anda sering merasa mudah lelah, letih, lesu, lunglai, lalai (sering lupa) atau sakit kepala ?", ["Tidak", "Ya"], bagian: "I. Anamnesis"),
            P("buah_sayur", "2. Apakah Anda mengkonsumsi sayur dan buah setiap hari ?", ["Tidak", "Ya"]),
            P("protein_hewani", "3. Apakah Anda sering mengkonsumsi protein hewani ?", ["Tidak", "Ya"]),
            P("masalah_pubertas", "- Masalah Pubertas", ["Tidak", "Ya"], bagian: "4. Kesehatan Reproduksi"),
            P("risiko_ims", "- Risiko IMS", ["Tidak", "Ya"]),
            P("kekerasan_seksual", "- Risiko Kekerasan Seksual", ["Tidak", "Ya"]),
            P("sudah_menstruasi", "- Apakah Anda sudah mengalami menstruasi ? (Rematri SMP dan SMA)", ["Tidak", "Ya"]),
            P("gangguan_menstruasi", "- Gangguan Menstruasi", ["Tidak", "Ya"]),
            P("tambah_darah", "5. Apakah Anda rutin mengkonsumsi Tablet Tambah Darah ? (Rematri SMP dan SMA)", ["Tidak", "Ya"]),
            P("kelainan_darah", "6. Apakah Anda memiliki riwayat penyakit kelainan darah ?", ["Tidak", "Ya"]),
            P("keluarga_thalasemia", "7. Apakah di keluarga Anda ada yang menderita Thalasemia ?", ["Tidak", "Ya"]),
            P("rambut", "- Rambut", ["Sehat", "Tidak Sehat"], bagian: "8. Pemeriksaan Kebersihan Diri"),
            P("kulit", "- Kulit", ["Sehat", "Tidak Sehat"]),
            P("bekas_sutikan", "- Kulit Ada Bekas Suntikan", ["Tidak", "Ya"]),
            P("kuku", "- Kuku", ["Sehat", "Tidak Sehat"]),
            P("tanda_klinis", "Tanda Klinis Anemia (Pucat Pada Bagian Konjungtiva/Kelopak Mata Bagian Dalam Bawah, Bibir, Lidah, Telapak Tangan)", ["Tidak", "Ya"], bagian: "II. Pemeriksaan Fisik"),
            T("pemeriksaan_hb", "Pemeriksaan HB (g/dl)", 8, bagian: "III. Pemeriksaan Penunjang"),
            H("kadar_hb", "Kadar Hemoglobin"),
            H("jenis_anemia", "Jenis Anemia"),
            T("hasil_skrining", "Hasil Skrining", 40, wajib: true, bagian: "IV. Interpretasi"),
            T("keterangan", "Keterangan", 100, wajib: true),
        ],
        lanjut: isi =>
        {
            var (kadar, jenis) = !Angka(Teks(isi, "pemeriksaan_hb"), out var hb) ? ("", "")
                : hb >= 12 ? (">= 12 g/dl", "Normal") : hb >= 11 && hb <= 11.9 ? ("11,9 - 11 g/dl", "Ringan")
                : hb >= 8 && hb <= 10.9 ? ("10.9 - 8 g/dl", "Sedang") : hb < 8 ? ("< 8 g/dl", "Berat") : ("", "");
            isi["kadar_hb"] = kadar;
            isi["jenis_anemia"] = jenis;
        }) with
    {
        SatuPerKunjungan = true,
        Periksa = isi => Teks(isi, "jenis_anemia").Length == 0
            ? "Isi Pemeriksaan HB dengan angka satu desimal (desimal memakai titik)." : null,
    };

    private static FormKlinis CURB65 => Buat(
        "btnSkriningCURB65", "Skrining CURB-65", "skrining_curb65", "skrining_curb65",
        [
            P("pernyataancurb651", "1. C (Confusion) : Disorientasi / Bingung ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "I. Tingkat Keparahan Pneumonia"),
            H("nilai_curb651", "Nilai"),
            P("pernyataancurb652", "2. U (Urea) : BUN > 20 mg/dL atau Urea > 7 mmol/L ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_curb652", "Nilai"),
            P("pernyataancurb653", "3. R (Respiratory Rate) : RR ≥ 30 x/menit ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_curb653", "Nilai"),
            P("pernyataancurb654", "4. B (Blood Pressure) : SBP < 90 atau DBP ≤ 60 ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_curb654", "Nilai"),
            P("pernyataancurb655", "5. 65 (Age ≥ 65) : Usia ≥ 65 tahun ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_curb655", "Nilai"),
            H("nilai_total_curb65", "Total Nilai"),
            T("kesimpulan", "Kesan/Kesimpulan", 100, bagian: "II. Interpretasi"),
        ],
        skor: [("pernyataancurb651", "nilai_curb651", ["1", "0"]),
            ("pernyataancurb652", "nilai_curb652", ["1", "0"]),
            ("pernyataancurb653", "nilai_curb653", ["1", "0"]),
            ("pernyataancurb654", "nilai_curb654", ["1", "0"]),
            ("pernyataancurb655", "nilai_curb655", ["1", "0"])],
        lanjut: isi => Jumlah(isi, "nilai_total_curb65", Seri("nilai_curb65", 1, 5))) with
    {
        SatuPerKunjungan = true,
        Usul = isi =>
        {
            var usul = new Dictionary<string, string>();
            if (Umur(isi) >= 65) usul["pernyataancurb655"] = "Ya";
            if (int.TryParse(Teks(isi, "nilai_total_curb65"), out var n))
                usul["kesimpulan"] = n <= 1 ? "Risiko Rendah, Tatalaksana Rawat Jalan" : n == 2 ? "Risiko Sedang, Tatalaksana Rawat Inap"
                    : "Risiko Tinggi, Tatalaksana Rawat Inap + Pertimbangkan ICU";
            return usul;
        },
    };

    private static FormKlinis DiabetesMelitus => Buat(
        "btnSkriningDiabetesMelitus", "Skrining Diabetes Melitus", "skrining_diabetes_melitus", "skrining_diabetes_melitus",
        [
            P("anamnesis1", "1. Sering Lapar ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "I. Anamnesis"),
            P("anamnesis2", "2. Sering Haus ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis3", "3. Sering BAK/Mengompol ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis4", "4. Penurunan Berat Badan 2-6 Minggu Terakhir ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis5", "5. Riwayat Pribadi Diabetes Melitus ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis6", "6. Riwayat Keluarga Diabetes Melitus ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis7", "7. Riwayat Merokok ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis8", "8. Riwayat Minum Alkohol/Merokok Di Keluarga ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis9", "9. Kebiasaan Makan Manis ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis10", "10. Aktifitas Fisik Setiap Hari ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis11", "11. Istirahat Cukup ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("anamnesis12", "12. Kurang Makan Buah Dan Sayur ?", ["Ya", "Tidak"], awal: "Tidak"),
            T("berat_badan", "BB (Kg)", 6, bagian: "II. Pemeriksaan Fisik (WHO Asia Pasifik)"),
            T("tinggi_badan", "TB (Cm)", 8),
            H("imt", "IMT (BB/TB²)"),
            H("kasifikasi_imt", "Klasifikasi IMT"),
            T("hasil_gds", "Hasil Pemeriksaan GDS", 10, bagian: "III. Pemeriksaan Penunjang"),
            T("keterangan_gds", "Keterangan Hasil Pemeriksaan GDS", 50),
            T("hasil_gdp", "Hasil Pemeriksaan GDP", 10),
            T("keterangan_gdp", "Keterangan Hasil Pemeriksaan GDP", 50),
            P("hasil_skrining", "Hasil Skrining", ["Normal", "Suspek"], bagian: "IV. Interpretasi"),
            T("keterangan_skrining", "Keterangan/Diskripsi Hasil", 60),
        ],
        lanjut: Imt) with
    {
        SatuPerKunjungan = true,
        Usul = isi => Satu("keterangan_skrining",
            Teks(isi, "hasil_skrining") == "Suspek" ? "Dicurigai diabetes melitus" : "Tidak dicurigai diabetes melitus"),
        Periksa = PeriksaImt,
    };

    private static FormKlinis Frailty => Buat(
        "btnSkriningFrailtySyndrome", "Skrining Frailty Syndrome", "skrining_frailty_syndrome", "skrining_frailty_syndrome",
        [
            P("resistensi", "1. Resistensi: dengan usaha sendiri tanpa bantuan alat berjalan, apakah Anda kesulitan naik 10 anak tangga tanpa istirahat di antaranya ?", ["Tidak", "Ya"], bagian: "I. Anamnesis"),
            H("nilai_resistensi", "Nilai"),
            P("aktivitas", "2. Aktivitas: seberapa sering dalam 4 minggu Anda merasa kelelahan ?", ["Jarang", "Kadang-kadang", "Sebagian Besar Waktu", "Sepanjang Waktu"]),
            H("nilai_aktivitas", "Nilai"),
            P("penyakit_tidak_pernah", "3. Penyakit (dari 11 penyakit utama): Tidak Pernah", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_kanker", "- Kanker (Selain Kanker Kulit Kecil)", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_gagal_jantung", "- Gagal Jantung Kongestif", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_ginjal", "- Penyakit Ginjal", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_nyeri_dada", "- Nyeri Dada", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_serangan_jantung", "- Serangan Jantung", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_stroke", "- Stroke", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_asma", "- Asma", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_nyeri_sendi", "- Nyeri Sendi", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_paru_kronis", "- Penyakit Paru Kronis", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_hipertensi", "- Hipertensi", ["Ya", "Tidak"], awal: "Tidak"),
            P("penyakit_diabetes", "- Diabetes", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_penyakit", "Nilai (lebih dari 4 penyakit)"),
            P("usaha_berjalan", "4. Usaha Berjalan: tanpa bantuan alat berjalan, apakah Anda kesulitan berjalan kira-kira 100 sampai 200 meter ?", ["Tidak", "Ya"]),
            H("nilai_usaha_berjalan", "Nilai"),
            P("berat_badan", "5. Hilangnya Berat Badan dalam satu tahun: [(BB setahun lalu - BB sekarang) / BB setahun lalu] x 100%", ["< 5%", ">= 5%"]),
            H("nilai_berat_badan", "Nilai"),
            H("nilai_total", "Total Skor"),
            H("hasil_skrining", "Hasil Skrining", bagian: "II. Interpretasi"),
            T("keterangan", "Keterangan", 40),
        ],
        skor: [("aktivitas", "nilai_aktivitas", ["0", "0", "1", "1"]),
            ("berat_badan", "nilai_berat_badan", ["0", "1"]),
            ("resistensi", "nilai_resistensi", ["0", "1"]),
            ("usaha_berjalan", "nilai_usaha_berjalan", ["0", "1"])],
        lanjut: isi =>
        {
            isi["nilai_penyakit"] = PenyakitFrailty.Count(k => Teks(isi, k) == "Ya") > 4 ? "1" : "0";
            var total = Jumlah(isi, "nilai_total", "nilai_resistensi", "nilai_aktivitas", "nilai_penyakit", "nilai_usaha_berjalan", "nilai_berat_badan");
            isi["hasil_skrining"] = total switch { null => "", > 2 => "Rapuh/Renta", > 0 => "Pra-Kerapuhan", _ => "Segar & Tidak Rapuh" };
        }) with
    {
        SatuPerKunjungan = true,
        Periksa = isi => Teks(isi, "penyakit_tidak_pernah") == "Ya" && PenyakitFrailty.Any(k => Teks(isi, k) == "Ya")
            ? "Pilihan Tidak Pernah tidak bisa bersama penyakit yang dijawab Ya." : null,
    };

    private static FormKlinis Hipertensi => Buat(
        "btnSkriningHipertensi", "Skrining Hipertensi", "skrining_hipertensi", "skrining_hipertensi",
        [
            P("anamnesis1", "1. Riwayat Pribadi Hipertensi ?", ["Tidak", "Ya"], bagian: "I. Anamnesis"),
            P("anamnesis2", "2. Riwayat Keluarga Tekanan Darah Tinggi ?", ["Tidak", "Ya"]),
            P("anamnesis3", "3. Riwayat Merokok ?", ["Tidak", "Ya"]),
            P("anamnesis4", "4. Riwayat Minum Alkohol / Merokok Di Keluarga ?", ["Tidak", "Ya"]),
            P("anamnesis5", "5. Kebiasaan Makan Asin ?", ["Tidak", "Ya"]),
            P("anamnesis6", "6. Aktifitas Fisik Setiap Hari ?", ["Tidak", "Ya"]),
            P("anamnesis7", "7. Istirahat Cukup ?", ["Tidak", "Ya"]),
            P("anamnesis8", "8. Kurang Makan Buah Dan Sayur ?", ["Tidak", "Ya"]),
            T("sistole", "TD Sistole", 3, wajib: true, bagian: "II. Pemeriksaan Fisik (WHO-ISHWG)"),
            T("diastole", "TD Diastole", 3, wajib: true),
            H("klasifikasi_hipertensi", "Klasifikasi Hipertensi"),
            T("hasil_skrining", "Hasil Skrining", 40, wajib: true, bagian: "III. Interpretasi"),
            T("keterangan", "Keterangan", 100, wajib: true),
        ],
        lanjut: isi => isi["klasifikasi_hipertensi"] =
            !Angka(Teks(isi, "sistole"), out var s) || !Angka(Teks(isi, "diastole"), out var d) ? ""
            : s < 120 && d < 80 ? "Optimal Normal"
            : s >= 120 && s < 130 && d >= 80 && d < 85 ? "Normal"
            : s >= 130 && s <= 139 && d >= 85 && d <= 89 ? "Tinggi"
            : s >= 140 && s <= 149 && d >= 90 && d <= 94 ? "Sub-group : Perbatasan"
            : s >= 140 && s <= 159 && d >= 90 && d <= 99 ? "Tingkat 1 (Hipertensi Ringan)"
            : s >= 160 && s <= 179 && d >= 100 && d <= 109 ? "Tingkat 2 (Hipertensi Sedang)"
            : s >= 180 && d >= 110 ? "Tingkat 3 (Hipertensi Berat)"
            : s >= 140 && s <= 149 && d < 90 ? "Hipertensi Sistol Tensolasi" : "Tidak Diketahui") with
    {
        SatuPerKunjungan = true,
        Periksa = isi => WajibAngka(isi, ("sistole", "TD Sistole"), ("diastole", "TD Diastole")),
    };

    private static FormKlinis IndraPendengaran => Buat(
        "btnSkriningIndraPendengaran", "Skrining Indra Pendengaran", "skrining_indra_pendengaran", "skrining_indra_pendengaran",
        [
            P("curiga_tuli_telinga_kiri", "1. Curiga Tuli Kongenital - Telinga Kiri", ["Tidak", "Ya"], bagian: "I. Pemeriksaan/Anamnesa"),
            P("curiga_tuli_telinga_kanan", "1. Curiga Tuli Kongenital - Telinga Kanan", ["Tidak", "Ya"]),
            P("curiga_tuli_telinga_rujuk", "1. Curiga Tuli Kongenital - Rujuk Spesialis", ["Tidak", "Ya"]),
            P("penurunan_pendengaran_telinga_kiri", "2. Presbikusis - Telinga Kiri", ["Tidak", "Ya"]),
            P("penurunan_pendengaran_telinga_kanan", "2. Presbikusis - Telinga Kanan", ["Tidak", "Ya"]),
            P("mendengar_bisikan_telinga_kiri", "3. Mendengar Bisikan Saat Tes Bisik - Telinga Kiri", ["Normal", "Gangguan Pendengaran"]),
            P("mendengar_bisikan_telinga_kanan", "3. Mendengar Bisikan Saat Tes Bisik - Telinga Kanan", ["Normal", "Gangguan Pendengaran"]),
            P("congek_telinga_kiri", "4. OMSK/Congek - Telinga Kiri", ["Tidak", "Ya"]),
            P("congek_telinga_kanan", "4. OMSK/Congek - Telinga Kanan", ["Tidak", "Ya"]),
            P("congek_telinga_rujuk", "4. OMSK/Congek - Rujuk Spesialis", ["Tidak", "Ya"]),
            P("sumbatan_serumen_telinga_kiri", "5. Sumbatan Serumen - Telinga Kiri", ["Tidak", "Ya"]),
            P("sumbatan_serumen_telinga_kanan", "5. Sumbatan Serumen - Telinga Kanan", ["Tidak", "Ya"]),
            P("sumbatan_serumen_telinga_rujuk", "5. Sumbatan Serumen - Rujuk Spesialis", ["Tidak", "Ya"]),
            T("hasil_skrining", "Hasil Skrining", 40, wajib: true, bagian: "II. Interpretasi"),
            T("keterangan", "Keterangan", 100, wajib: true),
        ]) with
    {
        SatuPerKunjungan = true,
    };

    private static FormKlinis Acrs => Buat(
        "btnSkriningInstrumenACRS", "Skrining Instrumen ACRS", "skrining_instrumen_acrs", "skrining_instrumen_acrs",
        [
            P("pernyataanacrs1", "1. Tidak kenal lelah, atau aktivitas yang berlebihan ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"], bagian: "I. Deteksi Dini GPPH (anak usia > 3 tahun)"),
            H("nilai_acrs1", "Nilai"),
            P("pernyataanacrs2", "2. Mudah jadi gembira, impulsive ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs2", "Nilai"),
            P("pernyataanacrs3", "3. Mengganggu anak-anak lain ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs3", "Nilai"),
            P("pernyataanacrs4", "4. Gagal menyelesaikan kegiatan yang telah dimulai, rentang perhatian pendek ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs4", "Nilai"),
            P("pernyataanacrs5", "5. Menggerak-gerakkan anggota badan atau kepala secara terus-menerus ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs5", "Nilai"),
            P("pernyataanacrs6", "6. Kurang perhatian, mudah teralihkan ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs6", "Nilai"),
            P("pernyataanacrs7", "7. Permintaannya harus segera dipenuhi, mudah menjadi frustasi ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs7", "Nilai"),
            P("pernyataanacrs8", "8. Sering dan mudah menangis ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs8", "Nilai"),
            P("pernyataanacrs9", "9. Suasana hatinya mudah berubah dengan cepat dan drastris ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs9", "Nilai"),
            P("pernyataanacrs10", "10. Ledakan kekesalan, tingkah laku eksplosif dan tak terduga ?", ["Tidak Pernah", "Kadang-kadang", "Sering", "Selalu"]),
            H("nilai_acrs10", "Nilai"),
            H("nilai_total_acrs", "Total Nilai"),
            T("kesimpulan", "Kesan/Kesimpulan", 100, bagian: "II. Interpretasi"),
        ],
        skor: [("pernyataanacrs10", "nilai_acrs10", ["0", "1", "2", "3"]),
            ("pernyataanacrs1", "nilai_acrs1", ["0", "1", "2", "3"]),
            ("pernyataanacrs2", "nilai_acrs2", ["0", "1", "2", "3"]),
            ("pernyataanacrs3", "nilai_acrs3", ["0", "1", "2", "3"]),
            ("pernyataanacrs4", "nilai_acrs4", ["0", "1", "2", "3"]),
            ("pernyataanacrs5", "nilai_acrs5", ["0", "1", "2", "3"]),
            ("pernyataanacrs6", "nilai_acrs6", ["0", "1", "2", "3"]),
            ("pernyataanacrs7", "nilai_acrs7", ["0", "1", "2", "3"]),
            ("pernyataanacrs8", "nilai_acrs8", ["0", "1", "2", "3"]),
            ("pernyataanacrs9", "nilai_acrs9", ["0", "1", "2", "3"])],
        lanjut: isi => Jumlah(isi, "nilai_total_acrs", Seri("nilai_acrs", 1, 10))) with
    {
        SatuPerKunjungan = true,
        Usul = isi => int.TryParse(Teks(isi, "nilai_total_acrs"), out var n)
            ? Satu("kesimpulan", n >= 13 ? "Anak Kemungkinan Dengan GPPH" : "Kondisi Normal") : [],
    };

    private static FormKlinis Amt => Buat(
        "btnSkriningInstrumenAMT", "Skrining Instrumen AMT", "skrining_instrumen_amt", "skrining_instrumen_amt",
        [
            P("pernyataanamt1", "1. Berapa umur Anda ?", ["Salah", "Benar"], bagian: "I. Deteksi Dini Fungsi Kognitif"),
            H("nilai_amt1", "Nilai"),
            P("pernyataanamt2", "2. Jam berapa sekarang ?", ["Salah", "Benar"]),
            H("nilai_amt2", "Nilai"),
            P("pernyataanamt3", "3. Dimana alamat rumah Anda ?", ["Salah", "Benar"]),
            H("nilai_amt3", "Nilai"),
            P("pernyataanamt4", "4. Tahun berapa sekarang ?", ["Salah", "Benar"]),
            H("nilai_amt4", "Nilai"),
            P("pernyataanamt5", "5. Saat ini Kita sedang ada di mana ?", ["Salah", "Benar"]),
            H("nilai_amt5", "Nilai"),
            P("pernyataanamt6", "6. Apa nama keluarga Anda ?", ["Salah", "Benar"]),
            H("nilai_amt6", "Nilai"),
            P("pernyataanamt7", "7. Tahun berapa indonesia merdeka ?", ["Salah", "Benar"]),
            H("nilai_amt7", "Nilai"),
            P("pernyataanamt8", "8. Siapa nama Presiden/Kepala Negara saat ini ?", ["Salah", "Benar"]),
            H("nilai_amt8", "Nilai"),
            P("pernyataanamt9", "9. Tahun berapa Anda lahir ?", ["Salah", "Benar"]),
            H("nilai_amt9", "Nilai"),
            P("pernyataanamt10", "10. Hitung mundur dari 20 ke 1", ["Salah", "Benar"]),
            H("nilai_amt10", "Nilai"),
            H("nilai_total_amt", "Total Nilai"),
            T("kesimpulan", "Kesan/Kesimpulan", 100, bagian: "II. Interpretasi"),
        ],
        skor: [("pernyataanamt10", "nilai_amt10", ["0", "1"]),
            ("pernyataanamt1", "nilai_amt1", ["0", "1"]),
            ("pernyataanamt2", "nilai_amt2", ["0", "1"]),
            ("pernyataanamt3", "nilai_amt3", ["0", "1"]),
            ("pernyataanamt4", "nilai_amt4", ["0", "1"]),
            ("pernyataanamt5", "nilai_amt5", ["0", "1"]),
            ("pernyataanamt6", "nilai_amt6", ["0", "1"]),
            ("pernyataanamt7", "nilai_amt7", ["0", "1"]),
            ("pernyataanamt8", "nilai_amt8", ["0", "1"]),
            ("pernyataanamt9", "nilai_amt9", ["0", "1"])],
        lanjut: isi => Jumlah(isi, "nilai_total_amt", Seri("nilai_amt", 1, 10))) with
    {
        SatuPerKunjungan = true,
        Usul = isi => int.TryParse(Teks(isi, "nilai_total_amt"), out var n)
            ? Satu("kesimpulan", n <= 3 ? "Gangguan Ingatan Berat" : n <= 7 ? "Gangguan Ingatan Sedang" : "Normal") : [],
    };

    private static FormKlinis Esat => Buat(
        "btnSkriningInstrumenESAT", "Skrining Instrumen ESAT", "skrining_instrumen_esat", "skrining_instrumen_esat",
        [
            P("pernyataanesat1", "1. Anak suka bermain cilukba atau permainan interaktif sederhana ?", ["Ya", "Tidak"], bagian: "I. Deteksi Dini Autis"),
            P("pernyataanesat2", "2. Apakah anak Anda sering melakukan kontak mata dengan Anda ketika berbicara atau bermain ?", ["Ya", "Tidak"]),
            P("pernyataanesat3", "3. Apakah anak Anda tersenyum sebagai respons terhadap senyum Anda ?", ["Ya", "Tidak"]),
            P("pernyataanesat4", "4. Apakah anak Anda meniru suara, ekspresi wajah, atau gerakan Anda ?", ["Ya", "Tidak"]),
            P("pernyataanesat5", "5. Apakah anak Anda menunjukkan benda yang menarik (misal menunjuk mainan untuk diperlihatkan pada Anda) ?", ["Ya", "Tidak"]),
            P("pernyataanesat6", "6. Apakah anak Anda tertarik bermain dengan anak lain ?", ["Ya", "Tidak"]),
            P("pernyataanesat7", "7. Apakah anak Anda sering memperhatikan bila Anda menunjuk sesuatu ?", ["Ya", "Tidak"]),
            P("pernyataanesat8", "8. Apakah anak Anda menggunakan jari untuk menunjuk sesuatu yang diinginkannya ?", ["Ya", "Tidak"]),
            P("pernyataanesat9", "9. Apakah anak Anda memperhatikan wajah orang saat mendengar suara baru atau asing ?", ["Ya", "Tidak"]),
            P("pernyataanesat10", "10. Apakah anak Anda tampak tidak peduli terhadap orang lain di sekitarnya ? (Jawaban \"Ya\" mengindikasikan risiko)", ["Ya", "Tidak"], awal: "Tidak"),
            T("kesimpulan", "Kesan/Kesimpulan", 150, wajib: true, bagian: "II. Interpretasi & Catatan"),
            T("catatan", "Catatan", 100),
        ]) with
    {
        SatuPerKunjungan = true,
        Usul = isi =>
        {
            var nilai = Seri("pernyataanesat", 1, 9).Count(k => Teks(isi, k) == "Tidak") + (Teks(isi, "pernyataanesat10") == "Ya" ? 3 : 0);
            return Satu("kesimpulan", nilai >= 3
                ? "Anak disarankan menjalani pemeriksaan lanjutan oleh psikolog/dokter anak tumbuh kembang/psikiater anak" : "Normal");
        },
    };

    private static FormKlinis Sdq => Buat(
        "btnSkriningInstrumenSDQ", "Skrining Instrumen SDQ (Usia 11-18 Tahun)", "skrining_instrumen_sdq", "skrining_instrumen_sdq",
        [
            P("pernyataansdq1", "1. Saya berusaha baik kepada orang lain. Saya peduli dengan perasaan mereka.", ["Tidak Benar", "Agak Benar", "Selalu Benar"], bagian: "I. Kuisioner Kekuatan dan Kelemahan (SDQ)"),
            H("nilai_sdq1", "Nilai"),
            P("pernyataansdq2", "2. Saya gelisah. Saya tidak dapat diam untuk waktu lama.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq2", "Nilai"),
            P("pernyataansdq3", "3. Saya sering sakit kepala, sakit perut atau macam-macam sakit lainnya.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq3", "Nilai"),
            P("pernyataansdq4", "4. Kalau Saya memiliki mainan, CD, atau makanan, Saya biasanya berbagi dengan orang lain.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq4", "Nilai"),
            P("pernyataansdq5", "5. Saya menjadi sangat marah dan sering tidak dapat mengendalikan kemarahan Saya.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq5", "Nilai"),
            P("pernyataansdq6", "6. Saya lebih suka sendiri daripada bersama dengan orang yang seusiaku.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq6", "Nilai"),
            P("pernyataansdq7", "7. Saya biasanya melakukan apa yang diperintahkan oleh orang lain.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq7", "Nilai"),
            P("pernyataansdq8", "8. Saya banyak merasa cemas atau khawatir terhadap apapun.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq8", "Nilai"),
            P("pernyataansdq9", "9. Saya selalu siap menolong jika seseorang terluka, kecewa atau merasa sakit.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq9", "Nilai"),
            P("pernyataansdq10", "10. Bila sedang gelisah atau cemas badan Saya sering bergerak–gerak tanpa Saya sadari.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq10", "Nilai"),
            P("pernyataansdq11", "11. Saya mempunyai satu orang teman baik atau lebih.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq11", "Nilai"),
            P("pernyataansdq12", "12. Saya sering bertengkar dengan orang lain. Saya dapat memaksa orang lain melakukan yang Saya inginkan.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq12", "Nilai"),
            P("pernyataansdq13", "13. Saya sering merasa tidak bahagia, sedih atau menangis.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq13", "Nilai"),
            P("pernyataansdq14", "14. Orang lain seusia Saya umumnya menyukai Saya.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq14", "Nilai"),
            P("pernyataansdq15", "15. Perhatian Saya mudah teralih, Saya sulit untuk memusatkan perhatian pada apapun.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq15", "Nilai"),
            P("pernyataansdq16", "16. Saya merasa gugup dalam situasi baru, Saya mudah kehilangan rasa percaya diri.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq16", "Nilai"),
            P("pernyataansdq17", "17. Saya bersikap baik terhadap anak-anak yang lebih muda dari Saya.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq17", "Nilai"),
            P("pernyataansdq18", "18. Saya sering dituduh berbohong atau berbuat curang.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq18", "Nilai"),
            P("pernyataansdq19", "19. Saya sering diganggu atau dipermainkan oleh anak-anak atau remaja lainnya.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq19", "Nilai"),
            P("pernyataansdq20", "20. Saya sering menawarkan diri untuk membantu orang lain (orang tua, guru, anak-anak).", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq20", "Nilai"),
            P("pernyataansdq21", "21. Saya berpikir terlebih dulu akibat yang akan terjadi, sebelum berbuat atau melakukan sesuatu.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq21", "Nilai"),
            P("pernyataansdq22", "22. Saya mengambil barang yang bukan milik Saya dari rumah, sekolah atau dari mana saja.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq22", "Nilai"),
            P("pernyataansdq23", "23. Saya lebih mudah berteman dengan orang dewasa daripada dengan orang seusia Saya.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq23", "Nilai"),
            P("pernyataansdq24", "24. Banyak yang Saya takuti, Saya mudah menjadi takut.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq24", "Nilai"),
            P("pernyataansdq25", "25. Memiliki perhatian yang baik terhadap apapun, mampu menyelesaikan tugas atau pekerjaan rumah sampai selesai.", ["Tidak Benar", "Agak Benar", "Selalu Benar"]),
            H("nilai_sdq25", "Nilai"),
            H("nilai_total_sdq", "Total Nilai"),
            H("gejala_emosional", "A. Pengkajian Gejala Emosional (E)", bagian: "II. Interpretasi dan Kesimpulan"),
            H("nilai_gejala_emosional", "Nilai"),
            H("masalah_perilaku", "B. Pengkajian Masalah Perilaku (C)"),
            H("nilai_masalah_perilaku", "Nilai"),
            H("hiperaktivitas", "C. Pengkajian Hiperaktivitas (H)"),
            H("nilai_hiperaktivitas", "Nilai"),
            H("teman_sebaya", "D. Masalah Teman Sebaya (P)"),
            H("nilai_teman_sebaya", "Nilai"),
            H("kekuatan", "E. Pengkajian Skor Kekuatan (Pr)"),
            H("nilai_kekuatan", "Nilai"),
            H("kesulitan", "F. Pengkajian Skor Kesulitan"),
            H("nilai_kesulitan", "Nilai"),
            T("keterangan", "Keterangan", 200),
        ],
        skor: [("pernyataansdq10", "nilai_sdq10", ["0", "1", "2"]),
            ("pernyataansdq11", "nilai_sdq11", ["0", "1", "2"]),
            ("pernyataansdq12", "nilai_sdq12", ["0", "1", "2"]),
            ("pernyataansdq13", "nilai_sdq13", ["0", "1", "2"]),
            ("pernyataansdq14", "nilai_sdq14", ["0", "1", "2"]),
            ("pernyataansdq15", "nilai_sdq15", ["0", "1", "2"]),
            ("pernyataansdq16", "nilai_sdq16", ["0", "1", "2"]),
            ("pernyataansdq17", "nilai_sdq17", ["0", "1", "2"]),
            ("pernyataansdq18", "nilai_sdq18", ["0", "1", "2"]),
            ("pernyataansdq19", "nilai_sdq19", ["0", "1", "2"]),
            ("pernyataansdq1", "nilai_sdq1", ["0", "1", "2"]),
            ("pernyataansdq20", "nilai_sdq20", ["0", "1", "2"]),
            ("pernyataansdq21", "nilai_sdq21", ["0", "1", "2"]),
            ("pernyataansdq22", "nilai_sdq22", ["0", "1", "2"]),
            ("pernyataansdq23", "nilai_sdq23", ["0", "1", "2"]),
            ("pernyataansdq24", "nilai_sdq24", ["0", "1", "2"]),
            ("pernyataansdq25", "nilai_sdq25", ["0", "1", "2"]),
            ("pernyataansdq2", "nilai_sdq2", ["0", "1", "2"]),
            ("pernyataansdq3", "nilai_sdq3", ["0", "1", "2"]),
            ("pernyataansdq4", "nilai_sdq4", ["0", "1", "2"]),
            ("pernyataansdq5", "nilai_sdq5", ["0", "1", "2"]),
            ("pernyataansdq6", "nilai_sdq6", ["0", "1", "2"]),
            ("pernyataansdq7", "nilai_sdq7", ["0", "1", "2"]),
            ("pernyataansdq8", "nilai_sdq8", ["0", "1", "2"]),
            ("pernyataansdq9", "nilai_sdq9", ["0", "1", "2"])],
        lanjut: isi =>
        {
            Jumlah(isi, "nilai_total_sdq", Seri("nilai_sdq", 1, 25));
            Subskala(isi, "nilai_gejala_emosional", "gejala_emosional", n => n > 7, n => n > 6, 3, 8, 13, 16, 24);
            Subskala(isi, "nilai_masalah_perilaku", "masalah_perilaku", n => n > 6, n => n > 4, 5, 7, 12, 18, 22);
            Subskala(isi, "nilai_hiperaktivitas", "hiperaktivitas", n => n > 7, n => n > 6, 2, 10, 15, 21, 25);
            Subskala(isi, "nilai_teman_sebaya", "teman_sebaya", n => n > 6, n => n > 4, 6, 11, 14, 19, 23);
            Subskala(isi, "nilai_kekuatan", "kekuatan", n => n < 4, n => n < 5, 1, 4, 9, 17, 20);
            var kesulitan = Jumlah(isi, "nilai_kesulitan", "nilai_gejala_emosional", "nilai_masalah_perilaku", "nilai_hiperaktivitas",
                "nilai_teman_sebaya");
            isi["kesulitan"] = kesulitan switch { null => "", > 20 => "Abnormal", > 16 => "Ambang/Boderline", _ => "Normal" };
        }) with
    {
        SatuPerKunjungan = true,
    };

    private static FormKlinis KankerKolorektal => Buat(
        "btnSkriningKankerKolorektal", "Skrining Kanker Kolorektal", "skrining_kanker_kolorektal", "skrining_kanker_kolorektal",
        [
            P("riwayat_polip_adenomatosa", "Riwayat Polip Adenomatosa", ["Tidak", "Ya"], bagian: "I. Riwayat Diri Sendiri"),
            P("riwayat_bab_berdarah", "Riwayat BAB Berdarah Tanpa Hemoroid", ["Tidak", "Ya"]),
            P("riwayat_reseksi_kuratif", "Riwayat Reseksi Kuratif Kanker Kolorektal", ["Tidak", "Ya"]),
            P("colok_dubur", "Colok Dubur", ["Tidak", "Ya"], bagian: "II. Hasil Pemeriksaan"),
            P("riwayat_kolorektal_keluarga", "Riwayat Kolorektal Di Keluarga", ["Tidak", "Ya"]),
            P("darah_samar_feses", "Darah Samar Feses", ["Tidak", "Ya"]),
            P("rujuk_faskes_lanjut", "Rujuk Ke Fasilitas Tingkat Lanjut", ["Tidak", "Ya"]),
            P("kesimpulan", "Kesimpulan", ["Normal", "Suspek"], bagian: "III. Interpretasi"),
            T("keterangan_kesimpulan", "Keterangan Kesimpulan", 100),
        ]) with
    {
        SatuPerKunjungan = true,
        Usul = isi => Satu("keterangan_kesimpulan",
            Teks(isi, "kesimpulan") == "Suspek" ? "Dicurigai kanker kolorektal" : "Tidak dicurigai kanker kolorektal"),
    };

    private static FormKlinis Kekerasan => Buat(
        "btnSkriningKekerasanPadaPerempuan", "Skrining Kekerasan Pada Perempuan", "skrining_kekerasan_pada_perempuan", "skrining_kekerasan_pada_perempuan",
        [
            P("menggambarkan_hubungan", "1. Secara umum, bagaimana Anda menggambarkan hubungan Anda ?", ["Tidak Ada Ketegangan", "Beberapa Ketegangan", "Banyak Ketegangan"], bagian: "I. Pertanyaan Awal"),
            H("skor_menggambarkan_hubungan", "Nilai"),
            P("berdebat_dengan_pasangan", "2. Seperti apa saat Anda dan pasangan berdebat ?", ["Tidak Ada Kesulitan", "Beberapa Kesulitan", "Kesulitan Besar"]),
            H("skor_berdebat_dengan_pasangan", "Nilai"),
            P("pertengkaran_membuat_sedih", "1. Apakah pertengkaran pernah membuat Anda merasa sedih atau buruk tentang diri sendiri ?", ["Tidak Pernah", "Kadang-kadang", "Sering"], bagian: "II. Pertanyaan Lanjutan"),
            H("skor_pertengkaran_membuat_sedih", "Nilai"),
            P("pertengkaran_menghasilkan_pukulan", "2. Apakah pertengkaran pernah menghasilkan pukulan, tendangan, atau dorongan ?", ["Tidak Pernah", "Kadang-kadang", "Sering"]),
            H("skor_pertengkaran_menghasilkan_pukulan", "Nilai"),
            P("pernah_merasa_takut_dengan_pasangan", "3. Apakah Anda pernah merasa takut dengan apa yang pasangan Anda katakan atau lakukan ?", ["Tidak Pernah", "Kadang-kadang", "Sering"]),
            H("skor_pernah_merasa_takut_dengan_pasangan", "Nilai"),
            P("pasangan_melecehkan_secara_fisik", "4. Apakah pasangan Anda pernah melecehkan Anda secara fisik ?", ["Tidak Pernah", "Kadang-kadang", "Sering"]),
            H("skor_pasangan_melecehkan_secara_fisik", "Nilai"),
            P("pasangan_melecehkan_secara_imosional", "5. Pernahkah pasangan Anda melecehkan Anda secara emosional ?", ["Tidak Pernah", "Kadang-kadang", "Sering"]),
            H("skor_pasangan_melecehkan_secara_imosional", "Nilai"),
            P("pasangan_melecehkan_secara_seksual", "6. Apakah pasangan Anda pernah melecehkan Anda secara seksual ?", ["Tidak Pernah", "Kadang-kadang", "Sering"]),
            H("skor_pasangan_melecehkan_secara_seksual", "Nilai"),
            H("totalskor", "Total Skor", bagian: "III. Interpretasi"),
            H("hasil_skrining", "Hasil Skrining"),
        ],
        skor: [("menggambarkan_hubungan", "skor_menggambarkan_hubungan", ["1", "2", "3"]),
            ("berdebat_dengan_pasangan", "skor_berdebat_dengan_pasangan", ["1", "2", "3"]),
            ("pertengkaran_membuat_sedih", "skor_pertengkaran_membuat_sedih", ["1", "2", "3"]),
            ("pertengkaran_menghasilkan_pukulan", "skor_pertengkaran_menghasilkan_pukulan", ["1", "2", "3"]),
            ("pernah_merasa_takut_dengan_pasangan", "skor_pernah_merasa_takut_dengan_pasangan", ["1", "2", "3"]),
            ("pasangan_melecehkan_secara_fisik", "skor_pasangan_melecehkan_secara_fisik", ["1", "2", "3"]),
            ("pasangan_melecehkan_secara_imosional", "skor_pasangan_melecehkan_secara_imosional", ["1", "2", "3"]),
            ("pasangan_melecehkan_secara_seksual", "skor_pasangan_melecehkan_secara_seksual", ["1", "2", "3"])],
        lanjut: isi =>
        {
            var total = Jumlah(isi, "totalskor", "skor_menggambarkan_hubungan", "skor_berdebat_dengan_pasangan", "skor_pertengkaran_membuat_sedih", "skor_pertengkaran_menghasilkan_pukulan", "skor_pernah_merasa_takut_dengan_pasangan", "skor_pasangan_melecehkan_secara_fisik", "skor_pasangan_melecehkan_secara_imosional", "skor_pasangan_melecehkan_secara_seksual");
            isi["hasil_skrining"] = total switch
            { null => "", > 12 => "Pasien Terindikasi Mengalami Kekerasan", _ => "Pasien Tidak Terindikasi Mengalami Kekerasan" };
        }) with
    {
        SatuPerKunjungan = true,
    };

    private static FormKlinis GigiBalita => Buat(
        "btnSkriningKesehatanGigiMulutBalita", "Skrining Kesehatan Gigi & Mulut Balita", "skrining_kesehatan_gigi_mulut_balita", "skrining_kesehatan_gigi_mulut_balita",
        [
            P("pernah_pemeriksaan_gigimulut", "1. Apakah Pernah Melakukan Pemeriksaan Gigi & Mulut ?", ["Pernah", "Tidak Pernah"], bagian: "I. Anamnesis"),
            P("sudah_tumbuh_gigi", "2. Apakah Sudah Ada Gigi Yang Tumbuh ?", ["Sudah", "Belum"]),
            P("jumlah_gigi_tumbuh", "3. Berapa Jumlah Gigi Yang Sudah Tumbuh ?", ["<20", ">20", "0"]),
            P("kondisi_kebersihan_gigimulut", "4. Bagaimana Kondisi Kebersihan Gigi & Mulut ?", ["Bersih", "Kotor"]),
            P("kebiasaan_susu_botol", "5. Apakah Anak Mempunyai Kebiasaan Minum Susu Botol Saat Tidur Malam ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("mengemil_manis", "6. Apakah Anak Mempunyai Kebiasaan Mengemil Makanan Manis Diantara Waktu Makan ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("menyikat_gigi_sebelum_tidur", "7. Apakah Anak Mempunyai Kebiasaan Menyikat Gigi Sebelum Tidur ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("mengemut_makanan", "8. Apakah Anak Mempunyai Kebiasaan Mengemut Makanan ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("lidah_kotor", "9. Apakah Lidah Anak Kotor ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("celah_bibir", "10. Apakah Anak Mempunyai Celah Bibir Atau Langit-langit ?", ["Tidak Ada", "Ada"]),
            T("hasil_skrining", "Hasil Skrining", 50, bagian: "II. Interpretasi"),
            T("keterangan", "Keterangan", 100),
        ]) with
    {
        SatuPerKunjungan = true,
        Usul = isi => new()
        {
            ["jumlah_gigi_tumbuh"] = Teks(isi, "sudah_tumbuh_gigi") == "Belum" ? "0" : "<20",
            ["hasil_skrining"] = Teks(isi, "kebiasaan_susu_botol") == "Tidak" && Teks(isi, "mengemil_manis") == "Tidak"
                && Teks(isi, "menyikat_gigi_sebelum_tidur") == "Ya" && Teks(isi, "mengemut_makanan") == "Tidak" && Teks(isi, "lidah_kotor") == "Tidak"
                ? "Tidak Perlu Dirujuk ke Poli Gigi" : "Perlu Dirujuk ke Poli Gigi",
        },
        Periksa = isi => Teks(isi, "sudah_tumbuh_gigi") == "Belum" && Teks(isi, "jumlah_gigi_tumbuh") != "0"
            ? "Bila gigi belum tumbuh, jumlah gigi harus 0." : null,
    };

    private static FormKlinis GigiDewasa => Buat(
        "btnSkriningKesehatanGigiMulutDewasa", "Skrining Kesehatan Gigi & Mulut Dewasa", "skrining_kesehatan_gigi_mulut_dewasa", "skrining_kesehatan_gigi_mulut_dewasa",
        [
            P("kontrol_gigi", "1. Apakah Rutin Kontrol Ke Dokter Gigi, Minimal Sekali Dalam 6 Bulan ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "I. Anamnesis"),
            P("gigi_bungsu_tumbuh", "2. Apakah Ada Gigi Bungsu Yang Tumbuh ( Usia > 18 Tahun) ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("gigi_hilang", "3. Apakah Ada Gigi Yang Hilang ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("gigi_berlubang", "4. Apakah Ada Gigi Yang Berlubang ?", ["Ya", "Tidak"], awal: "Tidak"),
            T("hasil_skrining", "Hasil Skrining", 50, bagian: "II. Interpretasi"),
            T("keterangan", "Keterangan", 100),
        ]) with
    {
        SatuPerKunjungan = true,
        Usul = isi => Satu("hasil_skrining", Teks(isi, "kontrol_gigi") == "Ya" ? "Tidak Perlu Dirujuk ke Poli Gigi" : "Perlu Dirujuk ke Poli Gigi"),
    };

    private static FormKlinis GigiLansia => Buat(
        "btnSkriningKesehatanGigiMulutLansia", "Skrining Kesehatan Gigi & Mulut Lansia", "skrining_kesehatan_gigi_mulut_lansia", "skrining_kesehatan_gigi_mulut_lansia",
        [
            P("kontrol_gigi", "1. Apakah Rutin Kontrol Ke Dokter Gigi, Minimal Sekali Dalam 6 Bulan ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "I. Anamnesis"),
            P("pola_makan", "2. Apakah Sudah Melakukan Pola Makan Yang Sehat Dan Pemilihan Makanan Yang Tepat ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("sikat_gigi", "3. Berapa Kali Sikat Gigi Dalam Sehari (Minimal 2 Kali) ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("gigi_palsu", "4. Jika Menggunakan Gigi Palsu, Apakah Rutin Selalu Dibersihkan ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("gigi_berfungsi", "5. Apakah Masih Memiliki Gigi Yang Berfungsi Baik (Minimal 20 Gigi Tidak Goyang & Lubang) ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("mukosa_mulut", "6. Apakah Ada Keluhan Pada Mukosa Mulut (Deteksi Dini Penyakit Mulut) ?", ["Ya", "Tidak"], awal: "Tidak"),
            T("hasil_skrining", "Hasil Skrining", 50, bagian: "II. Interpretasi"),
            T("keterangan", "Keterangan", 100),
        ]) with
    {
        SatuPerKunjungan = true,
        Usul = isi => Satu("hasil_skrining", Teks(isi, "kontrol_gigi") == "Ya" ? "Tidak Perlu Dirujuk ke Poli Gigi" : "Perlu Dirujuk ke Poli Gigi"),
    };

    private static FormKlinis GigiRemaja => Buat(
        "btnSkriningKesehatanGigiMulutRemaja", "Skrining Kesehatan Gigi & Mulut Anak Sekolah & Remaja", "skrining_kesehatan_gigi_mulut_remaja", "skrining_kesehatan_gigi_mulut_remaja",
        [
            P("pernah_pemeriksaan_gigimulut", "1. Apakah pernah melakukan pemeriksaan gigi & mulut ?", ["Pernah", "Tidak Pernah"], bagian: "I. Anamnesis"),
            P("jumlah_gigi_tumbuh", "2. Berapa jumlah gigi yang sudah tumbuh ?", ["<20", ">20"]),
            P("kondisi_kebersihan_gigimulut", "3. Bagaimana kondisi kebersihan gigi dan mulut ?", ["Bersih", "Kotor"]),
            P("punya_gigi_berlubang", "4. Apakah anak mempunyai gigi berlubang ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("pernah_gusi_berdarah", "5. Apakah anak pernah merasakan gusi mudah berdarah ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("punya_karang_gigi", "6. Apakah anak mempunyai karang gigi ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("gigi_depan_tidak_teratur", "7. Apakah anak mempunyai susunan gigi depan tidak teratur ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("menyikat_gigi_sebelum_tidur", "8. Apakah anak mempunyai kebiasaan menyikat gigi sebelum tidur ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("punya_sariawan", "9. Apakah anak mempunyai sariawan ?", ["Ya", "Tidak"], awal: "Tidak"),
            T("pemeriksaan_fisik", "Pemeriksaan Fisik", 600, bagian: "II. Pemeriksaan"),
            T("pemeriksaan_penunjang", "Pemeriksaan Penunjang", 600),
            T("hasil_skrining", "Hasil Skrining", 50, bagian: "III. Interpretasi"),
            T("keterangan", "Keterangan", 100),
        ]) with
    {
        SatuPerKunjungan = true,
    };

    private static FormKlinis KesehatanPenglihatan => Buat(
        "btnSkriningKesehatanPenglihatan", "Skrining Kesehatan Penglihatan", "skrining_kesehatan_penglihatan", "skrining_kesehatan_penglihatan",
        [
            P("mata_luar", "Hasil Pemeriksaan Mata Luar", ["Normal", "Tidak Sehat"], bagian: "I. Pemeriksaan Fisik"),
            P("tajam_kiri", "1. Tajam Penglihatan - Mata Kiri", ["Normal (6/6 - 6/18)", "Kelainan Refraksi (< 6/18 - 6/60)", "Low Vision (6/60 - 3/60)", "Kebutaan (< 3/60)"], bagian: "II. Pemeriksaan Penunjang"),
            P("tajam_kanan", "1. Tajam Penglihatan - Mata Kanan", ["Normal (6/6 - 6/18)", "Kelainan Refraksi (< 6/18 - 6/60)", "Low Vision (6/60 - 3/60)", "Kebutaan (< 3/60)"]),
            P("buta_warna_kiri", "2. Buta Warna - Mata Kiri", ["Tidak", "Ya"]),
            P("buta_warna_kanan", "2. Buta Warna - Mata Kanan", ["Tidak", "Ya"]),
            P("kacamata", "3. Kacamata", ["Tidak", "Ya"]),
            P("visus_kiri", "4. Nilai Visus - Mata Kiri", ["Normal (6/6 - 6/18)", "Kelainan Refraksi (< 6/18 - 6/60)", "Low Vision (6/60 - 3/60)", "Kebutaan (< 3/60)"]),
            P("visus_kanan", "4. Nilai Visus - Mata Kanan", ["Normal (6/6 - 6/18)", "Kelainan Refraksi (< 6/18 - 6/60)", "Low Vision (6/60 - 3/60)", "Kebutaan (< 3/60)"]),
            P("refraksi_kiri", "5. Gangguan Refraksi - Mata Kiri", ["Tidak", "Ya"]),
            P("refraksi_kanan", "5. Gangguan Refraksi - Mata Kanan", ["Tidak", "Ya"]),
            P("rujuk_refraksi", "5. Gangguan Refraksi - Rujuk Spesialis", ["Tidak", "Ya"]),
            P("katarak_kiri", "6. Katarak - Mata Kiri", ["Tidak", "Ya"]),
            P("katarak_kanan", "6. Katarak - Mata Kanan", ["Tidak", "Ya"]),
            P("rujuk_katarak", "6. Katarak - Rujuk Spesialis", ["Tidak", "Ya"]),
            T("hasil_skrining", "Hasil Skrining", 40, wajib: true, bagian: "III. Interpretasi"),
            T("keterangan", "Keterangan", 100, wajib: true),
        ]) with
    {
        SatuPerKunjungan = true,
    };

    private static FormKlinis Mpp => Buat(
        "btnSkriningManagerPelayananPasien", "Skrining Manager Pelayanan Pasien", "mpp_skrining", "mpp_skrining",
        [
            P("param1", "1. Keluhan pembiayaan", ["Ya", "Tidak"], awal: "Tidak"),
            P("param2", "2. Penundaan tindakan diagnostik", ["Ya", "Tidak"], awal: "Tidak"),
            P("param3", "3. Keluhan pembiayaan atau biaya klaim melebihi selama masa perawatan", ["Ya", "Tidak"], awal: "Tidak"),
            P("param4", "4. Berisiko tinggi terjadinya komplain", ["Ya", "Tidak"], awal: "Tidak"),
            P("param5", "5. Sering masuk IGD dalam waktu 1x24 jam", ["Ya", "Tidak"], awal: "Tidak"),
            P("param6", "6. Usia >65 tahun dengan ketergantungan", ["Ya", "Tidak"], awal: "Tidak"),
            P("param7", "7. Kasus kompleks (penyakit kronis, multiple diagnosa yang membutuhkan penanganan khusus, memerlukan biaya tinggi)", ["Ya", "Tidak"], awal: "Tidak"),
            P("param8", "8. Pasien APS (Atas Permintaan Sendiri)", ["Ya", "Tidak"], awal: "Tidak"),
            P("param9", "9. Tidak ada keluarga", ["Ya", "Tidak"], awal: "Tidak"),
            P("param10", "10. Ditangani lebih dari 2 dokter spesialis dengan diagnose bermasalah", ["Ya", "Tidak"], awal: "Tidak"),
            P("param11", "11. Penolakan tindakan diagnostik", ["Ya", "Tidak"], awal: "Tidak"),
            P("param12", "12. Penolakan tindakan keperawatan", ["Ya", "Tidak"], awal: "Tidak"),
            P("param13", "13. Penolakan tindakan medis", ["Ya", "Tidak"], awal: "Tidak"),
            P("param14", "14. Penundaan tindakan medis", ["Ya", "Tidak"], awal: "Tidak"),
            P("param15", "15. Riwayat gangguan mental, upaya bunuh diri, terlantar, kekerasan, pelecehan, tinggal sendiri, pemakai narkoba", ["Ya", "Tidak"], awal: "Tidak"),
            P("param16", "16. Membutuhkan kontinuitas pelayanan pasca discharge", ["Ya", "Tidak"], awal: "Tidak"),
        ], pemilik: PemilikForm.Pegawai) with
    {
        TanggalSaja = true,
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
        KolomPemilikKhusus = "nip",
    };

    private static FormKlinis NutrisiAnak => Buat(
        "btnSkriningNutrisiAnak", "Skrining Nutrisi Pasien Anak", "skrining_nutrisi_anak", "skrining_nutrisi_anak",
        [
            T("td", "TD (mmHg)", 8, wajib: true),
            T("hr", "HR (x/menit)", 5, wajib: true),
            T("rr", "RR (x/menit)", 5, wajib: true),
            T("suhu", "Suhu (°C)", 5, wajib: true),
            T("bb", "BB (Kg)", 5, wajib: true),
            T("tbpb", "TB/PB (Cm)", 5, wajib: true),
            T("spo2", "SpO2 (%)", 5, wajib: true),
            T("alergi", "Alergi", 100),
            P("sg1", "1. Apakah pasien tampak kurus?", ["Tidak", "Ya"], bagian: "Skrining Gizi Awal (Strong-kids)"),
            H("nilai1", "Nilai"),
            P("sg2", "2. Apakah terdapat penurunan berat badan selama satu bulan terakhir? (berdasarkan penilaian objektif data berat badan bila ada atau untuk bayi < 1 tahun ; berat badan tidak naik selama 3 bulan terakhir)", ["Tidak", "Ya"]),
            H("nilai2", "Nilai"),
            P("sg3", "3. Apakah terdapat salah satu dari kondisi tersebut? Diare > 5 kali/hari dan/muntah > 3 kali/hari dalam seminggu terakhir; Asupan makanan berkurang selama 1 minggu terakhir", ["Tidak", "Ya"]),
            H("nilai3", "Nilai"),
            P("sg4", "4. Apakah terdapat penyakit atau keadaan yang menyebabkan pasien beresiko mengalami malnutrisi?", ["Tidak", "Ya"]),
            H("nilai4", "Nilai"),
            H("total_hasil", "Total Skor"),
            H("skor_nutrisi", "Hasil Skrining"),
            P("diketahui_dietisien", "Diketahui Dietisien/Dilaporkan Kepada Dokter ?", ["Tidak", "Ya"]),
            T("keterangan_diketahui_dietisien", "Jam Dilaporkan", 10),
        ],
        skor: [("sg1", "nilai1", ["0", "1"]),
            ("sg2", "nilai2", ["0", "1"]),
            ("sg3", "nilai3", ["0", "1"]),
            ("sg4", "nilai4", ["0", "1"])],
        lanjut: isi =>
        {
            var total = Jumlah(isi, "total_hasil", Seri("nilai", 1, 4));
            // Ejaan "Risikio" mengikuti enum tabel Khanza.
            isi["skor_nutrisi"] = total switch { null => "", >= 4 => "Risikio Berat", >= 1 => "Risiko Sedang", _ => "Risiko Rendah" };
        }) with
    {
        SatuPerKunjungan = true,
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
    };

    private static FormKlinis NutrisiDewasa => Buat(
        "btnSkriningNutrisiDewasa", "Skrining Nutrisi Pasien Dewasa", "skrining_nutrisi_dewasa", "skrining_nutrisi_dewasa",
        [
            T("td", "TD (mmHg)", 8, wajib: true),
            T("hr", "HR (x/menit)", 5, wajib: true),
            T("rr", "RR (x/menit)", 5, wajib: true),
            T("suhu", "Suhu (°C)", 5, wajib: true),
            T("bb", "BB (Kg)", 5, wajib: true),
            T("tbpb", "TB/PB (Cm)", 5, wajib: true),
            T("spo2", "SpO2 (%)", 5, wajib: true),
            T("alergi", "Alergi", 100),
            P("sg1", "1. Apakah Ada Penurunan Berat Badan Yang Tidak Diinginkan Selama 6 Bulan Terakhir ?", ["Tidak", "Tidak Yakin", "Ya, 1-5 Kg", "Ya, 6-10 Kg", "Ya, 11-15 Kg", "Ya, >15 Kg"], bagian: "Skrining Gizi Awal (MST)"),
            H("nilai1", "Nilai"),
            P("sg2", "2. Apakah Nafsu Makan Berkurang Karena Tidak Nafsu Makan ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai2", "Nilai"),
            H("total_hasil", "Total Skor (≥ 2 berisiko malnutrisi, konsul ke ahli gizi)"),
        ],
        skor: [("sg1", "nilai1", ["0", "2", "1", "2", "3", "4"]),
            ("sg2", "nilai2", ["1", "0"])],
        lanjut: isi => Jumlah(isi, "total_hasil", "nilai1", "nilai2")) with
    {
        SatuPerKunjungan = true,
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
    };

    private static FormKlinis NutrisiLansia => Buat(
        "btnSkriningNutrisiLansia", "Skrining Nutrisi Pasien Lansia", "skrining_nutrisi_lansia", "skrining_nutrisi_lansia",
        [
            T("td", "TD (mmHg)", 8, wajib: true),
            T("hr", "HR (x/menit)", 5, wajib: true),
            T("rr", "RR (x/menit)", 5, wajib: true),
            T("suhu", "Suhu (°C)", 5, wajib: true),
            T("bb", "BB (Kg)", 5, wajib: true),
            T("tbpb", "TB/PB (Cm)", 5, wajib: true),
            T("spo2", "SpO2 (%)", 5, wajib: true),
            T("alergi", "Alergi", 100),
            P("sg1", "A. Apakah Asupan Makan Berkurang Selama 3 Bulan Terakhir ?", ["Asupan Makan Sangat Berkurang", "Asupan Makan Agak Berkurang", "Asupan Makan Tidak Berkurang"], bagian: "Skrining Gizi Awal (MNA)"),
            H("nilai1", "Nilai"),
            P("sg2", "B. Penurunan Berat Badan Selama 3 Bulan Terakhir", ["Penurunan Berat Badan Lebih Dari 3 Kg", "Tidak Tahu", "Penurunan Berat Badan Antara 1 Hingga 3 Kg", "Tidak Ada Penurunan Berat Badan"]),
            H("nilai2", "Nilai"),
            P("sg3", "C. Mobilitas", ["Terbatas Dari Tempat Tidur Atau Kursi", "Mampu Bangun Dari Tempat Tidur/Kursi Tetapi Tidak Bepergian Keluar Rumah", "Dapat Bepergian Keluar Rumah"]),
            H("nilai3", "Nilai"),
            P("sg4", "D. Menderita Tekanan Psikologis Atau Penyakit Berat Dalam 3 Bulan Terakhir", ["Ya", "Tidak"]),
            H("nilai4", "Nilai"),
            P("sg5", "E. Gangguan Neuropsikologis", ["Depresi Berat Atau Kepikunan Berat", "Kepikunan Ringan", "Tidak Ada Gangguan Psikologis"]),
            H("nilai5", "Nilai"),
            P("sg6", "F. Indeks Massa Tubuh (IMT), atau Lingkar Betis (cm) bila IMT tidak bisa diukur", ["IMT < 19", "19 Hingga < 21", "21 Hingga < 23", "IMT >= 23", "Lingkar Betis < 31", "Lingkar Betis >= 31"]),
            H("nilai6", "Nilai"),
            H("total_hasil", "Total Skor"),
            H("skor_nutrisi", "Hasil Skrining"),
        ],
        skor: [("sg1", "nilai1", ["0", "1", "2"]),
            ("sg2", "nilai2", ["0", "1", "2", "3"]),
            ("sg3", "nilai3", ["0", "1", "2"]),
            ("sg4", "nilai4", ["0", "1"]),
            ("sg5", "nilai5", ["0", "1", "2"])],
        lanjut: isi =>
        {
            // Kolom F menampung IMT (nilai 0-3) atau lingkar betis (0 atau 3).
            isi["nilai6"] = Teks(isi, "sg6") switch
            {
                "IMT < 19" or "Lingkar Betis < 31" => "0", "19 Hingga < 21" => "1", "21 Hingga < 23" => "2",
                "IMT >= 23" or "Lingkar Betis >= 31" => "3", _ => "",
            };
            var total = Jumlah(isi, "total_hasil", Seri("nilai", 1, 6));
            isi["skor_nutrisi"] = total switch { null => "", >= 12 => "Status Gizi Normal", >= 8 => "Beresiko Malnutrisi", _ => "Malnutrisi" };
        }) with
    {
        SatuPerKunjungan = true,
        Batas48Jam = false,
        CekWaktuRegistrasi = false,
        IkutKunciTanggal = false,
    };

    private static FormKlinis Obesitas => Buat(
        "btnSkriningObesitas", "Skrining Obesitas", "skrining_obesitas", "skrining_obesitas",
        [
            P("kebiasaan_makan_manis", "1. Kebiasaan Makan Manis", ["Ya", "Tidak"], awal: "Tidak", bagian: "I. Anamnesis"),
            P("aktifitas_fisik_setiap_hari", "2. Aktifitas Fisik Setiap Hari", ["Ya", "Tidak"], awal: "Tidak"),
            P("istirahat_cukup", "3. Istirahat Cukup", ["Ya", "Tidak"], awal: "Tidak"),
            P("risiko_merokok", "4. Risiko Merokok", ["Ya", "Tidak"], awal: "Tidak"),
            P("riwayat_minum_alkohol_merokok_keluarga", "5. Riwayat Minum Alkohol / Merokok Di Keluarga", ["Ya", "Tidak"], awal: "Tidak"),
            P("riwayat_penggunaan_obat_steroid", "6. Riwayat Penggunaan Obat-obatan Steroid", ["Ya", "Tidak"], awal: "Tidak"),
            T("berat_badan", "BB (Kg)", 6, wajib: true, bagian: "II. Pemeriksaan Fisik (WHO Asia Pasifik)"),
            T("tinggi_badan", "TB (Cm)", 8, wajib: true),
            H("imt", "IMT (BB/TB²)"),
            H("kasifikasi_imt", "Klasifikasi IMT"),
            T("lingkar_pinggang", "Lingkar Pinggang (Cm)", 6, wajib: true),
            H("risiko_lingkar_pinggang", "Risiko Berdasarkan Lingkar Pinggang"),
            H("status_obesitas", "Status Obesitas", bagian: "III. Interpretasi"),
            T("keterangan", "Keterangan", 40),
        ],
        lanjut: isi =>
        {
            Imt(isi);
            Pinggang(isi);
            isi["status_obesitas"] = Angka(Teks(isi, "lingkar_pinggang"), out var lp) && Angka(Teks(isi, "imt"), out var imt)
                ? lp >= AmbangPinggang(isi) || imt >= 25 ? "Berisiko" : "Normal" : "";
        }) with
    {
        SatuPerKunjungan = true,
        Periksa = PeriksaTubuh,
    };

    private static FormKlinis Puma => Buat(
        "btnSkriningPUMA", "Skrining PUMA", "skrining_puma", "skrining_puma",
        [
            P("jk", "1. Jenis Kelamin", ["Perempuan", "Laki-laki"], bagian: "I. Anamnesis"),
            H("nilai_jk", "Nilai"),
            P("usia", "2. Usia (tahun)", ["40-49", "50-59", ">60"]),
            H("nilai_usia", "Nilai"),
            P("pernah_merokok", "3. Apakah Anda pernah merokok ?", ["Pernah", "Tidak"], awal: "Tidak"),
            H("nilai_pernah_merokok", "Nilai"),
            T("jumlah_rokok_perhari", "- Jika pernah, rata-rata jumlah rokok/hari (batang)", 3, wajib: true),
            T("lama_merokok", "- Sudah berapa lama merokok (tahun)", 3, wajib: true),
            P("napas_pendek", "4. Apakah Anda pernah merasa napas pendek ketika Anda berjalan lebih cepat pada jalan yang datar atau pada jalan yang sedikit menanjak ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_napas_pendek", "Nilai"),
            P("punya_dahak", "5. Apakah Anda biasanya mempunyai dahak dari paru atau kesulitan mengeluarkan dahak saat sedang tidak menderita selesma/flu ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_punya_dahak", "Nilai"),
            P("biasa_batuk", "6. Apakah Anda biasanya batuk saat Anda sedang tidak menderita selesma/flu ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_biasa_batuk", "Nilai"),
            P("spirometri", "7. Apakah Dokter atau tenaga medis lainnya pernah meminta Anda untuk melakukan pemeriksaan spirometri atau peak flow meter (meniup ke dalam suatu alat) untuk mengetahui fungsi paru anda ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_spirometri", "Nilai"),
            H("nilai_total", "Total Skor"),
            T("keterangan_hasil_skrining", "Keterangan", 150, bagian: "II. Interpretasi"),
        ],
        skor: [("biasa_batuk", "nilai_biasa_batuk", ["1", "0"]),
            ("punya_dahak", "nilai_punya_dahak", ["1", "0"]),
            ("jk", "nilai_jk", ["0", "1"]),
            ("napas_pendek", "nilai_napas_pendek", ["1", "0"]),
            ("pernah_merokok", "nilai_pernah_merokok", ["1", "0"]),
            ("spirometri", "nilai_spirometri", ["1", "0"]),
            ("usia", "nilai_usia", ["0", "1", "2"])],
        lanjut: isi => Jumlah(isi, "nilai_total", "nilai_jk", "nilai_usia", "nilai_pernah_merokok", "nilai_napas_pendek", "nilai_punya_dahak", "nilai_biasa_batuk", "nilai_spirometri")) with
    {
        SatuPerKunjungan = true,
        Usul = isi =>
        {
            var usul = new Dictionary<string, string>();
            if (Teks(isi, FormKlinis.KunciJk) is { Length: > 0 } jk) usul["jk"] = jk == "L" ? "Laki-laki" : "Perempuan";
            if (Umur(isi) is >= 40 and var umur && umur != 60) usul["usia"] = umur < 50 ? "40-49" : umur < 60 ? "50-59" : ">60";
            if (int.TryParse(Teks(isi, "nilai_total"), out var n))
                usul["keterangan_hasil_skrining"] = n >= 5
                    ? "Edukasi & berikan konseling upaya berhenti merokok, lakukan pemeriksaan spirometri (bila tersedia), rujuk ke FKRTL"
                    : "Edukasi bahaya rokok";
            return usul;
        },
    };

    private static FormKlinis Psi => Buat(
        "btnSkriningPneumoniaSeverityIndex", "Skrining Pneumonia Severity Index", "skrining_pneumonia_severity_index", "skrining_pneumonia_severity_index",
        [
            H("nilai_umur", "Nilai Umur (laki-laki = umur, perempuan = umur - 10)", bagian: "I. Demografi"),
            P("tinggal_di_panti_jompo", "Tinggal Di Panti Jompo", ["Tidak", "Ya"]),
            H("nilai_tinggal_di_panti_jompo", "Nilai"),
            P("gagal_jantung", "Gagal Jantung Kongestif", ["Tidak Ada", "Ada"], bagian: "II. Riwayat Penyakit"),
            H("nilai_gagal_jantung", "Nilai"),
            P("penyakit_hati", "Penyakit Hati", ["Tidak Ada", "Ada"]),
            H("nilai_penyakit_hati", "Nilai"),
            P("penyakit_ginjal", "Penyakit Ginjal Kronis", ["Tidak Ada", "Ada"]),
            H("nilai_penyakit_ginjal", "Nilai"),
            P("kanker", "Kanker (Neoplasma)", ["Tidak Ada", "Ada"]),
            H("nilai_kanker", "Nilai"),
            P("penyakit_serebrovaskuler", "Penyakit Serebrovaskular", ["Tidak Ada", "Ada"]),
            H("nilai_penyakit_serebrovaskuler", "Nilai"),
            P("disorentasi_mental", "Disorientasi Mental", ["Tidak", "Ya"], bagian: "III. Pemeriksaan Fisik"),
            H("nilai_disorentasi_mental", "Nilai"),
            P("frekuensi_napas", "Frekuensi Napas ≥ 30 /menit", ["Tidak", "Ya"]),
            H("nilai_frekuensi_napas", "Nilai"),
            P("td_sistolik", "TD Sistolik < 90 mmHg", ["Tidak", "Ya"]),
            H("nilai_td_sistolik", "Nilai"),
            P("suhu", "Suhu < 35°C Atau ≥ 40°C", ["Tidak", "Ya"]),
            H("nilai_suhu", "Nilai"),
            P("nadi", "Nadi ≥ 125 /menit", ["Tidak", "Ya"]),
            H("nilai_nadi", "Nilai"),
            P("ph_darah", "pH Darah < 7.35", ["Tidak", "Ya"], bagian: "IV. Laboratorium & Radiologi"),
            H("nilai_ph_darah", "Nilai"),
            P("natrium", "Natrium < 130 mEq/L", ["Tidak", "Ya"]),
            H("nilai_natrium", "Nilai"),
            P("bun", "BUN ≥ 30 mg/dL", ["Tidak", "Ya"]),
            H("nilai_bun", "Nilai"),
            P("pao", "PaO₂ < 60 mmHg / SaO₂ < 90%", ["Tidak", "Ya"]),
            H("nilai_pao", "Nilai"),
            P("glukosa", "Glukosa ≥ 250 mg/dL", ["Tidak", "Ya"]),
            H("nilai_glukosa", "Nilai"),
            P("efusi_pleura", "Efusi Pleura (Foto Toraks)", ["Tidak", "Ya"]),
            H("nilai_efusi_pleura", "Nilai"),
            P("hematokrit", "Hematokrit < 30%", ["Tidak", "Ya"]),
            H("nilai_hematokrit", "Nilai"),
            H("total_skor", "Total Skor", bagian: "V. Total Skor dan Interpretasi"),
            H("kelas", "Kelas"),
            H("skor_interpretasi", "Skor Interpretasi"),
            H("mortalitas", "Mortalitas"),
            H("rekomendasi", "Rekomendasi"),
        ],
        skor: [("bun", "nilai_bun", ["0", "20"]),
            ("disorentasi_mental", "nilai_disorentasi_mental", ["0", "20"]),
            ("efusi_pleura", "nilai_efusi_pleura", ["0", "10"]),
            ("frekuensi_napas", "nilai_frekuensi_napas", ["0", "20"]),
            ("gagal_jantung", "nilai_gagal_jantung", ["0", "10"]),
            ("penyakit_ginjal", "nilai_penyakit_ginjal", ["0", "10"]),
            ("glukosa", "nilai_glukosa", ["0", "10"]),
            ("hematokrit", "nilai_hematokrit", ["0", "10"]),
            ("kanker", "nilai_kanker", ["0", "30"]),
            ("nadi", "nilai_nadi", ["0", "10"]),
            ("natrium", "nilai_natrium", ["0", "20"]),
            ("pao", "nilai_pao", ["0", "10"]),
            ("penyakit_hati", "nilai_penyakit_hati", ["0", "20"]),
            ("penyakit_serebrovaskuler", "nilai_penyakit_serebrovaskuler", ["0", "10"]),
            ("suhu", "nilai_suhu", ["0", "15"]),
            ("td_sistolik", "nilai_td_sistolik", ["0", "20"]),
            ("tinggal_di_panti_jompo", "nilai_tinggal_di_panti_jompo", ["0", "10"]),
            ("ph_darah", "nilai_ph_darah", ["0", "30"])],
        lanjut: isi =>
        {
            // Nilai umur: laki-laki sebesar umur, perempuan umur dikurangi 10.
            var umur = Umur(isi);
            isi["nilai_umur"] = Teks(isi, FormKlinis.KunciJk) switch
            { "L" => umur.ToString(CultureInfo.InvariantCulture), "P" => (UmurDaftar(isi) - 10).ToString(CultureInfo.InvariantCulture), _ => "" };
            var total = Jumlah(isi, "total_skor", ["nilai_umur", .. NilaiPsi]);
            // Kelas I: di bawah 50 tahun tanpa penyakit penyerta dan tanda vital berisiko.
            var (kelas, rentang, mati, saran) = total is null ? ("", "", "", "")
                : umur < 50 && KelasSatuPsi.All(k => Teks(isi, k) == "0") ? ("I", "Ditentukan Algoritma Awal", "<0.4%", "Rawat Jalan")
                : total <= 70 ? ("II", "?70", "~0.6%", "Rawat Jalan")   // "?70" mengikuti enum tabel Khanza
                : total <= 90 ? ("III", "71–90", "~2.8%", "Observasi/Rawat Inap Singkat")
                : total <= 130 ? ("IV", "91–130", "~8–9%", "Rawat Inap")
                : ("V", ">130", "~27–30%", "Rawat Inap, Pertimbangkan ICU");
            isi["kelas"] = kelas;
            isi["skor_interpretasi"] = rentang;
            isi["mortalitas"] = mati;
            isi["rekomendasi"] = saran;
        }) with
    {
        SatuPerKunjungan = true,
    };

    private static FormKlinis KankerParu => Buat(
        "btnSkriningRisikoKankerParu", "Skrining Risiko Kanker Paru", "skrining_risiko_kanker_paru", "skrining_risiko_kanker_paru",
        [
            P("jenis_kelamin", "1. Jenis Kelamin Anda ?", ["Laki-laki", "Perempuan"], awal: "Perempuan", bagian: "I. Anamnesis"),
            H("nilai_jenis_kelamin", "Nilai"),
            P("umur", "2. Usia / umur Anda sekarang ?", ["> 65 Tahun", "45 - 65 Tahun", "< 45 Tahun"], awal: "< 45 Tahun"),
            H("nilai_umur", "Nilai"),
            P("pernah_kanker", "3. Apakah pernah didiagnosis menderita kanker ?", ["Ya, Pernah > 5 Tahun Yang Lalu", "Ya, Pernah < 5 Tahun Yang Lalu", "Tidak Pernah"], awal: "Tidak Pernah"),
            H("nilai_pernah_kanker", "Nilai"),
            P("ada_keluarga_kanker", "4. Apakah ada keluarga (ayah/ibu/saudara kandung) menderita kanker ?", ["Ya, Kanker Paru", "Ya, Kanker Jenis Lain", "Tidak Ada"], awal: "Tidak Ada"),
            H("nilai_ada_keluarga_kanker", "Nilai"),
            P("riwayat_rokok", "5. Riwayat merokok/paparan asap rokok (rokok kretek/rokok putih/vape/ shisya/cerutu/rokok linting, dll)", ["Perokok Aktif, Masih Merokok 1 Tahun Ini", "Bekas Perokok, Berhenti < 15 Tahun", "Perokok Pasif", "Tidak Merokok"], awal: "Perokok Pasif"),
            H("nilai_riwayat_rokok", "Nilai"),
            P("riwayat_bekerja_mengandung_karsinogen", "6. Riwayat bekerja di lingkungan yang mengandung zat karsinogen (pertambangan/pabrik/ bengkel/garmen/laboratorium kimia/galangan kapal, dII)", ["Ya", "Tidak Yakin/Ragu-ragu", "Tidak"], awal: "Tidak"),
            H("nilai_riwayat_bekerja_mengandung_karsinogen", "Nilai"),
            P("lingkungan_tinggal_polusi_tinggi", "7. Lingkungan tempat tinggal berpolusi tinggi (lingkungan pabrik/pertambangan/tempat buangan sampah/tepi jalan besar)", ["Ya", "Tidak Yakin/Ragu-ragu", "Tidak"], awal: "Tidak"),
            H("nilai_lingkungan_tinggal_polusi_tinggi", "Nilai"),
            P("lingkungan_rumah_tidak_sehat", "8. Lingkungan dalam rumah yang tidak sehat (ventilasi buruk/atap dari asbes/lantai tanah/ dapur kayu bakar/dapur breket/menggunakan rutin obat nyamuk bakar/semprot, dll)", ["Ya", "Tidak Yakin/Ragu-ragu", "Tidak"], awal: "Tidak"),
            H("nilai_lingkungan_rumah_tidak_sehat", "Nilai"),
            P("pernah_paru_kronik", "9. Pernah didiagnosis/diobati penyakit paru kronik", ["Ya, Pernah Tuberkolosis (TBC)", "Ya, Pernah Penyakit Paru Kronik(PPOK)", "Tidak"], awal: "Tidak"),
            H("nilai_pernah_paru_kronik", "Nilai"),
            H("total_skor", "Total Skor", bagian: "II. Interpretasi"),
            H("hasil_skrining", "Hasil Skrining"),
            T("keterangan", "Keterangan", 50),
        ],
        skor: [("lingkungan_rumah_tidak_sehat", "nilai_lingkungan_rumah_tidak_sehat", ["3", "2", "1"]),
            ("jenis_kelamin", "nilai_jenis_kelamin", ["3", "1"]),
            ("ada_keluarga_kanker", "nilai_ada_keluarga_kanker", ["3", "2", "1"]),
            ("lingkungan_tinggal_polusi_tinggi", "nilai_lingkungan_tinggal_polusi_tinggi", ["3", "2", "1"]),
            ("pernah_kanker", "nilai_pernah_kanker", ["3", "2", "1"]),
            ("pernah_paru_kronik", "nilai_pernah_paru_kronik", ["3", "2", "1"]),
            ("riwayat_bekerja_mengandung_karsinogen", "nilai_riwayat_bekerja_mengandung_karsinogen", ["3", "2", "1"]),
            ("riwayat_rokok", "nilai_riwayat_rokok", ["3", "2", "1", "0"]),
            ("umur", "nilai_umur", ["3", "2", "1"])],
        lanjut: isi =>
        {
            var total = Jumlah(isi, "total_skor", "nilai_jenis_kelamin", "nilai_umur", "nilai_pernah_kanker", "nilai_ada_keluarga_kanker", "nilai_riwayat_rokok", "nilai_riwayat_bekerja_mengandung_karsinogen", "nilai_lingkungan_tinggal_polusi_tinggi", "nilai_lingkungan_rumah_tidak_sehat", "nilai_pernah_paru_kronik");
            isi["hasil_skrining"] = total switch { null => "", > 16 => "Risiko Berat", > 11 => "Risiko Sedang", _ => "Risiko Ringan" };
        }) with
    {
        SatuPerKunjungan = true,
        Usul = isi =>
        {
            var usul = new Dictionary<string, string>();
            if (Teks(isi, FormKlinis.KunciJk) is { Length: > 0 } jk) usul["jenis_kelamin"] = jk == "L" ? "Laki-laki" : "Perempuan";
            if (Teks(isi, FormKlinis.KunciUmur).Length > 0)
                usul["umur"] = Umur(isi) > 65 ? "> 65 Tahun" : Umur(isi) >= 45 ? "45 - 65 Tahun" : "< 45 Tahun";
            return usul;
        },
    };

    private static FormKlinis KankerPayudara => Buat(
        "btnSkriningRisikoKankerPayudara", "Skrining Risiko Kanker Payudara", "skrining_risiko_kanker_payudara", "skrining_risiko_kanker_payudara",
        [
            P("faktor_risiko_awal1", "1. Apakah anda mengalami infeksi yang masuk melalui puting susu, areola, atau kulit payudara ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "I.A. Faktor Awal (Ya = 1)"),
            H("nilai_risiko_awal1", "Nilai"),
            P("faktor_risiko_awal2", "2. Apakah anda tidak memperhatikan kebersihan puting susu, areola, atau kulit payudara ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal2", "Nilai"),
            P("faktor_risiko_awal3", "3. Apakah anda melahirkan anak pertama saat usia lebih dari 25 tahun ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal3", "Nilai"),
            P("faktor_risiko_awal4", "4. Apakah anda atau orang di sekitar anda merokok ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal4", "Nilai"),
            P("faktor_risiko_awal5", "5. Apakah anda mengkonsumsi alkohol ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal5", "Nilai"),
            P("faktor_risiko_awal6", "6. Apakah anda tinggal di daerah tinggi polusi (banyak asap kendaraan, asap pabrik, pemanasan global) ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal6", "Nilai"),
            P("faktor_risiko_awal7", "7. Apakah anda sering mengkonsumsi makanan yang prosesnya dibakar, digoreng, diasap, diasinkan, diacar, mengandung bahan pengawet, berlemak, dan cepat saji ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal7", "Nilai"),
            P("faktor_risiko_awal8", "8. Apakah anda mengalami menarche (saat haid pertama) di usia sangat muda ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal8", "Nilai"),
            P("faktor_risiko_awal9", "9. Apakah selisih kehamilan pertama anda dengan haid pertama lebih dari 15 tahun ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal9", "Nilai"),
            P("faktor_risiko_awal10", "10. Apakah anda mengalami menopause (henti haid) di usia lebih dari 50 tahun ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal10", "Nilai"),
            P("faktor_risiko_awal11", "11. Apakah anda termasuk golongan ras kulit putih (kaukasia) ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal11", "Nilai"),
            P("faktor_risiko_awal12", "12. Apakah anda terdiagnosa mengalami mutasi gen BRCA 1 & BRCA 2 ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal12", "Nilai"),
            P("faktor_risiko_awal13", "13. Apakah anda seorang perempuan (lebih tinggi resiko dibandingkan laki-laki) ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal13", "Nilai"),
            P("faktor_risiko_awal14", "14. Apakah anda termasuk golongan obesitas/kegemukan ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_awal14", "Nilai"),
            P("faktor_risiko_tinggi1", "1. Apakah anda tidak mempunyai anak ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "I.B. Faktor Risiko Tinggi (Ya = 5)"),
            H("nilai_risiko_tinggi1", "Nilai"),
            P("faktor_risiko_tinggi2", "2. Apakah anda menyusui anak kurang dari 6 bulan ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi2", "Nilai"),
            P("faktor_risiko_tinggi3", "3. Apakah anda memakai hormonal kontrasepsi atau terapi sulih hormon ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi3", "Nilai"),
            P("faktor_risiko_tinggi4", "4. Apakah anda dalam setahun terkena radiasi sinar-X (rontgen) lebih dari 1 kali ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi4", "Nilai"),
            P("faktor_risiko_tinggi5", "5. Apakah anda pernah menjalani tindakan pembedahan pada payudara ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi5", "Nilai"),
            P("faktor_risiko_tinggi6", "6. Apakah anda mendapat trauma payudara akibat aktifitas seksual berlebihan ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi6", "Nilai"),
            P("faktor_risiko_tinggi7", "7. Apakah anda bila sakit tidak kontrol atau tidak tuntas berobat ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi7", "Nilai"),
            P("faktor_risiko_tinggi8", "8. Apakah anda berusia di atas 25 tahun (semakin tua usia semakin tinggi resiko) ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi8", "Nilai"),
            P("faktor_risiko_tinggi9", "9. Apakah anda pernah memiliki tumor (benjolan) payudara ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi9", "Nilai"),
            P("faktor_risiko_tinggi10", "10. Apakah anda menderita kanker pada salah satu payudara (resiko bagi satu payudara yang lainnya) ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi10", "Nilai"),
            P("faktor_risiko_tinggi11", "11. Apakah anda memiliki riwayat sakit kanker endometrium ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi11", "Nilai"),
            P("faktor_risiko_tinggi12", "12. Apakah anda diperiksa secara radiologis dan ditemukan hasil densitas yang sangat tinggi ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi12", "Nilai"),
            P("faktor_risiko_tinggi13", "13. Apakah anda memiliki silsilah keluarga yang menderita kanker ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_risiko_tinggi13", "Nilai"),
            P("faktor_kecurigaan_ganas1", "1. Apakah ada benjolan/tumor di payudara anda ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "I.C. Kecurigaan Keganasan (Ya = 10)"),
            H("nilai_kecurigaan_ganas1", "Nilai"),
            P("faktor_kecurigaan_ganas2", "2. Apakah kulit/puting susu anda tertarik ke dalam ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_kecurigaan_ganas2", "Nilai"),
            P("faktor_kecurigaan_ganas3", "3. Apakah areola payudara anda berwarna merah muda atau kecoklat-coklatan sampai menjadi oedema hingga kulit kelihatan seperti kulit jeruk (peau dorange), mengkerut, atau timbul borok (ulkus) ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_kecurigaan_ganas3", "Nilai"),
            P("faktor_kecurigaan_ganas4", "4. Apakah puting susu anda mengeluarkan cairan tidak normal, darah atau nanah ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_kecurigaan_ganas4", "Nilai"),
            P("faktor_kecurigaan_ganas5", "5. Apakah ada benjolan di ketiak anda berdiameter lebih 2,5 cm, dapat melekat satu sama lain ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_kecurigaan_ganas5", "Nilai"),
            P("faktor_kecurigaan_ganas6", "6. Apakah ada benjolan di sekitar tulang belikat ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_kecurigaan_ganas6", "Nilai"),
            P("faktor_kecurigaan_ganas7", "7. Apakah lengan anda bengkak ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_kecurigaan_ganas7", "Nilai"),
            P("faktor_kecurigaan_ganas8", "8. Apakah ada erosi atau luka yang tidak sembuh-sembuh pada puting susu ?", ["Ya", "Tidak"], awal: "Tidak"),
            H("nilai_kecurigaan_ganas8", "Nilai"),
            H("total_skor", "Total Skor"),
            P("hasil_sadanis", "Hasil Pemeriksaan SADANIS", ["Benjolan", "Tidak Ada Benjolan", "Curiga Kanker"], awal: "Tidak Ada Benjolan", bagian: "II. Pemeriksaan SADANIS"),
            P("tindak_lanjut_sadanis", "Tindak Lanjut Hasil Pemeriksaan SADANIS", ["Dirujuk", "Tidak Dirujuk"], awal: "Tidak Dirujuk"),
            P("hasil_skrining", "Hasil Skrining", ["Normal", "Kemungkinan Kelainan Payudara Jinak", "Curiga Kelainan Payudara Ganas"], bagian: "III. Interpretasi"),
            T("keterangan", "Keterangan", 50),
        ],
        skor: [("faktor_risiko_awal10", "nilai_risiko_awal10", ["1", "0"]),
            ("faktor_risiko_awal11", "nilai_risiko_awal11", ["1", "0"]),
            ("faktor_risiko_awal12", "nilai_risiko_awal12", ["1", "0"]),
            ("faktor_risiko_awal13", "nilai_risiko_awal13", ["1", "0"]),
            ("faktor_risiko_awal14", "nilai_risiko_awal14", ["1", "0"]),
            ("faktor_risiko_awal1", "nilai_risiko_awal1", ["1", "0"]),
            ("faktor_risiko_awal2", "nilai_risiko_awal2", ["1", "0"]),
            ("faktor_risiko_awal3", "nilai_risiko_awal3", ["1", "0"]),
            ("faktor_risiko_awal4", "nilai_risiko_awal4", ["1", "0"]),
            ("faktor_risiko_awal5", "nilai_risiko_awal5", ["1", "0"]),
            ("faktor_risiko_awal6", "nilai_risiko_awal6", ["1", "0"]),
            ("faktor_risiko_awal7", "nilai_risiko_awal7", ["1", "0"]),
            ("faktor_risiko_awal8", "nilai_risiko_awal8", ["1", "0"]),
            ("faktor_risiko_awal9", "nilai_risiko_awal9", ["1", "0"]),
            ("faktor_risiko_tinggi10", "nilai_risiko_tinggi10", ["5", "0"]),
            ("faktor_risiko_tinggi11", "nilai_risiko_tinggi11", ["5", "0"]),
            ("faktor_risiko_tinggi12", "nilai_risiko_tinggi12", ["5", "0"]),
            ("faktor_risiko_tinggi13", "nilai_risiko_tinggi13", ["5", "0"]),
            ("faktor_risiko_tinggi1", "nilai_risiko_tinggi1", ["5", "0"]),
            ("faktor_risiko_tinggi2", "nilai_risiko_tinggi2", ["5", "0"]),
            ("faktor_risiko_tinggi3", "nilai_risiko_tinggi3", ["5", "0"]),
            ("faktor_risiko_tinggi4", "nilai_risiko_tinggi4", ["5", "0"]),
            ("faktor_risiko_tinggi5", "nilai_risiko_tinggi5", ["5", "0"]),
            ("faktor_risiko_tinggi6", "nilai_risiko_tinggi6", ["5", "0"]),
            ("faktor_risiko_tinggi7", "nilai_risiko_tinggi7", ["5", "0"]),
            ("faktor_risiko_tinggi8", "nilai_risiko_tinggi8", ["5", "0"]),
            ("faktor_risiko_tinggi9", "nilai_risiko_tinggi9", ["5", "0"]),
            ("faktor_kecurigaan_ganas1", "nilai_kecurigaan_ganas1", ["10", "0"]),
            ("faktor_kecurigaan_ganas2", "nilai_kecurigaan_ganas2", ["10", "0"]),
            ("faktor_kecurigaan_ganas3", "nilai_kecurigaan_ganas3", ["10", "0"]),
            ("faktor_kecurigaan_ganas4", "nilai_kecurigaan_ganas4", ["10", "0"]),
            ("faktor_kecurigaan_ganas5", "nilai_kecurigaan_ganas5", ["10", "0"]),
            ("faktor_kecurigaan_ganas6", "nilai_kecurigaan_ganas6", ["10", "0"]),
            ("faktor_kecurigaan_ganas7", "nilai_kecurigaan_ganas7", ["10", "0"]),
            ("faktor_kecurigaan_ganas8", "nilai_kecurigaan_ganas8", ["10", "0"])],
        lanjut: isi => Jumlah(isi, "total_skor", [.. Seri("nilai_risiko_awal", 1, 14), .. Seri("nilai_risiko_tinggi", 1, 13),
            .. Seri("nilai_kecurigaan_ganas", 1, 8)])) with
    {
        SatuPerKunjungan = true,
        Keterangan = isi => Tingkat(isi, "total_skor", t => (t >= 16
            ? "Resiko Tinggi. Segera periksa payudara, ikuti nasehat tenaga kesehatan/dokter...!"
            : "Waspada dan upayakan melakukan penanggulangan sehingga skor semakin kecil...!")
            + (Seri("nilai_kecurigaan_ganas", 1, 8).Any(k => Teks(isi, k) is not ("" or "0"))
                ? " Sangat curiga ada keganasan pada payudara. Segera ke dokter...!" : "")),
    };

    private static FormKlinis KankerServiks => Buat(
        "btnSkriningRisikoKankerServiks", "Skrining Risiko Kanker Serviks", "skrining_risiko_kanker_serviks", "skrining_risiko_kanker_serviks",
        [
            P("riwayat_penyakit_keluarga", "Riwayat Penyakit Keluarga", ["Kanker", "Benjolan Abnormal Pada Payudara", "-"], bagian: "I. Riwayat Penyakit"),
            P("riwayat_penyakit_sendiri", "Riwayat Penyakit Diri Sendiri", ["Kanker", "Benjolan Abnormal Pada Payudara", "-"]),
            P("risiko_merokok", "1. Merokok ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "II. Faktor Risiko"),
            P("risiko_kurang_fisik", "2. Kurang Aktifitas Fisik ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("risiko_gula_berlebihan", "3. Gula Berlebihan ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("risiko_garam_berlebihan", "4. Garam Berlebihan ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("risiko_lemak_berlebihan", "5. Lemak Berlebihan ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("risiko_kurang_buah_sayur", "6. Kurang Makan Buah Dan Sayur ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("risiko_alkohol", "7. Konsumsi Alkohol ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("hasil_iva", "Hasil Pemeriksaan IVA", ["Positif", "Negatif", "Curiga Kanker"], bagian: "III. Pemeriksaan IVA & Interpretasi"),
            T("hasil_skrining", "Hasil Skrining", 40, wajib: true),
            T("keterangan", "Keterangan", 100, wajib: true),
        ]) with
    {
        SatuPerKunjungan = true,
    };

    private static FormKlinis Srq => Buat(
        "btnSkriningInstrumenSRQ", "Skrining Instrumen SRQ", "skrining_instrumen_srq", "skrining_instrumen_srq",
        [
            P("pernyataansrq1", "1. Apakah Anda sering merasa sakit kepala ?", ["Tidak", "Ya"], bagian: "I. Self Reporting Questionnaire"),
            H("nilai_srq1", "Nilai"),
            P("pernyataansrq2", "2. Apakah Anda kehilangan nafsu makan ?", ["Tidak", "Ya"]),
            H("nilai_srq2", "Nilai"),
            P("pernyataansrq3", "3. Apakah tidur Anda tidak nyenyak ?", ["Tidak", "Ya"]),
            H("nilai_srq3", "Nilai"),
            P("pernyataansrq4", "4. Apakah Anda mudah merasa takut ?", ["Tidak", "Ya"]),
            H("nilai_srq4", "Nilai"),
            P("pernyataansrq5", "5. Apakah Anda merasa cemas, tegang, atau khawatir ?", ["Tidak", "Ya"]),
            H("nilai_srq5", "Nilai"),
            P("pernyataansrq6", "6. Apakah tangan Anda gemetar ?", ["Tidak", "Ya"]),
            H("nilai_srq6", "Nilai"),
            P("pernyataansrq7", "7. Apakah Anda mengalami gangguan pencernaan ?", ["Tidak", "Ya"]),
            H("nilai_srq7", "Nilai"),
            P("pernyataansrq8", "8. Apakah Anda merasa sulit berpikir jernih ?", ["Tidak", "Ya"]),
            H("nilai_srq8", "Nilai"),
            P("pernyataansrq9", "9. Apakah Anda merasa tidak bahagia ?", ["Tidak", "Ya"]),
            H("nilai_srq9", "Nilai"),
            P("pernyataansrq10", "10. Apakah Anda lebih sering menangis?", ["Tidak", "Ya"]),
            H("nilai_srq10", "Nilai"),
            P("pernyataansrq11", "11. Apakah Anda merasa sulit untuk menikmati aktivitas sehari-hari ?", ["Tidak", "Ya"]),
            H("nilai_srq11", "Nilai"),
            P("pernyataansrq12", "12. Apakah Anda mengalami kesulitan untuk mengambil keputusan ?", ["Tidak", "Ya"]),
            H("nilai_srq12", "Nilai"),
            P("pernyataansrq13", "13. Apakah aktivitas/tugas sehari-hari Anda terbengkalai ?", ["Tidak", "Ya"]),
            H("nilai_srq13", "Nilai"),
            P("pernyataansrq14", "14. Apakah Anda merasa tidak mampu berperan dalam kehidupan ini ?", ["Tidak", "Ya"]),
            H("nilai_srq14", "Nilai"),
            P("pernyataansrq15", "15. Apakah Anda kehilangan minat terhadap banyak hal ?", ["Tidak", "Ya"]),
            H("nilai_srq15", "Nilai"),
            P("pernyataansrq16", "16. Apakah Anda merasa tidak berharga ?", ["Tidak", "Ya"]),
            H("nilai_srq16", "Nilai"),
            P("pernyataansrq17", "17. Apakah Anda mempunyai pikiran untuk mengakhiri hidup Anda ?", ["Tidak", "Ya"]),
            H("nilai_srq17", "Nilai"),
            P("pernyataansrq18", "18. Apakah Anda merasa lelah sepanjang waktu ?", ["Tidak", "Ya"]),
            H("nilai_srq18", "Nilai"),
            P("pernyataansrq19", "19. Apakah Anda merasa tidak enak di perut ?", ["Tidak", "Ya"]),
            H("nilai_srq19", "Nilai"),
            P("pernyataansrq20", "20. Apakah Anda mudah lelah ?", ["Tidak", "Ya"]),
            H("nilai_srq20", "Nilai"),
            H("nilai_total_srq", "Total Nilai"),
            T("kesimpulan", "Kesimpulan", 100, bagian: "II. Interpretasi"),
        ],
        skor: [("pernyataansrq10", "nilai_srq10", ["0", "1"]),
            ("pernyataansrq11", "nilai_srq11", ["0", "1"]),
            ("pernyataansrq12", "nilai_srq12", ["0", "1"]),
            ("pernyataansrq13", "nilai_srq13", ["0", "1"]),
            ("pernyataansrq14", "nilai_srq14", ["0", "1"]),
            ("pernyataansrq15", "nilai_srq15", ["0", "1"]),
            ("pernyataansrq16", "nilai_srq16", ["0", "1"]),
            ("pernyataansrq17", "nilai_srq17", ["0", "1"]),
            ("pernyataansrq18", "nilai_srq18", ["0", "1"]),
            ("pernyataansrq19", "nilai_srq19", ["0", "1"]),
            ("pernyataansrq1", "nilai_srq1", ["0", "1"]),
            ("pernyataansrq20", "nilai_srq20", ["0", "1"]),
            ("pernyataansrq2", "nilai_srq2", ["0", "1"]),
            ("pernyataansrq3", "nilai_srq3", ["0", "1"]),
            ("pernyataansrq4", "nilai_srq4", ["0", "1"]),
            ("pernyataansrq5", "nilai_srq5", ["0", "1"]),
            ("pernyataansrq6", "nilai_srq6", ["0", "1"]),
            ("pernyataansrq7", "nilai_srq7", ["0", "1"]),
            ("pernyataansrq8", "nilai_srq8", ["0", "1"]),
            ("pernyataansrq9", "nilai_srq9", ["0", "1"])],
        lanjut: isi => Jumlah(isi, "nilai_total_srq", Seri("nilai_srq", 1, 20))) with
    {
        SatuPerKunjungan = true,
        Usul = isi => int.TryParse(Teks(isi, "nilai_total_srq"), out var n)
            ? Satu("kesimpulan", n > 5 ? "Gangguan Mental Emosional" : "Kondisi Normal") : [],
    };

    private static FormKlinis Tbc => Buat(
        "btnSkriningTBC", "Skrining TBC", "skrining_tbc", "skrining_tbc",
        [
            T("berat_badan", "BB (Kg)", 6, wajib: true, bagian: "I. Pemeriksaan Fisik (WHO Asia Pasifik)"),
            T("tinggi_badan", "TB (Cm)", 8, wajib: true),
            H("imt", "IMT (BB/TB²)"),
            H("kasifikasi_imt", "Klasifikasi IMT"),
            T("lingkar_pinggang", "Lingkar Pinggang (Cm)", 6, wajib: true),
            H("risiko_lingkar_pinggang", "Risiko Berdasarkan Lingkar Pinggang"),
            P("riwayat_kontak_tbc", "Riwayat Kontak TBC", ["Ya", "Tidak"], awal: "Tidak", bagian: "II. Riwayat Kontak TBC"),
            P("jenis_kontak_tbc", "Jenis Kontak TBC", ["Tidak", "TBC Paru Bakteriologis", "TBC Paru Klinis", "TBC Paru Ekstraparu"]),
            P("faktor_resiko_pernah_terdiagnosa_tbc", "1. Pernah Terdiagnosa / Berobat TBC ?", ["Ya", "Tidak"], awal: "Tidak", bagian: "III. Faktor Risiko"),
            T("keterangan_pernah_terdiagnosa", "2. Jika Pertanyaan 1 Ya, Kapan ?", 40),
            P("faktor_resiko_pernah_berobat_tbc", "3. Pernah Berobat TBC Tapi Pernah Tidak Tuntas", ["Ya", "Tidak"], awal: "Tidak"),
            P("faktor_resiko_malnutrisi", "4. Malnutrisi", ["Ya", "Tidak"], awal: "Tidak"),
            P("faktor_resiko_merokok", "5. Merokok / Perokok Pasif ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("faktor_resiko_riwayat_dm", "6. Riwayat DM / Kencing Manis ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("faktor_resiko_odhiv", "7. ODHIV", ["Ya", "Tidak"], awal: "Tidak"),
            P("faktor_resiko_lansia", "8. Lansia > 65 Tahun", ["Ya", "Tidak"], awal: "Tidak"),
            P("faktor_resiko_ibu_hamil", "9. Ibu Hamil ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("faktor_resiko_wbp", "10. Warga Binaan Permasyarakatan (WBP) ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("faktor_resiko_tinggal_diwilayah_padat_kumuh", "11. Tinggal diwilayah padat kumuh miskin ?", ["Ya", "Tidak"], awal: "Tidak"),
            P("abnormalitas_tbc", "Abnormalitas TBC", ["Normal", "Abnormalitas TBC", "Abnormalitas Bukan TBC"], bagian: "IV. Abnormalitas TBC"),
            P("gejala_tbc_batuk", "1. Batuk ( Semua bentuk batuk tanpa melihat durasi )", ["Ya", "Tidak"], awal: "Tidak", bagian: "V. Gejala TBC"),
            P("gejala_tbc_bb_turun", "2. BB turun tanpa penyebab jelas / BB tidak naik / Nafsu makan turun", ["Ya", "Tidak"], awal: "Tidak"),
            P("gejala_tbc_demam", "3. Demam yang tidak diketahui penyebabnya", ["Ya", "Tidak"], awal: "Tidak"),
            P("gejala_tbc_berkeringat_malam_hari", "4. Berkeringat malam hari tanpa kegiatan", ["Ya", "Tidak"], awal: "Tidak"),
            T("keterangan_gejala_penyakit_lain", "Gejala Penyakit Pernafasan Lainnya, Sebutkan ( Seperti pilek/flu, sakit tengorokan dll )", 30),
            P("kesimpulan_skrining", "Kesimpulan Hasil Skrining", ["Bukan Terduga TBC", "Kontak Erat", "Terduga TBC"], bagian: "VI. Hasil Skrining"),
            T("keterangan_hasil_skrining", "Keterangan", 40),
        ],
        lanjut: isi =>
        {
            Imt(isi);
            Pinggang(isi);
        }) with
    {
        SatuPerKunjungan = true,
        Usul = isi =>
        {
            string[] gejala = ["gejala_tbc_batuk", "gejala_tbc_bb_turun", "gejala_tbc_demam", "gejala_tbc_berkeringat_malam_hari"];
            return Satu("kesimpulan_skrining", Teks(isi, "riwayat_kontak_tbc") == "Ya"
                ? gejala.Any(k => Teks(isi, k) == "Tidak") ? "Kontak Erat" : "Terduga TBC"
                : gejala.Any(k => Teks(isi, k) == "Ya") ? "Terduga TBC" : "Bukan Terduga TBC");
        },
        Periksa = PeriksaTubuh,
    };

    private static FormKlinis Tolac => Buat(
        "btnSkriningTOLAC", "Skrining TOLAC (Poli Kebidanan)", "skrining_tolac", "skrining_tolac",
        [
            T("gpa", "G P A", 15, wajib: true),
            T("diagnosa", "Diagnosa", 100, wajib: true),
            T("jumlah_sc", "Jumlah SC", 1, bagian: "Riwayat SC Sebelumnya"),
            T("tahun_sc", "SC Terakhir", 4),
            T("indikasi_sc", "Indikasi SC", 50),
            P("jenis_insisi", "Jenis Insisi", ["-", "Transversal Rendah", "Klasik", "Tidak Diketahui"]),
            P("riwayat_pervaginam", "Riwayat Pervaginam", ["Sebelum & Sesudah SC", "Sesudah SC Saja", "Sebelum SC Saja", "Tidak Pernah"], awal: "Tidak Pernah"),
            T("tbj_gram", "TBJ USG (gram)", 5, wajib: true, bagian: "Data Kehamilan Sekarang"),
            T("presentasi_janin", "Presentasi Janin", 30, wajib: true),
            P("inklusi_riwayat_sc", "Riwayat SC ≤1x, Insisi Transversal Rendah", ["Ya", "Tidak"], bagian: "Kriteria Inklusi"),
            P("inklusi_panggul_adekuat", "Panggul Klinis Adekuat", ["Ya", "Tidak"]),
            P("inklusi_janin_tunggal_kepala", "Janin Tunggal, Presentasi Kepala", ["Ya", "Tidak"]),
            P("inklusi_tbj_sesuai", "TBJ Sesuai (Bukan Makrosomia Berat)", ["Ya", "Tidak"]),
            P("eksklusi_sc_klasik_ruptur", "Riwayat SC Klasik / Ruptur Uteri", ["Ya", "Tidak"], awal: "Tidak", bagian: "Kriteria Eksklusi"),
            P("eksklusi_sc_2x", "Riwayat SC ≥ 2 Kali", ["Ya", "Tidak"], awal: "Tidak"),
            P("eksklusi_plasenta_previa", "Plasenta Previa / Letak Lintang", ["Ya", "Tidak"], awal: "Tidak"),
            P("kesimpulan", "Kesimpulan", ["Kandidat TOLAC", "Rujuk SC Elektif"], bagian: "Hasil Skrining"),
            P("edukasi_diberikan", "Edukasi Risiko Diberikan", ["Ya", "Tidak"]),
            T("keterangan", "Keterangan", 100),
        ], pemilik: PemilikForm.Dokter) with
    {
        SatuPerKunjungan = true,
        Usul = isi => Satu("kesimpulan",
            new[] { "inklusi_riwayat_sc", "inklusi_panggul_adekuat", "inklusi_janin_tunggal_kepala", "inklusi_tbj_sesuai" }.All(k => Teks(isi, k) == "Ya")
            && !new[] { "eksklusi_sc_klasik_ruptur", "eksklusi_sc_2x", "eksklusi_plasenta_previa" }.Any(k => Teks(isi, k) == "Ya")
                ? "Kandidat TOLAC" : "Rujuk SC Elektif"),
    };

    private static FormKlinis Thalassemia => Buat(
        "btnSkriningThalassemia", "Skrining Thalassemia", "skrining_thalassemia", "skrining_thalassemia",
        [
            P("transfusi_darah", "1. Apakah rutin menerima transfusi darah ?", ["Tidak", "Ya"], bagian: "I. Anamnesis (bila ada jawaban Ya, lakukan pemeriksaan laboratorium)"),
            P("rutin_transfusi", "3. Apakah ada keluarga yang rutin melakukan transfusi darah ?", ["Tidak", "Ya"]),
            P("saudara_thalassemia", "2. Apakah memiliki saudara thalasemia ?", ["Tidak", "Ya"]),
            P("tumbuh_kembang_terlambat", "4. Riwayat tumbuh kembang dan pubertas terlambat ?", ["Tidak", "Ya"]),
            P("anemia", "Anemia/Pucat", ["Tidak", "Ya"], bagian: "II. Pemeriksaan Fisik"),
            P("ikterus", "Ikterus", ["Tidak", "Ya"]),
            P("perut_buncit", "Perut Buncit", ["Tidak", "Ya"]),
            P("gizi_kurang", "Gizi Kurang/Buruk", ["Tidak", "Ya"]),
            P("facies_cooley", "Facies Cooley", ["Tidak", "Ya"]),
            P("perawakan_pendek", "Perawakan Pendek", ["Tidak", "Ya"]),
            P("hiperpigmentasi_kulit", "Hiperpigmentasi Kulit", ["Tidak", "Ya"]),
            P("hemoglobin", "Hemoglobin", ["Normal", "< 11 mg/dl"], bagian: "III. Pemeriksaan Laboratorium"),
            P("mvc", "MVC", ["Normal", "< 80 fl"]),
            P("mchc", "MCHC", ["Normal", "< 27 pq"]),
            P("darah_tepi", "Darah Tepi", ["Normal", "< 100.000 mm3"]),
            T("tindak_lanjut", "Tindak Lanjut", 300, bagian: "IV. Tindak Lanjut"),
        ]) with
    {
        SatuPerKunjungan = true,
        Usul = isi => Satu("tindak_lanjut", new[] { "hemoglobin", "mvc", "mchc" }.Any(k => Teks(isi, k) is not ("" or "Normal"))
            ? "Pasien dicurigai sebagai pembawa sifat Thalassemia Rujuk Ke FKTRL atau dapat juga melakukan rujukan sampel"
            : "Edukasi Kesehatan"),
    };

    public static IReadOnlyList<FormKlinis> Semua =>
    [
        GiziKehamilan, LanjutanRisikoJatuhAnak, LanjutanRisikoJatuhGeriatri, LanjutanRisikoJatuhLansia, LanjutanRisikoJatuhPsikiatri, LanjutanFungsional,
        Dekubitus, RisikoJatuhNeonatus, AdiksiNikotin, Anemia, CURB65, DiabetesMelitus,
        Frailty, Hipertensi, IndraPendengaran, Acrs, Amt, Esat,
        Sdq, KankerKolorektal, Kekerasan, GigiBalita, GigiDewasa, GigiLansia,
        GigiRemaja, KesehatanPenglihatan, Mpp, NutrisiAnak, NutrisiDewasa, NutrisiLansia,
        Obesitas, Puma, Psi, KankerParu, KankerPayudara, KankerServiks,
        Srq, Tbc, Tolac, Thalassemia,
    ];
}
