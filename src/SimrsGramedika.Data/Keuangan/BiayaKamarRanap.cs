using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Core.Entities;

namespace SimrsGramedika.Data.Keuangan;

public sealed class AturanLamaRawat
{
    public int JamMinimal { get; set; }
    public bool HitungHariAwal { get; set; }
    public bool DiagnosaAkhirWajib { get; set; }

    public static int HitungHari(DateTime masuk, DateTime keluar, int jamMinimal, bool hariAwal)
    {
        if (keluar < masuk) throw new ArgumentException("Waktu keluar harus sama dengan atau sesudah waktu masuk.");
        var selisihHari = (keluar.Date - masuk.Date).Days;
        var lama = selisihHari > 0 ? selisihHari : (keluar - masuk).TotalHours > jamMinimal ? 1 : 0;
        return lama + (hariAwal ? 1 : 0);
    }

    internal static async Task<AturanLamaRawat> LoadAsync(SimrsGramedikaDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<AturanLamaRawat>($"""
            SELECT lamajam AS JamMinimal, (hariawal='Yes') AS HitungHariAwal,
                   COALESCE(diagnosaakhir='Yes',0) AS DiagnosaAkhirWajib
            FROM set_jam_minimal LIMIT 1
            """).FirstOrDefaultAsync(ct) ?? new();

    internal (decimal Lama, decimal Total) Rincian(KamarInap stay, DateTime sekarang)
    {
        var masuk = stay.TanggalMasuk.ToDateTime(stay.JamMasuk);
        var aktif = stay.StatusPulang is null or StatusPulang.BelumPulang;
        var akhir = aktif ? sekarang : stay.TanggalKeluar is { Year: >= 1900 } tanggal
            ? tanggal.ToDateTime(stay.JamKeluar ?? TimeOnly.MinValue) : masuk;
        var lama = !aktif && stay.Lama is { } lamaSimpan
            ? lamaSimpan : HitungHari(masuk, akhir < masuk ? masuk : akhir, JamMinimal, HitungHariAwal);
        return (lama, !aktif && stay.TotalBiaya is { } biayaSimpan ? biayaSimpan : lama * (stay.TarifKamar ?? 0m));
    }
}

internal static class KunjunganRanap
{
    internal static async Task<RegPeriksa> KunciAsync(SimrsGramedikaDbContext db, string noRawat, CancellationToken ct)
    {
        var rows = await db.RegPeriksaSet
            .FromSqlInterpolated($"SELECT * FROM reg_periksa WHERE no_rawat={noRawat} FOR UPDATE").ToListAsync(ct);
        return rows.SingleOrDefault() ?? throw new InvalidOperationException($"No. Rawat '{noRawat}' tidak ditemukan.");
    }

    internal static async Task PastikanBelumDitagihAsync(SimrsGramedikaDbContext db, RegPeriksa reg, CancellationToken ct)
    {
        if (reg.Status == StatusRegistrasi.Batal) throw new InvalidOperationException("Registrasi pasien sudah dibatalkan.");
        if (await TagihanKunjungan.SudahDitagihAsync(db, reg.NoRawat, ct))
            throw new InvalidOperationException("Tagihan pasien sudah disimpan. Periksa di Kasir sebelum mengubah data rawat inap.");
    }
}
