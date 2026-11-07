using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

/// <summary>Row for the "menunggu hasil" queue — one pending lab/radiologi order.</summary>
public record PenunjangAntrianItem(
    string NoOrder,
    string NoRawat,
    string NoRekamMedis,
    string NamaPasien,
    DateOnly TanggalPermintaan,
    TimeOnly JamPermintaan,
    string DokterPerujuk,
    string? DiagnosaKlinis);

public interface IPenunjangRepository
{
    /// <summary>Pending lab orders for one kategori (PK/PA/MB) — see KategoriLab's doc comment for how the tag is stored.</summary>
    Task<IReadOnlyList<PenunjangAntrianItem>> GetAntrianLabAsync(KategoriLab kategori, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<IReadOnlyList<PenunjangAntrianItem>> GetAntrianRadiologiAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>Real INSERT into permintaan_lab, tagged with `kategori` (see KategoriLab). Returns the generated NoOrder.</summary>
    Task<string> CreatePermintaanLabAsync(string noRawat, string dokterPerujuk, StatusKunjunganPenunjang status, KategoriLab kategori, string? diagnosaKlinis, string? informasiTambahan, CancellationToken ct = default);

    /// <summary>Real INSERT into permintaan_radiologi. Returns the generated NoOrder.</summary>
    Task<string> CreatePermintaanRadiologiAsync(string noRawat, string dokterPerujuk, StatusKunjunganPenunjang status, string? diagnosaKlinis, string? informasiTambahan, CancellationToken ct = default);

    /// <summary>Real INSERT into saran_kesan_lab — the v1 narrative lab result (see HasilLab doc comment).</summary>
    Task SaveHasilLabAsync(string noRawat, string? saran, string? kesan, CancellationToken ct = default);

    /// <summary>Real INSERT into hasil_radiologi.</summary>
    Task SaveHasilRadiologiAsync(string noRawat, string hasil, CancellationToken ct = default);
}

public class PenunjangRepository(SimrsKhanzaDbContext db) : IPenunjangRepository
{
    // Pending = a permintaan_lab/permintaan_radiologi row for which NO hasil
    // row exists yet for that no_rawat. Simplification: keyed by no_rawat, not
    // by NoOrder (hasil_radiologi/saran_kesan_lab don't carry noorder in the
    // legacy schema) — if one visit ever has two separate orders of the same
    // type, both drop off the queue together once either gets a result. Fine
    // for v1 (one order per visit is the common case); documented in HANDOVER.

    public async Task<IReadOnlyList<PenunjangAntrianItem>> GetAntrianLabAsync(KategoriLab kategori, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var sudahAdaHasil = db.HasilLabSet.Select(h => h.NoRawat);
        var tag = KategoriTag(kategori);

        var query =
            from pm in db.PermintaanLabSet
            join r in db.RegPeriksaSet on pm.NoRawat equals r.NoRawat into rj
            from r in rj.DefaultIfEmpty()
            where pm.TanggalPermintaan >= fromDate && pm.TanggalPermintaan <= toDate
                  && !sudahAdaHasil.Contains(pm.NoRawat)
                  && pm.InformasiTambahan != null && pm.InformasiTambahan.StartsWith(tag)
            orderby pm.TanggalPermintaan descending, pm.JamPermintaan descending
            select new PenunjangAntrianItem(
                pm.NoOrder, pm.NoRawat, r != null ? r.NoRekamMedis : "",
                r != null && r.Pasien != null ? r.Pasien.NamaPasien : "",
                pm.TanggalPermintaan, pm.JamPermintaan, pm.DokterPerujuk, pm.DiagnosaKlinis);

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PenunjangAntrianItem>> GetAntrianRadiologiAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var sudahAdaHasil = db.HasilRadiologiSet.Select(h => h.NoRawat);

        var query =
            from pm in db.PermintaanRadiologiSet
            join r in db.RegPeriksaSet on pm.NoRawat equals r.NoRawat into rj
            from r in rj.DefaultIfEmpty()
            where pm.TanggalPermintaan >= fromDate && pm.TanggalPermintaan <= toDate
                  && !sudahAdaHasil.Contains(pm.NoRawat)
            orderby pm.TanggalPermintaan descending, pm.JamPermintaan descending
            select new PenunjangAntrianItem(
                pm.NoOrder, pm.NoRawat, r != null ? r.NoRekamMedis : "",
                r != null && r.Pasien != null ? r.Pasien.NamaPasien : "",
                pm.TanggalPermintaan, pm.JamPermintaan, pm.DokterPerujuk, pm.DiagnosaKlinis);

        return await query.ToListAsync(ct);
    }

    public async Task<string> CreatePermintaanLabAsync(string noRawat, string dokterPerujuk, StatusKunjunganPenunjang status, KategoriLab kategori, string? diagnosaKlinis, string? informasiTambahan, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var noOrder = GenerateNoOrder();
        var taggedInfo = KategoriTag(kategori) + (string.IsNullOrWhiteSpace(informasiTambahan) ? "" : " " + informasiTambahan);

        db.PermintaanLabSet.Add(new PermintaanLab
        {
            NoOrder = noOrder,
            NoRawat = noRawat,
            TanggalPermintaan = DateOnly.FromDateTime(now),
            JamPermintaan = TimeOnly.FromDateTime(now),
            TanggalSampel = DateOnly.FromDateTime(now),
            JamSampel = TimeOnly.FromDateTime(now),
            TanggalHasil = DateOnly.FromDateTime(now),
            JamHasil = TimeOnly.FromDateTime(now),
            DokterPerujuk = dokterPerujuk,
            Status = status,
            InformasiTambahan = taggedInfo.Length > 60 ? taggedInfo[..60] : taggedInfo, // column is varchar(60)
            DiagnosaKlinis = diagnosaKlinis,
        });

        await db.SaveChangesAsync(ct);
        return noOrder;
    }

    /// <summary>See KategoriLab's doc comment — "[PK]"/"[PA]"/"[MB]" prefix tag stored in permintaan_lab.informasi_tambahan.</summary>
    private static string KategoriTag(KategoriLab kategori) => kategori switch
    {
        KategoriLab.PatologiKlinik => "[PK]",
        KategoriLab.PatologiAnatomi => "[PA]",
        KategoriLab.Mikrobiologi => "[MB]",
        _ => "[PK]",
    };

    public async Task<string> CreatePermintaanRadiologiAsync(string noRawat, string dokterPerujuk, StatusKunjunganPenunjang status, string? diagnosaKlinis, string? informasiTambahan, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var noOrder = GenerateNoOrder();

        db.PermintaanRadiologiSet.Add(new PermintaanRadiologi
        {
            NoOrder = noOrder,
            NoRawat = noRawat,
            TanggalPermintaan = DateOnly.FromDateTime(now),
            JamPermintaan = TimeOnly.FromDateTime(now),
            TanggalSampel = DateOnly.FromDateTime(now),
            JamSampel = TimeOnly.FromDateTime(now),
            TanggalHasil = DateOnly.FromDateTime(now),
            JamHasil = TimeOnly.FromDateTime(now),
            DokterPerujuk = dokterPerujuk,
            Status = status,
            InformasiTambahan = informasiTambahan,
            DiagnosaKlinis = diagnosaKlinis,
        });

        await db.SaveChangesAsync(ct);
        return noOrder;
    }

    public async Task SaveHasilLabAsync(string noRawat, string? saran, string? kesan, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        db.HasilLabSet.Add(new HasilLab
        {
            NoRawat = noRawat,
            TanggalPeriksa = DateOnly.FromDateTime(now),
            Jam = TimeOnly.FromDateTime(now),
            Saran = saran,
            Kesan = kesan,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveHasilRadiologiAsync(string noRawat, string hasil, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        db.HasilRadiologiSet.Add(new HasilRadiologi
        {
            NoRawat = noRawat,
            TanggalPeriksa = DateOnly.FromDateTime(now),
            Jam = TimeOnly.FromDateTime(now),
            Hasil = hasil,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// `noorder` is varchar(15), NOT an AUTO_INCREMENT column — legacy generates
    /// its own order numbering scheme we haven't reverse-engineered. Timestamp
    /// (12 digits, second resolution) + 2 random digits fits in 15 chars and is
    /// unique enough for this app's throughput; revisit if real usage needs
    /// cross-process collision safety.
    /// </summary>
    private static string GenerateNoOrder()
        => DateTime.Now.ToString("yyMMddHHmmss") + Random.Shared.Next(10, 99);
}
