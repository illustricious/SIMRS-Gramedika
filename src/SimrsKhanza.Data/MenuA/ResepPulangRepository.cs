using SimrsKhanza.Config;
using SimrsKhanza.Data.Farmasi;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.MenuA;

public sealed class ResepPulangRepository(DatabaseSettings settings)
{
    public async Task<IReadOnlyList<Row>> CariPermintaanAsync(string query)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT x.no_permintaan,x.no_rawat,x.tgl_permintaan,p.nm_pasien,
                   GROUP_CONCAT(CONCAT(b.nama_brng,' × ',d.jml) ORDER BY b.nama_brng SEPARATOR ', ') AS obat
            FROM permintaan_resep_pulang x JOIN reg_periksa r ON r.no_rawat=x.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN detail_permintaan_resep_pulang d ON d.no_permintaan=x.no_permintaan
            JOIN databarang b ON b.kode_brng=d.kode_brng
            WHERE x.status='Belum' AND (x.no_permintaan LIKE @p0 OR x.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0)
            GROUP BY x.no_permintaan,x.no_rawat,x.tgl_permintaan,p.nm_pasien
            ORDER BY x.tgl_permintaan DESC,x.no_permintaan DESC LIMIT 100
            """, $"%{query.Trim()}%");
    }

    public async Task<IReadOnlyList<Row>> CariStokAsync(string noRawat, string depo, string query)
    {
        await using var db = await Db.OpenAsync(settings);
        return await StokAsync(db, noRawat, depo, query.Trim(), perKode: false);
    }

    private Task<List<Row>> StokAsync(Db db, string noRawat, string depo, string query, bool perKode)
    {
        var harga = settings.AktifkanBatchObat ? "d" : "b";
        var batch = settings.AktifkanBatchObat
            ? "JOIN data_batch d ON d.kode_brng=g.kode_brng AND d.no_batch=g.no_batch AND d.no_faktur=g.no_faktur AND d.sisa>0"
            : "";
        var tersedia = settings.AktifkanBatchObat ? "LEAST(g.stok,d.sisa)" : "g.stok";
        var penanda = settings.AktifkanBatchObat ? "g.no_batch<>'' AND g.no_faktur<>''" : "g.no_batch='' AND g.no_faktur=''";
        var kadaluarsa = settings.AktifkanBatchObat ? "d.tgl_kadaluarsa" : "b.expire";
        var filter = perKode ? "g.kode_brng=@p2" : "CONCAT_WS(' ',g.kode_brng,b.nama_brng,g.no_batch,g.no_faktur) LIKE @p2";
        var limit = perKode ? "" : "LIMIT 100";
        return db.QueryAsync($"""
            SELECT g.kode_brng,b.nama_brng,b.kode_sat,g.no_batch,g.no_faktur,{tersedia} AS stok,
                   CEIL((CASE WHEN COALESCE(h.hargajual,0)>0 THEN {harga}.h_beli*(1+h.hargajual/100)
                       ELSE CASE k.kelas
                           WHEN 'Kelas 1' THEN {harga}.kelas1 WHEN 'Kelas 2' THEN {harga}.kelas2
                           WHEN 'Kelas 3' THEN {harga}.kelas3 WHEN 'Kelas Utama' THEN {harga}.utama
                           WHEN 'Kelas VIP' THEN {harga}.vip WHEN 'Kelas VVIP' THEN {harga}.vvip ELSE {harga}.h_beli
                       END END)/100)*100 AS harga
            FROM reg_periksa r
            LEFT JOIN kamar k ON k.kd_kamar=(
                SELECT i.kd_kamar FROM kamar_inap i WHERE i.no_rawat=r.no_rawat
                ORDER BY (i.stts_pulang='-') DESC,i.tgl_masuk DESC,i.jam_masuk DESC LIMIT 1)
            LEFT JOIN set_harga_obat_ranap h ON h.kd_pj=r.kd_pj AND h.kelas=k.kelas
            JOIN gudangbarang g ON g.kd_bangsal=@p1 JOIN databarang b ON b.kode_brng=g.kode_brng
            {batch}
            WHERE r.no_rawat=@p0 AND g.stok>0 AND b.status='1' AND {penanda} AND {filter}
            ORDER BY b.nama_brng,{kadaluarsa} IS NULL,{kadaluarsa},g.no_batch,g.no_faktur {limit}
            """, noRawat, depo, perKode ? query : $"%{query}%");
    }

    public async Task ProsesPermintaanAsync(string nomor, string depo)
    {
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var rawat = await db.StrAsync("SELECT no_rawat FROM permintaan_resep_pulang WHERE no_permintaan=@p0", nomor);
        var pasien = await MenuAWrites.KunciKunjunganAsync(db, rawat, cekBilling: true);
        var header = await db.FirstAsync("""
            SELECT no_rawat,status FROM permintaan_resep_pulang WHERE no_permintaan=@p0 FOR UPDATE
            """, nomor);
        if (header is null || header.S("no_rawat") != rawat || header.S("status") != "Belum")
            throw new InvalidOperationException("Permintaan resep tidak ditemukan atau sudah dilayani.");

        var details = await db.QueryAsync("""
            SELECT kode_brng,jml,dosis FROM detail_permintaan_resep_pulang
            WHERE no_permintaan=@p0 ORDER BY kode_brng,dosis FOR UPDATE
            """, nomor);
        if (details.Count == 0) throw new InvalidOperationException("Permintaan belum berisi obat.");

        var sekarang = (DateTime)(await db.ScalarAsync("SELECT NOW()"))!;
        foreach (var group in details.GroupBy(r => r.S("kode_brng"), StringComparer.OrdinalIgnoreCase))
        {
            var dosis = group.Select(r => r.S("dosis")).Distinct().ToArray();
            if (group.Key.Length == 0 || dosis.Length != 1 || dosis[0].Length == 0 || group.Any(r => !double.IsFinite(r.D("jml")) || r.D("jml") <= 0))
                throw new InvalidOperationException("Periksa kode obat, jumlah, dan aturan pakai pada permintaan resep.");
            var kurang = group.Sum(r => r.D("jml"));
            foreach (var row in await StokAsync(db, rawat, depo, group.Key, perKode: true))
            {
                var jumlah = Math.Min(kurang, row.D("stok"));
                if (jumlah <= 0) continue;
                var item = new StokItem(group.Key, row.S("nama_brng"), row.S("kode_sat"), "", row.S("no_batch"), row.S("no_faktur"),
                    row.D("stok"), row.D("harga"), null);
                if (row["harga"] is null || !double.IsFinite(item.Harga * jumlah) || item.Harga < 0)
                    throw new InvalidOperationException($"Harga {item.Nama} belum valid.");
                await MenuAWrites.MutasiResepAsync(db, item, depo, jumlah, masuk: false, $"{rawat} {pasien.S("nm_pasien")}");
                await db.ExecAsync("""
                    INSERT INTO resep_pulang (no_rawat,kode_brng,jml_barang,harga,total,dosis,tanggal,jam,kd_bangsal,no_batch,no_faktur)
                    VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10)
                    """, rawat, item.Kode, jumlah, item.Harga, item.Harga * jumlah, dosis[0], sekarang.Date, sekarang.TimeOfDay, depo, item.NoBatch, item.NoFaktur);
                kurang -= jumlah;
                if (kurang <= 0.0000001) break;
            }
            if (kurang > 0.0000001) throw new InvalidOperationException($"Stok obat {group.Key} di depo terpilih tidak cukup.");
        }

        await db.ExecAsync("""
            UPDATE permintaan_resep_pulang SET status='Sudah',tgl_validasi=@p0,jam_validasi=@p1 WHERE no_permintaan=@p2
            """, sekarang.Date, sekarang.TimeOfDay, nomor);
        await db.CommitAsync();
    }
}
