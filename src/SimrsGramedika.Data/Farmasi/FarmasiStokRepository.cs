using SimrsGramedika.Config;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Farmasi;

public enum StokMode { Mutasi, Opname, StokKeluar, Permintaan }

/// <summary>One stock row of a barang at a lokasi (per batch when batch obat is on).</summary>
public sealed record StokItem(string Kode, string Nama, string KodeSat, string Satuan, string NoBatch, string NoFaktur, double Stok, double Harga, DateTime? Kadaluarsa);

/// <summary>A line being entered: Jumlah = qty moved, or the counted (real) stock for opname.</summary>
public sealed class StokLine
{
    public required StokItem Item { get; init; }
    public double Jumlah { get; set; }
    public string Keterangan { get; set; } = "";
}

/// <summary>
/// Farmasi stock transactions with the same table effects as the Java app: DlgMutasiBarang,
/// DlgInputStok (opname), DlgPengeluaranApotek (stok keluar + jurnal) and DlgPermintaan.
/// Every movement writes riwayat_barang_medis (stok awal/akhir) and updates gudangbarang.
/// </summary>
public class FarmasiStokRepository(DatabaseSettings settings)
{
    private bool Batch => settings.AktifkanBatchObat;
    private string Hpp => settings.HppColumn;

    /// <summary>Columns written by INSERTs here (checked against the database by the smoke test).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> WrittenColumns = new Dictionary<string, string[]>
    {
        ["riwayat_barang_medis"] = ["kode_brng", "stok_awal", "masuk", "keluar", "stok_akhir", "posisi", "tanggal", "jam", "petugas", "kd_bangsal", "status", "no_batch", "no_faktur", "keterangan"],
        ["gudangbarang"] = ["kode_brng", "kd_bangsal", "stok", "no_batch", "no_faktur"],
        ["mutasibarang"] = ["kode_brng", "jml", "harga", "kd_bangsaldari", "kd_bangsalke", "tanggal", "keterangan", "no_batch", "no_faktur"],
        ["opname"] = ["kode_brng", "h_beli", "tanggal", "stok", "real", "selisih", "nomihilang", "lebih", "nomilebih", "keterangan", "kd_bangsal", "no_batch", "no_faktur"],
        ["pengeluaran_obat_bhp"] = ["no_keluar", "tanggal", "nip", "keterangan", "kd_bangsal"],
        ["detail_pengeluaran_obat_bhp"] = ["no_keluar", "kode_brng", "kode_sat", "no_batch", "jumlah", "harga_beli", "total", "no_faktur"],
        ["permintaan_medis"] = ["no_permintaan", "kd_bangsal", "nip", "tanggal", "status", "kd_bangsaltujuan"],
        ["detail_permintaan_medis"] = ["no_permintaan", "kode_brng", "kode_sat", "jumlah", "keterangan"],
        ["data_batch"] = ["sisa"],
        ["stok_obat_pasien"] = ["tanggal", "jam", "no_rawat", "kode_brng", "jumlah", "kd_bangsal", "no_batch", "no_faktur", "aturan_pakai", .. Enumerable.Range(0, 24).Select(h => $"jam{h:00}")],
        ["permintaan_stok_obat_pasien"] = ["status", "tgl_validasi", "jam_validasi"],
    };

    public async Task<List<(string Kode, string Nama)>> LokasiAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("SELECT kd_bangsal, nm_bangsal FROM bangsal WHERE status='1' ORDER BY nm_bangsal"))
            .Select(r => (r.S("kd_bangsal"), r.S("nm_bangsal"))).ToList();
    }

    public async Task<List<(string Kode, string Nama)>> PetugasAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("SELECT nip, nama FROM petugas WHERE status='1' ORDER BY nama")).Select(r => (r.S("nip"), r.S("nama"))).ToList();
    }

    private static StokItem Item(Row r) => new(r.S("kode_brng"), r.S("nama_brng"), r.S("kode_sat"), r.S("satuan"), r.S("no_batch"), r.S("no_faktur"),
        r.D("stok"), r.D("harga"), r.Dt("kadaluarsa"));

    /// <summary>Stock at a lokasi. includeZero (opname) also lists active barang with no stock row yet (non-batch mode).</summary>
    public async Task<List<StokItem>> CariStokAsync(string kdBangsal, string? cari, bool includeZero)
    {
        await using var db = await Db.OpenAsync(settings);
        var like = $"%{cari?.Trim()}%";
        string sql;
        if (Batch)
            sql = $"""
                SELECT g.kode_brng, b.nama_brng, b.kode_sat, s.satuan, g.no_batch, g.no_faktur, g.stok, IFNULL(d.{Hpp},b.{Hpp}) AS harga, d.tgl_kadaluarsa AS kadaluarsa
                FROM gudangbarang g JOIN databarang b ON b.kode_brng=g.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
                LEFT JOIN data_batch d ON d.no_batch=g.no_batch AND d.kode_brng=g.kode_brng AND d.no_faktur=g.no_faktur
                WHERE g.kd_bangsal=@p0 AND g.no_batch<>'' AND b.status='1' AND (g.stok>0 OR @p2=1) AND CONCAT_WS(' ',g.kode_brng,b.nama_brng,g.no_batch) LIKE @p1
                ORDER BY b.nama_brng, d.tgl_kadaluarsa LIMIT 500
                """;
        else if (includeZero)
            sql = $"""
                SELECT b.kode_brng, b.nama_brng, b.kode_sat, s.satuan, '' AS no_batch, '' AS no_faktur, IFNULL(g.stok,0) AS stok, b.{Hpp} AS harga, b.expire AS kadaluarsa
                FROM databarang b LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
                LEFT JOIN gudangbarang g ON g.kode_brng=b.kode_brng AND g.kd_bangsal=@p0 AND g.no_batch='' AND g.no_faktur=''
                WHERE b.status='1' AND @p2=1 AND CONCAT_WS(' ',b.kode_brng,b.nama_brng) LIKE @p1 ORDER BY b.nama_brng LIMIT 500
                """;
        else
            sql = $"""
                SELECT g.kode_brng, b.nama_brng, b.kode_sat, s.satuan, '' AS no_batch, '' AS no_faktur, g.stok, b.{Hpp} AS harga, b.expire AS kadaluarsa
                FROM gudangbarang g JOIN databarang b ON b.kode_brng=g.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
                WHERE g.kd_bangsal=@p0 AND g.no_batch='' AND g.no_faktur='' AND b.status='1' AND (g.stok>0 OR @p2=1)
                AND CONCAT_WS(' ',g.kode_brng,b.nama_brng) LIKE @p1 ORDER BY b.nama_brng LIMIT 500
                """;
        return (await db.QueryAsync(sql, kdBangsal, like, includeZero ? 1 : 0)).Select(Item).ToList();
    }

    /// <summary>Active barang with their total stock at a lokasi (for permintaan — no batch needed).</summary>
    public async Task<List<StokItem>> CariBarangAsync(string? kdBangsal, string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync($"""
            SELECT b.kode_brng, b.nama_brng, b.kode_sat, s.satuan, '' AS no_batch, '' AS no_faktur,
            IFNULL((SELECT SUM(g.stok) FROM gudangbarang g WHERE g.kode_brng=b.kode_brng AND g.kd_bangsal=@p0),0) AS stok, b.{Hpp} AS harga, b.expire AS kadaluarsa
            FROM databarang b LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            WHERE b.status='1' AND CONCAT_WS(' ',b.kode_brng,b.nama_brng) LIKE @p1 ORDER BY b.nama_brng LIMIT 500
            """, kdBangsal ?? "", $"%{cari?.Trim()}%")).Select(Item).ToList();
    }

    public async Task<string> NextNomorAsync(StokMode mode, DateTime tanggal)
    {
        await using var db = await Db.OpenAsync(settings);
        return await NomorAsync(db, mode, tanggal);
    }

    private static Task<string> NomorAsync(Db db, StokMode mode, DateTime t) => mode switch
    {
        StokMode.StokKeluar => db.AutoNomorAsync("SELECT IFNULL(MAX(CONVERT(RIGHT(no_keluar,3),SIGNED)),0) FROM pengeluaran_obat_bhp WHERE tanggal=@p0", $"SKM{t:yyyyMMdd}", 3, t.Date),
        StokMode.Permintaan => db.AutoNomorAsync("SELECT IFNULL(MAX(CONVERT(RIGHT(no_permintaan,3),SIGNED)),0) FROM permintaan_medis WHERE tanggal=@p0", $"PM{t:yyyyMMdd}", 3, t.Date),
        _ => Task.FromResult(""),
    };

    // ------------------------------------------------------------------ shared stock writes

    /// <summary>Legacy riwayatobat.catatRiwayat: stok awal from gudangbarang; for Opname stok akhir = the counted stock.</summary>
    internal static async Task CatatAsync(Db db, StokItem i, double masuk, double keluar, string posisi, string petugas, string bangsal, string keterangan,
        string status = "Simpan")
    {
        var awal = Convert.ToDouble(await db.ScalarAsync(
            "SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3",
            i.Kode, bangsal, i.NoBatch, i.NoFaktur) ?? 0d);
        var opname = posisi == "Opname";
        await db.ExecAsync("""
            INSERT INTO riwayat_barang_medis (kode_brng,stok_awal,masuk,keluar,stok_akhir,posisi,tanggal,jam,petugas,kd_bangsal,status,no_batch,no_faktur,keterangan)
            VALUES (@p0,@p1,@p2,@p3,@p4,@p5,CURRENT_DATE(),CURRENT_TIME(),@p6,@p7,@p11,@p8,@p9,@p10)
            """, i.Kode, awal, masuk, opname ? 0 : keluar, opname ? masuk : awal + masuk - keluar, posisi, petugas, bangsal, i.NoBatch, i.NoFaktur,
            keterangan.Length > 150 ? keterangan[..150] : keterangan, status);
    }

    internal static Task TambahStokAsync(Db db, StokItem i, string bangsal, double delta) => db.ExecAsync("""
        INSERT INTO gudangbarang (kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES (@p0,@p1,@p2,@p3,@p4)
        ON DUPLICATE KEY UPDATE stok=stok+@p2
        """, i.Kode, bangsal, delta, i.NoBatch, i.NoFaktur);

    private void CekBaris(IReadOnlyList<StokLine> lines, bool checkStok, bool allowZero = false)
    {
        if (lines.Count == 0) throw new ArgumentException("Belum ada barang yang dimasukkan.");
        foreach (var l in lines)
        {
            if (l.Jumlah < 0 || (!allowZero && l.Jumlah == 0)) throw new ArgumentException($"Jumlah {l.Item.Nama} harus lebih dari 0.");
            if (Batch && (l.Item.NoBatch.Length == 0 || l.Item.NoFaktur.Length == 0)) throw new ArgumentException($"No.Batch/No.Faktur {l.Item.Nama} kosong.");
        }
        if (!checkStok) return;
        foreach (var g in lines.GroupBy(l => (l.Item.Kode, l.Item.NoBatch, l.Item.NoFaktur)))
        {
            var it = g.First().Item;
            if (g.Sum(x => x.Jumlah) > it.Stok) throw new ArgumentException($"Stok {it.Nama}{(it.NoBatch.Length > 0 ? $" batch {it.NoBatch}" : "")} tidak cukup (sisa {it.Stok}).");
        }
    }

    private static async Task CekStokTerkiniAsync(Db db, IReadOnlyList<StokLine> lines, string bangsal)
    {
        foreach (var g in lines.GroupBy(l => (l.Item.Kode, l.Item.NoBatch, l.Item.NoFaktur)))
        {
            var it = g.First().Item;
            var stok = Convert.ToDouble(await db.ScalarAsync(
                "SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3 FOR UPDATE",
                it.Kode, bangsal, it.NoBatch, it.NoFaktur) ?? 0d);
            if (g.Sum(x => x.Jumlah) > stok) throw new InvalidOperationException($"Stok {it.Nama} berubah dan tidak cukup lagi (sisa {stok}). Cari ulang barangnya.");
        }
    }

    // ------------------------------------------------------------------ transactions

    public async Task SimpanMutasiAsync(string dari, string namaDari, string ke, string namaKe, DateTime tanggal, string keterangan,
        IReadOnlyList<StokLine> lines, string petugas, string? noPermintaan)
    {
        if (dari.Length == 0 || ke.Length == 0) throw new ArgumentException("Lokasi asal dan tujuan wajib diisi.");
        if (dari == ke) throw new ArgumentException("Lokasi asal dan tujuan harus berbeda.");
        if (keterangan.Trim().Length == 0) throw new ArgumentException("Keterangan wajib diisi.");
        CekBaris(lines, checkStok: true);

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await CekStokTerkiniAsync(db, lines, dari);
        var ket = $"{keterangan}, dari {namaDari} ke {namaKe}";
        foreach (var l in lines)
        {
            var i = l.Item;
            await db.ExecAsync("""
                INSERT INTO mutasibarang (kode_brng,jml,harga,kd_bangsaldari,kd_bangsalke,tanggal,keterangan,no_batch,no_faktur)
                VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)
                """, i.Kode, l.Jumlah, i.Harga, dari, ke, tanggal, keterangan.Length > 60 ? keterangan[..60] : keterangan, i.NoBatch, i.NoFaktur);
            await CatatAsync(db, i, 0, l.Jumlah, "Mutasi", petugas, dari, ket);
            await TambahStokAsync(db, i, dari, -l.Jumlah);
            await CatatAsync(db, i, l.Jumlah, 0, "Mutasi", petugas, ke, ket);
            await TambahStokAsync(db, i, ke, l.Jumlah);
        }
        if (noPermintaan is { Length: > 0 })
            await db.ExecAsync("UPDATE permintaan_medis SET status='Disetujui' WHERE no_permintaan=@p0", noPermintaan);
        await db.CommitAsync();
    }

    /// <summary>Line.Jumlah = counted (real) stock; Item.Stok = system stock at the moment it was looked up.</summary>
    public async Task SimpanOpnameAsync(string bangsal, DateTime tanggal, string keterangan, IReadOnlyList<StokLine> lines, string petugas)
    {
        if (bangsal.Length == 0) throw new ArgumentException("Lokasi wajib diisi.");
        CekBaris(lines, checkStok: false, allowZero: true);

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        foreach (var l in lines)
        {
            var i = l.Item;
            if (await db.CountAsync("SELECT COUNT(*) FROM opname WHERE kode_brng=@p0 AND tanggal=@p1 AND kd_bangsal=@p2 AND no_batch=@p3 AND no_faktur=@p4",
                    i.Kode, tanggal.Date, bangsal, i.NoBatch, i.NoFaktur) > 0)
                throw new InvalidOperationException($"{i.Nama} sudah diopname di lokasi ini pada tanggal tersebut.");
            var sistem = Convert.ToDouble(await db.ScalarAsync(
                "SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3",
                i.Kode, bangsal, i.NoBatch, i.NoFaktur) ?? 0d);
            var hilang = Math.Max(sistem - l.Jumlah, 0);
            var lebih = Math.Max(l.Jumlah - sistem, 0);
            await db.ExecAsync("""
                INSERT INTO opname (kode_brng,h_beli,tanggal,stok,`real`,selisih,nomihilang,lebih,nomilebih,keterangan,kd_bangsal,no_batch,no_faktur)
                VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12)
                """, i.Kode, i.Harga, tanggal.Date, sistem, l.Jumlah, hilang, hilang * i.Harga, lebih, lebih * i.Harga,
                keterangan.Length > 60 ? keterangan[..60] : keterangan, bangsal, i.NoBatch, i.NoFaktur);
            await CatatAsync(db, i, l.Jumlah, 0, "Opname", petugas, bangsal, keterangan);
            await db.ExecAsync("""
                INSERT INTO gudangbarang (kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES (@p0,@p1,@p2,@p3,@p4)
                ON DUPLICATE KEY UPDATE stok=@p2
                """, i.Kode, bangsal, l.Jumlah, i.NoBatch, i.NoFaktur);
        }
        await db.CommitAsync();
    }

    public async Task<string> SimpanStokKeluarAsync(string bangsal, DateTime tanggal, string keterangan, IReadOnlyList<StokLine> lines, string nip, string? noPermintaan)
    {
        if (bangsal.Length == 0) throw new ArgumentException("Lokasi wajib diisi.");
        if (nip.Length == 0) throw new ArgumentException("Petugas wajib diisi.");
        CekBaris(lines, checkStok: true);
        if (lines.GroupBy(l => l.Item.Kode).Any(g => g.Count() > 1))
            throw new ArgumentException("Satu barang hanya boleh satu baris per transaksi stok keluar (aturan tabel Khanza).");

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await CekStokTerkiniAsync(db, lines, bangsal);
        var no = await NomorAsync(db, StokMode.StokKeluar, tanggal);
        await db.ExecAsync("INSERT INTO pengeluaran_obat_bhp (no_keluar,tanggal,nip,keterangan,kd_bangsal) VALUES (@p0,@p1,@p2,@p3,@p4)",
            no, tanggal.Date, nip, keterangan, bangsal);
        double ttl = 0;
        foreach (var l in lines)
        {
            var i = l.Item;
            var total = l.Jumlah * i.Harga;
            ttl += total;
            await db.ExecAsync("""
                INSERT INTO detail_pengeluaran_obat_bhp (no_keluar,kode_brng,kode_sat,no_batch,jumlah,harga_beli,total,no_faktur)
                VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)
                """, no, i.Kode, i.KodeSat, i.NoBatch, l.Jumlah, i.Harga, total, i.NoFaktur);
            if (Batch)
                await db.ExecAsync("UPDATE data_batch SET sisa=sisa-@p0 WHERE no_batch=@p1 AND kode_brng=@p2 AND no_faktur=@p3", l.Jumlah, i.NoBatch, i.Kode, i.NoFaktur);
            await CatatAsync(db, i, 0, l.Jumlah, "Stok Keluar", nip, bangsal, $"{no}, {keterangan}");
            await TambahStokAsync(db, i, bangsal, -l.Jumlah);
        }
        if (ttl > 0)
        {
            var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun");
            await JurnalPoster.PostAsync(db.Conn, db.Tx!, no, $"STOK KELUAR BARANG MEDIS/OBAT/ALKES/BHP, OLEH {nip}",
            [
                new JurnalLine(akun.GetValueOrDefault("Kontra_Stok_Keluar_Medis", ""), "KONTRA PERSEDIAAN BARANG", ttl, 0),
                new JurnalLine(akun.GetValueOrDefault("Stok_Keluar_Medis", ""), "PERSEDIAAN BARANG", 0, ttl),
            ]);
        }
        if (noPermintaan is { Length: > 0 })
            await db.ExecAsync("UPDATE permintaan_medis SET status='Disetujui' WHERE no_permintaan=@p0", noPermintaan);
        await db.CommitAsync();
        return no;
    }

    public async Task<string> SimpanPermintaanAsync(string peminta, string tujuan, DateTime tanggal, IReadOnlyList<StokLine> lines, string nip)
    {
        if (peminta.Length == 0 || tujuan.Length == 0) throw new ArgumentException("Lokasi peminta dan tujuan wajib diisi.");
        if (peminta == tujuan) throw new ArgumentException("Lokasi peminta dan tujuan harus berbeda.");
        if (nip.Length == 0) throw new ArgumentException("Petugas wajib diisi.");
        CekBaris(lines, checkStok: false);

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var no = await NomorAsync(db, StokMode.Permintaan, tanggal);
        await db.ExecAsync("INSERT INTO permintaan_medis (no_permintaan,kd_bangsal,nip,tanggal,status,kd_bangsaltujuan) VALUES (@p0,@p1,@p2,@p3,'Baru',@p4)",
            no, peminta, nip, tanggal.Date, tujuan);
        foreach (var l in lines)
            await db.ExecAsync("INSERT INTO detail_permintaan_medis (no_permintaan,kode_brng,kode_sat,jumlah,keterangan) VALUES (@p0,@p1,@p2,@p3,@p4)",
                no, l.Item.Kode, l.Item.KodeSat, l.Jumlah, l.Keterangan.Replace("'", "").Replace("\"", ""));
        await db.CommitAsync();
        return no;
    }

    // ------------------------------------------------------------------ permintaan → mutasi / stok keluar

    public async Task<List<Row>> PermintaanBaruAsync(string kdBangsalTujuan)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT p.no_permintaan, p.tanggal, p.kd_bangsal, a.nm_bangsal AS peminta, pg.nama AS petugas,
            (SELECT COUNT(*) FROM detail_permintaan_medis d WHERE d.no_permintaan=p.no_permintaan) AS item
            FROM permintaan_medis p LEFT JOIN bangsal a ON a.kd_bangsal=p.kd_bangsal LEFT JOIN pegawai pg ON pg.nik=p.nip
            WHERE p.status='Baru' AND p.kd_bangsaltujuan=@p0 ORDER BY p.tanggal DESC LIMIT 200
            """, kdBangsalTujuan);
    }

    /// <summary>Items of a permintaan taken from stock at <paramref name="sumber"/>, earliest-expiry batch first.</summary>
    public async Task<(List<StokLine> Lines, List<string> Kurang)> AmbilPermintaanAsync(string noPermintaan, string sumber)
    {
        List<Row> detail;
        await using (var db = await Db.OpenAsync(settings))
            detail = await db.QueryAsync("""
                SELECT d.kode_brng, b.nama_brng, SUM(d.jumlah) AS jumlah FROM detail_permintaan_medis d JOIN databarang b ON b.kode_brng=d.kode_brng
                WHERE d.no_permintaan=@p0 GROUP BY d.kode_brng, b.nama_brng
                """, noPermintaan);
        var lines = new List<StokLine>();
        var kurang = new List<string>();
        foreach (var d in detail)
        {
            var need = d.D("jumlah");
            var stok = (await CariStokAsync(sumber, d.S("kode_brng"), false)).Where(s => s.Kode == d.S("kode_brng"))
                .OrderBy(s => s.Kadaluarsa ?? DateTime.MaxValue).ToList();
            foreach (var s in stok)
            {
                if (need <= 0) break;
                var take = Math.Min(need, s.Stok);
                lines.Add(new StokLine { Item = s, Jumlah = take });
                need -= take;
            }
            if (need > 0) kurang.Add($"{d.S("nama_brng")} kurang {need}");
        }
        return (lines, kurang);
    }

    // ------------------------------------------------------------------ permintaan stok obat pasien (ranap)

    private static readonly string JamCols = string.Join(",", Enumerable.Range(0, 24).Select(h => $"jam{h:00}"));

    public async Task<List<Row>> PermintaanStokPasienAsync(DateTime from, DateTime to, string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT h.no_permintaan, h.tgl_permintaan, h.jam, h.no_rawat, p.no_rkm_medis, p.nm_pasien, d.nm_dokter, h.status,
            IF(h.status='Sudah',CONCAT(h.tgl_validasi,' ',h.jam_validasi),'') AS validasi,
            (SELECT CONCAT(k.kd_kamar,' ',bs.nm_bangsal) FROM kamar_inap ki JOIN kamar k ON k.kd_kamar=ki.kd_kamar JOIN bangsal bs ON bs.kd_bangsal=k.kd_bangsal
             WHERE ki.no_rawat=COALESCE((SELECT rg.no_rawat FROM ranap_gabung rg WHERE rg.no_rawat2=h.no_rawat LIMIT 1),h.no_rawat) ORDER BY ki.tgl_masuk DESC, ki.jam_masuk DESC LIMIT 1) AS kamar,
            (SELECT COUNT(*) FROM detail_permintaan_stok_obat_pasien x WHERE x.no_permintaan=h.no_permintaan) AS item
            FROM permintaan_stok_obat_pasien h JOIN reg_periksa g ON g.no_rawat=h.no_rawat JOIN pasien p ON p.no_rkm_medis=g.no_rkm_medis
            LEFT JOIN dokter d ON d.kd_dokter=h.kd_dokter
            WHERE h.tgl_permintaan BETWEEN @p0 AND @p1 AND CONCAT_WS(' ',h.no_permintaan,h.no_rawat,p.no_rkm_medis,p.nm_pasien,d.nm_dokter,h.status) LIKE @p2
            ORDER BY h.status, h.tgl_permintaan DESC, h.jam DESC LIMIT 3000
            """, from.Date, to.Date.AddDays(1).AddSeconds(-1), $"%{cari?.Trim()}%");
    }

    public async Task<List<Row>> DetailStokPasienAsync(string noPermintaan)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync($"""
            SELECT x.kode_brng, b.nama_brng, s.satuan, x.jml, x.aturan_pakai,
            CONCAT_WS(',',{string.Join(",", Enumerable.Range(0, 24).Select(h => $"IF(x.jam{h:00}='true','{h:00}',NULL)"))}) AS jadwal
            FROM detail_permintaan_stok_obat_pasien x JOIN databarang b ON b.kode_brng=x.kode_brng LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            WHERE x.no_permintaan=@p0
            """, noPermintaan);
    }

    /// <summary>
    /// Validates a doctor's request: takes stock from the depo (earliest expiry first), writes stok_obat_pasien with the
    /// requested schedule, riwayat 'Stok Pasien Ranap', gudangbarang and data_batch — the inverse of the legacy delete.
    /// </summary>
    public async Task ValidasiStokPasienAsync(string noPermintaan, string depo, string petugas)
    {
        if (depo.Length == 0) throw new ArgumentException("Pilih depo/lokasi stok dulu.");
        await using var db = await Db.OpenAsync(settings);
        var h = await db.FirstAsync("SELECT no_rawat, status FROM permintaan_stok_obat_pasien WHERE no_permintaan=@p0", noPermintaan)
            ?? throw new InvalidOperationException("Permintaan tidak ditemukan.");
        if (h.S("status") == "Sudah") throw new InvalidOperationException("Permintaan ini sudah divalidasi.");
        var detail = await db.QueryAsync($"SELECT kode_brng, jml, aturan_pakai, {JamCols} FROM detail_permintaan_stok_obat_pasien WHERE no_permintaan=@p0", noPermintaan);
        if (detail.Count == 0) throw new InvalidOperationException("Permintaan tidak punya rincian obat.");

        // Plan batches before writing anything so a shortage aborts cleanly.
        var plan = new List<(Row D, StokItem Item, double Qty)>();
        var kurang = new List<string>();
        foreach (var d in detail)
        {
            var need = d.D("jml");
            foreach (var s in (await CariStokAsync(depo, d.S("kode_brng"), false)).Where(s => s.Kode == d.S("kode_brng")).OrderBy(s => s.Kadaluarsa ?? DateTime.MaxValue))
            {
                if (need <= 0) break;
                var take = Math.Min(need, s.Stok);
                plan.Add((d, s, take));
                need -= take;
            }
            if (need > 0) kurang.Add($"{d.S("kode_brng")} kurang {need}");
        }
        if (kurang.Count > 0) throw new InvalidOperationException("Stok depo tidak cukup: " + string.Join("; ", kurang));

        await db.BeginAsync();
        var now = Convert.ToDateTime(await db.ScalarAsync("SELECT NOW()"));
        var noRawat = h.S("no_rawat");
        foreach (var (d, i, qty) in plan)
        {
            var jam = Enumerable.Range(0, 24).Select(x => (object?)d.S($"jam{x:00}")).ToArray();
            await db.ExecAsync($"""
                INSERT INTO stok_obat_pasien (tanggal,jam,no_rawat,kode_brng,jumlah,kd_bangsal,no_batch,no_faktur,aturan_pakai,{JamCols})
                VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,{string.Join(",", Enumerable.Range(9, 24).Select(x => $"@p{x}"))})
                ON DUPLICATE KEY UPDATE jumlah=jumlah+@p4
                """, [now.Date, now.TimeOfDay, noRawat, i.Kode, qty, depo, i.NoBatch, i.NoFaktur, d.S("aturan_pakai"), .. jam]);
            await CatatAsync(db, i, 0, qty, "Stok Pasien Ranap", petugas, depo, $"Stok obat pasien {noRawat}, permintaan {noPermintaan}");
            if (i.NoBatch.Length > 0)
                await db.ExecAsync("UPDATE data_batch SET sisa=sisa-@p0 WHERE no_batch=@p1 AND kode_brng=@p2 AND no_faktur=@p3", qty, i.NoBatch, i.Kode, i.NoFaktur);
            await TambahStokAsync(db, i, depo, -qty);
        }
        await db.ExecAsync("UPDATE permintaan_stok_obat_pasien SET status='Sudah', tgl_validasi=CURRENT_DATE(), jam_validasi=CURRENT_TIME() WHERE no_permintaan=@p0", noPermintaan);
        await db.CommitAsync();
    }

    // ------------------------------------------------------------------ history

    public async Task<List<Row>> DaftarAsync(StokMode mode, DateTime from, DateTime to, string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        var sql = mode switch
        {
            StokMode.Mutasi => """
                SELECT m.tanggal, m.kode_brng, b.nama_brng, m.jml AS jumlah, m.harga, m.jml*m.harga AS total, a.nm_bangsal AS dari, k.nm_bangsal AS ke,
                m.no_batch, m.no_faktur, m.keterangan
                FROM mutasibarang m JOIN databarang b ON b.kode_brng=m.kode_brng LEFT JOIN bangsal a ON a.kd_bangsal=m.kd_bangsaldari
                LEFT JOIN bangsal k ON k.kd_bangsal=m.kd_bangsalke
                WHERE m.tanggal BETWEEN @p0 AND @p1 AND CONCAT_WS(' ',m.kode_brng,b.nama_brng,a.nm_bangsal,k.nm_bangsal,m.keterangan) LIKE @p2
                ORDER BY m.tanggal DESC LIMIT 5000
                """,
            StokMode.Opname => """
                SELECT o.tanggal, o.kode_brng, b.nama_brng, bg.nm_bangsal, o.stok, o.`real`, o.selisih, o.nomihilang, o.lebih, o.nomilebih, o.no_batch, o.keterangan
                FROM opname o JOIN databarang b ON b.kode_brng=o.kode_brng LEFT JOIN bangsal bg ON bg.kd_bangsal=o.kd_bangsal
                WHERE o.tanggal BETWEEN @p0 AND @p1 AND CONCAT_WS(' ',o.kode_brng,b.nama_brng,bg.nm_bangsal,o.keterangan) LIKE @p2
                ORDER BY o.tanggal DESC, bg.nm_bangsal, b.nama_brng LIMIT 5000
                """,
            StokMode.StokKeluar => """
                SELECT h.no_keluar, h.tanggal, bg.nm_bangsal, pt.nama AS petugas, d.kode_brng, b.nama_brng, d.jumlah, d.harga_beli, d.total, d.no_batch, h.keterangan
                FROM pengeluaran_obat_bhp h JOIN detail_pengeluaran_obat_bhp d ON d.no_keluar=h.no_keluar JOIN databarang b ON b.kode_brng=d.kode_brng
                LEFT JOIN bangsal bg ON bg.kd_bangsal=h.kd_bangsal LEFT JOIN petugas pt ON pt.nip=h.nip
                WHERE h.tanggal BETWEEN @p0 AND @p1 AND CONCAT_WS(' ',h.no_keluar,b.nama_brng,bg.nm_bangsal,h.keterangan) LIKE @p2
                ORDER BY h.tanggal DESC, h.no_keluar DESC LIMIT 5000
                """,
            _ => """
                SELECT p.no_permintaan, p.tanggal, a.nm_bangsal AS peminta, t.nm_bangsal AS tujuan, pg.nama AS petugas, p.status,
                d.kode_brng, b.nama_brng, d.jumlah, d.keterangan
                FROM permintaan_medis p JOIN detail_permintaan_medis d ON d.no_permintaan=p.no_permintaan JOIN databarang b ON b.kode_brng=d.kode_brng
                LEFT JOIN bangsal a ON a.kd_bangsal=p.kd_bangsal LEFT JOIN bangsal t ON t.kd_bangsal=p.kd_bangsaltujuan LEFT JOIN pegawai pg ON pg.nik=p.nip
                WHERE p.tanggal BETWEEN @p0 AND @p1 AND CONCAT_WS(' ',p.no_permintaan,a.nm_bangsal,t.nm_bangsal,b.nama_brng,p.status) LIKE @p2
                ORDER BY p.tanggal DESC, p.no_permintaan DESC LIMIT 5000
                """,
        };
        return await db.QueryAsync(sql, from.Date, to.Date.AddDays(1).AddSeconds(-1), $"%{cari?.Trim()}%");
    }
}
