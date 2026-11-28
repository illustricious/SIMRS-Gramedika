using System.ComponentModel;
using System.Runtime.CompilerServices;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Farmasi;

public sealed class ReturBeliLine : INotifyPropertyChanged
{
    private double _jumlah;
    public required string NoFaktur { get; init; }
    public required string Kode { get; init; }
    public required string Nama { get; init; }
    public required string KodeSat { get; init; }
    public required string Satuan { get; init; }
    public required double JumlahBeli { get; init; }
    public required double JumlahSudahDiretur { get; init; }
    public required double SisaRetur { get; init; }
    public required double Stok { get; init; }
    public required double HargaKecil { get; init; }
    public required string NoBatch { get; init; }
    public required string NoFakturStok { get; init; }
    public required DateTime? Kadaluarsa { get; init; }
    public double JumlahRetur { get => _jumlah; set { _jumlah = value; Changed(); } }
    public double Subtotal => JumlahRetur * HargaKecil;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new(name));
        PropertyChanged?.Invoke(this, new(nameof(Subtotal)));
    }
}

public sealed record ReturBeliHeader(string NoRetur, string Nip, DateTime Tanggal);

/// <summary>Retur supplier for items originally purchased in a supplier invoice.</summary>
public sealed class ReturBeliRepository(DatabaseSettings settings)
{
    private bool Batch => settings.AktifkanBatchObat;
    public const string Permission = "retur_ke_suplier";

    public async Task<List<(string Kode, string Nama)>> PetugasAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("SELECT nip,nama FROM petugas WHERE status='1' ORDER BY nama"))
            .Select(r => (r.S("nip"), r.S("nama"))).ToList();
    }

    public async Task<string> NomorBerikutnyaAsync(DateTime tanggal)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.AutoNomorAsync("SELECT IFNULL(MAX(CONVERT(RIGHT(no_retur_beli,3),SIGNED)),0) FROM returbeli WHERE tgl_retur=@p0",
            $"RB{tanggal:yyyyMMdd}", 3, tanggal.Date);
    }

    public async Task<List<Row>> CariFakturAsync(string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT h.no_faktur,h.tgl_beli,s.kode_suplier,s.nama_suplier,h.kd_bangsal,bg.nm_bangsal,h.nip,pt.nama AS petugas
            FROM pembelian h JOIN datasuplier s ON s.kode_suplier=h.kode_suplier
            JOIN bangsal bg ON bg.kd_bangsal=h.kd_bangsal LEFT JOIN petugas pt ON pt.nip=h.nip
            WHERE CONCAT_WS(' ',h.no_faktur,s.nama_suplier,bg.nm_bangsal) LIKE @p0
            ORDER BY h.tgl_beli DESC,h.no_faktur DESC LIMIT 300
            """, $"%{cari?.Trim()}%");
    }

    public async Task<List<ReturBeliLine>> BarangTersediaAsync(string noFaktur)
    {
        await using var db = await Db.OpenAsync(settings);
        var f = await db.FirstAsync("SELECT kd_bangsal FROM pembelian WHERE no_faktur=@p0", noFaktur)
            ?? throw new InvalidOperationException("Faktur pembelian tidak ditemukan.");
        var lokasi = f.S("kd_bangsal");
        var rows = await db.QueryAsync("""
            SELECT d.no_faktur,d.kode_brng,b.nama_brng,b.kode_sat,s.satuan,d.kode_sat AS sat_beli,
                   d.jumlah,d.jumlah2,d.h_beli,d.no_batch,d.kadaluarsa,
                   CASE WHEN @p2='yes' THEN d.no_faktur ELSE '' END AS faktur_stok,
                   COALESCE(g.stok,0) AS stok,COALESCE(r.jml,0) AS sudah_retur
            FROM detailbeli d JOIN databarang b ON b.kode_brng=d.kode_brng
            LEFT JOIN kodesatuan s ON s.kode_sat=b.kode_sat
            LEFT JOIN gudangbarang g ON g.kode_brng=d.kode_brng AND g.kd_bangsal=@p1
              AND g.no_batch=IF(@p2='yes',d.no_batch,'') AND g.no_faktur=IF(@p2='yes',d.no_faktur,'')
            LEFT JOIN (SELECT no_faktur,kode_brng,no_batch,SUM(jml_retur2) AS jml FROM detreturbeli GROUP BY no_faktur,kode_brng,no_batch) r
              ON r.no_faktur=d.no_faktur AND r.kode_brng=d.kode_brng AND r.no_batch=d.no_batch
            WHERE d.no_faktur=@p0
            ORDER BY b.nama_brng,d.no_batch
            """, noFaktur, lokasi, Batch ? "yes" : "no");
        return rows.Select(r =>
        {
            var qtyBeli = r.D("jumlah2");
            var qtyUnit = r.D("jumlah");
            var factor = qtyUnit > 0 && qtyBeli > 0 ? qtyBeli / qtyUnit : 1;
            var bought = qtyBeli > 0 ? qtyBeli : qtyUnit * factor;
            var returned = r.D("sudah_retur");
            var remaining = Math.Max(0, bought - returned);
            return new ReturBeliLine
            {
                NoFaktur = r.S("no_faktur"), Kode = r.S("kode_brng"), Nama = r.S("nama_brng"),
                KodeSat = r.S("kode_sat"), Satuan = r.S("satuan"), JumlahBeli = bought,
                JumlahSudahDiretur = returned, SisaRetur = remaining, Stok = r.D("stok"),
                HargaKecil = factor > 0 ? r.D("h_beli") / factor : r.D("h_beli"),
                NoBatch = Batch ? r.S("no_batch") : "", NoFakturStok = Batch ? r.S("no_faktur") : "",
                Kadaluarsa = r.Dt("kadaluarsa"),
            };
        }).Where(l => l.SisaRetur > 0 && l.Stok > 0).ToList();
    }

    public async Task<List<Row>> DaftarAsync(DateTime from, DateTime to, string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT h.no_retur_beli,h.tgl_retur,s.nama_suplier,p.nama AS petugas,bg.nm_bangsal,
                   COUNT(d.kode_brng) AS jumlah_item,SUM(d.total) AS total
            FROM returbeli h JOIN detreturbeli d ON d.no_retur_beli=h.no_retur_beli
            LEFT JOIN datasuplier s ON s.kode_suplier=h.kode_suplier LEFT JOIN petugas p ON p.nip=h.nip
            LEFT JOIN bangsal bg ON bg.kd_bangsal=h.kd_bangsal
            WHERE h.tgl_retur BETWEEN @p0 AND @p1
              AND CONCAT_WS(' ',h.no_retur_beli,s.nama_suplier,p.nama,bg.nm_bangsal) LIKE @p2
            GROUP BY h.no_retur_beli,h.tgl_retur,s.nama_suplier,p.nama,bg.nm_bangsal
            ORDER BY h.tgl_retur DESC,h.no_retur_beli DESC LIMIT 3000
            """, from.Date, to.Date, $"%{cari?.Trim()}%");
    }

    public async Task SimpanAsync(ReturBeliHeader header, IReadOnlyList<ReturBeliLine> lines, string petugasKode)
    {
        var no = header.NoRetur.Trim();
        if (no.Length == 0 || header.Nip.Length == 0) throw new ArgumentException("Nomor retur dan petugas wajib diisi.");
        var items = lines.Where(x => x.JumlahRetur > 0).ToList();
        if (items.Count == 0) throw new ArgumentException("Isi jumlah retur minimal satu barang.");
        if (items.Select(x => x.NoFaktur).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new ArgumentException("Satu dokumen retur hanya boleh mengacu ke satu faktur pembelian.");
        if (items.GroupBy(x => (x.NoFaktur, x.Kode, x.NoBatch)).Any(g => g.Count() > 1))
            throw new ArgumentException("Baris barang dan batch yang sama muncul lebih dari sekali.");
        foreach (var l in items)
            if (l.JumlahRetur > l.SisaRetur || l.JumlahRetur > l.Stok || l.HargaKecil < 0)
                throw new ArgumentException($"Jumlah retur {l.Nama} melebihi sisa faktur atau stok saat layar dimuat.");

        var total = items.Sum(x => x.Subtotal);
        if (total <= 0) throw new ArgumentException("Total retur harus lebih dari nol.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var purchase = await db.FirstAsync("SELECT kode_suplier,kd_bangsal FROM pembelian WHERE no_faktur=@p0 FOR UPDATE", items[0].NoFaktur)
            ?? throw new InvalidOperationException("Faktur pembelian asal sudah tidak ditemukan.");
        var supplier = purchase.S("kode_suplier");
        var lokasi = purchase.S("kd_bangsal");
        if (await db.CountAsync("SELECT COUNT(*) FROM petugas WHERE nip=@p0 AND status='1'", header.Nip) != 1)
            throw new InvalidOperationException("Petugas tidak aktif atau tidak ditemukan.");
        if (await db.CountAsync("SELECT COUNT(*) FROM returbeli WHERE no_retur_beli=@p0", no) != 0)
            throw new InvalidOperationException($"Nomor retur {no} sudah digunakan.");
        var setting = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun");
        var akunRetur = setting.GetValueOrDefault("Retur_Ke_Suplayer", "");
        var akunKontra = setting.GetValueOrDefault("Kontra_Retur_Ke_Suplayer", "");
        if (string.IsNullOrWhiteSpace(akunRetur) || string.IsNullOrWhiteSpace(akunKontra))
            throw new InvalidOperationException("Rekening retur supplier belum diatur pada set_akun.");

        await db.ExecAsync("INSERT INTO returbeli(no_retur_beli,tgl_retur,nip,kode_suplier,kd_bangsal) VALUES(@p0,@p1,@p2,@p3,@p4)",
            no, header.Tanggal.Date, header.Nip, supplier, lokasi);
        foreach (var l in items)
        {
            var source = await db.FirstAsync("""
                SELECT d.jumlah,d.jumlah2,d.h_beli FROM detailbeli d
                WHERE d.no_faktur=@p0 AND d.kode_brng=@p1 AND d.no_batch=@p2 FOR UPDATE
                """, l.NoFaktur, l.Kode, Batch ? l.NoBatch : "")
                ?? throw new InvalidOperationException($"Barang {l.Nama} tidak ditemukan di faktur asal.");
            var purchased = source.D("jumlah2");
            var factor = source.D("jumlah") > 0 && purchased > 0 ? purchased / source.D("jumlah") : 1;
            if (purchased <= 0) purchased = source.D("jumlah") * factor;
            var returned = Convert.ToDouble(await db.ScalarAsync("SELECT COALESCE(SUM(jml_retur2),0) FROM detreturbeli WHERE no_faktur=@p0 AND kode_brng=@p1 AND no_batch=@p2",
                l.NoFaktur, l.Kode, Batch ? l.NoBatch : "") ?? 0d);
            if (l.JumlahRetur > purchased - returned)
                throw new InvalidOperationException($"Jumlah retur {l.Nama} sudah melebihi sisa jumlah faktur.");

            var stockKeyBatch = Batch ? l.NoBatch : "";
            var stockKeyInvoice = Batch ? l.NoFaktur : "";
            var stock = await db.FirstAsync("""
                SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3 FOR UPDATE
                """, l.Kode, lokasi, stockKeyBatch, stockKeyInvoice);
            if (stock is null || stock.D("stok") < l.JumlahRetur)
                throw new InvalidOperationException($"Stok {l.Nama} tidak cukup untuk diretur ke supplier.");
            if (Batch)
            {
                var changed = await db.ExecAsync("UPDATE data_batch SET sisa=sisa-@p0 WHERE no_batch=@p1 AND kode_brng=@p2 AND no_faktur=@p3 AND sisa>=@p0",
                    l.JumlahRetur, l.NoBatch, l.Kode, l.NoFaktur);
                if (changed != 1) throw new InvalidOperationException($"Sisa batch {l.NoBatch} tidak cukup atau batch sudah berubah.");
            }
            var after = stock.D("stok") - l.JumlahRetur;
            if (await db.ExecAsync("UPDATE gudangbarang SET stok=@p0 WHERE kode_brng=@p1 AND kd_bangsal=@p2 AND no_batch=@p3 AND no_faktur=@p4 AND stok>=@p5",
                    after, l.Kode, lokasi, stockKeyBatch, stockKeyInvoice, l.JumlahRetur) != 1)
                throw new InvalidOperationException($"Stok {l.Nama} berubah saat transaksi retur.");

            await db.ExecAsync("""
                INSERT INTO detreturbeli(no_retur_beli,no_faktur,kode_brng,kode_sat,h_beli,jml_beli,h_retur,jml_retur,total,no_batch,jml_retur2)
                VALUES(@p0,@p1,@p2,@p3,@p4,0,@p5,@p6,@p7,@p8,@p6)
                """, no, l.NoFaktur, l.Kode, l.KodeSat, source.D("h_beli"), l.HargaKecil, l.JumlahRetur, l.Subtotal, stockKeyBatch);
            await db.ExecAsync("""
                INSERT INTO riwayat_barang_medis(kode_brng,stok_awal,masuk,keluar,stok_akhir,posisi,tanggal,jam,petugas,kd_bangsal,status,no_batch,no_faktur,keterangan)
                VALUES(@p0,@p1,0,@p2,@p3,'Retur Beli',@p4,CURRENT_TIME(),@p5,@p6,'Simpan',@p7,@p8,@p9)
                """, l.Kode, stock.D("stok"), l.JumlahRetur, after, header.Tanggal.Date, petugasKode, lokasi,
                stockKeyBatch, stockKeyInvoice, $"{no} {l.NoFaktur}");
        }

        await JurnalPoster.PostAsync(db.Conn, db.Tx!, no, $"RETUR PEMBELIAN DI {lokasi}, OLEH {petugasKode}",
            [new(akunRetur, "RETUR PEMBELIAN", total, 0), new(akunKontra, "KAS DI TANGAN", 0, total)]);
        await db.CommitAsync();
    }

    public async Task HapusAsync(string noRetur, string petugasKode)
    {
        var no = noRetur.Trim();
        if (no.Length == 0) throw new ArgumentException("Pilih dokumen retur yang akan dibatalkan.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var header = await db.FirstAsync("SELECT kd_bangsal FROM returbeli WHERE no_retur_beli=@p0 FOR UPDATE", no)
            ?? throw new InvalidOperationException("Dokumen retur sudah tidak ada.");
        var lokasi = header.S("kd_bangsal");
        var lines = await db.QueryAsync("""
            SELECT no_faktur,kode_brng,no_batch,jml_retur2,total
            FROM detreturbeli WHERE no_retur_beli=@p0 FOR UPDATE
            """, no);
        if (lines.Count == 0) throw new InvalidOperationException("Rincian retur kosong; pembatalan dihentikan.");

        var jurnalAsal = await db.FirstAsync("""
            SELECT no_jurnal FROM jurnal WHERE no_bukti=@p0 AND keterangan LIKE 'RETUR PEMBELIAN%'
            ORDER BY tgl_jurnal DESC,jam_jurnal DESC,no_jurnal DESC LIMIT 1 FOR UPDATE
            """, no);
        if (jurnalAsal is null) throw new InvalidOperationException("Jurnal retur asal tidak ditemukan; batalkan lewat Khanza setelah rekonsiliasi.");
        var akunAsal = await db.QueryAsync("SELECT kd_rek,debet,kredit FROM detailjurnal WHERE no_jurnal=@p0 FOR UPDATE", jurnalAsal.S("no_jurnal"));
        var total = lines.Sum(x => x.D("total"));
        if (akunAsal.Count == 0 || Math.Abs(akunAsal.Sum(x => x.D("debet")) - total) > 0.01
            || Math.Abs(akunAsal.Sum(x => x.D("kredit")) - total) > 0.01)
            throw new InvalidOperationException("Jurnal asal tidak cocok dengan nilai retur; batalkan lewat Khanza setelah rekonsiliasi.");

        foreach (var line in lines)
        {
            var batch = Batch ? line.S("no_batch") : "";
            var invoice = Batch ? line.S("no_faktur") : "";
            var qty = line.D("jml_retur2");
            var stock = await db.FirstAsync("""
                SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3 FOR UPDATE
                """, line.S("kode_brng"), lokasi, batch, invoice);
            var before = stock?.D("stok") ?? 0;
            var after = before + qty;
            await db.ExecAsync("""
                INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES(@p0,@p1,@p2,@p3,@p4)
                ON DUPLICATE KEY UPDATE stok=stok+@p2
                """, line.S("kode_brng"), lokasi, qty, batch, invoice);
            if (Batch && await db.ExecAsync("""
                UPDATE data_batch SET sisa=sisa+@p0 WHERE no_batch=@p1 AND kode_brng=@p2 AND no_faktur=@p3
                """, qty, batch, line.S("kode_brng"), line.S("no_faktur")) != 1)
                throw new InvalidOperationException($"Batch {batch} tidak ditemukan; retur tidak dibatalkan.");
            await db.ExecAsync("""
                INSERT INTO riwayat_barang_medis(kode_brng,stok_awal,masuk,keluar,stok_akhir,posisi,tanggal,jam,petugas,kd_bangsal,status,no_batch,no_faktur,keterangan)
                VALUES(@p0,@p1,@p2,0,@p3,'Retur Beli',CURRENT_DATE(),CURRENT_TIME(),@p4,@p5,'Hapus',@p6,@p7,@p8)
                """, line.S("kode_brng"), before, qty, after, petugasKode, lokasi, batch, invoice, no);
        }

        await db.ExecAsync("DELETE FROM returbeli WHERE no_retur_beli=@p0", no);
        // The Java handler writes the original debit/credit again when cancelling. Reverse the
        // saved posting here so cancellation actually nets to zero and remains correct if accounts changed.
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, no, $"BATAL RETUR PEMBELIAN DI {lokasi}, OLEH {petugasKode}",
            akunAsal.Select(x => new JurnalLine(x.S("kd_rek"), "Pembatalan Retur Pembelian", x.D("kredit"), x.D("debet"))));
        await db.CommitAsync();
    }
}
