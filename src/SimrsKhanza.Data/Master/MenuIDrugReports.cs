namespace SimrsKhanza.Data.Master;

public static class MenuIDrugReports
{
    private static ReportSpec Drugs(string title, string mode)
    {
        var prescriber = mode == "peresep";
        var registrationDate = mode is "poli" or "ranap";
        var date = registrationDate ? "r.tgl_registrasi" : "x.tgl_perawatan";
        var doctor = prescriber ? "rx.kd_dokter" : "r.kd_dokter";
        var prescriptionJoin = prescriber
            ? "JOIN resep_obat rx ON rx.no_rawat=x.no_rawat AND rx.tgl_perawatan=x.tgl_perawatan AND rx.jam=x.jam"
            : "";
        var status = mode is "poli" or "ralan" ? "AND x.status='Ralan'" : mode == "ranap" ? "AND x.status='Ranap'" : "";
        var scope = registrationDate
            ? "Periode menggunakan tanggal registrasi; seluruh pemberian pada kunjungan yang cocok disertakan."
            : "Periode menggunakan tanggal pemberian obat.";
        if (prescriber) scope += " Resep dipasangkan berdasarkan nomor rawat, tanggal dan jam seperti Khanza; resep dengan waktu sama dapat mencakup pemberian yang sama.";
        return new ReportSpec(title,
            $"""
            SELECT {date} AS tanggal, r.no_rawat, p.nm_pasien, j.png_jawab, poli.nm_poli,
                   d.nm_dokter, b.kode_brng, b.nama_brng, x.status,
                   SUM(x.jml) AS jumlah, SUM(x.total-x.embalase-x.tuslah) AS biaya,
                   SUM(x.embalase) AS embalase, SUM(x.tuslah) AS tuslah, SUM(x.total) AS total
            FROM detail_pemberian_obat x
            JOIN reg_periksa r ON r.no_rawat=x.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN penjab j ON j.kd_pj=r.kd_pj
            JOIN poliklinik poli ON poli.kd_poli=r.kd_poli
            {prescriptionJoin}
            JOIN dokter d ON d.kd_dokter={doctor}
            JOIN databarang b ON b.kode_brng=x.kode_brng
            WHERE {date} BETWEEN @p1 AND @p2 {status}
              AND (p.nm_pasien LIKE @p0 OR r.no_rawat LIKE @p0 OR b.nama_brng LIKE @p0
                   OR d.nm_dokter LIKE @p0 OR j.png_jawab LIKE @p0 OR poli.nm_poli LIKE @p0)
            GROUP BY {date}, r.no_rawat, p.nm_pasien, j.png_jawab, poli.nm_poli,
                     d.nm_dokter, b.kode_brng, b.nama_brng, x.status
            ORDER BY tanggal, r.no_rawat, b.nama_brng
            """,
            [("tanggal", registrationDate ? "Tanggal Registrasi" : "Tanggal Pemberian"), ("no_rawat", "No. Rawat"),
             ("nm_pasien", "Pasien"), ("png_jawab", "Cara Bayar"), ("nm_poli", "Poli"),
             ("nm_dokter", prescriber ? "Dokter Peresep" : "Dokter Registrasi"), ("kode_brng", "Kode"),
             ("nama_brng", "Barang"), ("status", "Layanan"), ("jumlah", "Jumlah"), ("biaya", "Biaya Obat"),
             ("embalase", "Embalase"), ("tuslah", "Tuslah"), ("total", "Total")], ReportPeriod.DateRange,
            scope + " Pengelompokan ditampilkan per kunjungan dan barang; filter khusus legacy belum tersedia.")
            { SearchWatermark = "Pasien, No. rawat, obat, dokter, poli, atau cara bayar", TotalColumns = ["total"] };
    }

    private static readonly ReportSpec DrugWard = new(
        "Obat Per Kamar", """
        SELECT w.kd_bangsal,w.nm_bangsal,d.kd_dokter,d.nm_dokter,b.kode_brng,b.nama_brng,
               SUM(x.jml) AS jumlah,SUM(x.total-x.embalase-x.tuslah) AS biaya,
               SUM(x.embalase) AS embalase,SUM(x.tuslah) AS tuslah,SUM(x.total) AS total
        FROM detail_pemberian_obat x JOIN reg_periksa r ON r.no_rawat=x.no_rawat
        JOIN kamar_inap ki ON ki.no_rawat=r.no_rawat AND ki.stts_pulang<>'Pindah Kamar'
        JOIN kamar km ON km.kd_kamar=ki.kd_kamar JOIN bangsal w ON w.kd_bangsal=km.kd_bangsal
        JOIN dokter d ON d.kd_dokter=r.kd_dokter JOIN databarang b ON b.kode_brng=x.kode_brng
        WHERE x.tgl_perawatan BETWEEN @p1 AND @p2
          AND EXISTS (SELECT 1 FROM reg_periksa r2 JOIN kamar_inap k2 ON k2.no_rawat=r2.no_rawat
                      JOIN kamar km2 ON km2.kd_kamar=k2.kd_kamar
                      WHERE r2.kd_dokter=r.kd_dokter AND km2.kd_bangsal=w.kd_bangsal
                        AND k2.stts_pulang<>'Pindah Kamar' AND k2.tgl_masuk BETWEEN @p1 AND @p2)
          AND (w.kd_bangsal LIKE @p0 OR w.nm_bangsal LIKE @p0 OR d.nm_dokter LIKE @p0 OR b.nama_brng LIKE @p0)
        GROUP BY w.kd_bangsal,w.nm_bangsal,d.kd_dokter,d.nm_dokter,b.kode_brng,b.nama_brng
        ORDER BY w.nm_bangsal,d.nm_dokter,b.nama_brng
        """, [("kd_bangsal", "Kode Bangsal"), ("nm_bangsal", "Bangsal"), ("nm_dokter", "Dokter Registrasi"),
         ("kode_brng", "Kode Barang"), ("nama_brng", "Barang"), ("jumlah", "Jumlah"),
         ("biaya", "Biaya Obat"), ("embalase", "Embalase"), ("tuslah", "Tuslah"), ("total", "Total")],
        ReportPeriod.DateRange, "Mengikuti pengelompokan bangsal dan dokter registrasi Khanza. Periode mencakup tanggal pemberian dan pemilihan dokter berdasarkan tanggal masuk kamar; data dapat terhitung lebih dari sekali bila satu kunjungan memiliki beberapa riwayat kamar non-pindah dalam bangsal yang sama.")
        { SearchWatermark = "Bangsal, dokter atau barang" };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnTagihanObatPoli"] = Drugs("Obat Per Poli", "poli"),
        ["btnObatPasienRalan"] = Drugs("Obat Per Dokter Ralan", "ralan"),
        ["btnObatPasienRanap"] = Drugs("Obat Per Dokter Ranap", "ranap"),
        ["btnObatPasienPeresep"] = Drugs("Obat Per Dokter Peresep", "peresep"),
        ["btnObatPerCaraBayar"] = Drugs("Obat Per Cara Bayar", "bayar"),
        ["btnTagihanObatBangsal"] = DrugWard,
    };
}
