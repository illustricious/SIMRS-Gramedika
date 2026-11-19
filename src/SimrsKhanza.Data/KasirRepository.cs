using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using SimrsKhanza.Core.Entities;
using SimrsKhanza.Data.Keuangan;

namespace SimrsKhanza.Data;

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
}

public interface IKasirRepository
{
    Task<KasirBillItem?> GetBillAsync(string noRawat, CancellationToken ct = default);
    Task MarkPaidAsync(string noRawat, CancellationToken ct = default);
    Task<IReadOnlyList<KasirBillItem>> GetLaporanPendapatanAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public class KasirRepository(SimrsKhanzaDbContext db) : IKasirRepository
{
    private const string Registrasi = "Registrasi";
    private const string Kamar = "Kamar";
    private const string Harian = "Harian";
    private const string BiayaMasuk = "Biaya Masuk";

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
        if (result.Ranap && result.StatusBayar != "Sudah Bayar" && result.AlasanBelumBisaBayar is null)
            result = result with { AlasanBelumBisaBayar = await CekKomponenLainAsync(noRawat, ct) };
        return result;
    }

    public async Task MarkPaidAsync(string noRawat, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reg = await KunjunganRanap.KunciAsync(db, noRawat, ct);
        if (reg.StatusLanjut == StatusLanjut.RawatInap ||
            await db.KamarInapSet.AnyAsync(k => k.NoRawat == noRawat, ct))
            throw new InvalidOperationException("Gunakan pelunasan rawat inap dengan akun bayar.");
        if (reg.Status == StatusRegistrasi.Batal) throw new InvalidOperationException("Registrasi pasien sudah dibatalkan.");
        reg.StatusBayar = StatusBayar.SudahBayar;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<string>> AkunBayarRanapAsync(CancellationToken ct = default) =>
        await db.Database.SqlQuery<string>($"""
            SELECT nama_bayar AS Value FROM akun_bayar WHERE COALESCE(ppn,0)=0 AND COALESCE(kd_rek,'')<>'' ORDER BY nama_bayar
            """).ToListAsync(ct);

    public async Task<string> BayarRanapAsync(KasirBillItem dilihat, string akunBayar, string petugas, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(akunBayar) || string.IsNullOrWhiteSpace(petugas))
            throw new ArgumentException("Pilih akun bayar dan pastikan petugas sudah login.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reg = await KunjunganRanap.KunciAsync(db, dilihat.NoRawat, ct);
        await KunjunganRanap.PastikanBelumDitagihAsync(db, reg, ct);
        var visit = await KunjunganQuery.SingleAsync(r => r.Reg.NoRawat == reg.NoRawat, ct);
        var bill = (await BuatRingkasanAsync([visit], ct)).Single();
        if (!bill.Ranap || bill.AlasanBelumBisaBayar is not null)
            throw new InvalidOperationException(bill.AlasanBelumBisaBayar ?? "Kunjungan ini bukan rawat inap.");
        if (await CekKomponenLainAsync(reg.NoRawat, ct) is { } alasan)
            throw new InvalidOperationException(alasan);
        if (dilihat.NoRekamMedis != bill.NoRekamMedis || dilihat.Total != bill.Total || !dilihat.Rincian.SequenceEqual(bill.Rincian))
            throw new InvalidOperationException("Rincian tagihan berubah. Muat ulang dan konfirmasi jumlah terbaru.");
        if (bill.Total < 0 || bill.Rincian.Any(r => r.Total < 0))
            throw new InvalidOperationException("Ada biaya negatif yang perlu diperiksa di Kasir Khanza.");

        var rekeningBayar = await db.Database.SqlQuery<string>($"""
            SELECT kd_rek AS Value FROM akun_bayar
            WHERE nama_bayar={akunBayar} AND COALESCE(ppn,0)=0 AND COALESCE(kd_rek,'')<>''
            """).SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Akun bayar belum valid atau memiliki biaya admin.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT NOW() AS Value").SingleAsync(ct);
        var tanggal = DateOnly.FromDateTime(now);
        var urut = (await db.Database.SqlQuery<long>($"""
            SELECT IFNULL(MAX(CAST(RIGHT(no_nota,4) AS SIGNED)),0) AS Value FROM nota_inap
            WHERE tanggal={tanggal}
            """).SingleAsync(ct)) + 1;
        if (urut > 9999) throw new InvalidOperationException("Nomor nota rawat inap untuk hari ini sudah habis.");
        var noNota = $"{now:yyyy'/'MM'/'dd}/RI{urut:0000}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO nota_inap (no_rawat,no_nota,tanggal,jam,Uang_Muka)
            VALUES ({reg.NoRawat},{noNota},{tanggal},{now.TimeOfDay},0)
            """, ct);
        for (var i = 0; i < bill.Rincian.Count; i++)
        {
            var line = bill.Rincian[i];
            var status = line.Jenis == BiayaMasuk ? Kamar : line.Jenis;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO billing (noindex,no_rawat,tgl_byr,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
                VALUES ({i},{reg.NoRawat},{tanggal},{line.Jenis},{line.Nama},':',{line.Tarif},{line.Jumlah},0,{line.Total},{status})
                """, ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO billing (noindex,no_rawat,tgl_byr,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya,status)
            VALUES ({bill.Rincian.Count},{reg.NoRawat},{tanggal},'','Total Tagihan',':',0,0,0,{bill.Total},'Tagihan')
            """, ct);
        if (bill.Total > 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO detail_nota_inap (no_rawat,nama_bayar,besarppn,besar_bayar)
                VALUES ({reg.NoRawat},{akunBayar},0,{bill.Total})
                """, ct);
            var conn = (MySqlConnection)db.Database.GetDbConnection();
            var sqlTx = (MySqlTransaction)tx.GetDbTransaction();
            var akun = await JurnalPoster.ReadAkunAsync(conn, sqlTx, "set_akun_ranap", ct);
            var akun2 = await JurnalPoster.ReadAkunAsync(conn, sqlTx, "set_akun_ranap2", ct);
            var lines = new List<JurnalLine> { new(rekeningBayar, akunBayar, (double)bill.Total, 0) };
            TambahKredit(lines, akun.GetValueOrDefault("Registrasi_Ranap", ""), Registrasi, bill.BiayaReg);
            TambahKredit(lines, akun.GetValueOrDefault("Kamar_Inap", ""), Kamar,
                bill.Rincian.Where(r => r.Jenis is Kamar or BiayaMasuk).Sum(r => r.Total));
            TambahKredit(lines, akun2.GetValueOrDefault("Harian_Ranap", ""), Harian,
                bill.Rincian.Where(r => r.Jenis == Harian).Sum(r => r.Total));
            await JurnalPoster.PostAsync(conn, sqlTx, reg.NoRawat,
                $"PEMBAYARAN PASIEN RAWAT INAP {reg.NoRawat} {bill.NoRekamMedis} {bill.NamaPasien}, DIPOSTING OLEH {petugas}", lines, ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tagihan_sadewa (no_nota,no_rkm_medis,nama_pasien,alamat,tgl_bayar,jenis_bayar,jumlah_tagihan,jumlah_bayar,status,petugas)
            VALUES ({reg.NoRawat},{bill.NoRekamMedis},{bill.NamaPasien},{visit.Alamat ?? ""},{now},'Pelunasan',{bill.Total},{bill.Total},'Sudah',{petugas})
            """, ct);
        reg.StatusBayar = StatusBayar.SudahBayar;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return noNota;
    }

    private static void TambahKredit(List<JurnalLine> lines, string akun, string nama, decimal jumlah)
    {
        if (jumlah > 0) lines.Add(new(akun, nama, 0, (double)jumlah));
    }

    private async Task<string?> CekKomponenLainAsync(string noRawat, CancellationToken ct)
    {
        var lainnya = await db.Database.SqlQuery<bool>($"""
            SELECT (
                EXISTS(SELECT 1 FROM deposit WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM piutang_pasien WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_inap_dr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_inap_pr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_inap_drpr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_jl_dr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_jl_pr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_jl_drpr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM operasi WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM beri_obat_operasi WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM detail_pemberian_obat WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM resep_pulang WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM tagihan_obat_langsung WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM returjual WHERE LEFT(no_retur_jual,17)={noRawat}) OR
                EXISTS(SELECT 1 FROM penjualan WHERE LEFT(nota_jual,17)={noRawat}) OR
                EXISTS(SELECT 1 FROM periksa_lab WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM periksa_radiologi WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM tambahan_biaya WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM pengurangan_biaya WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM temporary_tambahan_potongan WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM ranap_gabung WHERE no_rawat={noRawat} OR no_rawat2={noRawat}) OR
                EXISTS(SELECT 1 FROM set_service_ranap WHERE besar<>0 AND (kamar='Yes' OR registrasi='Yes' OR harian='Yes'))
            ) AS Value
            """).SingleAsync(ct);
        return lainnya ? "Ada komponen tambahan di luar rincian ini. Selesaikan tagihan di Kasir Khanza." : null;
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
            SELECT no_rawat AS NoRawat,no_nota AS NoNota FROM nota_inap
            """).Where(n => ranapIds.Contains(n.NoRawat)).ToListAsync(ct);

        var perKamar = fees.ToLookup(f => f.KodeKamar, StringComparer.OrdinalIgnoreCase);
        var perTagihan = detail.ToLookup(r => r.NoRawat);
        var perNota = notes.ToDictionary(r => r.NoRawat, r => r.NoNota);
        return visits.Select(v =>
        {
            var reg = v.Reg;
            var historis = perTagihan[reg.NoRawat].ToArray();
            var periode = byVisit[reg.NoRawat].ToArray();
            var lunas = reg.StatusBayar == StatusBayar.SudahBayar;
            var ranap = reg.StatusLanjut == StatusLanjut.RawatInap || periode.Length > 0;
            perNota.TryGetValue(reg.NoRawat, out var nota);
            var rows = historis.Length > 0
                ? historis.Where(r => r.Jenis != "-" && r.Jenis != "Tagihan" && !r.Jenis.StartsWith("Ttl", StringComparison.Ordinal))
                    .Select(r => new KasirRincian(r.Jenis == Kamar && r.Label == BiayaMasuk ? BiayaMasuk : r.Jenis, r.Nama, r.Tarif, r.Jumlah, r.Total)).ToList()
                : HitungRincian(reg, periode, perKamar, aturan, now, lunas, !ranap || lunas || registrasiRanap);
            var totalDisimpan = historis.LastOrDefault(r => r.Jenis == "Tagihan" && r.Total != 0)?.Total;
            var alasan = reg.Status == StatusRegistrasi.Batal ? "Registrasi pasien sudah dibatalkan."
                : ranap && !lunas && (historis.Length > 0 || nota is not null) ? "Tagihan sudah tercatat di Khanza. Periksa status pelunasannya."
                : ranap && periode.Length == 0 ? "Belum ada periode kamar yang tercatat."
                : ranap && !lunas && periode.Any(k => k.StatusPulang is null or StatusPulang.BelumPulang) ? "Pasien masih menempati kamar. Selesaikan proses pulang sebelum pelunasan."
                : null;
            return new KasirBillItem(reg.NoRawat, reg.NoRekamMedis, v.Pasien,
                rows.Where(r => r.Jenis == Registrasi).Sum(r => r.Total),
                rows.Where(r => r.Jenis == Kamar).Sum(r => r.Total), totalDisimpan ?? rows.Sum(r => r.Total),
                lunas ? "Sudah Bayar" : reg.StatusBayar is null ? null : "Belum Bayar")
            { Ranap = ranap, Rincian = rows, RincianTersimpan = historis.Length > 0, NoNota = nota, AlasanBelumBisaBayar = alasan };
        }).ToList();
    }

    private static List<KasirRincian> HitungRincian(RegPeriksa reg, KamarInap[] periode,
        ILookup<string, TarifTambahan> fees, AturanLamaRawat aturan, DateTime now, bool lunas, bool tagihRegistrasi)
    {
        var biayaReg = reg.BiayaReg ?? 0m;
        var rows = new List<KasirRincian>();
        if (tagihRegistrasi)
            rows.Add(new(Registrasi, "Biaya Registrasi", biayaReg, 1, Bulatkan(biayaReg)));
        foreach (var stay in periode)
        {
            var (lama, biaya) = lunas ? (stay.Lama ?? 0m, stay.TotalBiaya ?? 0m) : aturan.Rincian(stay, now);
            var judul = $"{stay.KodeKamar} · {stay.TanggalMasuk:dd/MM/yyyy} {stay.JamMasuk:HH:mm}";
            rows.Add(new(Kamar, judul, Bulatkan(stay.TarifKamar ?? 0m), Bulatkan(lama), Bulatkan(biaya)));
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

    private sealed class NotaSimpan
    {
        public string NoRawat { get; set; } = "";
        public string? NoNota { get; set; }
    }
}
