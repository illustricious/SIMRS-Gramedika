#pragma warning disable EF1002 // table names/suffixes come from the LabCategory enum, never from user text
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SimrsKhanza.Data;

// ---- read models -----------------------------------------------------------------------------

/// <summary>Filter bar of the legacy "Data Pemeriksaan Laboratorium" list (No.Rawat / Tanggal s.d. / Pasien / Petugas / Key Word).</summary>
public record LabPeriksaFilter(
    DateOnly From, DateOnly To, string? NoRawat = null, string? NoRm = null, string? Nip = null, string? Keyword = null);

/// <summary>Identifies one pemeriksaan visit: legacy keys list rows on no_rawat + tgl_periksa + jam.</summary>
public record LabPeriksaKey(string NoRawat, string Tgl, string Jam);

public class LabPeriksaItemRow
{
    public string Pemeriksaan { get; set; } = "";
    public string Metode { get; set; } = "";
    public string Hasil { get; set; } = "";
    public string Satuan { get; set; } = "";
    public string NilaiRujukan { get; set; } = "";
    public string Keterangan { get; set; } = "";
    public decimal BiayaItem { get; set; }
}

public class LabPeriksaPemeriksaanRow
{
    public string KdJenisPrw { get; set; } = "";
    public string Nama { get; set; } = "";
    public decimal Biaya { get; set; }
    public List<LabPeriksaItemRow> Items { get; } = new();
}

/// <summary>One visit of the PK/MB list: header + its pemeriksaan (+ item rows) + kesan/saran — the nested rows the legacy grid builds.</summary>
public class LabPeriksaGroup
{
    public LabPeriksaKey Key { get; set; } = new("", "", "");
    public string NoRm { get; set; } = "";
    public string NamaPasien { get; set; } = "";
    public string Petugas { get; set; } = "";
    public string DokterPerujuk { get; set; } = "";
    public string DokterPj { get; set; } = "";
    public string CaraBayar { get; set; } = "";
    public string Status { get; set; } = "";
    public string KamarPoli { get; set; } = "";
    /// <summary>Poli name (Ralan) or latest bangsal (Ranap) — the "Ruang" column of the item tab.</summary>
    public string Ruang { get; set; } = "";
    public string Saran { get; set; } = "";
    public string Kesan { get; set; } = "";
    public List<LabPeriksaPemeriksaanRow> Pemeriksaan { get; } = new();
    public decimal Total => Pemeriksaan.Sum(p => p.Biaya + p.Items.Sum(i => i.BiayaItem));
}

/// <summary>One row of the PA list: PA stores a narrative per pemeriksaan (detail_periksa_labpa), so the list is flat.</summary>
public class LabPeriksaPaRow
{
    public string NoRawat { get; set; } = "";
    public string NoRm { get; set; } = "";
    public string NamaPasien { get; set; } = "";
    public string KamarPoli { get; set; } = "";
    public string Petugas { get; set; } = "";
    public string Tgl { get; set; } = "";
    public string Jam { get; set; } = "";
    public string DokterPerujuk { get; set; } = "";
    public string DokterPj { get; set; } = "";
    public string CaraBayar { get; set; } = "";
    public string KdJenisPrw { get; set; } = "";
    public string Pemeriksaan { get; set; } = "";
    public decimal Biaya { get; set; }
    public string DiagnosaKlinik { get; set; } = "";
    public string Makroskopik { get; set; } = "";
    public string Mikroskopik { get; set; } = "";
    public string Kesimpulan { get; set; } = "";
    public string Kesan { get; set; } = "";
    public LabPeriksaKey Key => new(NoRawat, Tgl, Jam);
    public string PasienText => $"{NoRm} {NamaPasien} ({KamarPoli})";
}

public class LabPick
{
    public string Kode { get; set; } = "";
    public string Nama { get; set; } = "";
    public override string ToString() => Nama;
}

/// <summary>reg_periksa + pasien facts the entry form needs (sex/age drive which nilai_rujukan column is shown).</summary>
public class LabRegInfo
{
    public string NoRawat { get; set; } = "";
    public string NoRm { get; set; } = "";
    public string NamaPasien { get; set; } = "";
    public string Jk { get; set; } = "";
    public string Umur { get; set; } = "";
    public string Alamat { get; set; } = "";
    public string KodePj { get; set; } = "";
    public string CaraBayar { get; set; } = "";
    public string KodeDokter { get; set; } = "";
    public string NamaDokter { get; set; } = "";
    /// <summary>"Ralan" / "Ranap" — written to periksa_lab.status.</summary>
    public string Status { get; set; } = "Ralan";
    public string Stts { get; set; } = "";
    public string KamarPoli { get; set; } = "";
    /// <summary>kamar.kelas of the latest stay — only used when set_tarif.kelas_lab = Yes (Ranap).</summary>
    public string Kelas { get; set; } = "";
}

/// <summary>A selectable jns_perawatan_lab row with the tarif split periksa_lab stores per pemeriksaan.</summary>
public class LabTarifRow
{
    public string KdJenisPrw { get; set; } = "";
    public string Nama { get; set; } = "";
    public decimal Biaya { get; set; }
    public decimal BagianRs { get; set; }
    public decimal Bhp { get; set; }
    public decimal TarifPerujuk { get; set; }
    public decimal TarifDokter { get; set; }
    public decimal TarifPetugas { get; set; }
    public decimal Kso { get; set; }
    public decimal Menejemen { get; set; }
}

public class LabTemplateRow
{
    public string KdJenisPrw { get; set; } = "";
    public int IdTemplate { get; set; }
    public string Pemeriksaan { get; set; } = "";
    public string Satuan { get; set; } = "";
    public string NilaiRujukanLd { get; set; } = "";
    public string NilaiRujukanLa { get; set; } = "";
    public string NilaiRujukanPd { get; set; } = "";
    public string NilaiRujukanPa { get; set; } = "";
    public string Metode { get; set; } = "";
    public decimal BiayaItem { get; set; }
    public decimal BagianRs { get; set; }
    public decimal Bhp { get; set; }
    public decimal BagianPerujuk { get; set; }
    public decimal BagianDokter { get; set; }
    public decimal BagianLaborat { get; set; }
    public decimal Kso { get; set; }
    public decimal Menejemen { get; set; }
}

/// <summary>What a permintaan order asked for — preloaded into the entry form when "Hasil" is pressed on the permintaan screen.</summary>
public class LabOrderInfo
{
    public string DokterPerujuk { get; set; } = "";
    public List<string> KdJenisPrw { get; } = new();
    /// <summary>Requested template ids (PK/MB only; PA has no detail table).</summary>
    public List<int> IdTemplate { get; } = new();
}

public record LabTarifSetting(bool CaraBayar, bool Kelas);

// ---- write models ----------------------------------------------------------------------------

public record LabSaveItem(
    string KdJenisPrw, int IdTemplate, string Nilai, string NilaiRujukan, string Keterangan,
    decimal BagianRs, decimal Bhp, decimal BagianPerujuk, decimal BagianDokter, decimal BagianLaborat,
    decimal Kso, decimal Menejemen, decimal BiayaItem);

public record LabSavePa(string KdJenisPrw, string DiagnosaKlinik, string Makroskopik, string Mikroskopik, string Kesimpulan, string Kesan);

public record LabSaveRequest(
    LabCategory Category, string NoRawat, string Nip, string DokterPerujuk, string DokterPj,
    DateOnly Tanggal, TimeOnly Jam, string Status, string? NoOrder,
    IReadOnlyList<LabTarifRow> Pemeriksaan, IReadOnlyList<LabSaveItem> Items, IReadOnlyList<LabSavePa> PaHasil);

public interface ILabPeriksaRepository
{
    Task<IReadOnlyList<LabPeriksaGroup>> GetPeriksaGroupsAsync(LabCategory category, LabPeriksaFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<LabPeriksaPaRow>> GetPeriksaPaAsync(LabPeriksaFilter filter, CancellationToken ct = default);
    Task SaveKesanSaranAsync(LabPeriksaKey key, string saran, string kesan, CancellationToken ct = default);
    Task UpdatePaHasilAsync(LabPeriksaPaRow row, string diagnosa, string makro, string mikro, string kesimpulan, string kesan, CancellationToken ct = default);
    Task DeleteAsync(LabCategory category, LabPeriksaKey key, string? kdJenisPrw, CancellationToken ct = default);
    Task SaveAsync(LabSaveRequest request, CancellationToken ct = default);
    Task<LabRegInfo?> GetRegInfoAsync(string noRawat, CancellationToken ct = default);
    Task<LabTarifSetting> GetTarifSettingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LabTarifRow>> GetTarifAsync(LabCategory category, LabRegInfo? reg, string? keyword, CancellationToken ct = default);
    /// <summary>Tarif rows for explicit codes — the permintaan flow lists exactly what was ordered, regardless of payer/kelas filtering (legacy tampiltarif(order)).</summary>
    Task<IReadOnlyList<LabTarifRow>> GetTarifByKodeAsync(LabCategory category, IReadOnlyList<string> kodes, CancellationToken ct = default);
    Task<IReadOnlyList<LabTemplateRow>> GetTemplatesAsync(string kdJenisPrw, string? keyword, int[]? onlyIds, CancellationToken ct = default);
    Task<LabOrderInfo?> GetOrderAsync(LabCategory category, string noOrder, CancellationToken ct = default);
    Task<IReadOnlyList<LabPick>> GetDokterAsync(IReadOnlyList<string>? alwaysInclude, CancellationToken ct = default);
    Task<IReadOnlyList<LabPick>> GetPetugasAsync(CancellationToken ct = default);
    Task<string> GetDefaultDokterPjAsync(LabCategory category, CancellationToken ct = default);
}

/// <summary>
/// Periksa Lab PK/PA/MB over the legacy schema, raw SQL only (no EF entities — the lab tables are
/// wide and the legacy joins are easier to keep verbatim). Table names come from the LabCategory
/// enum, every user value goes through a {n} parameter.
/// </summary>
public class LabPeriksaRepository(SimrsKhanzaDbContext db) : ILabPeriksaRepository
{
    private const int MaxHeaders = 1000;

    private static string Cat(LabCategory c) => c.ToString();                     // PK / PA / MB — also periksa_lab.kategori
    private static string Suffix(LabCategory c) => c switch { LabCategory.PA => "pa", LabCategory.MB => "mb", _ => "" };
    private static string AntriTable(LabCategory c) => c switch { LabCategory.PA => "antrilabpa2", LabCategory.MB => "antrilabmb2", _ => "antrilabpk2" };

    private sealed class SqlParams
    {
        public List<object> Values { get; } = new();
        public string Add(object v) { Values.Add(v); return "{" + (Values.Count - 1) + "}"; }
    }

    private static decimal Num(string? s)
        => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    // ---- list: PK / MB ----------------------------------------------------------------------

    private const string KamarSub =
        "(select concat(ki.kd_kamar,', ',b.nm_bangsal) from kamar_inap ki inner join kamar k on k.kd_kamar=ki.kd_kamar " +
        "inner join bangsal b on b.kd_bangsal=k.kd_bangsal where ki.no_rawat={R}.no_rawat order by ki.tgl_masuk desc, ki.jam_masuk desc limit 1)";

    private static string FilterSql(LabPeriksaFilter f, SqlParams ps, string cat)
    {
        var sql = $"p.kategori={ps.Add(cat)} and p.tgl_periksa between {ps.Add(f.From.ToString("yyyy-MM-dd"))} and {ps.Add(f.To.ToString("yyyy-MM-dd"))} ";
        if (!string.IsNullOrWhiteSpace(f.NoRawat)) sql += $"and p.no_rawat like {ps.Add("%" + f.NoRawat.Trim() + "%")} ";
        if (!string.IsNullOrWhiteSpace(f.NoRm)) sql += $"and r.no_rkm_medis like {ps.Add("%" + f.NoRm.Trim() + "%")} ";
        if (!string.IsNullOrWhiteSpace(f.Nip)) sql += $"and p.nip={ps.Add(f.Nip.Trim())} ";
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var k = ps.Add("%" + f.Keyword.Trim() + "%");
            sql += $"and (pas.nm_pasien like {k} or pt.nama like {k} or r.no_rkm_medis like {k} or pj.png_jawab like {k} or dpj.nm_dokter like {k}) ";
        }
        return sql;
    }

    private const string PeriksaJoins =
        "from periksa_lab p inner join reg_periksa r on p.no_rawat=r.no_rawat " +
        "inner join pasien pas on r.no_rkm_medis=pas.no_rkm_medis " +
        "left join petugas pt on p.nip=pt.nip left join penjab pj on r.kd_pj=pj.kd_pj " +
        "left join dokter dpj on p.kd_dokter=dpj.kd_dokter left join dokter dp on p.dokter_perujuk=dp.kd_dokter " +
        "left join poliklinik pl on r.kd_poli=pl.kd_poli ";

    private sealed class FlatRow
    {
        public string? NoRawat { get; set; }
        public string? NoRm { get; set; }
        public string? NamaPasien { get; set; }
        public string? Petugas { get; set; }
        public string? Tgl { get; set; }
        public string? Jam { get; set; }
        public string? DokterPerujuk { get; set; }
        public string? DokterPj { get; set; }
        public string? CaraBayar { get; set; }
        public string? Status { get; set; }
        public string? NmPoli { get; set; }
        public string? Kamar { get; set; }
        public string? Saran { get; set; }
        public string? Kesan { get; set; }
        public string? KdJenisPrw { get; set; }
        public string? NmPerawatan { get; set; }
        public string? BiayaPrw { get; set; }
        public string? ItemNama { get; set; }
        public string? Metode { get; set; }
        public string? Nilai { get; set; }
        public string? Satuan { get; set; }
        public string? NilaiRujukan { get; set; }
        public string? Keterangan { get; set; }
        public string? BiayaItem { get; set; }
    }

    public async Task<IReadOnlyList<LabPeriksaGroup>> GetPeriksaGroupsAsync(LabCategory category, LabPeriksaFilter filter, CancellationToken ct = default)
    {
        var ps = new SqlParams();
        var where = FilterSql(filter, ps, Cat(category));
        var catParam = ps.Add(Cat(category));

        // One round trip: the capped header set is a derived table, items/kesan-saran join onto it.
        var sql = $@"select h.no_rawat as NoRawat, h.no_rkm_medis as NoRm, h.nm_pasien as NamaPasien, h.petugas as Petugas,
              date_format(h.tgl_periksa,'%Y-%m-%d') as Tgl, cast(h.jam as char) as Jam,
              h.dokter_perujuk as DokterPerujuk, h.dokter_pj as DokterPj, h.png_jawab as CaraBayar, h.status as Status, h.nm_poli as NmPoli,
              {KamarSub.Replace("{R}", "h")} as Kamar, sk.saran as Saran, sk.kesan as Kesan,
              p.kd_jenis_prw as KdJenisPrw, j.nm_perawatan as NmPerawatan, cast(p.biaya as char) as BiayaPrw,
              t.Pemeriksaan as ItemNama, '' as Metode, d.nilai as Nilai, t.satuan as Satuan, d.nilai_rujukan as NilaiRujukan,
              d.keterangan as Keterangan, cast(d.biaya_item as char) as BiayaItem
            from (select p.no_rawat, p.tgl_periksa, p.jam, min(r.no_rkm_medis) as no_rkm_medis, min(pas.nm_pasien) as nm_pasien,
                         min(pt.nama) as petugas, min(dp.nm_dokter) as dokter_perujuk, min(dpj.nm_dokter) as dokter_pj,
                         min(pj.png_jawab) as png_jawab, min(p.status) as status, min(pl.nm_poli) as nm_poli
                  {PeriksaJoins}
                  where {where}
                  group by p.no_rawat, p.tgl_periksa, p.jam
                  order by p.tgl_periksa desc, p.jam desc limit {MaxHeaders}) h
            inner join periksa_lab p on p.no_rawat=h.no_rawat and p.tgl_periksa=h.tgl_periksa and p.jam=h.jam and p.kategori={catParam}
            left join jns_perawatan_lab j on j.kd_jenis_prw=p.kd_jenis_prw
            left join detail_periksa_lab d on d.no_rawat=p.no_rawat and d.kd_jenis_prw=p.kd_jenis_prw and d.tgl_periksa=p.tgl_periksa and d.jam=p.jam
            left join template_laboratorium t on t.id_template=d.id_template
            left join saran_kesan_lab sk on sk.no_rawat=h.no_rawat and sk.tgl_periksa=h.tgl_periksa and sk.jam=h.jam
            order by h.tgl_periksa desc, h.jam desc, h.no_rawat, p.kd_jenis_prw, t.urut";

        var rows = await db.Database.SqlQueryRaw<FlatRow>(sql, ps.Values.ToArray()).ToListAsync(ct);

        var groups = new List<LabPeriksaGroup>();
        var byKey = new Dictionary<LabPeriksaKey, LabPeriksaGroup>();
        foreach (var r in rows)
        {
            var key = new LabPeriksaKey(r.NoRawat ?? "", r.Tgl ?? "", r.Jam ?? "");
            if (!byKey.TryGetValue(key, out var g))
            {
                g = new LabPeriksaGroup
                {
                    Key = key, NoRm = r.NoRm ?? "", NamaPasien = r.NamaPasien ?? "", Petugas = r.Petugas ?? "",
                    DokterPerujuk = r.DokterPerujuk ?? "", DokterPj = r.DokterPj ?? "", CaraBayar = r.CaraBayar ?? "",
                    Status = r.Status ?? "", Saran = r.Saran ?? "", Kesan = r.Kesan ?? "",
                };
                var inRanap = !string.IsNullOrEmpty(r.Kamar);
                g.KamarPoli = inRanap ? $"Kamar : {r.Kamar}" : $"Poli : {r.NmPoli}";
                g.Ruang = r.Status == "Ranap" && inRanap ? (r.Kamar!.Split(", ", 2).Last()) : r.NmPoli ?? "";
                byKey[key] = g;
                groups.Add(g);
            }

            var prw = g.Pemeriksaan.FirstOrDefault(x => x.KdJenisPrw == r.KdJenisPrw);
            if (prw is null)
            {
                prw = new LabPeriksaPemeriksaanRow { KdJenisPrw = r.KdJenisPrw ?? "", Nama = r.NmPerawatan ?? "", Biaya = Num(r.BiayaPrw) };
                g.Pemeriksaan.Add(prw);
            }
            if (r.ItemNama is not null)
            {
                prw.Items.Add(new LabPeriksaItemRow
                {
                    Pemeriksaan = r.ItemNama, Metode = r.Metode ?? "", Hasil = (r.Nilai ?? "").Replace("'", "`"),
                    Satuan = r.Satuan ?? "", NilaiRujukan = r.NilaiRujukan ?? "", Keterangan = r.Keterangan ?? "",
                    BiayaItem = Num(r.BiayaItem),
                });
            }
        }
        return groups;
    }

    // ---- list: PA ---------------------------------------------------------------------------

    private sealed class PaFlat
    {
        public string? NoRawat { get; set; }
        public string? NoRm { get; set; }
        public string? NamaPasien { get; set; }
        public string? Petugas { get; set; }
        public string? Tgl { get; set; }
        public string? Jam { get; set; }
        public string? DokterPerujuk { get; set; }
        public string? DokterPj { get; set; }
        public string? CaraBayar { get; set; }
        public string? NmPoli { get; set; }
        public string? Kamar { get; set; }
        public string? KdJenisPrw { get; set; }
        public string? NmPerawatan { get; set; }
        public string? Biaya { get; set; }
        public string? DiagnosaKlinik { get; set; }
        public string? Makroskopik { get; set; }
        public string? Mikroskopik { get; set; }
        public string? Kesimpulan { get; set; }
        public string? Kesan { get; set; }
    }

    public async Task<IReadOnlyList<LabPeriksaPaRow>> GetPeriksaPaAsync(LabPeriksaFilter filter, CancellationToken ct = default)
    {
        var ps = new SqlParams();
        var where = FilterSql(filter, ps, "PA");
        var sql = $@"select p.no_rawat as NoRawat, r.no_rkm_medis as NoRm, pas.nm_pasien as NamaPasien, pt.nama as Petugas,
              date_format(p.tgl_periksa,'%Y-%m-%d') as Tgl, cast(p.jam as char) as Jam, dp.nm_dokter as DokterPerujuk, dpj.nm_dokter as DokterPj,
              pj.png_jawab as CaraBayar, pl.nm_poli as NmPoli, {KamarSub.Replace("{R}", "p")} as Kamar,
              p.kd_jenis_prw as KdJenisPrw, j.nm_perawatan as NmPerawatan, cast(p.biaya as char) as Biaya,
              x.diagnosa_klinik as DiagnosaKlinik, x.makroskopik as Makroskopik, x.mikroskopik as Mikroskopik, x.kesimpulan as Kesimpulan, x.kesan as Kesan
            {PeriksaJoins}
            left join jns_perawatan_lab j on j.kd_jenis_prw=p.kd_jenis_prw
            left join detail_periksa_labpa x on x.no_rawat=p.no_rawat and x.kd_jenis_prw=p.kd_jenis_prw and x.tgl_periksa=p.tgl_periksa and x.jam=p.jam
            where {where}
            order by p.tgl_periksa desc, p.jam desc limit {MaxHeaders}";

        var rows = await db.Database.SqlQueryRaw<PaFlat>(sql, ps.Values.ToArray()).ToListAsync(ct);
        return rows.Select(r => new LabPeriksaPaRow
        {
            NoRawat = r.NoRawat ?? "", NoRm = r.NoRm ?? "", NamaPasien = r.NamaPasien ?? "", Petugas = r.Petugas ?? "",
            Tgl = r.Tgl ?? "", Jam = r.Jam ?? "", DokterPerujuk = r.DokterPerujuk ?? "", DokterPj = r.DokterPj ?? "",
            CaraBayar = r.CaraBayar ?? "", KamarPoli = string.IsNullOrEmpty(r.Kamar) ? $"Poli : {r.NmPoli}" : $"Kamar : {r.Kamar}",
            KdJenisPrw = r.KdJenisPrw ?? "", Pemeriksaan = r.NmPerawatan ?? "", Biaya = Num(r.Biaya),
            DiagnosaKlinik = r.DiagnosaKlinik ?? "", Makroskopik = r.Makroskopik ?? "", Mikroskopik = r.Mikroskopik ?? "",
            Kesimpulan = r.Kesimpulan ?? "", Kesan = r.Kesan ?? "",
        }).ToList();
    }

    // ---- writes -----------------------------------------------------------------------------

    /// <summary>
    /// Joins a transaction the caller already opened (caller then owns commit/rollback — used to
    /// dry-run writes against the real DB), otherwise opens its own and commits on success.
    /// </summary>
    private async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await work();
            return;
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await work();
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<int> ScalarAsync(string sql, object[] p, CancellationToken ct)
        => await db.Database.SqlQueryRaw<int>(sql, p).FirstAsync(ct);

    /// <summary>Legacy Sequel.cariRegistrasi: a billed (billing row) or cancelled (stts='Batal') visit is locked against edits.</summary>
    private async Task EnsureNotLockedAsync(string noRawat, string action, CancellationToken ct)
    {
        var billed = await ScalarAsync("select count(*) as Value from billing where no_rawat={0}", [noRawat], ct);
        var batal = await ScalarAsync("select count(*) as Value from reg_periksa where no_rawat={0} and stts='Batal'", [noRawat], ct);
        if (billed + batal > 0)
            throw new InvalidOperationException($"Data billing sudah terverifikasi (atau registrasi batal), {action}.\nSilahkan hubungi bagian kasir/keuangan.");
    }

    public async Task SaveKesanSaranAsync(LabPeriksaKey key, string saran, string kesan, CancellationToken ct = default)
    {
        await InTransactionAsync(async () =>
        {
            // Legacy: both empty → delete; otherwise insert, falling back to update on duplicate key.
            await db.Database.ExecuteSqlRawAsync("delete from saran_kesan_lab where no_rawat={0} and tgl_periksa={1} and jam={2}",
                new object[] { key.NoRawat, key.Tgl, key.Jam }, ct);
            if (saran.Length > 0 || kesan.Length > 0)
                await db.Database.ExecuteSqlRawAsync("insert into saran_kesan_lab(no_rawat,tgl_periksa,jam,saran,kesan) values({0},{1},{2},{3},{4})",
                    new object[] { key.NoRawat, key.Tgl, key.Jam, saran, kesan }, ct);
        }, ct);
    }

    public async Task UpdatePaHasilAsync(LabPeriksaPaRow row, string diagnosa, string makro, string mikro, string kesimpulan, string kesan, CancellationToken ct = default)
    {
        var n = await db.Database.ExecuteSqlRawAsync(
            "update detail_periksa_labpa set diagnosa_klinik={0},makroskopik={1},mikroskopik={2},kesimpulan={3},kesan={4} " +
            "where no_rawat={5} and kd_jenis_prw={6} and tgl_periksa={7} and jam={8}",
            new object[] { diagnosa, makro, mikro, kesimpulan, kesan, row.NoRawat, row.KdJenisPrw, row.Tgl, row.Jam }, ct);
        if (n == 0)
            throw new InvalidOperationException("Data hasil PA tidak ditemukan (belum ada baris detail_periksa_labpa untuk pemeriksaan ini).");
    }

    public async Task DeleteAsync(LabCategory category, LabPeriksaKey key, string? kdJenisPrw, CancellationToken ct = default)
    {
        await EnsureNotLockedAsync(key.NoRawat, "data tidak boleh dihapus", ct);
        await InTransactionAsync(async () =>
        {
            var cat = Cat(category);
            if (category == LabCategory.PA)
            {
                if (string.IsNullOrEmpty(kdJenisPrw)) throw new InvalidOperationException("Pilih satu baris pemeriksaan PA yang mau dihapus.");
                var p = new object[] { key.NoRawat, key.Tgl, key.Jam, kdJenisPrw };
                await db.Database.ExecuteSqlRawAsync("delete from detail_periksa_labpa_gambar where no_rawat={0} and tgl_periksa={1} and jam={2} and kd_jenis_prw={3}", p, ct);
                await db.Database.ExecuteSqlRawAsync("delete from detail_periksa_labpa where no_rawat={0} and tgl_periksa={1} and jam={2} and kd_jenis_prw={3}", p, ct);
                var n = await db.Database.ExecuteSqlRawAsync("delete from periksa_lab where kategori='PA' and no_rawat={0} and tgl_periksa={1} and jam={2} and kd_jenis_prw={3}", p, ct);
                if (n == 0) throw new InvalidOperationException("Data pemeriksaan tidak ditemukan.");
            }
            else
            {
                var p = new object[] { cat, key.NoRawat, key.Tgl, key.Jam };
                // Details first (they are matched through the periksa_lab rows of THIS category).
                await db.Database.ExecuteSqlRawAsync(
                    "delete d from detail_periksa_lab d inner join periksa_lab pl on pl.no_rawat=d.no_rawat and pl.kd_jenis_prw=d.kd_jenis_prw " +
                    "and pl.tgl_periksa=d.tgl_periksa and pl.jam=d.jam where pl.kategori={0} and pl.no_rawat={1} and pl.tgl_periksa={2} and pl.jam={3}", p, ct);
                var n = await db.Database.ExecuteSqlRawAsync("delete from periksa_lab where kategori={0} and no_rawat={1} and tgl_periksa={2} and jam={3}", p, ct);
                if (n == 0) throw new InvalidOperationException("Data pemeriksaan tidak ditemukan.");
                // Orphan kesan/saran would resurface for a later pemeriksaan with the same timestamp.
                await db.Database.ExecuteSqlRawAsync(
                    "delete from saran_kesan_lab where no_rawat={0} and tgl_periksa={1} and jam={2} and not exists " +
                    "(select 1 from periksa_lab x where x.no_rawat={0} and x.tgl_periksa={1} and x.jam={2})",
                    new object[] { key.NoRawat, key.Tgl, key.Jam }, ct);
            }
        }, ct);
    }

    public async Task SaveAsync(LabSaveRequest rq, CancellationToken ct = default)
    {
        if (rq.Pemeriksaan.Count == 0) throw new InvalidOperationException("Pilih minimal satu pemeriksaan.");
        await EnsureNotLockedAsync(rq.NoRawat, "data tidak bisa disimpan", ct);

        var tgl = rq.Tanggal.ToString("yyyy-MM-dd");
        var jam = rq.Jam.ToString("HH:mm:ss");
        var cat = Cat(rq.Category);
        var sfx = Suffix(rq.Category);

        await InTransactionAsync(async () =>
        {
            foreach (var t in rq.Pemeriksaan)
            {
                // Same column order as legacy simpanlab(); `biaya` = jns_perawatan_lab.total_byr.
                await db.Database.ExecuteSqlRawAsync(
                    "insert into periksa_lab(no_rawat,nip,kd_jenis_prw,tgl_periksa,jam,dokter_perujuk,bagian_rs,bhp,tarif_perujuk," +
                    "tarif_tindakan_dokter,tarif_tindakan_petugas,kso,menejemen,biaya,kd_dokter,status,kategori) " +
                    "values({0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16})",
                    new object[]
                    {
                        rq.NoRawat, rq.Nip, t.KdJenisPrw, tgl, jam, rq.DokterPerujuk, (double)t.BagianRs, (double)t.Bhp, (double)t.TarifPerujuk,
                        (double)t.TarifDokter, (double)t.TarifPetugas, (double)t.Kso, (double)t.Menejemen, (double)t.Biaya, rq.DokterPj, rq.Status, cat,
                    }, ct);
            }

            if (rq.Category == LabCategory.PA)
            {
                foreach (var h in rq.PaHasil)
                    await db.Database.ExecuteSqlRawAsync(
                        "insert into detail_periksa_labpa(no_rawat,kd_jenis_prw,tgl_periksa,jam,diagnosa_klinik,makroskopik,mikroskopik,kesimpulan,kesan) " +
                        "values({0},{1},{2},{3},{4},{5},{6},{7},{8})",
                        new object[] { rq.NoRawat, h.KdJenisPrw, tgl, jam, h.DiagnosaKlinik, h.Makroskopik, h.Mikroskopik, h.Kesimpulan, h.Kesan }, ct);
            }
            else
            {
                foreach (var i in rq.Items)
                    await db.Database.ExecuteSqlRawAsync(
                        "insert into detail_periksa_lab(no_rawat,kd_jenis_prw,tgl_periksa,jam,id_template,nilai,nilai_rujukan,keterangan,bagian_rs,bhp," +
                        "bagian_perujuk,bagian_dokter,bagian_laborat,kso,menejemen,biaya_item) " +
                        "values({0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15})",
                        new object[]
                        {
                            rq.NoRawat, i.KdJenisPrw, tgl, jam, i.IdTemplate, i.Nilai, i.NilaiRujukan, i.Keterangan, (double)i.BagianRs, (double)i.Bhp,
                            (double)i.BagianPerujuk, (double)i.BagianDokter, (double)i.BagianLaborat, (double)i.Kso, (double)i.Menejemen, (double)i.BiayaItem,
                        }, ct);
            }

            if (!string.IsNullOrWhiteSpace(rq.NoOrder))
            {
                // Marks the permintaan as answered (the permintaan list's "Hasil" column).
                await db.Database.ExecuteSqlRawAsync($"update permintaan_lab{sfx} set tgl_hasil={{0}}, jam_hasil={{1}} where noorder={{2}}",
                    new object[] { tgl, jam, rq.NoOrder }, ct);
                if (rq.Status == "Ralan")
                {
                    // Legacy pokes the one-row waiting-room display queue (antrilab*2) so the screen refreshes.
                    await db.Database.ExecuteSqlRawAsync($"delete from {AntriTable(rq.Category)}", Array.Empty<object>(), ct);
                    await db.Database.ExecuteSqlRawAsync($"insert into {AntriTable(rq.Category)} values('1')", Array.Empty<object>(), ct);
                }
            }
        }, ct);
    }

    // ---- lookups ----------------------------------------------------------------------------

    private sealed class RegRow
    {
        public string? NoRawat { get; set; }
        public string? NoRm { get; set; }
        public string? NamaPasien { get; set; }
        public string? Jk { get; set; }
        public string? TglLahir { get; set; }
        public string? Alamat { get; set; }
        public string? KodePj { get; set; }
        public string? CaraBayar { get; set; }
        public string? KodeDokter { get; set; }
        public string? NamaDokter { get; set; }
        public string? Status { get; set; }
        public string? Stts { get; set; }
        public string? NmPoli { get; set; }
        public string? Kamar { get; set; }
        public string? Kelas { get; set; }
    }

    public async Task<LabRegInfo?> GetRegInfoAsync(string noRawat, CancellationToken ct = default)
    {
        var r = await db.Database.SqlQueryRaw<RegRow>(
            @"select r.no_rawat as NoRawat, r.no_rkm_medis as NoRm, pas.nm_pasien as NamaPasien, pas.jk as Jk, cast(pas.tgl_lahir as char) as TglLahir,
                     concat(pas.alamat,', ',ifnull(kl.nm_kel,''),', ',ifnull(kc.nm_kec,''),', ',ifnull(kb.nm_kab,'')) as Alamat,
                     r.kd_pj as KodePj, pj.png_jawab as CaraBayar, r.kd_dokter as KodeDokter, dk.nm_dokter as NamaDokter,
                     r.status_lanjut as Status, r.stts as Stts, pl.nm_poli as NmPoli,
                     (select concat(ki.kd_kamar,', ',b.nm_bangsal) from kamar_inap ki inner join kamar k on k.kd_kamar=ki.kd_kamar
                        inner join bangsal b on b.kd_bangsal=k.kd_bangsal where ki.no_rawat=r.no_rawat order by ki.tgl_masuk desc, ki.jam_masuk desc limit 1) as Kamar,
                     (select k.kelas from kamar_inap ki inner join kamar k on k.kd_kamar=ki.kd_kamar
                        where ki.no_rawat=r.no_rawat order by ki.tgl_masuk desc, ki.jam_masuk desc limit 1) as Kelas
              from reg_periksa r inner join pasien pas on r.no_rkm_medis=pas.no_rkm_medis
              left join kelurahan kl on pas.kd_kel=kl.kd_kel left join kecamatan kc on pas.kd_kec=kc.kd_kec left join kabupaten kb on pas.kd_kab=kb.kd_kab
              left join penjab pj on r.kd_pj=pj.kd_pj left join dokter dk on r.kd_dokter=dk.kd_dokter left join poliklinik pl on r.kd_poli=pl.kd_poli
              where r.no_rawat={0}", noRawat).FirstOrDefaultAsync(ct);
        if (r is null) return null;

        var ranap = r.Status == "Ranap";
        return new LabRegInfo
        {
            NoRawat = r.NoRawat ?? "", NoRm = r.NoRm ?? "", NamaPasien = r.NamaPasien ?? "", Jk = r.Jk ?? "", Umur = UmurText(r.TglLahir),
            Alamat = r.Alamat ?? "", KodePj = r.KodePj ?? "", CaraBayar = r.CaraBayar ?? "", KodeDokter = r.KodeDokter ?? "",
            NamaDokter = r.NamaDokter ?? "", Status = ranap ? "Ranap" : "Ralan", Stts = r.Stts ?? "",
            KamarPoli = ranap && !string.IsNullOrEmpty(r.Kamar) ? $"Kamar : {r.Kamar}" : $"Poli : {r.NmPoli}", Kelas = r.Kelas ?? "",
        };
    }

    private static string UmurText(string? tglLahir)
    {
        if (!DateOnly.TryParseExact(tglLahir, "yyyy-MM-dd", out var lahir) || lahir.Year < 1900) return "-";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var years = today.Year - lahir.Year - (today < lahir.AddYears(today.Year - lahir.Year) ? 1 : 0);
        var anchor = lahir.AddYears(years);
        var months = (today.Year - anchor.Year) * 12 + today.Month - anchor.Month - (today.Day < anchor.Day ? 1 : 0);
        var days = today.DayNumber - anchor.AddMonths(months).DayNumber;
        return $"{years} Th {months} Bl {days} Hr";
    }

    private sealed class SettingRow
    {
        public string? CaraBayar { get; set; }
        public string? Kelas { get; set; }
    }

    public async Task<LabTarifSetting> GetTarifSettingAsync(CancellationToken ct = default)
    {
        var s = await db.Database.SqlQueryRaw<SettingRow>("select cara_bayar_lab as CaraBayar, kelas_lab as Kelas from set_tarif limit 1").FirstOrDefaultAsync(ct);
        // No row → legacy defaults to Yes/Yes.
        return s is null ? new LabTarifSetting(true, true) : new LabTarifSetting(s.CaraBayar != "No", s.Kelas != "No");
    }

    private sealed class TarifSql
    {
        public string? KdJenisPrw { get; set; }
        public string? Nama { get; set; }
        public string? Biaya { get; set; }
        public string? BagianRs { get; set; }
        public string? Bhp { get; set; }
        public string? TarifPerujuk { get; set; }
        public string? TarifDokter { get; set; }
        public string? TarifPetugas { get; set; }
        public string? Kso { get; set; }
        public string? Menejemen { get; set; }
    }

    private const string TarifCols =
        @"j.kd_jenis_prw as KdJenisPrw, j.nm_perawatan as Nama, cast(j.total_byr as char) as Biaya, cast(j.bagian_rs as char) as BagianRs,
          cast(j.bhp as char) as Bhp, cast(j.tarif_perujuk as char) as TarifPerujuk, cast(j.tarif_tindakan_dokter as char) as TarifDokter,
          cast(j.tarif_tindakan_petugas as char) as TarifPetugas, cast(j.kso as char) as Kso, cast(j.menejemen as char) as Menejemen";

    private static LabTarifRow ToTarif(TarifSql t) => new()
    {
        KdJenisPrw = t.KdJenisPrw ?? "", Nama = t.Nama ?? "", Biaya = Num(t.Biaya), BagianRs = Num(t.BagianRs), Bhp = Num(t.Bhp),
        TarifPerujuk = Num(t.TarifPerujuk), TarifDokter = Num(t.TarifDokter), TarifPetugas = Num(t.TarifPetugas), Kso = Num(t.Kso), Menejemen = Num(t.Menejemen),
    };

    public async Task<IReadOnlyList<LabTarifRow>> GetTarifAsync(LabCategory category, LabRegInfo? reg, string? keyword, CancellationToken ct = default)
    {
        var setting = await GetTarifSettingAsync(ct);
        var ps = new SqlParams();
        // Same four cara_bayar_lab / kelas_lab variants as the legacy tampiltarif().
        var sql = $"select {TarifCols} from jns_perawatan_lab j inner join penjab pj on pj.kd_pj=j.kd_pj where j.kategori={ps.Add(Cat(category))} and j.status='1' ";
        if (setting.CaraBayar && reg is not null) sql += $"and (j.kd_pj={ps.Add(reg.KodePj)} or j.kd_pj='-') ";
        if (setting.Kelas && reg is not null) sql += $"and (j.kelas={ps.Add(reg.Status == "Ranap" ? reg.Kelas : "Rawat Jalan")} or j.kelas='-') ";
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = ps.Add("%" + keyword.Trim() + "%");
            sql += $"and (j.kd_jenis_prw like {k} or j.nm_perawatan like {k}) ";
        }
        sql += "order by j.kd_jenis_prw";
        return (await db.Database.SqlQueryRaw<TarifSql>(sql, ps.Values.ToArray()).ToListAsync(ct)).Select(ToTarif).ToList();
    }

    public async Task<IReadOnlyList<LabTarifRow>> GetTarifByKodeAsync(LabCategory category, IReadOnlyList<string> kodes, CancellationToken ct = default)
    {
        if (kodes.Count == 0) return Array.Empty<LabTarifRow>();
        var ps = new SqlParams();
        var sql = $"select {TarifCols} from jns_perawatan_lab j where j.kategori={ps.Add(Cat(category))} and j.kd_jenis_prw in (" +
                  string.Join(",", kodes.Select(k => ps.Add(k))) + ") order by j.kd_jenis_prw";
        return (await db.Database.SqlQueryRaw<TarifSql>(sql, ps.Values.ToArray()).ToListAsync(ct)).Select(ToTarif).ToList();
    }

    private sealed class TemplateSql
    {
        public string? KdJenisPrw { get; set; }
        public string? IdTemplate { get; set; }
        public string? Pemeriksaan { get; set; }
        public string? Satuan { get; set; }
        public string? Ld { get; set; }
        public string? La { get; set; }
        public string? Pd { get; set; }
        public string? Pa { get; set; }
        public string? Metode { get; set; }
        public string? BiayaItem { get; set; }
        public string? BagianRs { get; set; }
        public string? Bhp { get; set; }
        public string? BagianPerujuk { get; set; }
        public string? BagianDokter { get; set; }
        public string? BagianLaborat { get; set; }
        public string? Kso { get; set; }
        public string? Menejemen { get; set; }
    }

    public async Task<IReadOnlyList<LabTemplateRow>> GetTemplatesAsync(string kdJenisPrw, string? keyword, int[]? onlyIds, CancellationToken ct = default)
    {
        var ps = new SqlParams();
        var sql = $@"select t.kd_jenis_prw as KdJenisPrw, cast(t.id_template as char) as IdTemplate, t.Pemeriksaan as Pemeriksaan, t.satuan as Satuan,
                t.nilai_rujukan_ld as Ld, t.nilai_rujukan_la as La, t.nilai_rujukan_pd as Pd, t.nilai_rujukan_pa as Pa, '' as Metode,
                cast(t.biaya_item as char) as BiayaItem, cast(t.bagian_rs as char) as BagianRs, cast(t.bhp as char) as Bhp,
                cast(t.bagian_perujuk as char) as BagianPerujuk, cast(t.bagian_dokter as char) as BagianDokter,
                cast(t.bagian_laborat as char) as BagianLaborat, cast(t.kso as char) as Kso, cast(t.menejemen as char) as Menejemen
              from template_laboratorium t where t.kd_jenis_prw={ps.Add(kdJenisPrw)} and t.Pemeriksaan like {ps.Add("%" + (keyword?.Trim() ?? "") + "%")} ";
        if (onlyIds is { Length: > 0 })
            sql += "and t.id_template in (" + string.Join(",", onlyIds.Select(i => ps.Add(i))) + ") ";
        sql += "order by t.urut";

        return (await db.Database.SqlQueryRaw<TemplateSql>(sql, ps.Values.ToArray()).ToListAsync(ct)).Select(t => new LabTemplateRow
        {
            KdJenisPrw = t.KdJenisPrw ?? "", IdTemplate = (int)Num(t.IdTemplate), Pemeriksaan = t.Pemeriksaan ?? "", Satuan = t.Satuan ?? "",
            NilaiRujukanLd = t.Ld ?? "", NilaiRujukanLa = t.La ?? "", NilaiRujukanPd = t.Pd ?? "", NilaiRujukanPa = t.Pa ?? "",
            Metode = t.Metode ?? "", BiayaItem = Num(t.BiayaItem), BagianRs = Num(t.BagianRs), Bhp = Num(t.Bhp),
            BagianPerujuk = Num(t.BagianPerujuk), BagianDokter = Num(t.BagianDokter), BagianLaborat = Num(t.BagianLaborat),
            Kso = Num(t.Kso), Menejemen = Num(t.Menejemen),
        }).ToList();
    }

    private sealed class OrderRow
    {
        public string? DokterPerujuk { get; set; }
        public string? KdJenisPrw { get; set; }
        public string? IdTemplate { get; set; }
    }

    public async Task<LabOrderInfo?> GetOrderAsync(LabCategory category, string noOrder, CancellationToken ct = default)
    {
        var sfx = Suffix(category);
        var head = await db.Database.SqlQueryRaw<OrderRow>($"select dokter_perujuk as DokterPerujuk, '' as KdJenisPrw, '' as IdTemplate from permintaan_lab{sfx} where noorder={{0}}", noOrder).FirstOrDefaultAsync(ct);
        if (head is null) return null;
        var info = new LabOrderInfo { DokterPerujuk = head.DokterPerujuk ?? "" };

        var pem = await db.Database.SqlQueryRaw<OrderRow>($"select '' as DokterPerujuk, kd_jenis_prw as KdJenisPrw, '' as IdTemplate from permintaan_pemeriksaan_lab{sfx} where noorder={{0}} order by kd_jenis_prw", noOrder).ToListAsync(ct);
        info.KdJenisPrw.AddRange(pem.Select(p => p.KdJenisPrw ?? ""));

        if (category != LabCategory.PA)
        {
            var det = await db.Database.SqlQueryRaw<OrderRow>($"select '' as DokterPerujuk, '' as KdJenisPrw, cast(id_template as char) as IdTemplate from permintaan_detail_permintaan_lab{sfx} where noorder={{0}}", noOrder).ToListAsync(ct);
            info.IdTemplate.AddRange(det.Select(d => (int)Num(d.IdTemplate)));
        }
        return info;
    }

    public async Task<IReadOnlyList<LabPick>> GetDokterAsync(IReadOnlyList<string>? alwaysInclude, CancellationToken ct = default)
    {
        // Active doctors, plus specific codes (a default PJ / the visit's doctor may be flagged inactive).
        var ps = new SqlParams();
        var sql = "select kd_dokter as Kode, nm_dokter as Nama from dokter where status='1' ";
        var extra = alwaysInclude?.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (extra is { Count: > 0 }) sql += "or kd_dokter in (" + string.Join(",", extra.Select(c => ps.Add(c))) + ") ";
        sql += "order by nm_dokter";
        return await db.Database.SqlQueryRaw<LabPick>(sql, ps.Values.ToArray()).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LabPick>> GetPetugasAsync(CancellationToken ct = default)
        => await db.Database.SqlQueryRaw<LabPick>("select nip as Kode, nama as Nama from petugas where status='1' order by nama").ToListAsync(ct);

    public async Task<string> GetDefaultDokterPjAsync(LabCategory category, CancellationToken ct = default)
    {
        var col = category switch { LabCategory.PA => "kd_dokterlabpa", LabCategory.MB => "kd_dokterlabmb", _ => "kd_dokterlab" };
        var v = await db.Database.SqlQueryRaw<LabPick>($"select {col} as Kode, '' as Nama from set_pjlab limit 1").FirstOrDefaultAsync(ct);
        return v?.Kode ?? "";
    }
}
