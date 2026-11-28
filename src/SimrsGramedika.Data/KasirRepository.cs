using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data.Keuangan;

namespace SimrsGramedika.Data;

public record KasirPembayaran(string AkunBayar, decimal BiayaAdmin, decimal Jumlah);
public record KasirKuitansi(KasirBillItem Tagihan, DateTime WaktuBayar, IReadOnlyList<KasirPembayaran> Pembayaran)
{
    public IReadOnlyList<KasirPiutangRincian> Piutang { get; init; } = [];
}

public record KasirRincian(string Jenis, string Nama, decimal Tarif, decimal Jumlah, decimal Total);

public record KasirBillItem(
    string NoRawat, string NoRekamMedis, string NamaPasien,
    decimal BiayaReg, decimal BiayaKamar, decimal Total, string? StatusBayar)
{
    public bool Ranap { get; init; }
    public IReadOnlyList<KasirRincian> Rincian { get; init; } = [];
    public bool RincianTersimpan { get; init; }
    public string? NoNota { get; init; }
    public string? AlasanBelumBisaBayar { get; init; }
    /// <summary>Terisi untuk bayi rawat gabung: seluruh tagihannya ada pada No. Rawat ibu.</summary>
    public string? NoRawatIbu { get; init; }
    /// <summary>Isi piutang_pasien.status bila tagihan disimpan sebagai piutang.</summary>
    public string? StatusPiutang { get; init; }
    /// <summary>Jumlah deposit rawat inap yang diperhitungkan pada tagihan ini.</summary>
    public decimal Deposit { get; init; }
    public decimal HarusDibayar => Math.Max(Total - Deposit, 0);
    /// <summary>Kelebihan deposit yang dicatat sebagai pengembalian kepada pasien.</summary>
    public decimal SisaDeposit => Math.Max(Deposit - Total, 0);
}

public interface IKasirRepository
{
    Task<KasirBillItem?> GetBillAsync(string noRawat, CancellationToken ct = default);
    Task<IReadOnlyList<KasirBillItem>> GetLaporanPendapatanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public partial class KasirRepository(SimrsGramedikaDbContext db) : IKasirRepository
{
    private const string Registrasi = "Registrasi";
    private const string Kamar = "Kamar";
    private const string Harian = "Harian";
    private const string BiayaMasuk = "Biaya Masuk";
    private const string BayarDiKhanza = "Ada komponen yang harus diselesaikan di Kasir Khanza.";
    private const string PotonganBerlebih = "Potongan biaya melebihi tagihan. Perbaiki potongan sebelum menyimpan.";

    private sealed class Kunjungan
    {
        public required RegPeriksa Reg { get; init; }
        public required string Pasien { get; init; }
        public string? Alamat { get; init; }
    }

    private IQueryable<Kunjungan> KunjunganQuery =>
        from r in db.RegPeriksaSet.AsNoTracking()
        join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
        select new Kunjungan { Reg = r, Pasien = p.NamaPasien, Alamat = p.Alamat };

    public async Task<KasirBillItem?> GetBillAsync(string noRawat, CancellationToken ct = default)
    {
        var visit = await KunjunganQuery.SingleOrDefaultAsync(r => r.Reg.NoRawat == noRawat, ct);
        if (visit is null) return null;
        var result = (await BuatRingkasanAsync([visit], ct)).Single();
        if (result.StatusBayar == "Sudah Bayar")
            result = result with
            {
                StatusPiutang = await db.Database.SqlQuery<string>(
                    $"SELECT status AS Value FROM piutang_pasien WHERE no_rawat={noRawat}").SingleOrDefaultAsync(ct),
            };
        if (!result.Ranap)
        {
            if (result.StatusBayar == "Sudah Bayar")
                return await MuatNotaRalanAsync(result, ct);
            if (result.AlasanBelumBisaBayar is not null) return result;
            if (await AdaNotaAtauBillingRalanAsync(noRawat, ct))
                return result with { AlasanBelumBisaBayar = "Tagihan sudah tercatat di Khanza. Periksa status pelunasannya." };
            return await LengkapiTagihanRalanAsync(result, ct);
        }

        if (result.NoRawatIbu is not null || result.StatusBayar == "Sudah Bayar" || result.RincianTersimpan
            || result.NoNota is not null || visit.Reg.Status == StatusRegistrasi.Batal)
            return result;

        return await LengkapiTagihanRanapAsync(result, ct);
    }

    public async Task<IReadOnlyList<string>> AkunBayarRanapAsync(CancellationToken ct = default) =>
        await db.Database.SqlQuery<string>($"""
            SELECT nama_bayar AS Value FROM akun_bayar WHERE COALESCE(ppn,0)=0 AND COALESCE(kd_rek,'')<>'' ORDER BY nama_bayar
            """).ToListAsync(ct);

    public async Task<string> BayarRalanAsync(KasirBillItem dilihat, string akunBayar, AuthenticatedUser user,
        CancellationToken ct = default, KasirPiutang? piutang = null)
    {
        if (!user.HasPermission("kasir_ralan") && !user.HasPermission("billing_ralan"))
            throw new InvalidOperationException("Akun tidak memiliki hak billing rawat jalan.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reg = await KunjunganRanap.KunciAsync(db, dilihat.NoRawat, ct);
        var rawat = reg.NoRawat;
        if (reg.StatusLanjut != StatusLanjut.RawatJalan || reg.Status == StatusRegistrasi.Batal || reg.StatusBayar != StatusBayar.BelumBayar ||
            await db.KamarInapSet.AnyAsync(k => k.NoRawat == rawat, ct) || await AdaNotaAtauBillingRalanAsync(rawat, ct))
            throw new InvalidOperationException("Kunjungan berubah, batal, atau tagihan sudah tercatat.");
        var visit = await KunjunganQuery.SingleAsync(r => r.Reg.NoRawat == rawat, ct);
        var dasar = (await BuatRingkasanAsync([visit], ct)).Single();
        var bill = await LengkapiTagihanRalanAsync(dasar, ct);
        if (bill.AlasanBelumBisaBayar is { } alasan) throw new InvalidOperationException(alasan);
        if (dilihat.NoRekamMedis != bill.NoRekamMedis || dilihat.Total != bill.Total || !dilihat.Rincian.SequenceEqual(bill.Rincian) || bill.Total < 0)
            throw new InvalidOperationException("Rincian tagihan berubah. Muat ulang sebelum mengonfirmasi.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT NOW() AS Value").SingleAsync(ct);
        var tanggal = DateOnly.FromDateTime(now);
        var terima = await SiapkanPenerimaanAsync(bill.Total, akunBayar, piutang, tanggal, ct);
        var urut = 1 + await db.Database.SqlQuery<long>($"""
            SELECT COALESCE(MAX(CAST(RIGHT(no_nota,4) AS SIGNED)),0) AS Value FROM nota_jalan WHERE tanggal={tanggal} FOR UPDATE
            """).SingleAsync(ct);
        if (urut > 9999) throw new InvalidOperationException("Nomor nota rawat jalan hari ini sudah penuh.");
        var nota = $"{now:yyyy'/'MM'/'dd}/RJ{urut:0000}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO nota_jalan(no_rawat,no_nota,tanggal,jam) VALUES ({rawat},{nota},{tanggal},{now.TimeOfDay})
            """, ct);
        for (var i = 0; i < bill.Rincian.Count; i++)
        {
            var line = bill.Rincian[i];
            var tambahan = SelisihBaris(line);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO billing(noindex,no_rawat,tgl_byr,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
                VALUES ({i},{rawat},{tanggal},'',{line.Nama},':',{line.Tarif},{line.Jumlah},{tambahan},{line.Total},{line.Jenis})
                """, ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO billing(noindex,no_rawat,tgl_byr,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
            VALUES ({bill.Rincian.Count},{rawat},{tanggal},'','Total Tagihan',':',0,0,0,{bill.Total},'Tagihan')
            """, ct);
        if (terima.Tunai > 0)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO detail_nota_jalan(no_rawat,nama_bayar,besarppn,besar_bayar) VALUES ({rawat},{akunBayar},0,{terima.Tunai})
                """, ct);
        // Tagihan yang habis oleh potongan tetap dijurnal; tagihan tanpa nilai tidak menghasilkan baris.
        if (bill.Rincian.Any(r => r.Total != 0))
        {
            var conn = (MySqlConnection)db.Database.GetDbConnection();
            var sqlTx = (MySqlTransaction)tx.GetDbTransaction();
            var lines = await BuatJurnalRalanAsync(bill, terima.Debit, conn, sqlTx, ct);
            var keterangan = piutang is null ? $"PELUNASAN RAWAT JALAN {rawat}, OLEH {user.KodeLegacy}"
                : $"PIUTANG PASIEN RAWAT JALAN {rawat} {bill.NoRekamMedis} {bill.NamaPasien}, DIPOSTING OLEH {user.KodeLegacy}";
            await JurnalPoster.PostAsync(conn, sqlTx, rawat, keterangan, lines, ct);
        }
        if (piutang is not null)
            await SimpanPiutangAsync(rawat, bill.NoRekamMedis, bill.Total, terima, piutang, tanggal, ct);
        if (piutang is null || terima.Tunai > 0)
        {
            var (jenis, status) = piutang is null ? ("Pelunasan", "Sudah") : ("Uang Muka", "Belum");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tagihan_sadewa(no_nota,no_rkm_medis,nama_pasien,alamat,tgl_bayar,jenis_bayar,jumlah_tagihan,jumlah_bayar,status,petugas)
                VALUES({rawat},{bill.NoRekamMedis},{bill.NamaPasien},{visit.Alamat ?? ""},{now},{jenis},{bill.Total},{terima.Tunai},{status},{user.KodeLegacy})
                """, ct);
        }
        reg.StatusBayar = StatusBayar.SudahBayar;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return nota;
    }

    public async Task<string> BayarRanapAsync(KasirBillItem dilihat, string akunBayar, AuthenticatedUser user,
        CancellationToken ct = default, KasirPiutang? piutang = null)
    {
        if (!user.HasPermission("billing_ranap")) throw new InvalidOperationException("Akun tidak memiliki hak billing rawat inap.");
        var petugas = user.KodeLegacy;
        if (string.IsNullOrWhiteSpace(petugas))
            throw new ArgumentException("Pastikan petugas sudah login.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reg = await KunjunganRanap.KunciAsync(db, dilihat.NoRawat, ct);
        // Kunci bayi rawat gabung sebelum pembacaan biasa, agar transaksi bayi tidak menyusul tagihan yang sedang disimpan.
        var bayi = new List<RegPeriksa>();
        foreach (var noRawatBayi in await db.Database.SqlQuery<string>(
                     $"SELECT no_rawat2 AS Value FROM ranap_gabung WHERE no_rawat={reg.NoRawat} FOR UPDATE").ToListAsync(ct))
            bayi.Add(await KunjunganRanap.KunciAsync(db, noRawatBayi, ct));
        await KunjunganRanap.PastikanBelumDitagihAsync(db, reg, ct);
        if (reg.StatusBayar == StatusBayar.SudahBayar || await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS(SELECT 1 FROM nota_inap WHERE no_rawat={reg.NoRawat}) AS Value
            """).SingleAsync(ct))
            throw new InvalidOperationException("Tagihan pasien sudah disimpan. Periksa di Kasir sebelum mengubah data rawat inap.");
        var visit = await KunjunganQuery.SingleAsync(r => r.Reg.NoRawat == reg.NoRawat, ct);
        var bill = (await BuatRingkasanAsync([visit], ct)).Single();
        if (!bill.Ranap) throw new InvalidOperationException("Kunjungan ini bukan rawat inap.");
        bill = await LengkapiTagihanRanapAsync(bill, ct);
        if (bill.AlasanBelumBisaBayar is { } alasan) throw new InvalidOperationException(alasan);
        if (dilihat.NoRekamMedis != bill.NoRekamMedis || dilihat.Total != bill.Total || dilihat.Deposit != bill.Deposit
            || !dilihat.Rincian.SequenceEqual(bill.Rincian))
            throw new InvalidOperationException("Rincian tagihan berubah. Muat ulang dan konfirmasi jumlah terbaru.");

        var now = await db.Database.SqlQuery<DateTime>($"SELECT NOW() AS Value").SingleAsync(ct);
        var tanggal = DateOnly.FromDateTime(now);
        var terima = await SiapkanPenerimaanAsync(bill.HarusDibayar, akunBayar, piutang, tanggal, ct);
        var urut = (await db.Database.SqlQuery<long>($"""
            SELECT IFNULL(MAX(CAST(RIGHT(no_nota,4) AS SIGNED)),0) AS Value FROM nota_inap
            WHERE tanggal={tanggal} FOR UPDATE
            """).SingleAsync(ct)) + 1;
        if (urut > 9999) throw new InvalidOperationException("Nomor nota rawat inap untuk hari ini sudah habis.");
        var noNota = $"{now:yyyy'/'MM'/'dd}/RI{urut:0000}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO nota_inap (no_rawat,no_nota,tanggal,jam,Uang_Muka)
            VALUES ({reg.NoRawat},{noNota},{tanggal},{now.TimeOfDay},{bill.Deposit})
            """, ct);
        for (var i = 0; i < bill.Rincian.Count; i++)
        {
            var line = bill.Rincian[i];
            var status = line.Jenis == BiayaMasuk ? Kamar : line.Jenis;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO billing (noindex,no_rawat,tgl_byr,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
                VALUES ({i},{reg.NoRawat},{tanggal},{line.Jenis},{line.Nama},':',{line.Tarif},{line.Jumlah},{SelisihBaris(line)},{line.Total},{status})
                """, ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO billing (noindex,no_rawat,tgl_byr,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
            VALUES ({bill.Rincian.Count},{reg.NoRawat},{tanggal},'','Total Tagihan',':',0,0,0,{bill.Total},'Tagihan')
            """, ct);
        if (terima.Tunai > 0)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO detail_nota_inap (no_rawat,nama_bayar,besarppn,besar_bayar)
                VALUES ({reg.NoRawat},{akunBayar},0,{terima.Tunai})
                """, ct);
        if (bill.Deposit > 0 || bill.Rincian.Any(r => r.Total != 0))
        {
            var conn = (MySqlConnection)db.Database.GetDbConnection();
            var sqlTx = (MySqlTransaction)tx.GetDbTransaction();
            var akun = await JurnalPoster.ReadAkunAsync(conn, sqlTx, "set_akun_ranap", ct);
            var akun2 = await JurnalPoster.ReadAkunAsync(conn, sqlTx, "set_akun_ranap2", ct);
            var lines = new List<JurnalLine>(terima.Debit);
            TambahKredit(lines, akun.GetValueOrDefault("Registrasi_Ranap", ""), Registrasi, bill.BiayaReg);
            TambahKredit(lines, akun.GetValueOrDefault("Kamar_Inap", ""), Kamar,
                bill.Rincian.Where(r => r.Jenis is Kamar or BiayaMasuk).Sum(r => r.Total));
            TambahKredit(lines, akun2.GetValueOrDefault("Harian_Ranap", ""), Harian,
                bill.Rincian.Where(r => r.Jenis == Harian).Sum(r => r.Total));
            await TambahJurnalLayananRanapAsync(bill, lines, akun, conn, sqlTx, ct);
            TambahDebit(lines, akun2.GetValueOrDefault("Uang_Muka_Ranap", ""), "Kontra Akun Uang Muka", bill.Deposit);
            TambahKredit(lines, akun2.GetValueOrDefault("Sisa_Uang_Muka_Ranap", ""), "Sisa Uang Muka Ranap", bill.SisaDeposit);
            var judul = piutang is null ? "PEMBAYARAN" : "PIUTANG";
            await JurnalPoster.PostAsync(conn, sqlTx, reg.NoRawat,
                $"{judul} PASIEN RAWAT INAP {reg.NoRawat} {bill.NoRekamMedis} {bill.NamaPasien}, DIPOSTING OLEH {petugas}", lines, ct);
        }
        if (piutang is not null)
            await SimpanPiutangAsync(reg.NoRawat, bill.NoRekamMedis, bill.Total, terima, piutang, tanggal, ct, bill.Deposit);
        if (bill.SisaDeposit > 0)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO pengembalian_deposit (no_rawat,tanggal,petugas,besar_pengembalian)
                VALUES ({reg.NoRawat},{now},{petugas},{bill.SisaDeposit})
                """, ct);
        // Seperti Khanza, kelebihan deposit tidak dicatat sebagai penerimaan.
        else if (piutang is null || terima.Tunai > 0)
        {
            var (jenis, status) = piutang is null ? ("Pelunasan", "Sudah") : ("Uang Muka", "Belum");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tagihan_sadewa (no_nota,no_rkm_medis,nama_pasien,alamat,tgl_bayar,jenis_bayar,jumlah_tagihan,jumlah_bayar,status,petugas)
                VALUES ({reg.NoRawat},{bill.NoRekamMedis},{bill.NamaPasien},{visit.Alamat ?? ""},{now},{jenis},{bill.Total},{terima.Tunai},{status},{petugas})
                """, ct);
        }
        reg.StatusBayar = StatusBayar.SudahBayar;
        foreach (var anak in bayi) anak.StatusBayar = StatusBayar.SudahBayar;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return noNota;
    }

    private static void TambahKredit(List<JurnalLine> lines, string akun, string nama, decimal jumlah)
    {
        if (jumlah > 0) lines.Add(new(akun, nama, 0, (double)jumlah));
    }

    private async Task<string?> CekKomponenLainAsync(string noRawat, bool bayi, CancellationToken ct)
    {
        // Deposit, tambahan dan potongan dihitung pada No. Rawat yang ditagih; milik bayi gabung tetap diselesaikan di Khanza.
        if (bayi && await db.Database.SqlQuery<bool>($"""
                SELECT (EXISTS(SELECT 1 FROM deposit WHERE no_rawat={noRawat}) OR
                    EXISTS(SELECT 1 FROM tambahan_biaya WHERE no_rawat={noRawat}) OR
                    EXISTS(SELECT 1 FROM pengurangan_biaya WHERE no_rawat={noRawat})) AS Value
                """).SingleAsync(ct))
            return BayarDiKhanza;
        var lainnya = await db.Database.SqlQuery<bool>($"""
            SELECT (
                EXISTS(SELECT 1 FROM pengembalian_deposit WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM piutang_pasien WHERE no_rawat={noRawat}) OR
                (EXISTS(SELECT 1 FROM beri_obat_operasi WHERE no_rawat={noRawat}) AND
                    EXISTS(SELECT 1 FROM set_nota WHERE centangobatranap='No')) OR
                EXISTS(SELECT 1 FROM tagihan_obat_langsung WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM returpasien WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM penjualan WHERE LEFT(nota_jual,17)={noRawat}) OR
                EXISTS(SELECT 1 FROM temporary_tambahan_potongan WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM set_service_ranap WHERE besar<>0 AND (kamar='Yes' OR registrasi='Yes' OR harian='Yes'
                    OR laborat='Yes' OR radiologi='Yes' OR operasi='Yes' OR obat='Yes' OR resep_Pulang='Yes'
                    OR ranap_dokter='Yes' OR ranap_paramedis='Yes' OR ralan_dokter='Yes' OR ralan_paramedis='Yes'
                    OR tambahan='Yes' OR potongan='Yes' OR retur_Obat='Yes'))
            ) AS Value
            """).SingleAsync(ct);
        return lainnya ? BayarDiKhanza : null;
    }

    private async Task<IReadOnlyList<KasirRincian>> MuatRincianTindakanObatAsync(
        string noRawat, bool ranap, CancellationToken ct)
    {
        // Billing Ranap uses the master tarif for paramedic-only services, including services before admission.
        // Other procedures use the price originally recorded on the transaction.
        var rows = await db.Database.SqlQuery<KasirRincian>($"""
            SELECT Jenis,Nama,CAST(Tarif AS DECIMAL(20,4)) AS Tarif,
                   CAST(SUM(Jumlah) AS DECIMAL(20,4)) AS Jumlah,
                   CAST(SUM(Total) AS DECIMAL(20,4)) AS Total
            FROM (
                SELECT 'Ralan Dokter' AS Jenis,j.kd_jenis_prw AS Kode,j.nm_perawatan AS Nama,
                       COALESCE(r.biaya_rawat,0) AS Tarif,1 AS Jumlah,COALESCE(r.biaya_rawat,0) AS Total
                FROM rawat_jl_dr r JOIN jns_perawatan j ON j.kd_jenis_prw=r.kd_jenis_prw WHERE r.no_rawat={noRawat}
                UNION ALL
                SELECT 'Ralan Paramedis',j.kd_jenis_prw,j.nm_perawatan,
                       COALESCE(CASE WHEN {ranap} THEN j.total_byrpr ELSE r.biaya_rawat END,0),
                       1,COALESCE(CASE WHEN {ranap} THEN j.total_byrpr ELSE r.biaya_rawat END,0)
                FROM rawat_jl_pr r JOIN jns_perawatan j ON j.kd_jenis_prw=r.kd_jenis_prw WHERE r.no_rawat={noRawat}
                UNION ALL
                SELECT 'Ralan Dokter Paramedis',j.kd_jenis_prw,j.nm_perawatan,
                       COALESCE(r.biaya_rawat,0),1,COALESCE(r.biaya_rawat,0)
                FROM rawat_jl_drpr r JOIN jns_perawatan j ON j.kd_jenis_prw=r.kd_jenis_prw WHERE r.no_rawat={noRawat}
                UNION ALL
                SELECT 'Ranap Dokter',j.kd_jenis_prw,j.nm_perawatan,
                       COALESCE(r.biaya_rawat,0),1,COALESCE(r.biaya_rawat,0)
                FROM rawat_inap_dr r JOIN jns_perawatan_inap j ON j.kd_jenis_prw=r.kd_jenis_prw
                WHERE r.no_rawat={noRawat} AND {ranap}
                UNION ALL
                SELECT 'Ranap Paramedis',j.kd_jenis_prw,j.nm_perawatan,
                       COALESCE(j.total_byrpr,0),1,COALESCE(j.total_byrpr,0)
                FROM rawat_inap_pr r JOIN jns_perawatan_inap j ON j.kd_jenis_prw=r.kd_jenis_prw
                WHERE r.no_rawat={noRawat} AND {ranap}
                UNION ALL
                SELECT 'Ranap Dokter Paramedis',j.kd_jenis_prw,j.nm_perawatan,
                       COALESCE(r.biaya_rawat,0),1,COALESCE(r.biaya_rawat,0)
                FROM rawat_inap_drpr r JOIN jns_perawatan_inap j ON j.kd_jenis_prw=r.kd_jenis_prw
                WHERE r.no_rawat={noRawat} AND {ranap}
                UNION ALL
                SELECT 'Obat',b.kode_brng,b.nama_brng,COALESCE(o.biaya_obat,0),o.jml,o.total
                FROM detail_pemberian_obat o JOIN databarang b ON b.kode_brng=o.kode_brng
                WHERE o.no_rawat={noRawat} AND ({ranap} OR o.status='Ralan')
            ) layanan
            GROUP BY layanan.Jenis,layanan.Kode,layanan.Nama,layanan.Tarif
            """).OrderBy(r => r.Jenis).ThenBy(r => r.Nama).ThenBy(r => r.Tarif).ThenBy(r => r.Jumlah).ThenBy(r => r.Total).ToListAsync(ct);
        return rows;
    }

    public async Task<IReadOnlyList<KasirBillItem>> GetLaporanPendapatanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var visits = await KunjunganQuery.Where(r => r.Reg.TanggalRegistrasi >= fromDate && r.Reg.TanggalRegistrasi <= toDate)
            .OrderByDescending(r => r.Reg.TanggalRegistrasi).Take(300).ToListAsync(ct);
        return await BuatRingkasanAsync(visits, ct);
    }

    private async Task<List<KasirBillItem>> BuatRingkasanAsync(IReadOnlyList<Kunjungan> visits, CancellationToken ct)
    {
        if (visits.Count == 0) return [];
        var ids = visits.Select(r => r.Reg.NoRawat).ToList();
        var stays = await db.KamarInapSet.AsNoTracking()
            .Where(k => ids.Contains(k.NoRawat)).OrderBy(k => k.TanggalMasuk).ThenBy(k => k.JamMasuk).ToListAsync(ct);
        var byVisit = stays.ToLookup(k => k.NoRawat);
        var ranapIds = visits.Where(v => v.Reg.StatusLanjut == StatusLanjut.RawatInap || byVisit.Contains(v.Reg.NoRawat))
            .Select(v => v.Reg.NoRawat).ToList();
        var rooms = stays.Select(k => k.KodeKamar).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var aturan = rooms.Count == 0 ? new AturanLamaRawat() : await AturanLamaRawat.LoadAsync(db, ct);
        var registrasiRanap = ranapIds.Count > 0 && await db.Database.SqlQuery<bool>($"""
            SELECT (tampilkan_administrasi_di_billingranap='Yes') AS Value FROM set_nota LIMIT 1
            """).FirstOrDefaultAsync(ct);
        var gabung = ranapIds.Count == 0 ? [] : await db.Database.SqlQuery<GabungSimpan>($"""
            SELECT no_rawat AS NoRawatIbu,no_rawat2 AS NoRawat FROM ranap_gabung
            """).Where(g => ranapIds.Contains(g.NoRawat) || ranapIds.Contains(g.NoRawatIbu)).ToListAsync(ct);
        var ibuDari = gabung.GroupBy(g => g.NoRawat).ToDictionary(g => g.Key, g => g.First().NoRawatIbu);
        var punyaBayi = gabung.Select(g => g.NoRawatIbu).ToHashSet();
        var now = DateTime.Now;
        var fees = rooms.Count == 0 ? [] : await db.Database.SqlQuery<TarifTambahan>($"""
            SELECT kd_kamar AS KodeKamar,nama_biaya AS Nama,CAST(besar_biaya AS DECIMAL(20,4)) AS Tarif,
                   CAST(jml AS DECIMAL(20,4)) AS Jumlah,'Harian' AS Jenis FROM biaya_harian
            UNION ALL
            SELECT kd_kamar,nama_biaya,CAST(besar_biaya AS DECIMAL(20,4)),1,'Biaya Masuk' FROM biaya_sekali
            """).Where(f => rooms.Contains(f.KodeKamar)).OrderBy(f => f.Jenis).ThenBy(f => f.Nama).ToListAsync(ct);
        var detail = ranapIds.Count == 0 ? [] : await db.Database.SqlQuery<DetailSimpan>($"""
            SELECT no_rawat AS NoRawat,noindex AS Urut,no AS Label,nm_perawatan AS Nama,COALESCE(status,'-') AS Jenis,
                   CAST(biaya AS DECIMAL(20,4)) AS Tarif,CAST(jumlah AS DECIMAL(20,4)) AS Jumlah,
                   CAST(totalbiaya AS DECIMAL(20,4)) AS Total
            FROM billing
            """).Where(r => ranapIds.Contains(r.NoRawat)).OrderBy(r => r.Urut).ToListAsync(ct);
        var notes = ranapIds.Count == 0 ? [] : await db.Database.SqlQuery<NotaSimpan>($"""
            SELECT no_rawat AS NoRawat,no_nota AS NoNota,CAST(COALESCE(Uang_Muka,0) AS DECIMAL(20,4)) AS UangMuka
            FROM nota_inap
            """).Where(n => ranapIds.Contains(n.NoRawat)).ToListAsync(ct);

        var perKamar = fees.ToLookup(f => f.KodeKamar, StringComparer.OrdinalIgnoreCase);
        var perTagihan = detail.ToLookup(r => r.NoRawat);
        var perNota = notes.ToDictionary(r => r.NoRawat);
        return visits.Select(v =>
        {
            var reg = v.Reg;
            var historis = perTagihan[reg.NoRawat].ToArray();
            var periode = byVisit[reg.NoRawat].ToArray();
            var lunas = reg.StatusBayar == StatusBayar.SudahBayar;
            var ranap = reg.StatusLanjut == StatusLanjut.RawatInap || periode.Length > 0;
            perNota.TryGetValue(reg.NoRawat, out var tersimpan);
            var nota = tersimpan?.NoNota;
            if (historis.Length == 0 && ibuDari.TryGetValue(reg.NoRawat, out var noRawatIbu))
                return new KasirBillItem(reg.NoRawat, reg.NoRekamMedis, v.Pasien, 0, 0, 0, null)
                {
                    Ranap = true, NoRawatIbu = noRawatIbu,
                    AlasanBelumBisaBayar = $"Rawat gabung: tagihan digabung pada No. Rawat ibu {noRawatIbu}.",
                };
            var rows = historis.Length > 0
                ? historis.Where(r => r.Jenis != "-" && r.Jenis != "Tagihan" && !r.Jenis.StartsWith("Ttl", StringComparison.Ordinal))
                    .Select(r => new KasirRincian(r.Jenis == Kamar && r.Label == BiayaMasuk ? BiayaMasuk : r.Jenis, r.Nama, r.Tarif, r.Jumlah, r.Total)).ToList()
                : HitungRincian(reg, periode, perKamar, aturan, now, lunas, !ranap || lunas || registrasiRanap,
                    punyaBayi.Contains(reg.NoRawat) ? aturan.PersenKamarBayi : 0);
            var totalDisimpan = historis.LastOrDefault(r => r.Jenis == "Tagihan" && r.Total != 0)?.Total;
            var alasan = reg.Status == StatusRegistrasi.Batal ? "Registrasi pasien sudah dibatalkan."
                : ranap && !lunas && (historis.Length > 0 || nota is not null) ? "Tagihan sudah tercatat di Khanza. Periksa status pelunasannya."
                : ranap && periode.Length == 0 ? "Belum ada periode kamar yang tercatat."
                : ranap && !lunas && periode.Any(k => k.StatusPulang is null or StatusPulang.BelumPulang) ? "Pasien masih menempati kamar. Selesaikan proses pulang sebelum pelunasan."
                : ranap && !lunas && periode.Any(k => k.TanggalKeluar is null or { Year: < 1900 } || k.JamKeluar is null)
                    ? "Waktu keluar kamar belum lengkap."
                : null;
            return new KasirBillItem(reg.NoRawat, reg.NoRekamMedis, v.Pasien,
                rows.Where(r => r.Jenis == Registrasi).Sum(r => r.Total),
                rows.Where(r => r.Jenis == Kamar).Sum(r => r.Total), totalDisimpan ?? rows.Sum(r => r.Total),
                lunas ? "Sudah Bayar" : reg.StatusBayar is null ? null : "Belum Bayar")
            {
                Ranap = ranap, Rincian = rows, RincianTersimpan = historis.Length > 0, NoNota = nota, AlasanBelumBisaBayar = alasan,
                Deposit = tersimpan?.UangMuka ?? 0,
            };
        }).ToList();
    }

    private static List<KasirRincian> HitungRincian(RegPeriksa reg, KamarInap[] periode,
        ILookup<string, TarifTambahan> fees, AturanLamaRawat aturan, DateTime now, bool lunas, bool tagihRegistrasi,
        int persenKamarBayi)
    {
        var biayaReg = reg.BiayaReg ?? 0m;
        var rows = new List<KasirRincian>();
        if (tagihRegistrasi)
            rows.Add(new(Registrasi, "Biaya Registrasi", biayaReg, 1, Bulatkan(biayaReg)));
        foreach (var stay in periode)
        {
            var (lama, biaya) = lunas ? (stay.Lama ?? 0m, stay.TotalBiaya ?? 0m) : aturan.Rincian(stay, now);
            var judul = $"{stay.KodeKamar} · {stay.TanggalMasuk:dd/MM/yyyy} {stay.JamMasuk:HH:mm}";
            var tambahBayi = persenKamarBayi > 0 && !lunas;
            rows.Add(new(Kamar, tambahBayi ? $"{judul} (Ibu)" : judul, Bulatkan(stay.TarifKamar ?? 0m), Bulatkan(lama), Bulatkan(biaya)));
            if (tambahBayi) // Khanza menambah persentase hanya pada tarif kamar, bukan biaya masuk atau harian.
                rows.Add(new(Kamar, $"{judul} (Bayi)", Bulatkan((stay.TarifKamar ?? 0m) * persenKamarBayi / 100), Bulatkan(lama),
                    Bulatkan(biaya * persenKamarBayi / 100)));
            if (lunas) continue; // Payments marked by earlier versions have no stored fee lines.
            foreach (var fee in fees[stay.KodeKamar])
            {
                var jumlah = fee.Jenis == Harian ? fee.Jumlah * lama : 1;
                rows.Add(new(fee.Jenis, $"{fee.Nama} ({judul})", fee.Tarif, Bulatkan(jumlah), Bulatkan(fee.Tarif * jumlah)));
            }
        }
        return rows;
    }

    private static decimal Bulatkan(decimal nilai) => decimal.Round(nilai, 4, MidpointRounding.AwayFromZero);

    private sealed class TarifTambahan
    {
        public string KodeKamar { get; set; } = "";
        public string Nama { get; set; } = "";
        public string Jenis { get; set; } = "";
        public decimal Tarif { get; set; }
        public decimal Jumlah { get; set; }
    }

    private sealed class DetailSimpan
    {
        public string NoRawat { get; set; } = "";
        public int Urut { get; set; }
        public string Label { get; set; } = "";
        public string Nama { get; set; } = "";
        public string Jenis { get; set; } = "";
        public decimal Tarif { get; set; }
        public decimal Jumlah { get; set; }
        public decimal Total { get; set; }
    }

    private sealed class GabungSimpan
    {
        public string NoRawatIbu { get; set; } = "";
        public string NoRawat { get; set; } = "";
    }

    private sealed class NotaSimpan
    {
        public string NoRawat { get; set; } = "";
        public string? NoNota { get; set; }
        public decimal UangMuka { get; set; }
    }
}
