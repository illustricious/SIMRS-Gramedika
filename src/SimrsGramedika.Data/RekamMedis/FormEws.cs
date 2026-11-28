using System.Globalization;

namespace SimrsGramedika.Data.RekamMedis;

/// <summary>
/// Lembar peringatan dini (RMPemantauanEWSD, RMPemantauanPEWS, RMPemantauanEWSNeonatus, RMPemantauanMEOWS).
/// Pilihan ditulis menurut urutan enum kolom; nilai, pilihan awal dan kalimat tindak lanjut disalin dari layar Java,
/// termasuk salah ketiknya, supaya isi database sama dengan Khanza.
/// </summary>
internal static class FormEws
{
    private sealed record Parameter(string Kolom, string KolomSkor, string Label, string Awal, (string Pilihan, int Skor)[] Skala);

    private static Parameter P(string kolom, string skor, string label, string awal, params (string, int)[] skala) =>
        new(kolom, skor, label, awal, skala);

    /// <param name="tindakLanjut">Kalimat tindak lanjut dari total skor dan isian.</param>
    /// <param name="diluarTotal">Kolom skor yang disimpan tetapi tidak dijumlahkan, mengikuti layar Java.</param>
    private static FormKlinis Buat(string tile, string judul, string tabel, Parameter[] daftar,
        Func<int, IDictionary<string, string>, string> tindakLanjut, bool codeBlue = false, string? diluarTotal = null)
    {
        var isian = daftar.SelectMany((p, i) => new IsianKlinis[]
        {
            new(p.Kolom, p.Label, Pilihan: [.. p.Skala.Select(x => x.Pilihan)], Awal: p.Awal),
            new(p.KolomSkor, $"Skor {i + 1}", Otomatis: true),
        }).ToList();
        isian.Add(new("skor_total", "Total Skor", Otomatis: true));
        isian.Add(new("parameter_total", "Tindak Lanjut", 250, Otomatis: true));
        if (codeBlue) isian.Add(new("code_blue", "Aktifkan Code Blue", Pilihan: ["Ya", "Tidak"], Awal: "Tidak"));

        return new(tile, judul, tabel, tabel, PemilikForm.Petugas, [.. isian])
        {
            Kelompok = FormKlinisKatalog.PeringatanDini,
            DaftarLengkap = true,
            // Total berada di bawah isian yang digulung, jadi diulang pada baris penjelasan yang selalu terlihat.
            Keterangan = isi =>
            {
                var teks = $"Total skor {isi.GetValueOrDefault("skor_total")} · {isi.GetValueOrDefault("parameter_total")}";
                return diluarTotal is not null && isi.GetValueOrDefault(diluarTotal) is { Length: > 0 } skor && skor != "0"
                    ? $"{teks}{Environment.NewLine}Perhatian: {daftar.Single(p => p.KolomSkor == diluarTotal).Label[3..]} bernilai {skor} dan tidak ikut total, sama seperti Khanza."
                    : teks;
            },
            Hitung = isi =>
            {
                var total = 0;
                foreach (var p in daftar)
                {
                    var pilihan = isi.TryGetValue(p.Kolom, out var v) ? v : "";
                    var skor = p.Skala.FirstOrDefault(x => x.Pilihan == pilihan).Skor;
                    isi[p.KolomSkor] = skor.ToString(CultureInfo.InvariantCulture);
                    if (p.KolomSkor != diluarTotal) total += skor;
                }
                isi["skor_total"] = total.ToString(CultureInfo.InvariantCulture);
                isi["parameter_total"] = tindakLanjut(total, isi);
            },
        };
    }

    private const string RisikoRendah = "Beresiko rendah, ulangi setiap 7 jam";

    private static bool Salah(IDictionary<string, string> isi, string kolom, params string[] pilihan) =>
        isi.TryGetValue(kolom, out var v) && pilihan.Contains(v);

    private static readonly FormKlinis Dewasa = Buat(
        "btnPemantauanPEWSDewasa", "Pemantauan EWS Pasien Dewasa", "pemantauan_pews_dewasa",
        [
            P("parameter_laju_respirasi", "skor_laju_respirasi", "1. Laju Respirasi/Menit", "12 - 20",
                ("<= 5", 7), ("6 - 8", 3), ("9 - 11", 1), ("12 - 20", 0), ("21 - 24", 2), ("25 - 34", 3), (">= 35", 7)),
            P("parameter_saturasi_oksigen", "skor_saturasi_oksigen", "2. Saturasi Oksigen", ">= 95",
                (">= 95", 0), ("94 - 95", 1), ("92 - 93", 2), ("<= 92", 3)),
            P("parameter_suplemen_oksigen", "skor_suplemen_oksigen", "3. Suplemen Oksigen", "Tidak",
                ("Ya", 2), ("Tidak", 0)),
            P("parameter_tekanan_darah_sistolik", "skor_tekanan_darah_sistolik", "4. Tekanan Darah Sistolik", "111 - 180",
                (">= 220", 3), ("181 - 220", 2), ("111 - 180", 0), ("101 - 110", 1), ("91 - 100", 2), ("71 - 90", 3), ("<= 70", 7)),
            P("parameter_laju_jantung", "skor_laju_jantung", "5. Laju Jantung/Menit", "51 - 90",
                (">= 140", 7), ("131 - 140", 3), ("111 - 130", 2), ("91 - 110", 1), ("51 - 90", 0), ("41 - 50", 2), ("<= 40", 7)),
            P("parameter_kesadaran", "skor_kesadaran", "6. Kesadaran", "Sadar",
                ("Sadar", 0), ("Nyeri/Verbal", 3), ("Unrespon", 7)),
            P("parameter_temperatur", "skor_temperatur", "7. Temperatur", "36.1 - 38",
                ("<= 35", 3), ("35.1 - 36", 1), ("36.1 - 38", 0), ("38.1 - 39", 1), (">= 39", 2)),
        ],
        (total, isi) =>
        {
            // Satu tanda bahaya sudah cukup untuk tingkat resusitasi.
            var bahaya = Salah(isi, "parameter_laju_respirasi", "<= 5", ">= 35")
                || Salah(isi, "parameter_tekanan_darah_sistolik", "<= 70")
                || Salah(isi, "parameter_laju_jantung", ">= 140", "<= 40")
                || Salah(isi, "parameter_kesadaran", "Unrespon");
            return total > 8 ? "Lakukan RJP oleh petugas/tim primer, aktivasi code blue henti jantung (...), respon Tim Medis Emergency (TME) segera, maksimal 5 menit, informasikan dan konsultasikan ke DPJP"
                : total >= 7 || bahaya ? "Resusitasi dan monitoring secara kontinyu ole dokter jaga dan perawat senior, aktivasi code blue kegawatan medis (....) respon Tim Medis Emergency (TME) segera, maksimal 10 menit, informasikr dan konsultasikan ke DPJP"
                : total >= 5 ? "Asesment segera oleh dokter jaga (respon segera, maks 5 menit) konsultasi DPJP dan spesialis terkait, eskalasi perawatan dan monitoring tap jam, pertimbangkan perawatan dengan monitoring yang sesua (HCU)"
                : total > 0 ? "Assement segera oleh perawat senior, respon segera, maks 5 menit, eskalasi perawat dan frekuensi monitoring per 4-6 jam, jika diperlukan assesment oleh dokter jaga bangsal"
                : RisikoRendah;
        });

    private static readonly FormKlinis Anak = Buat(
        "btnPemantauanPEWSAnak", "Pemantauan PEWS Pasien Anak", "pemantauan_pews_anak",
        [
            P("parameter_perilaku", "skor_perilaku", "1. Perilaku", "Sadar / Bermain",
                ("Sadar / Bermain", 0), ("Tidur / Perubahan Perilaku", 1), ("Gelisah", 2),
                ("Tidak Merespon Terhadap Nyeri Penurunan Kesadaran", 3)),
            P("parameter_crt_atau_warna_kulit", "skor_crt_atau_warna_kulit", "2. CRT / Warna Kulit", "1 - 2 dtk / Pink",
                ("1 - 2 dtk / Pink", 0), ("3 dtk / Pucat", 1), ("4 dtk / Sianosis", 2), (">=5 dtk / Mottle", 3)),
            P("parameter_perespirasi", "skor_perespirasi", "3. Perespirasi", "Tidak Ada Retraksi",
                ("Tidak Ada Retraksi", 0), ("Cuping Hidung / O2 1-3 Lpm", 1), ("Retraksi Dada / O2 4-6 Lpm", 2),
                ("Stridor / O2 7-8 Lpm", 3)),
        ],
        (total, _) => total > 4 ? "Laporkan perubahan klinis ke perawat ketua tim, dokter jaga dan DPJP. Kolaborasikan langkah selanjutnya dengan seluruh tim perawatan"
            : total > 3 ? "Monitor per 1 jam. Laporkan ke dokter jaga dan kemudian tindak lanjut lapor ke DPJP untuk advis selanjutnya. Kolaborasi langkah selanjutnya dengan seluruh tim perawatan. Jika masih di perlukan lapor ulang keperawat ketua tim dan DPJP"
            : total > 2 ? "Monitoring 1 sampai 2 jam. Pengkajian ulang dilakukan oleh PJ sift dan laporkan ke dokter jaga"
            : total > 0 ? "Monitoring setiap 4 jam oleh perawat pelaksana dan di lanjutkan observasi atau monitoring secara rutin"
            : RisikoRendah);

    /// <remarks>
    /// Layar Java mewajibkan dan mencetak skor warna kulit (Pucat = 2), tetapi tidak menjumlahkannya. Pemilik meminta
    /// dijumlahkan hanya bila sesuai aturan WHO atau Kemenkes; aturan EWS neonatus dari keduanya tidak ditemukan, jadi
    /// total mengikuti Khanza dan layar mengingatkan bila warna kulit pucat.
    /// </remarks>
    private static readonly FormKlinis Neonatus = Buat(
        "btnPemantauanEWSNeonatus", "Pemantauan EWS Pasien Neonatus", "pemantauan_ews_neonatus",
        [
            P("parameter1", "skor1", "1. Pernapasan", "40 - 60", ("<= 29", 2), ("30 - 39", 1), ("40 - 60", 0), (">= 61", 2)),
            P("parameter2", "skor2", "2. Saturasi Oksigen", ">= 94", ("<= 90", 2), ("90 - 93", 1), (">= 94", 0)),
            P("parameter3", "skor3", "3. Penggunaan O2", "Tidak", ("Tidak", 0), ("Ya", 2)),
            P("parameter4", "skor4", "4. Denyut Jantung", "120 - 160",
                ("<= 80", 2), ("81 - 119", 1), ("120 - 160", 0), ("161 - 180", 1), (">= 181", 2)),
            P("parameter5", "skor5", "5. Retraksi Dada", "Tidak", ("Berat", 2), ("Ringan", 1), ("Tidak", 0)),
            P("parameter6", "skor6", "6. CRT", "<= 3 Detik", (">= 3 Detik", 1), ("<= 3 Detik", 0)),
            P("parameter7", "skor7", "7. Suhu", "36,5 - 37,5", ("<= 36,5", 1), ("36,5 - 37,5", 0), (">= 37,5", 1)),
            P("parameter8", "skor8", "8. Warna Kulit", "Pink", ("Pink", 0), ("Pucat", 2)),
        ],
        (total, _) => total switch
        {
            > 6 => "Petugas : Panggilan Darurat, Monitoring : Berkelanjutan, Tindakan : Menghubungi tim emergency jaga/aktifkan Code Blue",
            6 => "Petugas : Perawat Jaga & Dokter Jaga, Monitoring : Berkelanjutan, Tindakan : Melapor ke DPJP",
            4 or 5 => "Petugas : Perawat Jaga & Dokter Jaga, Monitoring : Per 30 menit, Tindakan : Perawat jaga melakukan monitoring ulang dan melapor ke dokter jaga",
            3 => "Petugas : Perawat Jaga & Dokter Jaga, Monitoring : Per 1 jam, Tindakan : Perawat jaga melakukan monitoring ulang dan melapor ke dokter jaga",
            2 => "Petugas : Perawat Jaga, Monitoring : Per 2 jam, Tindakan : Semua perubahan kondisi pasien harus meningkatkan frekuensi monitor untuk tindakan klinis yang tepat",
            1 => "Petugas : Perawat Jaga, Monitoring : Per 4 jam, Tindakan : Semua perubahan kondisi pasien harus meningkatkan frekuensi monitor untuk tindakan klinis yang tepat",
            _ => RisikoRendah,
        },
        codeBlue: true, diluarTotal: "skor8");

    private static readonly FormKlinis Obstetri = Buat(
        "btnPemantauanMEOWS", "Pemantauan MEOWS Pasien Obstetri", "pemantauan_meows_obstetri",
        [
            P("parameter_pernapasan", "skor_pernapasan", "1. Pernapasan", "11 - 20",
                (">= 30", 2), ("21 - 30", 1), ("11 - 20", 0), ("< 12", 2)),
            P("parameter_saturasi", "skor_saturasi", "2. Saturasi Oksigen", "> 95", ("> 95", 0), ("90 - 94", 1), ("< 90", 2)),
            P("parameter_temperatur", "skor_temperatur", "3. Temperatur", "36 - 37.9",
                ("> 38", 2), ("35 - 35.9", 1), ("36 - 37.9", 0), ("< 35", 2)),
            P("parameter_tekanan_darah_sistole", "skor_tekanan_darah_sistole", "4. Tekanan Darah Sistolik", "100 - 140",
                ("> 160", 2), ("150 - 159", 1), ("100 - 140", 0), ("90 - 99", 1), ("< 90", 2)),
            P("parameter_tekanan_darah_diastole", "skor_tekanan_darah_diastole", "5. Tekanan Darah Diastole", "< 90",
                ("> 110", 2), ("90 - 109", 1), ("< 90", 0)),
            P("parameter_denyut_jantung", "skor_denyut_jantung", "6. Denyut Jantung", "51 - 99",
                ("> 120", 2), ("100 - 120", 1), ("51 - 99", 0), ("40 - 50", 1), ("< 40", 2)),
            P("parameter_kesadaran", "skor_kesadaran", "7. Kesadaran", "Alert",
                ("Alert", 0), ("Verbal", 1), ("Pain", 2), ("Unresponsive", 2)),
            P("parameter_ketuban", "skor_ketuban", "8. Bau Cairan Ketuban", "Khas", ("Khas", 0), ("Busuk", 2)),
            P("parameter_discharge", "skor_discharge", "9. Discharge / Locia", "Normal", ("Normal", 0), ("Banyak", 2)),
            P("parameter_proteinuria", "skor_proteinuria", "10. Proteinuria", "Negatif", ("Negatif", 0), ("+", 1), ("++>", 2)),
        ],
        (total, _) => total >= 7 ? "Monitoring secara kontinyu oleh dokter jaga & Bidan/Katim/PJ Shift, Informasikan & konsultasikan ke DPJP untuk menentukan rencana perawatan pasien selanjutnya/rawat intensive. Aktifasi Code Blue jika pasien Henti Nafas & Henti jantung."
            : total >= 5 ? "Assessment segera oleh Bidan/Katim/PJ Shift dan dokter jaga, evaluasi perawatan dan monitoring setiap 1-2 jam, pertimbangkan perawatan dengan monitoring yang sesuai."
            : total >= 2 ? "Assessment segera oleh Bidan respon segera, maksimal 5 menit, evaluasi perawatan dan frekuensi monitoring setiap 4 - 6 jam."
            : "Berisiko rendah, observasi setiap 7 jam atau Setiap Shift",
        codeBlue: true);

    public static IReadOnlyList<FormKlinis> Semua => [Dewasa, Anak, Neonatus, Obstetri];
}
