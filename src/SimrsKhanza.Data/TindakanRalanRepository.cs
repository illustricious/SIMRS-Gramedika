using Microsoft.EntityFrameworkCore;

namespace SimrsKhanza.Data;

/// <summary>
/// One row of the legacy "Perawatan/Tindakan Rawat Jalan" tables — the same
/// shape serves all three tabs (Dokter / Paramedis / Dokter+Paramedis); fields
/// that don't apply to a tab are empty. Numbers arrive as text and are
/// formatted in <see cref="TindakanRalanRepository"/>.
/// </summary>
public class TindakanRalanRow
{
    public string NoRawat { get; set; } = "";
    public string NoRm { get; set; } = "";
    public string NamaPasien { get; set; } = "";
    public string Tindakan { get; set; } = "";
    public string KodeDokter { get; set; } = "";
    public string NamaDokter { get; set; } = "";
    public string Nip { get; set; } = "";
    public string NamaPetugas { get; set; } = "";
    public string TglRawat { get; set; } = "";
    public string JamRawat { get; set; } = "";
    public string Biaya { get; set; } = "";
    public string Kode { get; set; } = "";
    public string TarifDokter { get; set; } = "";
    public string TarifPetugas { get; set; } = "";
    public string Kso { get; set; } = "";
    public string JasaSarana { get; set; } = "";
    public string Bhp { get; set; } = "";
    public string Menejemen { get; set; } = "";
}

public enum TindakanRalanJenis { Dokter, Paramedis, DokterParamedis }

public interface ITindakanRalanRepository
{
    /// <summary>Read-only list of treatments given, same joins/filters as legacy DlgRawatJalan.tampilDr/Pr/DrPr (capped at 1000 rows).</summary>
    Task<IReadOnlyList<TindakanRalanRow>> GetAsync(
        TindakanRalanJenis jenis, DateOnly from, DateOnly to, string? noRm, string? keyword, CancellationToken ct = default, string? noRawat = null);
}

public class TindakanRalanRepository(SimrsKhanzaDbContext db) : ITindakanRalanRepository
{
    private const string Select = @"
        d.no_rawat AS NoRawat, r.no_rkm_medis AS NoRm, p.nm_pasien AS NamaPasien,
        concat(d.kd_jenis_prw,' ',j.nm_perawatan) AS Tindakan,
        {DR} AS KodeDokter, {DRN} AS NamaDokter, {NIP} AS Nip, {NIPN} AS NamaPetugas,
        date_format(d.tgl_perawatan,'%Y-%m-%d') AS TglRawat, cast(d.jam_rawat AS char) AS JamRawat,
        cast(d.biaya_rawat AS char) AS Biaya, d.kd_jenis_prw AS Kode,
        {TDR} AS TarifDokter, {TPR} AS TarifPetugas,
        cast(d.kso AS char) AS Kso, cast(d.material AS char) AS JasaSarana,
        cast(d.bhp AS char) AS Bhp, cast(d.menejemen AS char) AS Menejemen";

    public async Task<IReadOnlyList<TindakanRalanRow>> GetAsync(
        TindakanRalanJenis jenis, DateOnly from, DateOnly to, string? noRm, string? keyword, CancellationToken ct = default, string? noRawat = null)
    {
        var kw = "%" + (keyword?.Trim() ?? "") + "%";
        var rm = "%" + (noRm?.Trim() ?? "") + "%";

        // Table/column names below are constants chosen from the enum, never user text;
        // every user-supplied value goes through a {n} parameter.
        var (table, cols, joins, kwCond) = jenis switch
        {
            TindakanRalanJenis.Dokter => (
                "rawat_jl_dr",
                Select.Replace("{DR}", "d.kd_dokter").Replace("{DRN}", "dk.nm_dokter").Replace("{NIP}", "''").Replace("{NIPN}", "''")
                      .Replace("{TDR}", "cast(d.tarif_tindakandr AS char)").Replace("{TPR}", "''"),
                "inner join dokter dk on d.kd_dokter=dk.kd_dokter",
                "d.kd_dokter like {3} or dk.nm_dokter like {3}"),
            TindakanRalanJenis.Paramedis => (
                "rawat_jl_pr",
                Select.Replace("{DR}", "''").Replace("{DRN}", "''").Replace("{NIP}", "d.nip").Replace("{NIPN}", "pt.nama")
                      .Replace("{TDR}", "''").Replace("{TPR}", "cast(d.tarif_tindakanpr AS char)"),
                "inner join petugas pt on d.nip=pt.nip",
                "d.nip like {3} or pt.nama like {3}"),
            _ => (
                "rawat_jl_drpr",
                Select.Replace("{DR}", "d.kd_dokter").Replace("{DRN}", "dk.nm_dokter").Replace("{NIP}", "d.nip").Replace("{NIPN}", "pt.nama")
                      .Replace("{TDR}", "cast(d.tarif_tindakandr AS char)").Replace("{TPR}", "cast(d.tarif_tindakanpr AS char)"),
                "inner join dokter dk on d.kd_dokter=dk.kd_dokter inner join petugas pt on d.nip=pt.nip",
                "d.kd_dokter like {3} or dk.nm_dokter like {3} or d.nip like {3} or pt.nama like {3}"),
        };

        var sql = $@"select {cols}
            from {table} d
            inner join reg_periksa r on r.no_rawat=d.no_rawat
            inner join pasien p on p.no_rkm_medis=r.no_rkm_medis
            inner join jns_perawatan j on d.kd_jenis_prw=j.kd_jenis_prw
            {joins}
            where (({{4}}='' and d.tgl_perawatan between {{0}} and {{1}}) or ({{4}}<>'' and d.no_rawat={{4}}))
              and r.no_rkm_medis like {{2}}
              and (d.no_rawat like {{3}} or r.no_rkm_medis like {{3}} or p.nm_pasien like {{3}} or j.nm_perawatan like {{3}} or {kwCond})
            order by d.no_rawat, d.tgl_perawatan, d.jam_rawat desc
            limit 1000";

        var rows = await db.Database
            .SqlQueryRaw<TindakanRalanRow>(sql, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"), rm, kw, noRawat ?? "")
            .ToListAsync(ct);

        foreach (var r in rows)
        {
            r.Biaya = Money(r.Biaya);
            r.TarifDokter = Money(r.TarifDokter);
            r.TarifPetugas = Money(r.TarifPetugas);
            r.Kso = Money(r.Kso);
            r.JasaSarana = Money(r.JasaSarana);
            r.Bhp = Money(r.Bhp);
            r.Menejemen = Money(r.Menejemen);
        }
        return rows;
    }

    private static string Money(string s)
        => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v.ToString("N0") : s;
}
