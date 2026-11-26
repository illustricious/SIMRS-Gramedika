using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Farmasi;

public sealed class PenjualanTunaiService(DatabaseSettings settings, AuthenticatedUser user)
{
    public const string Izin = "penjualan_obat";

    public async Task<IReadOnlyList<string>> AkunAsync()
    {
        PastikanIzin();
        if (settings.AktifkanBatchObat)
            throw new InvalidOperationException("Profil database memakai stok batch. Gunakan Khanza untuk penjualan.");
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("SELECT nama_bayar FROM akun_bayar WHERE COALESCE(ppn,0)=0 AND kd_rek<>'' ORDER BY nama_bayar");
        return rows.Select(r => r.S("nama_bayar")).ToList();
    }

    public async Task<string> SimpanAsync(IReadOnlyList<PenjualanLineInput> lines, string keterangan, string nip,
        string noRm, string lokasi, string akunBayar, decimal diterima)
    {
        PastikanIzin();
        if (settings.AktifkanBatchObat)
            throw new InvalidOperationException("Profil database memakai stok batch. Gunakan Khanza untuk penjualan.");
        if (!user.IsAdminUtama && !string.Equals(user.UserId, nip, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Gunakan kode petugas akun sendiri.");
        if (string.IsNullOrWhiteSpace(noRm) || string.IsNullOrWhiteSpace(lokasi) || string.IsNullOrWhiteSpace(akunBayar)
            || keterangan.Length is 0 or > 40)
            throw new ArgumentException("Isi nomor RM, lokasi, akun bayar dan catatan hingga 40 karakter.");
        if (lines.Count == 0 || lines.Select(x => x.KodeBarang).Distinct(StringComparer.OrdinalIgnoreCase).Count() != lines.Count ||
            lines.Any(x => x.Jumlah <= 0 || x.PtgRp != 0 || x.TambahanRp != 0 || !string.IsNullOrEmpty(x.NoBatch) || !string.IsNullOrEmpty(x.NoFaktur)))
            throw new ArgumentException("Gunakan item berbeda dengan jumlah positif untuk jual bebas tanpa batch, diskon atau biaya tambahan.");
        await using var db = await Db.OpenAsync(settings);
        for (var attempt = 1; ; attempt++)
        {
            await db.BeginAsync();
            try
            {
                var config = await db.FirstAsync("SELECT verifikasi_penjualan_di_kasir,tampilkan_ppnobat_ralan FROM set_nota LIMIT 1");
                if (config?.S("verifikasi_penjualan_di_kasir") != "No" || config.S("tampilkan_ppnobat_ralan") != "No")
                    throw new InvalidOperationException("Gunakan Khanza untuk instalasi dengan verifikasi Kasir atau PPN obat.");
                var nama = await db.StrAsync("SELECT nm_pasien FROM pasien WHERE no_rkm_medis=@p0", noRm);
                var lokasiNama = await db.StrAsync("SELECT nm_bangsal FROM bangsal WHERE kd_bangsal=@p0 AND status='1'", lokasi);
                var rekening = await db.StrAsync("SELECT kd_rek FROM akun_bayar WHERE nama_bayar=@p0 AND COALESCE(ppn,0)=0", akunBayar);
                if (nama.Length == 0 || lokasiNama.Length == 0 || rekening.Length == 0 ||
                    await db.CountAsync("SELECT COUNT(*) FROM petugas WHERE nip=@p0 AND status='1'", nip) != 1)
                    throw new InvalidOperationException("Nomor RM, lokasi, petugas atau akun bayar tidak valid.");
                var now = (await db.FirstAsync("SELECT NOW() waktu"))!.Dt("waktu")!.Value;
                var urut = await db.CountAsync("SELECT COALESCE(MAX(CAST(RIGHT(nota_jual,3) AS UNSIGNED)),0) FROM penjualan WHERE tgl_jual=@p0", now.Date);
                if (urut >= 999) throw new InvalidOperationException("Nomor nota penjualan hari ini sudah penuh.");
                var nota = $"PJ{now:yyyyMMdd}{urut + 1:D3}";
                await db.ExecAsync("""
                    INSERT INTO penjualan(nota_jual,tgl_jual,nip,no_rkm_medis,nm_pasien,keterangan,jns_jual,ongkir,ppn,status,kd_bangsal,kd_rek,nama_bayar)
                    VALUES(@p0,@p1,@p2,@p3,@p4,@p5,'Jual Bebas',0,0,'Sudah Dibayar',@p6,@p7,@p8)
                    """, nota, now.Date, nip, noRm, nama, keterangan, lokasi, rekening, akunBayar);
                decimal total = 0, hpp = 0;
                foreach (var line in lines.OrderBy(x => x.KodeBarang, StringComparer.Ordinal))
                {
                    var master = await db.FirstAsync($"""
                        SELECT kode_sat,jualbebas,{settings.HppColumn} hpp FROM databarang WHERE kode_brng=@p0 AND status='1' FOR UPDATE
                        """, line.KodeBarang) ?? throw new InvalidOperationException($"Barang {line.KodeBarang} sudah tidak aktif.");
                    var stok = await db.QueryAsync("""
                        SELECT no_batch,no_faktur,CAST(stok AS DECIMAL(20,6)) stok FROM gudangbarang
                        WHERE kode_brng=@p0 AND kd_bangsal=@p1 FOR UPDATE
                        """, line.KodeBarang, lokasi);
                    if (stok.Any(r => r.S("no_batch") != "" || r.S("no_faktur") != ""))
                        throw new InvalidOperationException($"Barang {line.KodeBarang} memiliki stok batch. Gunakan Khanza.");
                    var awal = Convert.ToDecimal(stok.SingleOrDefault()?["stok"] ?? 0m);
                    var harga = Convert.ToDecimal(master["jualbebas"] ?? 0m);
                    var dasar = Convert.ToDecimal(master["hpp"] ?? throw new InvalidOperationException($"HPP {line.KodeBarang} belum diatur."));
                    if (harga % 100 != 0)
                        throw new InvalidOperationException($"Harga {line.KodeBarang} perlu pemeriksaan pembulatan di Khanza.");
                    if (awal < line.Jumlah || harga <= 0 || harga != line.HargaSatuan || dasar < 0)
                        throw new InvalidOperationException($"Harga atau stok {line.KodeBarang} berubah. Muat ulang katalog.");
                    var subtotal = harga * line.Jumlah;
                    await db.ExecAsync("""
                        INSERT INTO detailjual(nota_jual,kode_brng,kode_sat,h_jual,h_beli,jumlah,subtotal,dis,bsr_dis,tambahan,
                            embalase,tuslah,aturan_pakai,total,no_batch,no_faktur)
                        VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,0,0,0,0,0,'',@p6,'','')
                        """, nota, line.KodeBarang, master["kode_sat"], harga, dasar, line.Jumlah, subtotal);
                    await db.ExecAsync("""
                        INSERT INTO riwayat_barang_medis(kode_brng,stok_awal,masuk,keluar,stok_akhir,posisi,tanggal,jam,petugas,kd_bangsal,status,no_batch,no_faktur,keterangan)
                        VALUES(@p0,@p1,0,@p2,@p3,'Penjualan',@p4,@p5,@p6,@p7,'Simpan','','',@p8)
                        """, line.KodeBarang, awal, line.Jumlah, awal-line.Jumlah, now.Date, now.TimeOfDay, nip, lokasi, $"{nota} {noRm} {nama}");
                    await db.ExecAsync("""
                        UPDATE gudangbarang SET stok=stok-@p2 WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch='' AND no_faktur=''
                        """, line.KodeBarang, lokasi, line.Jumlah);
                    total += subtotal;
                    hpp += dasar * line.Jumlah;
                }
                if (diterima < total) throw new InvalidOperationException("Jumlah bayar kurang dari total nota.");
                var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun");
                await JurnalPoster.PostAsync(db.Conn, db.Tx!, nota, $"PENJUALAN DI {lokasiNama}, OLEH {user.KodeLegacy}",
                    [new(rekening, akunBayar, (double)total, 0), new(akun.GetValueOrDefault("Penjualan_Obat",""), "Penjualan obat", 0, (double)total),
                     new(akun.GetValueOrDefault("HPP_Obat_Jual_Bebas",""), "HPP obat", (double)hpp, 0),
                     new(akun.GetValueOrDefault("Persediaan_Obat_Jual_Bebas",""), "Persediaan obat", 0, (double)hpp)]);
                await db.ExecAsync("""
                    INSERT INTO tagihan_sadewa(no_nota,no_rkm_medis,nama_pasien,alamat,tgl_bayar,jenis_bayar,jumlah_tagihan,jumlah_bayar,status,petugas)
                    VALUES(@p0,@p1,@p2,'-',@p3,'Pelunasan',@p4,@p4,'Sudah',@p5)
                    """, nota, noRm, nama, now, total, nip);
                await db.CommitAsync();
                return nota;
            }
            catch (MySqlException ex) when (attempt < 5 && ex.Number is 1062 or 1213 or 1205) { await db.DisposeTxAsync(); }
        }
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak penjualan obat.");
    }
}
