using Microsoft.EntityFrameworkCore;

namespace SimrsGramedika.Data;

/// <summary>
/// Read-side row for the Registrasi / "Daftar Pasien Rawat Jalan" list screen
/// (legacy: reg_periksa+pasien+poliklinik+dokter+penjab join). Not an EF
/// entity — a projection shaped for the grid. Column set cross-checked
/// against the "Daftar Pasien Rawat Jalan" screenshot the user shared:
/// Kode Dokter, Dokter Dituju, No.RM, Pasien, Poliklinik, Penanggung Jawab,
/// Alamat P.J., Hubungan P.J., Biaya Reg, Jenis Bayar, Status, No.Rawat,
/// Tanggal, Jam, No.Reg, Status Bayar, Stts Poli, No.Telp Pasien.
/// </summary>
public record RegistrasiListItem(
    string? NoReg,
    string NoRawat,
    string? KodeDokter,
    string? NamaDokter,
    string NoRekamMedis,
    string NamaPasien,
    string? NamaPoli,
    string? PenanggungJawab,
    string? AlamatPj,
    string? HubunganPj,
    decimal? BiayaReg,
    string? JenisBayar,
    string? Status,
    DateOnly? TanggalRegistrasi,
    TimeOnly? JamRegistrasi,
    string? StatusBayar,
    string? StatusPoli,
    string? NoTelpPasien,
    string? JenisKelamin = null,
    string? Umur = null,
    string? StatusRawat = null,
    string? KodePoli = null,
    string? KodePj = null);

/// <summary>Optional narrowing filters — mirrors the "No.Rawat / No.Reg / No.R.M. / Nama Pasien" search row in the screenshot.</summary>
public record RegistrasiSearchFilter(
    string? NoRawat = null,
    string? NoReg = null,
    string? NoRekamMedis = null,
    string? NamaPasien = null);

public sealed record RegistrasiInputDetails(string? StatusPendaftaran, string? NoKartu, string? AsalRujukan);

public interface IRegistrasiRepository
{
    /// <summary>Convenience wrapper — kept for callers that only care about "today".</summary>
    Task<IReadOnlyList<RegistrasiListItem>> GetTodayAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists registrations in [from, to] inclusive, optionally narrowed by
    /// RegistrasiSearchFilter. Backs the "Periode" date range + search-field
    /// row in RegistrasiView.
    /// </summary>
    Task<IReadOnlyList<RegistrasiListItem>> GetByPeriodeAsync(
        DateOnly fromDate, DateOnly toDate, RegistrasiSearchFilter? filter = null, CancellationToken ct = default, string? kodePoli = null);

    Task<IReadOnlyList<Core.Entities.Poliklinik>> GetPoliklinikAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Core.Entities.Dokter>> GetDokterAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Core.Entities.Penjab>> GetPenjabAsync(CancellationToken ct = default);
}

public class RegistrasiRepository(SimrsGramedikaDbContext db) : IRegistrasiRepository
{
    public async Task UpdateAsync(string noRawat, string noRm, string kodeDokter, string kodePoli,
        string kodePenjab, string penanggungJawab, string alamat, string hubungan, CancellationToken ct = default)
    {
        var row = await db.RegPeriksaSet.SingleOrDefaultAsync(x => x.NoRawat == noRawat, ct)
            ?? throw new InvalidOperationException("Registrasi tidak ditemukan lagi. Muat ulang daftar.");
        if (row.NoRekamMedis != noRm)
            throw new InvalidOperationException("Data pasien berubah. Muat ulang daftar sebelum menyimpan.");
        row.KodeDokter = kodeDokter;
        row.KodePoli = kodePoli;
        row.KodePenjab = kodePenjab;
        row.PenanggungJawab = penanggungJawab;
        row.AlamatPj = alamat;
        row.HubunganPj = hubungan;
        await db.SaveChangesAsync(ct);
    }
    public Task<IReadOnlyList<RegistrasiListItem>> GetTodayAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return GetByPeriodeAsync(today, today, null, ct);
    }

    public Task<IReadOnlyList<RegistrasiListItem>> GetByPeriodeAsync(
        DateOnly fromDate, DateOnly toDate, RegistrasiSearchFilter? filter = null, CancellationToken ct = default, string? kodePoli = null)
        => GetVisitsAsync(fromDate, toDate, filter, ct, kodePoli, null);

    public Task<IReadOnlyList<RegistrasiListItem>> GetPatientHistoryAsync(string noRekamMedis, CancellationToken ct = default)
        => GetVisitsAsync(null, null, null, ct, null, noRekamMedis);

    private async Task<IReadOnlyList<RegistrasiListItem>> GetVisitsAsync(
        DateOnly? fromDate, DateOnly? toDate, RegistrasiSearchFilter? filter, CancellationToken ct, string? kodePoli, string? noRekamMedis)
    {
        var query =
            from r in db.RegPeriksaSet
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join poli in db.PoliklinikSet on r.KodePoli equals poli.KodePoli into poliJoin
            from poli in poliJoin.DefaultIfEmpty()
            join dok in db.DokterSet on r.KodeDokter equals dok.KodeDokter into dokJoin
            from dok in dokJoin.DefaultIfEmpty()
            join pj in db.PenjabSet on r.KodePenjab equals pj.KodePenjab into pjJoin
            from pj in pjJoin.DefaultIfEmpty()
            where (!fromDate.HasValue || r.TanggalRegistrasi >= fromDate)
                  && (!toDate.HasValue || r.TanggalRegistrasi <= toDate)
                  && (noRekamMedis == null || r.NoRekamMedis == noRekamMedis)
                  && (kodePoli == null || r.KodePoli == kodePoli)
            select new
            {
                r.NoReg,
                r.NoRawat,
                r.KodeDokter,
                DokterNama = dok != null ? dok.NamaDokter : null,
                r.NoRekamMedis,
                p.NamaPasien,
                PoliNama = poli != null ? poli.NamaPoli : null,
                r.PenanggungJawab,
                r.AlamatPj,
                r.HubunganPj,
                r.BiayaReg,
                JenisBayar = pj != null ? pj.NamaPenanggungJawab : null,
                r.Status,
                r.TanggalRegistrasi,
                r.JamRegistrasi,
                r.StatusBayar,
                r.StatusPoli,
                p.NoTelepon,
                p.JenisKelamin,
                p.TanggalLahir,
                r.StatusLanjut,
                r.KodePoli,
                r.KodePenjab,
            };

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.NoRawat))
                query = query.Where(x => x.NoRawat.Contains(filter.NoRawat));
            if (!string.IsNullOrWhiteSpace(filter.NoReg))
                query = query.Where(x => x.NoReg != null && x.NoReg.Contains(filter.NoReg));
            if (!string.IsNullOrWhiteSpace(filter.NoRekamMedis))
                query = query.Where(x => x.NoRekamMedis.Contains(filter.NoRekamMedis));
            if (!string.IsNullOrWhiteSpace(filter.NamaPasien))
                query = query.Where(x => x.NamaPasien.Contains(filter.NamaPasien));
        }

        var rows = await query.OrderByDescending(x => x.TanggalRegistrasi).ThenByDescending(x => x.JamRegistrasi).ToListAsync(ct);

        return rows.Select(row => new RegistrasiListItem(
            NoReg: row.NoReg,
            NoRawat: row.NoRawat,
            KodeDokter: row.KodeDokter,
            NamaDokter: row.DokterNama,
            NoRekamMedis: row.NoRekamMedis,
            NamaPasien: row.NamaPasien,
            NamaPoli: row.PoliNama,
            PenanggungJawab: row.PenanggungJawab,
            AlamatPj: row.AlamatPj,
            HubunganPj: row.HubunganPj,
            BiayaReg: row.BiayaReg,
            JenisBayar: row.JenisBayar,
            Status: row.Status is { } s ? ToDisplay(s) : null,
            TanggalRegistrasi: row.TanggalRegistrasi,
            JamRegistrasi: row.JamRegistrasi,
            StatusBayar: row.StatusBayar is { } sb ? ToDisplay(sb) : null,
            StatusPoli: row.StatusPoli is { } sp ? ToDisplay(sp) : null,
            NoTelpPasien: row.NoTelepon,
            JenisKelamin: row.JenisKelamin == Core.Entities.JenisKelamin.LakiLaki ? "L" : "P",
            Umur: UmurText(row.TanggalLahir, row.TanggalRegistrasi),
            StatusRawat: row.StatusLanjut == Core.Entities.StatusLanjut.RawatInap ? "Ranap" : "Ralan",
            KodePoli: row.KodePoli,
            KodePj: row.KodePenjab)).ToList();
    }

    public Task<RegistrasiInputDetails?> GetInputDetailsAsync(string noRawat, CancellationToken ct = default)
        => db.Database.SqlQuery<RegistrasiInputDetails>($"""
            SELECT r.stts_daftar AS StatusPendaftaran, p.no_peserta AS NoKartu,
                   (SELECT MIN(m.perujuk) FROM rujuk_masuk m WHERE m.no_rawat = r.no_rawat) AS AsalRujukan
            FROM reg_periksa r
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            WHERE r.no_rawat = {noRawat}
            """).SingleOrDefaultAsync(ct);


    public async Task<IReadOnlyList<Core.Entities.Poliklinik>> GetPoliklinikAsync(CancellationToken ct = default)
        => await db.PoliklinikSet.OrderBy(p => p.NamaPoli).ToListAsync(ct);

    public async Task<IReadOnlyList<Core.Entities.Dokter>> GetDokterAsync(CancellationToken ct = default)
        => await db.DokterSet.OrderBy(d => d.NamaDokter).ToListAsync(ct);

    public async Task<IReadOnlyList<Core.Entities.Penjab>> GetPenjabAsync(CancellationToken ct = default)
        => await db.PenjabSet.OrderBy(p => p.NamaPenanggungJawab).ToListAsync(ct);

    // .ToString() on these enums gives the C# member name (e.g. "SudahBayar"),
    // not the legacy display string ("Sudah Bayar") — map explicitly instead.
    // (Mirrors the dictionaries SimrsGramedikaDbContext uses for the DB-level
    // HasConversion; duplicated here because those are private to the
    // ModelBuilder config, and this is a read-projection concern, not a
    // mapping concern.)
    private static string ToDisplay(Core.Entities.StatusRegistrasi v) => v switch
    {
        Core.Entities.StatusRegistrasi.Belum => "Belum",
        Core.Entities.StatusRegistrasi.Sudah => "Sudah",
        Core.Entities.StatusRegistrasi.Batal => "Batal",
        Core.Entities.StatusRegistrasi.BerkasDiterima => "Berkas Diterima",
        Core.Entities.StatusRegistrasi.Dirujuk => "Dirujuk",
        Core.Entities.StatusRegistrasi.Meninggal => "Meninggal",
        Core.Entities.StatusRegistrasi.Dirawat => "Dirawat",
        Core.Entities.StatusRegistrasi.PulangPaksa => "Pulang Paksa",
        _ => v.ToString(),
    };

    private static string ToDisplay(Core.Entities.StatusBayar v)
        => v == Core.Entities.StatusBayar.SudahBayar ? "Sudah Bayar" : "Belum Bayar";

    private static string ToDisplay(Core.Entities.StatusPendaftaran v)
        => v == Core.Entities.StatusPendaftaran.Baru ? "Baru" : "Lama";

    private static string UmurText(DateOnly? lahir, DateOnly? daftar)
    {
        if (lahir is null || lahir.Value.Year < 1900) return "-";
        var today = daftar ?? DateOnly.FromDateTime(DateTime.Today);
        var years = today.Year - lahir.Value.Year - (today < lahir.Value.AddYears(today.Year - lahir.Value.Year) ? 1 : 0);
        if (years >= 1) return $"{years} Th";
        var months = (today.Year - lahir.Value.Year) * 12 + today.Month - lahir.Value.Month - (today.Day < lahir.Value.Day ? 1 : 0);
        return months >= 1 ? $"{months} Bl" : $"{Math.Max(0, today.DayNumber - lahir.Value.DayNumber)} Hr";
    }
}
