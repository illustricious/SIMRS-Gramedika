using Microsoft.EntityFrameworkCore;

namespace SimrsKhanza.Data;

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
    string? NoTelpPasien);

/// <summary>Optional narrowing filters — mirrors the "No.Rawat / No.Reg / No.R.M. / Nama Pasien" search row in the screenshot.</summary>
public record RegistrasiSearchFilter(
    string? NoRawat = null,
    string? NoReg = null,
    string? NoRekamMedis = null,
    string? NamaPasien = null);

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
        DateOnly fromDate, DateOnly toDate, RegistrasiSearchFilter? filter = null, CancellationToken ct = default);
}

public class RegistrasiRepository(SimrsKhanzaDbContext db) : IRegistrasiRepository
{
    public Task<IReadOnlyList<RegistrasiListItem>> GetTodayAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return GetByPeriodeAsync(today, today, null, ct);
    }

    public async Task<IReadOnlyList<RegistrasiListItem>> GetByPeriodeAsync(
        DateOnly fromDate, DateOnly toDate, RegistrasiSearchFilter? filter = null, CancellationToken ct = default)
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
            where r.TanggalRegistrasi >= fromDate && r.TanggalRegistrasi <= toDate
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
            NoTelpPasien: row.NoTelepon)).ToList();
    }

    // .ToString() on these enums gives the C# member name (e.g. "SudahBayar"),
    // not the legacy display string ("Sudah Bayar") — map explicitly instead.
    // (Mirrors the dictionaries SimrsKhanzaDbContext uses for the DB-level
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
}
