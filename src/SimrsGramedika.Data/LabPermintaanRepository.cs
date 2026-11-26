using System.Text;
using Microsoft.EntityFrameworkCore;

// Interpolated table names come only from the LabCategory switch; every user value is a {n} parameter.
#pragma warning disable EF1002

namespace SimrsGramedika.Data;

/// <summary>One permintaan lab header row (shared by PK/PA/MB; PA extras are empty for the others). All values are display strings.</summary>
public class LabPermintaanRow
{
    public string NoOrder { get; set; } = "";
    public string NoRawat { get; set; } = "";
    public string NoRm { get; set; } = "";
    public string NamaPasien { get; set; } = "";
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
    public string TglBahan { get; set; } = "";
    public string DiperolehDengan { get; set; } = "";
    public string LokasiJaringan { get; set; } = "";
    public string DiawetkanDengan { get; set; } = "";
    public string PernahDilakukanDi { get; set; } = "";
    public string TglPaSebelumnya { get; set; } = "";
    public string NoPaSebelumnya { get; set; } = "";
    public string DiagnosaPaSebelumnya { get; set; } = "";
}

/// <summary>One ordered examination (and, for PK/MB, one of its template items) under a permintaan.</summary>
public class LabPermintaanItem
{
    public string NoOrder { get; set; } = "";
    public string KodeJenis { get; set; } = "";
    public string Pemeriksaan { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Satuan { get; set; } = "";
    public string RujukanLd { get; set; } = "";
    public string RujukanLa { get; set; } = "";
    public string RujukanPd { get; set; } = "";
    public string RujukanPa { get; set; } = "";
}

public enum LabLokasi { RawatJalan, RawatInap }

public record LabPermintaanFilter(
    LabLokasi Lokasi, DateOnly From, DateOnly To,
    string? Dokter, string? PoliAtauKamar, string? Keyword, bool HanyaBelumPulang);

public interface ILabPermintaanRepository
{
    Task<IReadOnlyList<LabPermintaanRow>> GetListAsync(LabCategory category, LabPermintaanFilter filter, CancellationToken ct = default);

    /// <summary>Examinations (and template items where the category has them) for the given permintaan numbers.</summary>
    Task<IReadOnlyList<LabPermintaanItem>> GetItemsAsync(LabCategory category, IReadOnlyCollection<string> noOrders, CancellationToken ct = default);

    /// <summary>Legacy rule: refuse when already sampled (unless admin) or when any item is already paid.</summary>
    Task DeleteAsync(LabCategory category, string noOrder, bool bypassSampelCheck, CancellationToken ct = default);

    Task UpdateSampelAsync(LabCategory category, string noOrder, DateTime waktu, CancellationToken ct = default);
}

public class LabPermintaanRepository(SimrsGramedikaDbContext db) : ILabPermintaanRepository
{
    private const int MaxRows = 1000;

    // Table names come only from this enum mapping, never from user text.
    private static (string Header, string Pemeriksaan, string? Detail) Tables(LabCategory c) => c switch
    {
        LabCategory.PK => ("permintaan_lab", "permintaan_pemeriksaan_lab", "permintaan_detail_permintaan_lab"),
        LabCategory.PA => ("permintaan_labpa", "permintaan_pemeriksaan_labpa", null),
        _ => ("permintaan_labmb", "permintaan_pemeriksaan_labmb", "permintaan_detail_permintaan_labmb"),
    };

    public async Task<IReadOnlyList<LabPermintaanRow>> GetListAsync(LabCategory category, LabPermintaanFilter f, CancellationToken ct = default)
    {
        var t = Tables(category).Header;
        var ranap = f.Lokasi == LabLokasi.RawatInap;
        var args = new List<object> { f.From.ToString("yyyy-MM-dd"), f.To.ToString("yyyy-MM-dd") };
        string P(string value) { args.Add(value); return "{" + (args.Count - 1) + "}"; }

        var sb = new StringBuilder();
        sb.Append($@"select {t}.noorder AS NoOrder, {t}.no_rawat AS NoRawat, r.no_rkm_medis AS NoRm, p.nm_pasien AS NamaPasien,
            date_format({t}.tgl_permintaan,'%Y-%m-%d') AS TglPermintaan,
            if({t}.jam_permintaan='00:00:00','',cast({t}.jam_permintaan as char)) AS JamPermintaan,
            if({t}.tgl_sampel='0000-00-00','',date_format({t}.tgl_sampel,'%Y-%m-%d')) AS TglSampel,
            if({t}.jam_sampel='00:00:00','',cast({t}.jam_sampel as char)) AS JamSampel,
            if({t}.tgl_hasil='0000-00-00','',date_format({t}.tgl_hasil,'%Y-%m-%d')) AS TglHasil,
            if({t}.jam_hasil='00:00:00','',cast({t}.jam_hasil as char)) AS JamHasil,
            {t}.dokter_perujuk AS KodeDokter, dk.nm_dokter AS NamaDokter,
            ifnull({t}.informasi_tambahan,'') AS InformasiTambahan, ifnull({t}.diagnosa_klinis,'') AS DiagnosaKlinis,
            r.kd_pj AS KodePj, pj.png_jawab AS JenisBayar, ");

        sb.Append(ranap
            ? "concat(ifnull(ki_last.kd_kamar,''),' ',ifnull(bg.nm_bangsal,'Ranap Gabung')) AS Lokasi"
            : "ifnull(pl.nm_poli,'') AS Lokasi");

        if (category == LabCategory.PA)
        {
            sb.Append($@", if({t}.pengambilan_bahan='0000-00-00','',date_format({t}.pengambilan_bahan,'%Y-%m-%d')) AS TglBahan,
                ifnull({t}.diperoleh_dengan,'') AS DiperolehDengan, ifnull({t}.lokasi_jaringan,'') AS LokasiJaringan,
                ifnull({t}.diawetkan_dengan,'') AS DiawetkanDengan, ifnull({t}.pernah_dilakukan_di,'') AS PernahDilakukanDi,
                if({t}.tanggal_pa_sebelumnya='0000-00-00','',date_format({t}.tanggal_pa_sebelumnya,'%Y-%m-%d')) AS TglPaSebelumnya,
                ifnull({t}.nomor_pa_sebelumnya,'') AS NoPaSebelumnya, ifnull({t}.diagnosa_pa_sebelumnya,'') AS DiagnosaPaSebelumnya");
        }
        else
        {
            sb.Append(@", '' AS TglBahan, '' AS DiperolehDengan, '' AS LokasiJaringan, '' AS DiawetkanDengan,
                '' AS PernahDilakukanDi, '' AS TglPaSebelumnya, '' AS NoPaSebelumnya, '' AS DiagnosaPaSebelumnya");
        }

        sb.Append($@" from {t}
            inner join reg_periksa r on {t}.no_rawat=r.no_rawat
            inner join pasien p on r.no_rkm_medis=p.no_rkm_medis
            inner join dokter dk on {t}.dokter_perujuk=dk.kd_dokter
            inner join penjab pj on r.kd_pj=pj.kd_pj ");

        if (ranap)
        {
            // "Belum Pulang" narrows the last stay to the still-open one (stts_pulang='-'), as legacy tampil3.
            var open = f.HanyaBelumPulang ? "where stts_pulang='-' " : "";
            var openOuter = f.HanyaBelumPulang ? "where ki.stts_pulang='-'" : "";
            sb.Append($@"left join (select ki.no_rawat, ki.kd_kamar from kamar_inap ki
                inner join (select no_rawat, max(concat(tgl_masuk,' ',jam_masuk)) as max_masuk from kamar_inap {open}group by no_rawat) latest
                on ki.no_rawat=latest.no_rawat and concat(ki.tgl_masuk,' ',ki.jam_masuk)=latest.max_masuk {openOuter}) ki_last
                on r.no_rawat=ki_last.no_rawat
                left join kamar km on ki_last.kd_kamar=km.kd_kamar
                left join bangsal bg on km.kd_bangsal=bg.kd_bangsal ");
        }
        else
        {
            sb.Append("inner join poliklinik pl on r.kd_poli=pl.kd_poli ");
        }

        sb.Append($"where {t}.status='{(ranap ? "ranap" : "ralan")}' and {t}.tgl_permintaan between {{0}} and {{1}} ");

        if (!string.IsNullOrWhiteSpace(f.Dokter)) sb.Append($"and dk.nm_dokter like {P("%" + f.Dokter.Trim() + "%")} ");
        if (!string.IsNullOrWhiteSpace(f.PoliAtauKamar))
            sb.Append(ranap
                ? $"and bg.nm_bangsal like {P("%" + f.PoliAtauKamar.Trim() + "%")} "
                : $"and pl.nm_poli like {P("%" + f.PoliAtauKamar.Trim() + "%")} ");
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var kw = P("%" + f.Keyword.Trim() + "%");
            sb.Append($"and ({t}.noorder like {kw} or {t}.no_rawat like {kw} or r.no_rkm_medis like {kw} or p.nm_pasien like {kw} or {t}.diagnosa_klinis like {kw} or pj.png_jawab like {kw}) ");
        }

        sb.Append($"group by {t}.noorder order by {t}.tgl_permintaan, {t}.jam_permintaan desc limit {MaxRows}");

        return await db.Database.SqlQueryRaw<LabPermintaanRow>(sb.ToString(), args.ToArray()).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LabPermintaanItem>> GetItemsAsync(LabCategory category, IReadOnlyCollection<string> noOrders, CancellationToken ct = default)
    {
        if (noOrders.Count == 0) return Array.Empty<LabPermintaanItem>();

        var (_, pem, det) = Tables(category);
        var args = noOrders.Cast<object>().ToList();
        var inList = string.Join(",", Enumerable.Range(0, args.Count).Select(i => "{" + i + "}"));

        var exams = await db.Database.SqlQueryRaw<LabPermintaanItem>($@"
            select x.noorder AS NoOrder, x.kd_jenis_prw AS KodeJenis, j.nm_perawatan AS Pemeriksaan,
                   '' AS Detail, '' AS Satuan, '' AS RujukanLd, '' AS RujukanLa, '' AS RujukanPd, '' AS RujukanPa
            from {pem} x inner join jns_perawatan_lab j on x.kd_jenis_prw=j.kd_jenis_prw
            where x.noorder in ({inList}) order by x.noorder, j.nm_perawatan", args.ToArray()).ToListAsync(ct);

        if (det is null) return exams;

        var details = await db.Database.SqlQueryRaw<LabPermintaanItem>($@"
            select d.noorder AS NoOrder, d.kd_jenis_prw AS KodeJenis, '' AS Pemeriksaan,
                   t.Pemeriksaan AS Detail, ifnull(t.satuan,'') AS Satuan,
                   ifnull(t.nilai_rujukan_ld,'') AS RujukanLd, ifnull(t.nilai_rujukan_la,'') AS RujukanLa,
                   ifnull(t.nilai_rujukan_pd,'') AS RujukanPd, ifnull(t.nilai_rujukan_pa,'') AS RujukanPa
            from {det} d inner join template_laboratorium t on d.id_template=t.id_template
            where d.noorder in ({inList}) order by d.noorder, d.kd_jenis_prw, t.urut", args.ToArray()).ToListAsync(ct);

        // Legacy nests template items under their examination; keep that order.
        var result = new List<LabPermintaanItem>();
        foreach (var e in exams)
        {
            result.Add(e);
            result.AddRange(details.Where(d => d.NoOrder == e.NoOrder && d.KodeJenis == e.KodeJenis));
        }
        return result;
    }

    public async Task DeleteAsync(LabCategory category, string noOrder, bool bypassSampelCheck, CancellationToken ct = default)
    {
        var (hdr, pem, det) = Tables(category);

        var sampel = (await db.Database.SqlQueryRaw<string>(
            $"select if(tgl_sampel='0000-00-00','',date_format(tgl_sampel,'%Y-%m-%d')) AS Value from {hdr} where noorder={{0}}",
            noOrder).ToListAsync(ct)).FirstOrDefault()
            ?? throw new InvalidOperationException($"Permintaan '{noOrder}' tidak ditemukan.");

        if (sampel.Length > 0 && !bypassSampelCheck)
            throw new InvalidOperationException("Maaf, sudah dilakukan pengambilan sampel.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        long paid = (await db.Database.SqlQueryRaw<long>($"select count(*) AS Value from {pem} where stts_bayar='Sudah' and noorder={{0}}", noOrder).ToListAsync(ct)).First();
        if (det is not null)
            paid += (await db.Database.SqlQueryRaw<long>($"select count(*) AS Value from {det} where stts_bayar='Sudah' and noorder={{0}}", noOrder).ToListAsync(ct)).First();
        if (paid > 0)
            throw new InvalidOperationException("Maaf, tidak boleh dihapus karena sudah ada tindakan yang sudah dibayar. Silakan hubungi kasir.");

        if (det is not null) await db.Database.ExecuteSqlRawAsync($"delete from {det} where noorder={{0}}", [noOrder], ct);
        await db.Database.ExecuteSqlRawAsync($"delete from {pem} where noorder={{0}}", [noOrder], ct);
        await db.Database.ExecuteSqlRawAsync($"delete from {hdr} where noorder={{0}}", [noOrder], ct);

        await tx.CommitAsync(ct);
    }

    public async Task UpdateSampelAsync(LabCategory category, string noOrder, DateTime waktu, CancellationToken ct = default)
    {
        var hdr = Tables(category).Header;
        var n = await db.Database.ExecuteSqlRawAsync(
            $"update {hdr} set tgl_sampel={{0}}, jam_sampel={{1}} where noorder={{2}}",
            [waktu.ToString("yyyy-MM-dd"), waktu.ToString("HH:mm:ss"), noOrder], ct);
        if (n == 0) throw new InvalidOperationException($"Permintaan '{noOrder}' tidak ditemukan.");
    }
}
