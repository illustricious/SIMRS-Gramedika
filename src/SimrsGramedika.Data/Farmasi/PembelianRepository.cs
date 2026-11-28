using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Farmasi;

/// <summary>One line in legacy inventory/DlgPembelian (direct supplier purchase, paid from an account).</summary>
public sealed class PembelianLine : INotifyPropertyChanged
{
    public required string Kode { get; init; }
    public required string Nama { get; init; }
    public required string KdJns { get; init; }
    public required string SatKecil { get; init; }
    public required string NamaSatKecil { get; init; }
    public required string SatBesar { get; init; }
    public required string NamaSatBesar { get; init; }
    public required double Isi { get; init; }
    public required double[] HargaLama { get; init; }

    private double _jumlah = 1, _harga, _diskon;
    private bool _besar, _ubahHarga;
    private string _batch = "", _kadaluarsa = "";

    public double Jumlah { get => _jumlah; set { _jumlah = value; Changed(); } }
    public bool PakaiSatuanBesar
    {
        get => _besar;
        set
        {
            if (_besar == value) return;
            _besar = value;
            if (Isi > 0) _harga = value ? _harga * Isi : _harga / Isi;
            Changed();
        }
    }
    public string SatuanBeli => PakaiSatuanBesar ? NamaSatBesar : NamaSatKecil;
    public string KodeSatBeli => PakaiSatuanBesar ? SatBesar : SatKecil;
    public double Faktor => PakaiSatuanBesar && Isi > 0 ? Isi : 1;
    public double JumlahKecil => Jumlah * Faktor;
    public double Harga { get => _harga; set { _harga = value; Changed(); } }
    public double DiskonPersen { get => _diskon; set { _diskon = value; Changed(); } }
    public double Subtotal => Jumlah * Harga;
    public double BesarDiskon => Math.Round(Subtotal * DiskonPersen / 100, 2);
    public double Total => Subtotal - BesarDiskon;
    public string NoBatch { get => _batch; set { _batch = (value ?? "").Trim(); Changed(); } }
    public string Kadaluarsa { get => _kadaluarsa; set { _kadaluarsa = (value ?? "").Trim(); Changed(); } }
    public DateTime? TanggalKadaluarsa => DateTime.TryParseExact(Kadaluarsa,
        ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;
    public bool UbahHarga { get => _ubahHarga; set { _ubahHarga = value; Changed(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? property = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        foreach (var name in new[] { nameof(SatuanBeli), nameof(JumlahKecil), nameof(Subtotal), nameof(BesarDiskon), nameof(Total) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed record AkunPembelian(string Nama, string KdRek);
public sealed record PembelianHeader(string NoFaktur, string KodeSuplier, string Nip, DateTime Tanggal,
    string KdBangsal, string NamaBangsal, string NamaAkun, double PpnPersen);

/// <summary>
/// Directly paid purchase from legacy inventory/DlgPembelian. Writes pembelian/detailbeli,
/// increases stock, tracks batch and price changes, then posts Persediaan + PPN Masukan / payment account.
/// </summary>
public sealed class PembelianRepository(DatabaseSettings settings)
{
    private bool Batch => settings.AktifkanBatchObat;

    public static readonly IReadOnlyDictionary<string, string[]> WrittenColumns = new Dictionary<string, string[]>
    {
        ["pembelian"] = ["no_faktur", "kode_suplier", "nip", "tgl_beli", "total1", "potongan", "total2", "ppn", "tagihan", "kd_bangsal", "kd_rek"],
        ["detailbeli"] = ["no_faktur", "kode_brng", "kode_sat", "jumlah", "h_beli", "subtotal", "dis", "besardis", "total", "no_batch", "jumlah2", "kadaluarsa"],
        ["riwayat_barang_medis"] = ["kode_brng", "stok_awal", "masuk", "keluar", "stok_akhir", "posisi", "tanggal", "jam", "petugas", "kd_bangsal", "status", "no_batch", "no_faktur", "keterangan"],
        ["gudangbarang"] = ["kode_brng", "kd_bangsal", "stok", "no_batch", "no_faktur"],
        ["data_batch"] = ["no_batch", "kode_brng", "tgl_beli", "tgl_kadaluarsa", "asal", "no_faktur", "dasar", "h_beli", .. HargaObat.JualCols, "jumlahbeli", "sisa"],
        ["databarang"] = ["expire", "dasar", "h_beli", .. HargaObat.JualCols],
    };

    private const string SettingSql = "SELECT setharga,hargadasar,ppn FROM set_harga_obat LIMIT 1";
    private static HargaSetting Setting(Row? r) => r is null ? new HargaSetting("Umum", false, false)
        : new HargaSetting(r.S("setharga"), r.S("hargadasar") != "Harga Beli", r.S("ppn") == "Yes");

    public async Task<(List<(string Kode, string Nama)> Suplier, List<(string Kode, string Nama)> Petugas,
        List<(string Kode, string Nama)> Lokasi, List<AkunPembelian> Akun)> ReferensiAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        var suplier = (await db.QueryAsync("SELECT kode_suplier,nama_suplier FROM datasuplier ORDER BY nama_suplier"))
            .Select(r => (r.S("kode_suplier"), r.S("nama_suplier"))).ToList();
        var petugas = (await db.QueryAsync("SELECT nip,nama FROM petugas WHERE status='1' ORDER BY nama"))
            .Select(r => (r.S("nip"), r.S("nama"))).ToList();
        var lokasi = (await db.QueryAsync("SELECT kd_bangsal,nm_bangsal FROM bangsal WHERE status='1' ORDER BY nm_bangsal"))
            .Select(r => (r.S("kd_bangsal"), r.S("nm_bangsal"))).ToList();
        var akun = (await db.QueryAsync("SELECT nama_bayar,kd_rek FROM akun_bayar WHERE COALESCE(kd_rek,'')<>'' ORDER BY nama_bayar"))
            .Select(r => new AkunPembelian(r.S("nama_bayar"), r.S("kd_rek"))).ToList();
        return (suplier, petugas, lokasi, akun);
    }

    public async Task<string> NomorBerikutnyaAsync(DateTime tanggal)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.AutoNomorAsync(
            "SELECT IFNULL(MAX(CONVERT(RIGHT(no_faktur,3),SIGNED)),0) FROM pembelian WHERE tgl_beli=@p0",
            $"PG{tanggal:yyyyMMdd}", 3, tanggal.Date);
    }

    public async Task<List<PembelianLine>> CariBarangAsync(string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("""
            SELECT b.kode_brng,b.nama_brng,b.kdjns,b.kode_sat,s.satuan AS nm_kecil,b.kode_satbesar,sb.satuan AS nm_besar,
                   b.isi,b.h_beli,b.dasar,b.ralan,b.kelas1,b.kelas2,b.kelas3,b.utama,b.vip,b.vvip,b.beliluar,b.jualbebas,b.karyawan
            FROM databarang b LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat LEFT JOIN kodesatuan sb ON sb.kode_sat=b.kode_satbesar
            WHERE b.status='1' AND CONCAT_WS(' ',b.kode_brng,b.nama_brng) LIKE @p0 ORDER BY b.nama_brng LIMIT 300
            """, $"%{cari?.Trim()}%")).Select(r => new PembelianLine
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
            SELECT h.no_faktur,h.tgl_beli,s.nama_suplier,pt.nama AS petugas,bg.nm_bangsal,h.total1,h.potongan,h.total2,h.ppn,h.tagihan,ab.nama_bayar
            FROM pembelian h LEFT JOIN datasuplier s ON s.kode_suplier=h.kode_suplier LEFT JOIN petugas pt ON pt.nip=h.nip
            LEFT JOIN bangsal bg ON bg.kd_bangsal=h.kd_bangsal LEFT JOIN akun_bayar ab ON ab.kd_rek=h.kd_rek
            WHERE h.tgl_beli BETWEEN @p0 AND @p1 AND CONCAT_WS(' ',h.no_faktur,s.nama_suplier,pt.nama,bg.nm_bangsal,ab.nama_bayar) LIKE @p2
            ORDER BY h.tgl_beli DESC,h.no_faktur DESC LIMIT 3000
            """, from.Date, to.Date.AddDays(1).AddSeconds(-1), $"%{cari?.Trim()}%");
    }

    public static (double Subtotal, double Diskon, double Total, double Ppn, double Tagihan) Totals(IEnumerable<PembelianLine> lines, double ppnPersen)
    {
        var rows = lines.Where(l => l.Jumlah > 0).ToList();
        var subtotal = rows.Sum(l => l.Subtotal);
        var diskon = rows.Sum(l => l.BesarDiskon);
        var total = subtotal - diskon;
        var ppn = total * ppnPersen / 100;
        return (subtotal, diskon, total, ppn, total + ppn);
    }

    private static async Task<double[]> PersenAsync(Db db, HargaSetting set, PembelianLine l)
    {
        var (sql, arg) = set.Mode switch
        {
            "Per Jenis" => ("SELECT * FROM setpenjualan WHERE kdjns=@p0", l.KdJns),
            "Per Barang" => ("SELECT * FROM setpenjualanperbarang WHERE kode_brng=@p0", l.Kode),
            _ => ("SELECT * FROM setpenjualanumum LIMIT 1", ""),
        };
        var row = await db.FirstAsync(sql, arg)
            ?? throw new InvalidOperationException($"Pengaturan harga jual ({set.Mode}) untuk {l.Nama} tidak ditemukan.");
        return HargaObat.JualCols.Select(row.D).ToArray();
    }

    public async Task SimpanAsync(PembelianHeader h, IReadOnlyList<PembelianLine> lines, bool bolehUbahHarga, string petugasKode)
    {
        var no = h.NoFaktur.Trim();
        if (no.Length == 0) throw new ArgumentException("No.Faktur wajib diisi.");
        if (h.KodeSuplier.Length == 0 || h.Nip.Length == 0 || h.KdBangsal.Length == 0 || h.NamaAkun.Length == 0)
            throw new ArgumentException("Suplier, petugas, lokasi, dan akun pembayaran wajib dipilih dari daftar.");
        if (h.PpnPersen is < 0 or > 100) throw new ArgumentException("PPN harus berada antara 0 dan 100 persen.");
        var rows = lines.Where(l => l.Jumlah > 0).ToList();
        if (rows.Count == 0) throw new ArgumentException("Masukkan minimal satu barang dengan jumlah lebih dari nol.");
        foreach (var l in rows)
        {
            if (l.Harga < 0 || l.DiskonPersen is < 0 or > 100) throw new ArgumentException($"Harga/diskon {l.Nama} tidak valid.");
            if (l.KodeSatBeli.Length == 0) throw new ArgumentException($"Satuan beli {l.Nama} belum dipilih/tersedia.");
            if (l.Kadaluarsa.Length > 0 && l.TanggalKadaluarsa is null) throw new ArgumentException($"Tanggal kadaluarsa {l.Nama} harus dd/MM/yyyy.");
            if (Batch && (l.NoBatch.Length == 0 || l.TanggalKadaluarsa is null)) throw new ArgumentException($"No.Batch dan kadaluarsa {l.Nama} wajib diisi (batch obat aktif).");
            if (l.UbahHarga && !bolehUbahHarga) throw new InvalidOperationException("Akun ini tidak punya hak mengubah harga obat.");
        }
        if (rows.GroupBy(l => (l.Kode, Batch ? l.NoBatch : "")).Any(g => g.Count() > 1))
            throw new ArgumentException("Barang dan nomor batch yang sama muncul lebih dari sekali; gabungkan barisnya.");
        var totals = Totals(rows, h.PpnPersen);
        if (totals.Total <= 0) throw new ArgumentException("Total pembelian harus lebih dari nol.");

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var supplier = await db.StrAsync("SELECT nama_suplier FROM datasuplier WHERE kode_suplier=@p0", h.KodeSuplier);
        var petugasValid = await db.CountAsync("SELECT COUNT(*) FROM petugas WHERE nip=@p0 AND status='1'", h.Nip);
        var lokasi = await db.StrAsync("SELECT nm_bangsal FROM bangsal WHERE kd_bangsal=@p0 AND status='1'", h.KdBangsal);
        var rekening = await db.StrAsync("SELECT kd_rek FROM akun_bayar WHERE nama_bayar=@p0 AND COALESCE(kd_rek,'')<>''", h.NamaAkun);
        if (supplier.Length == 0 || petugasValid != 1 || lokasi.Length == 0 || rekening.Length == 0)
            throw new InvalidOperationException("Suplier, petugas, lokasi, atau akun pembayaran sudah tidak valid. Muat ulang daftar.");
        if (await db.CountAsync("SELECT COUNT(*) FROM pembelian WHERE no_faktur=@p0", no) > 0)
            throw new InvalidOperationException($"No.Faktur {no} sudah pernah disimpan.");
        var hargaSetting = Setting(await db.FirstAsync(SettingSql));
        await db.ExecAsync("""
            INSERT INTO pembelian(no_faktur,kode_suplier,nip,tgl_beli,total1,potongan,total2,ppn,tagihan,kd_bangsal,kd_rek)
            VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10)
            """, no, h.KodeSuplier, h.Nip, h.Tanggal.Date, totals.Subtotal, totals.Diskon, totals.Total,
            totals.Ppn, totals.Tagihan, h.KdBangsal, rekening);

        foreach (var l in rows)
        {
            var batch = Batch ? l.NoBatch : "";
            var fakturStok = Batch ? no : "";
            var ed = l.TanggalKadaluarsa;
            await db.ExecAsync("""
                INSERT INTO detailbeli(no_faktur,kode_brng,kode_sat,jumlah,h_beli,subtotal,dis,besardis,total,no_batch,jumlah2,kadaluarsa)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11)
                """, no, l.Kode, l.KodeSatBeli, l.Jumlah, l.Harga, l.Subtotal, l.DiskonPersen, l.BesarDiskon, l.Total,
                batch, l.JumlahKecil, ed);

            var awal = Convert.ToDouble(await db.ScalarAsync("""
                SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3 FOR UPDATE
                """, l.Kode, h.KdBangsal, batch, fakturStok) ?? 0d);
            await db.ExecAsync("""
                INSERT INTO riwayat_barang_medis(kode_brng,stok_awal,masuk,keluar,stok_akhir,posisi,tanggal,jam,petugas,kd_bangsal,status,no_batch,no_faktur,keterangan)
                VALUES(@p0,@p1,@p2,0,@p3,'Pengadaan',@p4,CURRENT_TIME(),@p5,@p6,'Simpan',@p7,@p8,@p9)
                """, l.Kode, awal, l.JumlahKecil, awal + l.JumlahKecil, h.Tanggal.Date, petugasKode, h.KdBangsal,
                batch, fakturStok, $"{no} {supplier}"[..Math.Min(150, no.Length + supplier.Length + 1)]);
            await db.ExecAsync("""
                INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES(@p0,@p1,@p2,@p3,@p4)
                ON DUPLICATE KEY UPDATE stok=stok+@p2
                """, l.Kode, h.KdBangsal, l.JumlahKecil, batch, fakturStok);

            var hargaBaru = l.HargaLama;
            if (l.UbahHarga)
            {
                var (hBeli, dasar, jual) = HargaObat.Hitung(l.Harga, l.Faktor, l.Total, l.JumlahKecil,
                    h.PpnPersen, hargaSetting, await PersenAsync(db, hargaSetting, l));
                hargaBaru = [dasar, hBeli, .. jual];
                await db.ExecAsync($"""
                    UPDATE databarang SET expire=COALESCE(@p0,expire), dasar=@p1, h_beli=@p2,
                    {string.Join(",", HargaObat.JualCols.Select((c, i) => $"{c}=@p{i + 3}"))}
                    WHERE kode_brng=@p{HargaObat.JualCols.Length + 3}
                    """, [ed, .. hargaBaru.Cast<object?>(), l.Kode]);
            }
            if (Batch)
                await db.ExecAsync($"""
                    INSERT INTO data_batch(no_batch,kode_brng,tgl_beli,tgl_kadaluarsa,asal,no_faktur,dasar,h_beli,{string.Join(",", HargaObat.JualCols)},jumlahbeli,sisa)
                    VALUES(@p0,@p1,@p2,@p3,'Pengadaan',@p4,{string.Join(",", Enumerable.Range(5, 12).Select(i => $"@p{i}"))},@p17,@p17)
                    """, [batch, l.Kode, h.Tanggal.Date, ed, no, .. hargaBaru.Cast<object?>(), l.JumlahKecil]);
        }

        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun");
        var jurnal = new List<JurnalLine>
        {
            new(akun.GetValueOrDefault("Pengadaan_Obat", ""), "PEMBELIAN", totals.Total, 0),
        };
        if (totals.Ppn > 0) jurnal.Add(new(akun.GetValueOrDefault("PPN_Masukan", ""), "PPN Masukan Obat", totals.Ppn, 0));
        jurnal.Add(new(rekening, h.NamaAkun, 0, totals.Tagihan));
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, no, $"PEMBELIAN DI {lokasi.ToUpperInvariant()}, OLEH {petugasKode}", jurnal);
        await db.CommitAsync();
    }
}
