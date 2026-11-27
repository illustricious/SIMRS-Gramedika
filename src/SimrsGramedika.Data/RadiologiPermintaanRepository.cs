using System.Text;
using Microsoft.EntityFrameworkCore;

// Interpolated fragments are fixed table/column names and generated {n} placeholders; every user value is a {n} parameter.
#pragma warning disable EF1002

namespace SimrsGramedika.Data;

/// <summary>
/// One permintaan radiologi row. In the "Data Permintaan" list <see cref="Pemeriksaan"/> is empty; in the
/// "Item Permintaan" list there is one row per ordered examination. All values are display strings.
/// </summary>
public class RadiologiPermintaanRow
{
    public string NoOrder { get; set; } = "";
    public string NoRawat { get; set; } = "";
    public string NoRm { get; set; } = "";
    public string NamaPasien { get; set; } = "";
    public string Pemeriksaan { get; set; } = "";
    public string TglPermintaan { get; set; } = "";
    public string JamPermintaan { get; set; } = "";
    public string TglSampel { get; set; } = "";
    public string JamSampel { get; set; } = "";
    public string TglHasil { get; set; } = "";
    public string JamHasil { get; set; } = "";
    public string KodeDokter { get; set; } = "";
    public string NamaDokter { get; set; } = "";
    public string Lokasi { get; set; } = "";
    public string InformasiTambahan { get; set; } = "";
    public string DiagnosaKlinis { get; set; } = "";
    public string KodePj { get; set; } = "";
    public string JenisBayar { get; set; } = "";
}

/// <summary>One ordered examination under a permintaan (the nested rows of the legacy "Data Permintaan" grid).</summary>
public class RadiologiPermintaanItem
{
    public string NoOrder { get; set; } = "";
    public string KodeJenis { get; set; } = "";
    public string Pemeriksaan { get; set; } = "";
}

public enum RadiologiLokasi { RawatJalan, RawatInap }

public record RadiologiPermintaanFilter(
    RadiologiLokasi Lokasi, DateOnly From, DateOnly To,
    string? Dokter, string? PoliAtauKamar, string? Keyword, bool HanyaBelumPulang);

public interface IRadiologiPermintaanRepository
{
    /// <summary>Legacy tampil()/tampil3(): one row per permintaan.</summary>
    Task<IReadOnlyList<RadiologiPermintaanRow>> GetListAsync(RadiologiPermintaanFilter filter, CancellationToken ct = default);

    /// <summary>Legacy tampil2()/tampil4(): one row per ordered examination (keyword also matches exam and doctor names).</summary>
    Task<IReadOnlyList<RadiologiPermintaanRow>> GetItemRowsAsync(RadiologiPermintaanFilter filter, CancellationToken ct = default);

    Task<IReadOnlyList<RadiologiPermintaanItem>> GetItemsAsync(IReadOnlyCollection<string> noOrders, CancellationToken ct = default);

    /// <summary>Legacy rule: refuse when already sampled (unless admin) or when any examination is already paid.</summary>
    Task DeleteAsync(string noOrder, bool bypassSampelCheck, CancellationToken ct = default);

    /// <summary>
    /// Stamps tgl_sampel/jam_sampel. For rawat jalan legacy also re-arms the `antriradiologi` display queue
    /// (delete + insert '1'); the rawat inap branch does not.
    /// </summary>
    Task UpdateSampelAsync(string noOrder, DateTime waktu, bool rawatJalan, CancellationToken ct = default);
}

public class RadiologiPermintaanRepository(SimrsGramedikaDbContext db) : IRadiologiPermintaanRepository
{
    private const int MaxRows = 1000;

    private const string SelectHeader = @"select p.noorder AS NoOrder, p.no_rawat AS NoRawat, r.no_rkm_medis AS NoRm, ps.nm_pasien AS NamaPasien,
            {PEMERIKSAAN} AS Pemeriksaan,
            date_format(p.tgl_permintaan,'%Y-%m-%d') AS TglPermintaan,
            if(p.jam_permintaan='00:00:00','',cast(p.jam_permintaan as char)) AS JamPermintaan,
            if(p.tgl_sampel='0000-00-00','',date_format(p.tgl_sampel,'%Y-%m-%d')) AS TglSampel,
            if(p.jam_sampel='00:00:00','',cast(p.jam_sampel as char)) AS JamSampel,
            if(p.tgl_hasil='0000-00-00','',date_format(p.tgl_hasil,'%Y-%m-%d')) AS TglHasil,
            if(p.jam_hasil='00:00:00','',cast(p.jam_hasil as char)) AS JamHasil,
            p.dokter_perujuk AS KodeDokter, dk.nm_dokter AS NamaDokter,
            ifnull(p.informasi_tambahan,'') AS InformasiTambahan, ifnull(p.diagnosa_klinis,'') AS DiagnosaKlinis,
            r.kd_pj AS KodePj, pj.png_jawab AS JenisBayar, ";

    public Task<IReadOnlyList<RadiologiPermintaanRow>> GetListAsync(RadiologiPermintaanFilter f, CancellationToken ct = default)
        => QueryAsync(f, perItem: false, ct);

    public Task<IReadOnlyList<RadiologiPermintaanRow>> GetItemRowsAsync(RadiologiPermintaanFilter f, CancellationToken ct = default)
        => QueryAsync(f, perItem: true, ct);

    private async Task<IReadOnlyList<RadiologiPermintaanRow>> QueryAsync(RadiologiPermintaanFilter f, bool perItem, CancellationToken ct)
    {
        var ranap = f.Lokasi == RadiologiLokasi.RawatInap;
        var args = new List<object> { f.From.ToString("yyyy-MM-dd"), f.To.ToString("yyyy-MM-dd") };
        string P(string value) { args.Add(value); return "{" + (args.Count - 1) + "}"; }

        var sb = new StringBuilder();
        sb.Append(SelectHeader.Replace("{PEMERIKSAAN}", perItem ? "jr.nm_perawatan" : "''"));
        sb.Append(ranap
            ? "concat(ifnull(ki_last.kd_kamar,''),' ',ifnull(bg.nm_bangsal,'Ranap Gabung')) AS Lokasi"
            : "ifnull(pl.nm_poli,'') AS Lokasi");

        sb.Append(@" from permintaan_radiologi p
            inner join reg_periksa r on p.no_rawat=r.no_rawat
            inner join pasien ps on r.no_rkm_medis=ps.no_rkm_medis ");
        if (perItem)
        {
            sb.Append(@"inner join permintaan_pemeriksaan_radiologi pp on p.noorder=pp.noorder
                inner join jns_perawatan_radiologi jr on jr.kd_jenis_prw=pp.kd_jenis_prw ");
        }
        sb.Append(@"inner join dokter dk on p.dokter_perujuk=dk.kd_dokter
            inner join penjab pj on r.kd_pj=pj.kd_pj ");

        if (ranap)
        {
            // "Belum Pulang" narrows the last stay to the still-open one (stts_pulang='-'), as legacy tampil3/tampil4.
            var open = f.HanyaBelumPulang ? "where stts_pulang='-' " : "";
            var openOuter = f.HanyaBelumPulang ? "where ki.stts_pulang='-'" : "";
            sb.Append($@"left join (select ki.no_rawat, ki.kd_kamar from kamar_inap ki
                inner join (select no_rawat, max(concat(tgl_masuk,' ',jam_masuk)) as max_masuk from kamar_inap {open}group by no_rawat) latest
                on ki.no_rawat=latest.no_rawat and concat(ki.tgl_masuk,' ',ki.jam_masuk)=latest.max_masuk {openOuter}) ki_last
                on ki_last.no_rawat=coalesce((select rg.no_rawat from ranap_gabung rg where rg.no_rawat2=r.no_rawat limit 1),r.no_rawat)
                left join kamar km on ki_last.kd_kamar=km.kd_kamar
                left join bangsal bg on km.kd_bangsal=bg.kd_bangsal ");
        }
        else
        {
            sb.Append("inner join poliklinik pl on r.kd_poli=pl.kd_poli ");
        }

        sb.Append($"where p.status='{(ranap ? "ranap" : "ralan")}' and p.tgl_permintaan between {{0}} and {{1}} ");

        if (!string.IsNullOrWhiteSpace(f.Dokter)) sb.Append($"and dk.nm_dokter like {P("%" + f.Dokter.Trim() + "%")} ");
        if (!string.IsNullOrWhiteSpace(f.PoliAtauKamar))
            sb.Append(ranap
                ? $"and bg.nm_bangsal like {P("%" + f.PoliAtauKamar.Trim() + "%")} "
                : $"and pl.nm_poli like {P("%" + f.PoliAtauKamar.Trim() + "%")} ");
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var kw = P("%" + f.Keyword.Trim() + "%");
            sb.Append($"and (p.noorder like {kw} or p.no_rawat like {kw} or r.no_rkm_medis like {kw} or ps.nm_pasien like {kw} or p.diagnosa_klinis like {kw} or pj.png_jawab like {kw}");
            if (perItem) sb.Append($" or jr.nm_perawatan like {kw} or dk.nm_dokter like {kw}");
            sb.Append(") ");
        }

        // Legacy ordering: rawat jalan ascending by date, rawat inap descending; both newest time first within a day.
        var dateOrder = ranap ? "p.tgl_permintaan desc" : "p.tgl_permintaan";
        sb.Append(perItem ? "group by p.noorder, pp.kd_jenis_prw " : "group by p.noorder ");
        sb.Append($"order by {dateOrder}, p.jam_permintaan desc limit {MaxRows}");

        return await db.Database.SqlQueryRaw<RadiologiPermintaanRow>(sb.ToString(), args.ToArray()).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RadiologiPermintaanItem>> GetItemsAsync(IReadOnlyCollection<string> noOrders, CancellationToken ct = default)
    {
        if (noOrders.Count == 0) return Array.Empty<RadiologiPermintaanItem>();

        var args = noOrders.Cast<object>().ToList();
        var inList = string.Join(",", Enumerable.Range(0, args.Count).Select(i => "{" + i + "}"));

        return await db.Database.SqlQueryRaw<RadiologiPermintaanItem>($@"
            select x.noorder AS NoOrder, x.kd_jenis_prw AS KodeJenis, j.nm_perawatan AS Pemeriksaan
            from permintaan_pemeriksaan_radiologi x inner join jns_perawatan_radiologi j on x.kd_jenis_prw=j.kd_jenis_prw
            where x.noorder in ({inList}) order by x.noorder, j.nm_perawatan", args.ToArray()).ToListAsync(ct);
    }

    public async Task DeleteAsync(string noOrder, bool bypassSampelCheck, CancellationToken ct = default)
    {
        var sampel = (await db.Database.SqlQueryRaw<string>(
            "select if(tgl_sampel='0000-00-00','',date_format(tgl_sampel,'%Y-%m-%d')) AS Value from permintaan_radiologi where noorder={0}",
            noOrder).ToListAsync(ct)).FirstOrDefault()
            ?? throw new InvalidOperationException($"Permintaan '{noOrder}' tidak ditemukan.");

        if (sampel.Length > 0 && !bypassSampelCheck)
            throw new InvalidOperationException("Maaf, sudah dilakukan pengambilan sampel.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var paid = (await db.Database.SqlQueryRaw<long>(
            "select count(*) AS Value from permintaan_pemeriksaan_radiologi where stts_bayar='Sudah' and noorder={0}", noOrder).ToListAsync(ct)).First();
        if (paid > 0)
            throw new InvalidOperationException("Maaf, tidak boleh dihapus karena sudah ada tindakan yang sudah dibayar. Silakan hubungi kasir.");

        // Legacy deletes only the header and relies on ON DELETE CASCADE; deleting the examinations first is
        // equivalent there and keeps this correct on a schema without the cascade.
        await db.Database.ExecuteSqlRawAsync("delete from permintaan_pemeriksaan_radiologi where noorder={0}", [noOrder], ct);
        await db.Database.ExecuteSqlRawAsync("delete from permintaan_radiologi where noorder={0}", [noOrder], ct);

        await tx.CommitAsync(ct);
    }

    public async Task UpdateSampelAsync(string noOrder, DateTime waktu, bool rawatJalan, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var n = await db.Database.ExecuteSqlRawAsync(
            "update permintaan_radiologi set tgl_sampel={0}, jam_sampel={1} where noorder={2}",
            [waktu.ToString("yyyy-MM-dd"), waktu.ToString("HH:mm:ss"), noOrder], ct);
        if (n == 0) throw new InvalidOperationException($"Permintaan '{noOrder}' tidak ditemukan.");

        if (rawatJalan)
        {
            await db.Database.ExecuteSqlRawAsync("delete from antriradiologi", [], ct);
            await db.Database.ExecuteSqlRawAsync("insert into antriradiologi values('1')", [], ct);
        }

        await tx.CommitAsync(ct);
    }
}
