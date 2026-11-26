using SimrsGramedika.Config;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Barcode;

public record BarcodeContext(
    bool Ranap, string NoRawat, string NoRm, string NamaPasien, string KdPj, string KdDokter, string NamaDokter,
    string KdPoliAtauBangsal, string Unit, string Kelas, string LokasiStok, double Kenaikan, bool TarifPerUnit, bool TarifPerCaraBayar);

public record TindakanItem(string Kode, string Nama, string Kategori, double Tarif, double Material, double Bhp,
    double JmDokter, double JmPerawat, double Kso, double Menejemen);

public record ObatItem(string Kode, string Nama, string Satuan, string Letak, string Jenis, double Harga, double Stok,
    double Hpp, string NoBatch, string NoFaktur, double Kapasitas);

public record ObatInput(ObatItem Obat, double Jumlah, bool PakaiKapasitas, double Embalase, double Tuslah);

/// <summary>
/// Port of legacy viabarcode/DlgBarcodeRalan + DlgBarcodeRanap: scan/search tindakan and
/// obat/BHP for one No.Rawat, then save rawat_jl_dr / rawat_inap_dr + detail_pemberian_obat,
/// cut stock (gudangbarang, data_batch when batch is on, riwayat_barang_medis) and post the
/// same two journals (tindakan, obat) as the original — all in one transaction.
/// </summary>
public class BarcodeRepository(DatabaseSettings settings)
{
    private Task<Db> OpenAsync() => Db.OpenAsync(settings);

    private static string KelasColumn(string kelas) => kelas switch
    {
        "Kelas 1" => "kelas1", "Kelas 2" => "kelas2", "Kelas 3" => "kelas3",
        "Kelas Utama" => "utama", "Kelas VIP" => "vip", "Kelas VVIP" => "vvip",
        _ => "",
    };

    public async Task<BarcodeContext> LoadAsync(string noRawat, bool ranap)
    {
        await using var db = await OpenAsync();
        var reg = await db.FirstAsync("""
            SELECT r.no_rkm_medis, p.nm_pasien, r.kd_pj, r.kd_dokter, IFNULL(d.nm_dokter,'') AS nm_dokter, r.kd_poli,
                   IFNULL(pl.nm_poli,'') AS nm_poli, r.stts
            FROM reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            LEFT JOIN dokter d ON d.kd_dokter=r.kd_dokter LEFT JOIN poliklinik pl ON pl.kd_poli=r.kd_poli
            WHERE r.no_rawat=@p0
            """, noRawat) ?? throw new InvalidOperationException($"No.Rawat {noRawat} tidak ditemukan.");

        var tarif = await db.FirstAsync("SELECT * FROM set_tarif LIMIT 1");
        string Yes(string col) => tarif?.S(col) ?? "Yes";
        var lokasiDefault = await db.FirstAsync("SELECT kd_bangsal, asal_stok FROM set_lokasi LIMIT 1");

        if (!ranap)
        {
            var depo = await db.StrAsync("SELECT kd_bangsal FROM set_depo_ralan WHERE kd_poli=@p0", reg.S("kd_poli"));
            if (depo.Length == 0) depo = lokasiDefault?.S("kd_bangsal") ?? "";
            var kenaikan = Convert.ToDouble(await db.ScalarAsync("SELECT hargajual/100 FROM set_harga_obat_ralan WHERE kd_pj=@p0", reg.S("kd_pj")) ?? 0d);
            return new BarcodeContext(false, noRawat, reg.S("no_rkm_medis"), reg.S("nm_pasien"), reg.S("kd_pj"), reg.S("kd_dokter"), reg.S("nm_dokter"),
                reg.S("kd_poli"), reg.S("nm_poli"), "", depo, kenaikan, Yes("poli_ralan") == "Yes", Yes("cara_bayar_ralan") == "Yes");
        }

        var kamar = await db.FirstAsync("""
            SELECT b.kd_bangsal, b.nm_bangsal, k.kelas FROM kamar_inap ki
            JOIN kamar k ON k.kd_kamar=ki.kd_kamar JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal
            WHERE ki.no_rawat=@p0 AND ki.stts_pulang='-'
            ORDER BY STR_TO_DATE(CONCAT(ki.tgl_masuk,' ',ki.jam_masuk),'%Y-%m-%d %H:%i:%s') DESC LIMIT 1
            """, noRawat) ?? throw new InvalidOperationException("Pasien ini tidak sedang dirawat inap (tidak ada kamar aktif).");
        var bangsal = kamar.S("kd_bangsal");
        var lokasi = await db.StrAsync("SELECT kd_depo FROM set_depo_ranap WHERE kd_bangsal=@p0", bangsal);
        if (lokasi.Length == 0)
            lokasi = lokasiDefault?.S("asal_stok") == "Gunakan Stok Bangsal" ? bangsal : lokasiDefault?.S("kd_bangsal") ?? "";
        var naik = Convert.ToDouble(await db.ScalarAsync("SELECT hargajual/100 FROM set_harga_obat_ranap WHERE kd_pj=@p0 AND kelas=@p1",
            reg.S("kd_pj"), kamar.S("kelas")) ?? 0d);
        return new BarcodeContext(true, noRawat, reg.S("no_rkm_medis"), reg.S("nm_pasien"), reg.S("kd_pj"), reg.S("kd_dokter"), reg.S("nm_dokter"),
            bangsal, kamar.S("nm_bangsal"), kamar.S("kelas"), lokasi, naik, Yes("ruang_ranap") == "Yes", Yes("cara_bayar_ranap") == "Yes");
    }

    public async Task<List<TindakanItem>> CariTindakanAsync(BarcodeContext c, string cari)
    {
        var table = c.Ranap ? "jns_perawatan_inap" : "jns_perawatan";
        var unitCol = c.Ranap ? "kd_bangsal" : "kd_poli";
        var filter = "j.status='1'";
        var args = new List<object?> { $"%{cari.Trim()}%" };
        if (c.TarifPerCaraBayar) { filter += " AND (j.kd_pj=@p1 OR j.kd_pj='-')"; args.Add(c.KdPj); }
        if (c.TarifPerUnit) { filter += $" AND (j.{unitCol}=@p{args.Count} OR j.{unitCol}='-')"; args.Add(c.KdPoliAtauBangsal); }

        await using var db = await OpenAsync();
        var rows = await db.QueryAsync($"""
            SELECT j.kd_jenis_prw, j.nm_perawatan, k.nm_kategori, j.total_byrdr, j.material, j.bhp, j.tarif_tindakandr, j.tarif_tindakanpr, j.kso, j.menejemen
            FROM {table} j JOIN kategori_perawatan k ON k.kd_kategori=j.kd_kategori
            WHERE {filter} AND (j.kd_jenis_prw LIKE @p0 OR j.nm_perawatan LIKE @p0 OR k.nm_kategori LIKE @p0) AND j.total_byrdr>0
            ORDER BY j.nm_perawatan LIMIT 300
            """, args.ToArray());
        return rows.Select(r => new TindakanItem(r.S("kd_jenis_prw"), r.S("nm_perawatan"), r.S("nm_kategori"), r.D("total_byrdr"),
            r.D("material"), r.D("bhp"), r.D("tarif_tindakandr"), r.D("tarif_tindakanpr"), r.D("kso"), r.D("menejemen"))).ToList();
    }

    public async Task<List<ObatItem>> CariObatAsync(BarcodeContext c, string cari)
    {
        if (c.LokasiStok.Length == 0) throw new InvalidOperationException("Lokasi stok (depo) belum di-setting untuk unit ini.");
        var hpp = settings.HppColumn;
        var batch = settings.AktifkanBatchObat;
        string harga;
        if (c.Kenaikan > 0)
            harga = batch ? "(db.h_beli+(db.h_beli*@p2))" : "(b.h_beli+(b.h_beli*@p2))";
        else if (!c.Ranap)
            harga = batch ? "db.ralan" : "b.ralan";
        else
        {
            var col = KelasColumn(c.Kelas);
            if (col.Length == 0) throw new InvalidOperationException($"Kelas kamar '{c.Kelas}' tidak dikenali — harga obat tidak bisa ditentukan.");
            harga = (batch ? "db." : "b.") + col;
        }
        // Legacy ranap+kenaikan+batch reads HPP from databarang, every other combination from the price source table.
        var hppSrc = batch && !(c.Ranap && c.Kenaikan > 0) ? $"db.{hpp}" : $"b.{hpp}";

        var sql = batch
            ? $"""
              SELECT b.kode_brng, b.nama_brng, j.nama AS jenis, b.kode_sat, b.letak_barang, {harga} AS harga, g.stok, {hppSrc} AS hpp,
                     db.no_batch, db.no_faktur, IFNULL(b.kapasitas,1) AS kapasitas
              FROM data_batch db JOIN databarang b ON b.kode_brng=db.kode_brng JOIN jenis j ON j.kdjns=b.kdjns
              JOIN gudangbarang g ON g.kode_brng=db.kode_brng AND g.no_batch=db.no_batch AND g.no_faktur=db.no_faktur
              WHERE g.stok>0 AND g.kd_bangsal=@p0 AND (b.kode_brng LIKE @p1 OR b.nama_brng LIKE @p1 OR j.nama LIKE @p1)
              ORDER BY b.nama_brng LIMIT 300
              """
            : $"""
              SELECT b.kode_brng, b.nama_brng, j.nama AS jenis, b.kode_sat, b.letak_barang, {harga} AS harga, g.stok, {hppSrc} AS hpp,
                     '' AS no_batch, '' AS no_faktur, IFNULL(b.kapasitas,1) AS kapasitas
              FROM databarang b JOIN jenis j ON j.kdjns=b.kdjns JOIN gudangbarang g ON g.kode_brng=b.kode_brng
              WHERE g.no_batch='' AND g.no_faktur='' AND g.stok>0 AND g.kd_bangsal=@p0 AND b.status='1'
                AND (b.kode_brng LIKE @p1 OR b.nama_brng LIKE @p1 OR j.nama LIKE @p1)
              ORDER BY b.nama_brng LIMIT 300
              """;
        await using var db = await OpenAsync();
        var rows = await db.QueryAsync(sql, c.LokasiStok, $"%{cari.Trim()}%", c.Kenaikan);
        return rows.Select(r => new ObatItem(r.S("kode_brng"), r.S("nama_brng"), r.S("kode_sat"), r.S("letak_barang"), r.S("jenis"),
            r.D("harga"), r.D("stok"), r.D("hpp"), r.S("no_batch"), r.S("no_faktur"), r.D("kapasitas") <= 0 ? 1 : r.D("kapasitas"))).ToList();
    }

    public async Task SaveAsync(BarcodeContext c, IReadOnlyList<TindakanItem> tindakan, IReadOnlyList<ObatInput> obat, string petugasKode)
    {
        var obatRows = obat.Where(o => o.Jumlah > 0).ToList();
        if (tindakan.Count == 0 && obatRows.Count == 0) throw new ArgumentException("Belum ada tindakan atau obat yang dipilih.");
        if (c.KdDokter.Length == 0) throw new InvalidOperationException("Dokter registrasi kosong.");
        if (obatRows.Count > 0 && c.LokasiStok.Length == 0) throw new InvalidOperationException("Lokasi stok belum di-setting.");
        if (settings.AktifkanBatchObat && obatRows.Any(o => o.Obat.NoBatch.Length == 0 || o.Obat.NoFaktur.Length == 0))
            throw new InvalidOperationException("No.Batch/No.Faktur obat kosong.");
        foreach (var o in obatRows)
        {
            var jml = o.PakaiKapasitas ? o.Jumlah / o.Obat.Kapasitas : o.Jumlah;
            if (jml > o.Obat.Stok) throw new InvalidOperationException($"Stok {o.Obat.Nama} tidak cukup (sisa {o.Obat.Stok}).");
        }

        await using var db = await OpenAsync();
        await db.BeginAsync();
        var visit = await db.FirstAsync("SELECT stts,status_bayar FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", c.NoRawat);
        if (visit is null || visit.S("stts") == "Batal" || visit.S("status_bayar") == "Sudah Bayar" ||
            await db.CountAsync("SELECT COUNT(*) FROM billing WHERE no_rawat=@p0", c.NoRawat) > 0)
            throw new InvalidOperationException("Data billing sudah terverifikasi / registrasi batal, silahkan hubungi bagian kasir/keuangan.");

        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, c.Ranap ? "set_akun_ranap" : "set_akun_ralan");
        var now = Convert.ToDateTime(await db.ScalarAsync("SELECT NOW()"));
        var sfx = c.Ranap ? "Ranap" : "Ralan";

        // ---- tindakan
        if (tindakan.Count > 0)
        {
            foreach (var t in tindakan)
            {
                if (c.Ranap)
                    await db.ExecAsync("INSERT INTO rawat_inap_dr VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10)",
                        c.NoRawat, t.Kode, c.KdDokter, now.Date, now.TimeOfDay, t.Material, t.Bhp, t.JmDokter, t.Kso, t.Menejemen, t.Tarif);
                else
                    await db.ExecAsync("INSERT INTO rawat_jl_dr VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,'Belum')",
                        c.NoRawat, t.Kode, c.KdDokter, now.Date, now.TimeOfDay, t.Material, t.Bhp, t.JmDokter, t.Kso, t.Menejemen, t.Tarif);
            }
            string A(string col) => akun.GetValueOrDefault(col, "");
            var lines = new List<JurnalLine>();
            void Pair(double v, string dCol, string dNama, string kCol, string kNama)
            {
                if (v <= 0) return;
                lines.Add(new JurnalLine(A(dCol), dNama, v, 0));
                lines.Add(new JurnalLine(A(kCol), kNama, 0, v));
            }
            var unit = c.Ranap ? "Ranap" : "Ralan";
            Pair(tindakan.Sum(t => t.Tarif), $"Suspen_Piutang_Tindakan_{sfx}", $"Suspen Piutang Tindakan {unit}", $"Tindakan_{sfx}", c.Ranap ? "Pendapatan Tindakan Rawat Inap" : "Pendapatan Tindakan Rawat Jalan");
            Pair(tindakan.Sum(t => t.JmDokter), $"Beban_Jasa_Medik_Dokter_Tindakan_{sfx}", $"Beban Jasa Medik Dokter Tindakan {unit}", $"Utang_Jasa_Medik_Dokter_Tindakan_{sfx}", $"Utang Jasa Medik Dokter Tindakan {unit}");
            Pair(tindakan.Sum(t => t.Kso), $"Beban_KSO_Tindakan_{sfx}", $"Beban KSO Tindakan {unit}", $"Utang_KSO_Tindakan_{sfx}", $"Utang KSO Tindakan {unit}");
            Pair(tindakan.Sum(t => t.Material), $"Beban_Jasa_Sarana_Tindakan_{sfx}", $"Beban Jasa Sarana Tindakan {unit}", $"Utang_Jasa_Sarana_Tindakan_{sfx}", $"Utang Jasa Sarana Tindakan {unit}");
            Pair(tindakan.Sum(t => t.Bhp), $"HPP_BHP_Tindakan_{sfx}", $"HPP BHP Tindakan {unit}", $"Persediaan_BHP_Tindakan_{sfx}", $"Persediaan BHP Tindakan {unit}");
            Pair(tindakan.Sum(t => t.Menejemen), $"Beban_Jasa_Menejemen_Tindakan_{sfx}", $"Beban Jasa Menejemen Tindakan {unit}", $"Utang_Jasa_Menejemen_Tindakan_{sfx}", $"Utang Jasa Menejemen Tindakan {unit}");
            await JurnalPoster.PostAsync(db.Conn, db.Tx!, c.NoRawat,
                $"TINDAKAN {(c.Ranap ? "RAWAT INAP" : "RAWAT JALAN")} PASIEN {c.NoRawat} DIPOSTING OLEH {petugasKode}", lines);
        }

        // ---- obat / BHP
        if (obatRows.Count > 0)
        {
            double ttlJual = 0, ttlHpp = 0;
            foreach (var o in obatRows)
            {
                var b = o.Obat;
                var jml = o.PakaiKapasitas ? o.Jumlah / b.Kapasitas : o.Jumlah;
                var total = o.Embalase + o.Tuslah + b.Harga * jml;
                await db.ExecAsync("INSERT INTO detail_pemberian_obat VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",
                    now.Date, now.TimeOfDay, c.NoRawat, b.Kode, b.Hpp, b.Harga, jml, o.Embalase, o.Tuslah, total, sfx, c.LokasiStok, b.NoBatch, b.NoFaktur);

                var stokAwal = Convert.ToDouble(await db.ScalarAsync(
                    "SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3",
                    b.Kode, c.LokasiStok, b.NoBatch, b.NoFaktur) ?? 0d);
                await db.ExecAsync("INSERT INTO riwayat_barang_medis VALUES (@p0,@p1,0,@p2,@p3,'Pemberian Obat',CURRENT_DATE(),CURRENT_TIME(),@p4,@p5,'Simpan',@p6,@p7,@p8)",
                    b.Kode, stokAwal, jml, stokAwal - jml, petugasKode, c.LokasiStok, b.NoBatch, b.NoFaktur, $"{c.NoRawat} {c.NoRm}");
                if (settings.AktifkanBatchObat)
                    await db.ExecAsync("UPDATE data_batch SET sisa=sisa-@p0 WHERE no_batch=@p1 AND kode_brng=@p2 AND no_faktur=@p3", jml, b.NoBatch, b.Kode, b.NoFaktur);
                await db.ExecAsync("""
                    INSERT INTO gudangbarang VALUES (@p0,@p1,@p2,@p3,@p4)
                    ON DUPLICATE KEY UPDATE stok=stok-@p5
                    """, b.Kode, c.LokasiStok, -jml, b.NoBatch, b.NoFaktur, jml);

                ttlJual += total;
                ttlHpp += b.Hpp * jml;
            }
            string A(string col) => akun.GetValueOrDefault(col, "");
            var rawat = c.Ranap ? "Rawat_Inap" : "Rawat_Jalan";
            var rawatNama = c.Ranap ? "Rawat Inap" : "Rawat Jalan";
            var lines = new List<JurnalLine>();
            if (ttlJual > 0)
            {
                lines.Add(new JurnalLine(A($"Suspen_Piutang_Obat_{sfx}"), $"Suspen Piutang Obat {sfx}", ttlJual, 0));
                lines.Add(new JurnalLine(A($"Obat_{sfx}"), $"Pendapatan Obat {rawatNama}", 0, ttlJual));
            }
            if (ttlHpp > 0)
            {
                lines.Add(new JurnalLine(A($"HPP_Obat_{rawat}"), $"HPP Persediaan Obat {rawatNama}", ttlHpp, 0));
                lines.Add(new JurnalLine(A($"Persediaan_Obat_{rawat}"), $"Persediaan Obat {rawatNama}", 0, ttlHpp));
            }
            await JurnalPoster.PostAsync(db.Conn, db.Tx!, c.NoRawat,
                $"PEMBERIAN OBAT {(c.Ranap ? "RAWAT INAP" : "RAWAT JALAN")} PASIEN, DIPOSTING OLEH {petugasKode}", lines);
        }

        await db.CommitAsync();
    }
}
