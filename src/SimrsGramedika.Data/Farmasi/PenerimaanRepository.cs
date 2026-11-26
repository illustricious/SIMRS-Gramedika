using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Farmasi;

/// <summary>Legacy set_harga_obat: how sale prices follow the purchase price.</summary>
public sealed record HargaSetting(string Mode, bool DasarDiskon, bool PakaiPpn);

/// <summary>Price maths of legacy DlgPemesanan.setKonversi, kept pure so it can be tested.</summary>
public static class HargaObat
{
    /// <summary>databarang/data_batch sale-price columns in the order of the setpenjualan* percentages.</summary>
    public static readonly string[] JualCols = ["ralan", "kelas1", "kelas2", "kelas3", "utama", "vip", "vvip", "beliluar", "jualbebas", "karyawan"];

    /// <summary>Legacy Valid.roundUp(x, 100).</summary>
    public static double RoundUp(double v, double step = 100) => Math.Ceiling(Math.Round(v, 6) / step) * step;

    /// <summary>
    /// Per small unit: h_beli = purchase price (+PPN when set), dasar = discounted price (+PPN), and the 10 sale
    /// prices = basis + basis×pct%, rounded up to 100 (basis = h_beli, or dasar when set_harga_obat says "Harga Diskon").
    /// </summary>
    public static (double HBeli, double Dasar, double[] Jual) Hitung(double hargaSatuanBeli, double faktor, double totalBaris, double jumlahKecil,
        double ppnPersen, HargaSetting set, double[] persen)
    {
        var ppn = set.PakaiPpn ? 1 + ppnPersen / 100 : 1;
        var hBeli = hargaSatuanBeli / faktor * ppn;
        var dasar = jumlahKecil > 0 ? totalBaris / jumlahKecil * ppn : hBeli;
        var basis = set.DasarDiskon ? dasar : hBeli;
        return (Math.Round(hBeli), Math.Round(dasar), persen.Select(p => RoundUp(basis + basis * p / 100)).ToArray());
    }
}

/// <summary>One line of a penerimaan (legacy DlgPemesanan table row).</summary>
public sealed class PenerimaanLine : INotifyPropertyChanged
{
    public required string Kode { get; init; }
    public required string Nama { get; init; }
    public required string KdJns { get; init; }
    public required string SatKecil { get; init; }
    public required string NamaSatKecil { get; init; }
    public required string SatBesar { get; init; }
    public required string NamaSatBesar { get; init; }
    public required double Isi { get; init; }
    /// <summary>Current databarang prices: dasar, h_beli, then <see cref="HargaObat.JualCols"/>.</summary>
    public required double[] HargaLama { get; init; }

    private double _jumlah = 1, _harga, _disk;
    private bool _besar, _ubah;
    private string _batch = "", _ed = "";

    public double Jumlah { get => _jumlah; set { _jumlah = value; Changed(); } }
    public bool PakaiSatuanBesar { get => _besar; set { _besar = value; Changed(); } }
    public double Harga { get => _harga; set { _harga = value; Changed(); } }
    public double DiskonPersen { get => _disk; set { _disk = value; Changed(); } }
    public string NoBatch { get => _batch; set { _batch = (value ?? "").Trim(); Changed(); } }
    /// <summary>dd/MM/yyyy as typed in the grid.</summary>
    public string Kadaluarsa { get => _ed; set { _ed = (value ?? "").Trim(); Changed(); } }
    public bool UbahHarga { get => _ubah; set { _ubah = value; Changed(); } }

    public string SatuanBeli => PakaiSatuanBesar ? NamaSatBesar : NamaSatKecil;
    public string KodeSatBeli => PakaiSatuanBesar ? SatBesar : SatKecil;
    public double Faktor => PakaiSatuanBesar && Isi > 0 ? Isi : 1;
    public double JumlahKecil => Jumlah * Faktor;
    public double Subtotal => Jumlah * Harga;
    public double BesarDiskon => Math.Round(Subtotal * DiskonPersen / 100, 2);
    public double Total => Subtotal - BesarDiskon;

    public DateTime? TanggalKadaluarsa =>
        DateTime.TryParseExact(Kadaluarsa, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? p = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        foreach (var n in new[] { nameof(SatuanBeli), nameof(JumlahKecil), nameof(Subtotal), nameof(BesarDiskon), nameof(Total) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

public sealed record PenerimaanHeader(string NoFaktur, string NoOrder, string KodeSuplier, string Nip, DateTime TglTerima, DateTime TglFaktur,
    DateTime TglTempo, string KdBangsal, string NamaBangsal, double PpnPersen, double Meterai);

/// <summary>
/// Penerimaan obat/BHP dari suplier (legacy DlgPemesanan): pemesanan + detailpesan, stock into the gudang
/// (gudangbarang + riwayat 'Penerimaan'), data_batch, optional new sale prices on databarang, and the
/// jurnal Persediaan (+PPN Masukan) / Hutang Usaha.
/// </summary>
public class PenerimaanRepository(DatabaseSettings settings)
{
    private bool Batch => settings.AktifkanBatchObat;

    public static readonly IReadOnlyDictionary<string, string[]> WrittenColumns = new Dictionary<string, string[]>
    {
        ["pemesanan"] = ["no_faktur", "no_order", "kode_suplier", "nip", "tgl_pesan", "tgl_faktur", "tgl_tempo", "total1", "potongan", "total2", "ppn", "meterai", "tagihan", "kd_bangsal", "status"],
        ["detailpesan"] = ["no_faktur", "kode_brng", "kode_sat", "jumlah", "h_pesan", "subtotal", "dis", "besardis", "total", "no_batch", "jumlah2", "kadaluarsa"],
        ["data_batch"] = ["no_batch", "kode_brng", "tgl_beli", "tgl_kadaluarsa", "asal", "no_faktur", "dasar", "h_beli", .. HargaObat.JualCols, "jumlahbeli", "sisa"],
        ["databarang"] = ["expire", "dasar", "h_beli", .. HargaObat.JualCols],
    };

    private const string SettingSql = "SELECT setharga, hargadasar, ppn FROM set_harga_obat LIMIT 1";

    private static HargaSetting Setting(Row? r) => r is null ? new HargaSetting("Umum", false, false)
        : new HargaSetting(r.S("setharga"), r.S("hargadasar") != "Harga Beli", r.S("ppn") == "Yes");

    public async Task<HargaSetting> SettingAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        return Setting(await db.FirstAsync(SettingSql));
    }

    public async Task<List<(string Kode, string Nama)>> SuplierAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("SELECT kode_suplier, nama_suplier FROM datasuplier ORDER BY nama_suplier"))
            .Select(r => (r.S("kode_suplier"), r.S("nama_suplier"))).ToList();
    }

    public async Task<List<PenerimaanLine>> CariBarangAsync(string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("""
            SELECT b.*, s.satuan AS nm_kecil, sb.satuan AS nm_besar FROM databarang b
            LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat LEFT JOIN kodesatuan sb ON sb.kode_sat=b.kode_satbesar
            WHERE b.status='1' AND CONCAT_WS(' ',b.kode_brng,b.nama_brng) LIKE @p0 ORDER BY b.nama_brng LIMIT 300
            """, $"%{cari?.Trim()}%")).Select(r => new PenerimaanLine
        {
            Kode = r.S("kode_brng"), Nama = r.S("nama_brng"), KdJns = r.S("kdjns"),
            SatKecil = r.S("kode_sat"), NamaSatKecil = r.S("nm_kecil"), SatBesar = r.S("kode_satbesar"), NamaSatBesar = r.S("nm_besar"),
            Isi = r.D("isi"), Harga = r.D("h_beli"),
            HargaLama = [r.D("dasar"), r.D("h_beli"), .. HargaObat.JualCols.Select(r.D)],
        }).ToList();
    }

    public async Task<List<Row>> DaftarAsync(DateTime from, DateTime to, string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT p.no_faktur, p.no_order, p.tgl_pesan, p.tgl_faktur, p.tgl_tempo, sp.nama_suplier, bg.nm_bangsal, pt.nama AS petugas,
            p.total1, p.potongan, p.total2, p.ppn, p.meterai, p.tagihan, p.status
            FROM pemesanan p LEFT JOIN datasuplier sp ON sp.kode_suplier=p.kode_suplier LEFT JOIN bangsal bg ON bg.kd_bangsal=p.kd_bangsal
            LEFT JOIN petugas pt ON pt.nip=p.nip
            WHERE p.tgl_pesan BETWEEN @p0 AND @p1 AND CONCAT_WS(' ',p.no_faktur,p.no_order,sp.nama_suplier,bg.nm_bangsal,p.status) LIKE @p2
            ORDER BY p.tgl_pesan DESC, p.no_faktur
            """, from.Date, to.Date.AddDays(1).AddSeconds(-1), $"%{cari?.Trim()}%");
    }

    public static (double Subtotal, double Diskon, double Total, double Ppn, double Tagihan) Totals(IEnumerable<PenerimaanLine> lines, double ppnPersen, double meterai)
    {
        var l = lines.ToList();
        var sub = l.Sum(x => x.Subtotal);
        var disk = l.Sum(x => x.BesarDiskon);
        var ttl = sub - disk;
        var ppn = ppnPersen / 100 * ttl;
        return (sub, disk, ttl, ppn, ttl + ppn + meterai);
    }

    private static async Task<double[]> PersenAsync(Db db, HargaSetting set, PenerimaanLine l)
    {
        var (sql, arg) = set.Mode switch
        {
            "Per Jenis" => ("SELECT * FROM setpenjualan WHERE kdjns=@p0", l.KdJns),
            "Per Barang" => ("SELECT * FROM setpenjualanperbarang WHERE kode_brng=@p0", l.Kode),
            _ => ("SELECT * FROM setpenjualanumum LIMIT 1", ""),
        };
        var r = await db.FirstAsync(sql, arg)
            ?? throw new InvalidOperationException($"Pengaturan harga jual ({set.Mode}) untuk {l.Nama} tidak ditemukan. Hilangkan centang \"Ubah Harga\" atau lengkapi pengaturannya.");
        return HargaObat.JualCols.Select(r.D).ToArray();
    }

    public async Task SimpanAsync(PenerimaanHeader h, IReadOnlyList<PenerimaanLine> lines, bool bolehUbahHarga, string petugasKode)
    {
        if (h.NoFaktur.Trim().Length == 0) throw new ArgumentException("No.Faktur wajib diisi.");
        if (h.KodeSuplier.Length == 0) throw new ArgumentException("Suplier wajib diisi.");
        if (h.Nip.Length == 0) throw new ArgumentException("Petugas wajib diisi.");
        if (h.KdBangsal.Length == 0) throw new ArgumentException("Lokasi penerimaan wajib diisi.");
        var rows = lines.Where(l => l.Jumlah > 0).ToList();
        if (rows.Count == 0) throw new ArgumentException("Belum ada barang dengan jumlah > 0.");
        foreach (var l in rows)
        {
            if (l.Harga < 0 || l.DiskonPersen is < 0 or > 100) throw new ArgumentException($"Harga/diskon {l.Nama} tidak valid.");
            if (l.Kadaluarsa.Length > 0 && l.TanggalKadaluarsa is null) throw new ArgumentException($"Tanggal kadaluarsa {l.Nama} harus dd/MM/yyyy.");
            if (Batch && (l.NoBatch.Length == 0 || l.TanggalKadaluarsa is null)) throw new ArgumentException($"No.Batch dan kadaluarsa {l.Nama} wajib diisi (batch obat aktif).");
            if (l.UbahHarga && !bolehUbahHarga) throw new InvalidOperationException("Anda tidak punya hak akses mengubah harga obat; hilangkan centang \"Ubah Harga\".");
        }
        if (rows.GroupBy(l => (l.Kode, l.NoBatch)).Any(g => g.Count() > 1))
            throw new ArgumentException("Barang yang sama dengan No.Batch yang sama muncul dua kali; gabungkan barisnya.");
        var t = Totals(rows, h.PpnPersen, h.Meterai);

        await using var db = await Db.OpenAsync(settings);
        if (await db.CountAsync("SELECT COUNT(*) FROM pemesanan WHERE no_faktur=@p0", h.NoFaktur.Trim()) > 0)
            throw new InvalidOperationException($"No.Faktur {h.NoFaktur} sudah pernah disimpan.");
        var set = Setting(await db.FirstAsync(SettingSql));
        await db.BeginAsync();
        var no = h.NoFaktur.Trim();
        await db.ExecAsync("""
            INSERT INTO pemesanan (no_faktur,no_order,kode_suplier,nip,tgl_pesan,tgl_faktur,tgl_tempo,total1,potongan,total2,ppn,meterai,tagihan,kd_bangsal,status)
            VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,'Belum Dibayar')
            """, no, h.NoOrder.Trim(), h.KodeSuplier, h.Nip, h.TglTerima.Date, h.TglFaktur.Date, h.TglTempo.Date,
            t.Subtotal, t.Diskon, t.Total, t.Ppn, h.Meterai, t.Tagihan, h.KdBangsal);

        var ket = $"{no} {h.NoOrder}".Trim();
        foreach (var l in rows)
        {
            await db.ExecAsync("""
                INSERT INTO detailpesan (no_faktur,kode_brng,kode_sat,jumlah,h_pesan,subtotal,dis,besardis,total,no_batch,jumlah2,kadaluarsa)
                VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11)
                """, no, l.Kode, l.KodeSatBeli, l.Jumlah, l.Harga, l.Subtotal, l.DiskonPersen, l.BesarDiskon, l.Total, l.NoBatch, l.JumlahKecil, l.TanggalKadaluarsa);

            var item = new StokItem(l.Kode, l.Nama, l.SatKecil, l.NamaSatKecil, Batch ? l.NoBatch : "", Batch ? no : "", 0, 0, l.TanggalKadaluarsa);
            var awal = Convert.ToDouble(await db.ScalarAsync(
                "SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3",
                item.Kode, h.KdBangsal, item.NoBatch, item.NoFaktur) ?? 0d);
            await db.ExecAsync("""
                INSERT INTO riwayat_barang_medis (kode_brng,stok_awal,masuk,keluar,stok_akhir,posisi,tanggal,jam,petugas,kd_bangsal,status,no_batch,no_faktur,keterangan)
                VALUES (@p0,@p1,@p2,0,@p3,'Penerimaan',CURRENT_DATE(),CURRENT_TIME(),@p4,@p5,'Simpan',@p6,@p7,@p8)
                """, item.Kode, awal, l.JumlahKecil, awal + l.JumlahKecil, petugasKode, h.KdBangsal, item.NoBatch, item.NoFaktur, ket.Length > 150 ? ket[..150] : ket);
            await db.ExecAsync("""
                INSERT INTO gudangbarang (kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES (@p0,@p1,@p2,@p3,@p4)
                ON DUPLICATE KEY UPDATE stok=stok+@p2
                """, item.Kode, h.KdBangsal, l.JumlahKecil, item.NoBatch, item.NoFaktur);

            // Prices: new ones from the setting when "Ubah Harga" is ticked, otherwise the barang's current prices.
            double[] harga = l.HargaLama; // dasar, h_beli, jual...
            if (l.UbahHarga)
            {
                var (hBeli, dasar, jual) = HargaObat.Hitung(l.Harga, l.Faktor, l.Total, l.JumlahKecil, h.PpnPersen, set, await PersenAsync(db, set, l));
                harga = [dasar, hBeli, .. jual];
                await db.ExecAsync($"""
                    UPDATE databarang SET expire=IFNULL(@p0,expire), dasar=@p1, h_beli=@p2, {string.Join(",", HargaObat.JualCols.Select((c, i) => $"{c}=@p{i + 3}"))}
                    WHERE kode_brng=@p{HargaObat.JualCols.Length + 3}
                    """, [l.TanggalKadaluarsa, .. harga.Cast<object?>(), l.Kode]);
            }
            if (l.NoBatch.Length > 0 && l.TanggalKadaluarsa is { } ed)
                await db.ExecAsync($"""
                    INSERT INTO data_batch (no_batch,kode_brng,tgl_beli,tgl_kadaluarsa,asal,no_faktur,dasar,h_beli,{string.Join(",", HargaObat.JualCols)},jumlahbeli,sisa)
                    VALUES (@p0,@p1,@p2,@p3,'Penerimaan',@p4,{string.Join(",", Enumerable.Range(5, 12).Select(i => $"@p{i}"))},@p17,@p17)
                    """, [l.NoBatch, l.Kode, h.TglTerima.Date, ed, no, .. harga.Cast<object?>(), l.JumlahKecil]);
        }

        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun");
        List<JurnalLine> jurnal = [new(akun.GetValueOrDefault("Pemesanan_Obat", ""), "PERSEDIAAN BARANG", t.Total + h.Meterai, 0)];
        if (t.Ppn > 0) jurnal.Add(new(akun.GetValueOrDefault("PPN_Masukan", ""), "PPN Masukan Obat", t.Ppn, 0));
        jurnal.Add(new(akun.GetValueOrDefault("Kontra_Pemesanan_Obat", ""), "HUTANG USAHA", 0, t.Tagihan));
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, no, $"PENERIMAAN BARANG DI {h.NamaBangsal.ToUpperInvariant()}, OLEH {petugasKode}", jurnal);
        await db.CommitAsync();
    }
}
