using SimrsGramedika.Data.Keuangan;
#pragma warning disable EF1002 // table names are constants in this file, never user text
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SimrsGramedika.Data;

// ---- read models -----------------------------------------------------------------------------

/// <summary>Legacy popup menu "Sudah/Belum Keluar Bacaan" — filters on whether a hasil_radiologi row exists.</summary>
public enum RadHasilStatus { Semua, SudahAdaHasil, BelumAdaHasil }

/// <summary>Filter bar of the legacy "Data Pemeriksaan Radiologi" list (No.Rawat / Tanggal s.d. / Pasien / Petugas / Key Word).</summary>
public record RadPeriksaFilter(
    DateOnly From, DateOnly To, string? NoRawat = null, string? NoRm = null, string? Nip = null,
    string? Keyword = null, RadHasilStatus Hasil = RadHasilStatus.Semua);

/// <summary>Identifies one pemeriksaan visit: legacy keys list rows on no_rawat + tgl_periksa + jam.</summary>
public record RadPeriksaKey(string NoRawat, string Tgl, string Jam);

public class RadPeriksaItemRow
{
    public string KdJenisPrw { get; set; } = "";
    public string Nama { get; set; } = "";
    public decimal Biaya { get; set; }
    /// <summary>"Proyeksi : …, kV : …, …, Dosis Radiasi : …" — built in SQL exactly like legacy's `proyeksi` column.</summary>
    public string Proyeksi { get; set; } = "";
}

public class RadPeriksaBhpRow
{
    public string KodeBrng { get; set; } = "";
    public string Nama { get; set; } = "";
    public string Satuan { get; set; } = "";
    public string Jumlah { get; set; } = "";
}

/// <summary>One visit of the list: header + pemeriksaan + BHP + hasil — the nested rows the legacy grid builds.</summary>
public class RadPeriksaGroup
{
    public RadPeriksaKey Key { get; set; } = new("", "", "");
    public string NoRm { get; set; } = "";
    public string NamaPasien { get; set; } = "";
    public string Petugas { get; set; } = "";
    public string Nip { get; set; } = "";
    public string DokterPerujukNama { get; set; } = "";
    public string DokterPerujukKode { get; set; } = "";
    public string DokterPjNama { get; set; } = "";
    public string DokterPjKode { get; set; } = "";
    public string CaraBayar { get; set; } = "";
    public string KamarPoli { get; set; } = "";
    public string Hasil { get; set; } = "";
    public List<RadPeriksaItemRow> Pemeriksaan { get; } = new();
    public List<RadPeriksaBhpRow> Bhp { get; } = new();
    public decimal Total => Pemeriksaan.Sum(p => p.Biaya);
}

/// <summary>permintaan_radiologi.informasi_tambahan / diagnosa_klinis linked to a periksa row via tgl_hasil/jam_hasil (legacy ps5).</summary>
public record RadPermintaanInfo(string InformasiTambahan, string DiagnosaKlinis);

/// <summary>A selectable jns_perawatan_radiologi row with the tarif split periksa_radiologi stores per pemeriksaan.</summary>
public class RadTarifRow
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

/// <summary>ipsrsbarang rows of jenis "…Rad…" — the BHP (consumables) table of the entry form.</summary>
public class RadBhpRow
{
    public string KodeBrng { get; set; } = "";
    public string Nama { get; set; } = "";
    public string Satuan { get; set; } = "";
    public decimal Harga { get; set; }
}

public class RadTemplateRow
{
    public string NoTemplate { get; set; } = "";
    public string Nama { get; set; } = "";
    public string Isi { get; set; } = "";
    public override string ToString() => Nama;
}

public class RadOrderInfo
{
    public string DokterPerujuk { get; set; } = "";
    public List<string> KdJenisPrw { get; } = new();
}

public record RadTarifSetting(bool CaraBayar, bool Kelas);

// ---- write models ----------------------------------------------------------------------------

/// <summary>One ticked pemeriksaan with its tarif split and the editable exposure parameters (legacy columns 11–18, truncated to the DB widths).</summary>
public record RadSavePemeriksaan(
    RadTarifRow Tarif, string Proyeksi, string KV, string MAs, string Ffd, string Bsf, string Inak, string JmlPenyinaran, string Dosis);

public record RadSaveBhp(string KodeBrng, string KodeSat, decimal Jumlah, decimal Harga);

public record RadSaveRequest(
    string NoRawat, string Nip, string DokterPerujuk, string DokterPj, DateOnly Tanggal, TimeOnly Jam, string Status,
    string? NoOrder, IReadOnlyList<RadSavePemeriksaan> Pemeriksaan, IReadOnlyList<RadSaveBhp> Bhp, string Hasil);

public interface IRadiologiPeriksaRepository
{
    Task<IReadOnlyList<RadPeriksaGroup>> GetPeriksaGroupsAsync(RadPeriksaFilter filter, CancellationToken ct = default);
    Task<RadPermintaanInfo?> GetPermintaanInfoAsync(RadPeriksaKey key, CancellationToken ct = default);
    Task UpdateHasilAsync(RadPeriksaKey key, string hasil, CancellationToken ct = default);
    Task UpdateDokterPetugasAsync(RadPeriksaKey key, string nip, string dokterPerujuk, string dokterPj, CancellationToken ct = default);
    Task DeleteAsync(RadPeriksaKey key, CancellationToken ct = default);
    Task SaveAsync(RadSaveRequest request, CancellationToken ct = default);
    Task<RadTarifSetting> GetTarifSettingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RadTarifRow>> GetTarifAsync(LabRegInfo? reg, string? keyword, CancellationToken ct = default);
    /// <summary>Tarif rows of what a permintaan ordered, regardless of payer/kelas filtering (legacy tampil(order)).</summary>
    Task<IReadOnlyList<RadTarifRow>> GetTarifByOrderAsync(string noOrder, CancellationToken ct = default);
    Task<IReadOnlyList<RadBhpRow>> GetBhpAsync(string? keyword, CancellationToken ct = default);
    Task<IReadOnlyList<RadTemplateRow>> GetHasilTemplatesAsync(CancellationToken ct = default);
    Task<RadOrderInfo?> GetOrderAsync(string noOrder, CancellationToken ct = default);
    Task<string> GetDefaultDokterPjAsync(CancellationToken ct = default);
}

/// <summary>
/// Periksa Radiologi over the legacy schema, raw SQL only. Table names are constants, every user
/// value goes through a {n} parameter. Patient/doctor/petugas lookups are shared with
/// <see cref="LabPeriksaRepository"/> (identical SQL in legacy).
/// </summary>
public class RadiologiPeriksaRepository(SimrsGramedikaDbContext db) : IRadiologiPeriksaRepository
{
    private const int MaxHeaders = 1000;

    private sealed class SqlParams
    {
        public List<object> Values { get; } = new();
        public string Add(object v) { Values.Add(v); return "{" + (Values.Count - 1) + "}"; }
    }

    private static decimal Num(string? s)
        => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    // ---- list -------------------------------------------------------------------------------

    private const string KamarSub =
        "(select concat(ki.kd_kamar,', ',b.nm_bangsal) from kamar_inap ki inner join kamar k on k.kd_kamar=ki.kd_kamar " +
        "inner join bangsal b on b.kd_bangsal=k.kd_bangsal where ki.no_rawat=h.no_rawat order by ki.tgl_masuk desc, ki.jam_masuk desc limit 1)";

    private static string FilterSql(RadPeriksaFilter f, SqlParams ps)
    {
        var sql = $"p.tgl_periksa between {ps.Add(f.From.ToString("yyyy-MM-dd"))} and {ps.Add(f.To.ToString("yyyy-MM-dd"))} ";
        if (!string.IsNullOrWhiteSpace(f.NoRawat)) sql += $"and p.no_rawat like {ps.Add("%" + f.NoRawat.Trim() + "%")} ";
        if (!string.IsNullOrWhiteSpace(f.NoRm)) sql += $"and r.no_rkm_medis like {ps.Add("%" + f.NoRm.Trim() + "%")} ";
        if (!string.IsNullOrWhiteSpace(f.Nip)) sql += $"and p.nip={ps.Add(f.Nip.Trim())} ";
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var k = ps.Add("%" + f.Keyword.Trim() + "%");
            sql += $"and (pas.nm_pasien like {k} or pt.nama like {k} or r.no_rkm_medis like {k} or pj.png_jawab like {k}) ";
        }
        sql += f.Hasil switch
        {
            RadHasilStatus.SudahAdaHasil => "and exists (select 1 from hasil_radiologi x where x.no_rawat=p.no_rawat and x.tgl_periksa=p.tgl_periksa and x.jam=p.jam) ",
            RadHasilStatus.BelumAdaHasil => "and not exists (select 1 from hasil_radiologi x where x.no_rawat=p.no_rawat and x.tgl_periksa=p.tgl_periksa and x.jam=p.jam) ",
            _ => "",
        };
        return sql;
    }

    private sealed class HeaderRow
    {
        public string? NoRawat { get; set; }
        public string? NoRm { get; set; }
        public string? NamaPasien { get; set; }
        public string? Petugas { get; set; }
        public string? Nip { get; set; }
        public string? Tgl { get; set; }
        public string? Jam { get; set; }
        public string? PerujukKode { get; set; }
        public string? PerujukNama { get; set; }
        public string? PjKode { get; set; }
        public string? PjNama { get; set; }
        public string? CaraBayar { get; set; }
        public string? NmPoli { get; set; }
        public string? Kamar { get; set; }
        public string? Hasil { get; set; }
    }

    private sealed class ItemRow
    {
        public string? NoRawat { get; set; }
        public string? Tgl { get; set; }
        public string? Jam { get; set; }
        public string? KdJenisPrw { get; set; }
        public string? Nama { get; set; }
        public string? Biaya { get; set; }
        public string? Proyeksi { get; set; }
    }

    private sealed class BhpRow
    {
        public string? NoRawat { get; set; }
        public string? Tgl { get; set; }
        public string? Jam { get; set; }
        public string? KodeBrng { get; set; }
        public string? Nama { get; set; }
        public string? Satuan { get; set; }
        public string? Jumlah { get; set; }
    }

    public async Task<IReadOnlyList<RadPeriksaGroup>> GetPeriksaGroupsAsync(RadPeriksaFilter filter, CancellationToken ct = default)
    {
        // Same capped header set (derived table h) is reused by the three child queries; parameters repeat per query.
        string HeaderSet(SqlParams p)
        {
            var w = FilterSql(filter, p);
            return $@"(select p.no_rawat, p.tgl_periksa, p.jam, min(r.no_rkm_medis) as no_rkm_medis, min(pas.nm_pasien) as nm_pasien,
                             min(pt.nama) as petugas, min(p.nip) as nip, min(p.dokter_perujuk) as perujuk_kode, min(dp.nm_dokter) as perujuk_nama,
                             min(p.kd_dokter) as pj_kode, min(dpj.nm_dokter) as pj_nama, min(pj.png_jawab) as png_jawab, min(pl.nm_poli) as nm_poli
                      from periksa_radiologi p inner join reg_periksa r on p.no_rawat=r.no_rawat
                      inner join pasien pas on r.no_rkm_medis=pas.no_rkm_medis
                      left join petugas pt on p.nip=pt.nip left join penjab pj on r.kd_pj=pj.kd_pj
                      left join dokter dpj on p.kd_dokter=dpj.kd_dokter left join dokter dp on p.dokter_perujuk=dp.kd_dokter
                      left join poliklinik pl on r.kd_poli=pl.kd_poli
                      where {w}
                      group by p.no_rawat, p.tgl_periksa, p.jam
                      order by p.tgl_periksa desc, p.jam desc limit {MaxHeaders}) h";
        }

        var ps1 = new SqlParams();
        var headers = await db.Database.SqlQueryRaw<HeaderRow>($@"select h.no_rawat as NoRawat, h.no_rkm_medis as NoRm, h.nm_pasien as NamaPasien,
                  h.petugas as Petugas, h.nip as Nip, date_format(h.tgl_periksa,'%Y-%m-%d') as Tgl, cast(h.jam as char) as Jam,
                  h.perujuk_kode as PerujukKode, h.perujuk_nama as PerujukNama, h.pj_kode as PjKode, h.pj_nama as PjNama,
                  h.png_jawab as CaraBayar, h.nm_poli as NmPoli, {KamarSub} as Kamar, hr.hasil as Hasil
                from {HeaderSet(ps1)}
                left join hasil_radiologi hr on hr.no_rawat=h.no_rawat and hr.tgl_periksa=h.tgl_periksa and hr.jam=h.jam
                order by h.tgl_periksa desc, h.jam desc", ps1.Values.ToArray()).ToListAsync(ct);

        var groups = new List<RadPeriksaGroup>();
        var byKey = new Dictionary<RadPeriksaKey, RadPeriksaGroup>();
        foreach (var r in headers)
        {
            var key = new RadPeriksaKey(r.NoRawat ?? "", r.Tgl ?? "", r.Jam ?? "");
            if (byKey.ContainsKey(key)) continue;
            var g = new RadPeriksaGroup
            {
                Key = key, NoRm = r.NoRm ?? "", NamaPasien = r.NamaPasien ?? "", Petugas = r.Petugas ?? "", Nip = r.Nip ?? "",
                DokterPerujukKode = r.PerujukKode ?? "", DokterPerujukNama = r.PerujukNama ?? "",
                DokterPjKode = r.PjKode ?? "", DokterPjNama = r.PjNama ?? "", CaraBayar = r.CaraBayar ?? "", Hasil = r.Hasil ?? "",
                KamarPoli = string.IsNullOrEmpty(r.Kamar) ? $"Poli : {r.NmPoli}" : $"Kamar : {r.Kamar}",
            };
            byKey[key] = g;
            groups.Add(g);
        }
        if (groups.Count == 0) return groups;

        var ps2 = new SqlParams();
        var items = await db.Database.SqlQueryRaw<ItemRow>($@"select h.no_rawat as NoRawat, date_format(h.tgl_periksa,'%Y-%m-%d') as Tgl, cast(h.jam as char) as Jam,
                  j.kd_jenis_prw as KdJenisPrw, j.nm_perawatan as Nama, cast(p.biaya as char) as Biaya,
                  concat(if(p.proyeksi<>'',concat('Proyeksi : ',p.proyeksi,', '),''),
                         if(p.kV<>'',concat('kV : ',p.kV,', '),''),
                         if(p.mAS<>'',concat('mAS : ',p.mAS,', '),''),
                         if(p.FFD<>'',concat('FFD : ',p.FFD,', '),''),
                         if(p.BSF<>'',concat('BSF : ',p.BSF,', '),''),
                         if(p.inak<>'',concat('Inak : ',p.inak,', '),''),
                         if(p.jml_penyinaran<>'',concat('Jml Penyinaran : ',p.jml_penyinaran,', '),''),
                         if(p.dosis<>'',concat('Dosis Radiasi : ',p.dosis),'')) as Proyeksi
                from {HeaderSet(ps2)}
                inner join periksa_radiologi p on p.no_rawat=h.no_rawat and p.tgl_periksa=h.tgl_periksa and p.jam=h.jam
                inner join jns_perawatan_radiologi j on j.kd_jenis_prw=p.kd_jenis_prw
                order by h.tgl_periksa desc, h.jam desc, p.kd_jenis_prw", ps2.Values.ToArray()).ToListAsync(ct);
        foreach (var i in items)
        {
            if (!byKey.TryGetValue(new RadPeriksaKey(i.NoRawat ?? "", i.Tgl ?? "", i.Jam ?? ""), out var g)) continue;
            g.Pemeriksaan.Add(new RadPeriksaItemRow
            {
                KdJenisPrw = i.KdJenisPrw ?? "", Nama = i.Nama ?? "", Biaya = Num(i.Biaya), Proyeksi = i.Proyeksi ?? "",
            });
        }

        var ps3 = new SqlParams();
        var bhps = await db.Database.SqlQueryRaw<BhpRow>($@"select h.no_rawat as NoRawat, date_format(h.tgl_periksa,'%Y-%m-%d') as Tgl, cast(h.jam as char) as Jam,
                  b.kode_brng as KodeBrng, ib.nama_brng as Nama, b.kode_sat as Satuan, cast(b.jumlah as char) as Jumlah
                from {HeaderSet(ps3)}
                inner join beri_bhp_radiologi b on b.no_rawat=h.no_rawat and b.tgl_periksa=h.tgl_periksa and b.jam=h.jam
                inner join ipsrsbarang ib on ib.kode_brng=b.kode_brng
                order by h.tgl_periksa desc, h.jam desc, b.kode_brng", ps3.Values.ToArray()).ToListAsync(ct);
        foreach (var b in bhps)
        {
            if (!byKey.TryGetValue(new RadPeriksaKey(b.NoRawat ?? "", b.Tgl ?? "", b.Jam ?? ""), out var g)) continue;
            g.Bhp.Add(new RadPeriksaBhpRow { KodeBrng = b.KodeBrng ?? "", Nama = b.Nama ?? "", Satuan = b.Satuan ?? "", Jumlah = b.Jumlah ?? "" });
        }
        return groups;
    }

    private sealed class PermintaanRow
    {
        public string? Info { get; set; }
        public string? Diagnosa { get; set; }
    }

    public async Task<RadPermintaanInfo?> GetPermintaanInfoAsync(RadPeriksaKey key, CancellationToken ct = default)
    {
        var r = await db.Database.SqlQueryRaw<PermintaanRow>(
            "select informasi_tambahan as Info, diagnosa_klinis as Diagnosa from permintaan_radiologi where no_rawat={0} and tgl_hasil={1} and jam_hasil={2} limit 1",
            key.NoRawat, key.Tgl, key.Jam).FirstOrDefaultAsync(ct);
        return r is null ? null : new RadPermintaanInfo(r.Info ?? "", r.Diagnosa ?? "");
    }

    // ---- writes -----------------------------------------------------------------------------

    /// <summary>
    /// Joins a transaction the caller already opened (caller then owns commit/rollback — used to
    /// dry-run writes), otherwise opens its own and commits on success.
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
        var visit = await KunjunganRanap.KunciAsync(db, noRawat, ct);
        var billed = await ScalarAsync("select count(*) as Value from billing where no_rawat={0}", [noRawat], ct);
        if (billed > 0 || visit.Status == SimrsGramedika.Core.Entities.StatusRegistrasi.Batal ||
            visit.StatusBayar == SimrsGramedika.Core.Entities.StatusBayar.SudahBayar)
            throw new InvalidOperationException($"Data billing sudah terverifikasi (atau registrasi batal), {action}.\nSilahkan hubungi bagian kasir/keuangan.");
    }

    public async Task UpdateHasilAsync(RadPeriksaKey key, string hasil, CancellationToken ct = default)
    {
        await InTransactionAsync(async () =>
        {
            // Legacy: always delete, re-insert only when the text is not empty.
            await db.Database.ExecuteSqlRawAsync("delete from hasil_radiologi where no_rawat={0} and tgl_periksa={1} and jam={2}",
                new object[] { key.NoRawat, key.Tgl, key.Jam }, ct);
            if (hasil.Length > 0)
                await db.Database.ExecuteSqlRawAsync("insert into hasil_radiologi(no_rawat,tgl_periksa,jam,hasil) values({0},{1},{2},{3})",
                    new object[] { key.NoRawat, key.Tgl, key.Jam, hasil }, ct);
        }, ct);
    }

    public async Task UpdateDokterPetugasAsync(RadPeriksaKey key, string nip, string dokterPerujuk, string dokterPj, CancellationToken ct = default)
    {
        var n = await db.Database.ExecuteSqlRawAsync(
            "update periksa_radiologi set nip={0}, dokter_perujuk={1}, kd_dokter={2} where no_rawat={3} and tgl_periksa={4} and jam={5}",
            new object[] { nip, dokterPerujuk, dokterPj, key.NoRawat, key.Tgl, key.Jam }, ct);
        if (n == 0) throw new InvalidOperationException("Data pemeriksaan tidak ditemukan.");
    }

    public async Task DeleteAsync(RadPeriksaKey key, CancellationToken ct = default)
    {
        await InTransactionAsync(async () =>
        {
            await EnsureNotLockedAsync(key.NoRawat, "data tidak boleh dihapus", ct);
            var p = new object[] { key.NoRawat, key.Tgl, key.Jam };
            var n = await db.Database.ExecuteSqlRawAsync("delete from periksa_radiologi where no_rawat={0} and tgl_periksa={1} and jam={2}", p, ct);
            if (n == 0) throw new InvalidOperationException("Data pemeriksaan tidak ditemukan.");
            await db.Database.ExecuteSqlRawAsync("delete from hasil_radiologi where no_rawat={0} and tgl_periksa={1} and jam={2}", p, ct);
            await db.Database.ExecuteSqlRawAsync("delete from gambar_radiologi where no_rawat={0} and tgl_periksa={1} and jam={2}", p, ct);
            await db.Database.ExecuteSqlRawAsync("delete from beri_bhp_radiologi where no_rawat={0} and tgl_periksa={1} and jam={2}", p, ct);
        }, ct);
    }

    private static string Cut(string s, int max) => s.Length > max ? s[..(max - 1)] : s;

    public async Task SaveAsync(RadSaveRequest rq, CancellationToken ct = default)
    {
        if (rq.Pemeriksaan.Count == 0) throw new InvalidOperationException("Pilih minimal satu pemeriksaan.");

        var tgl = rq.Tanggal.ToString("yyyy-MM-dd");
        var jam = rq.Jam.ToString("HH:mm:ss");

        await InTransactionAsync(async () =>
        {
            await EnsureNotLockedAsync(rq.NoRawat, "data tidak bisa disimpan", ct);
            foreach (var x in rq.Pemeriksaan)
            {
                var t = x.Tarif;
                // Same column order as legacy simpan(); `biaya` = jns_perawatan_radiologi.total_byr. Text parameters are
                // cut to the DB widths the same way legacy does (substring(0,n-1)).
                await db.Database.ExecuteSqlRawAsync(
                    "insert into periksa_radiologi(no_rawat,nip,kd_jenis_prw,tgl_periksa,jam,dokter_perujuk,bagian_rs,bhp,tarif_perujuk," +
                    "tarif_tindakan_dokter,tarif_tindakan_petugas,kso,menejemen,biaya,kd_dokter,status,proyeksi,kV,mAS,FFD,BSF,inak,jml_penyinaran,dosis) " +
                    "values({0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23})",
                    new object[]
                    {
                        rq.NoRawat, rq.Nip, t.KdJenisPrw, tgl, jam, rq.DokterPerujuk, (double)t.BagianRs, (double)t.Bhp, (double)t.TarifPerujuk,
                        (double)t.TarifDokter, (double)t.TarifPetugas, (double)t.Kso, (double)t.Menejemen, (double)t.Biaya, rq.DokterPj, rq.Status,
                        Cut(x.Proyeksi, 50), Cut(x.KV, 10), Cut(x.MAs, 10), Cut(x.Ffd, 10), Cut(x.Bsf, 10), Cut(x.Inak, 10),
                        Cut(x.JmlPenyinaran, 10), Cut(x.Dosis, 20),
                    }, ct);
            }

            foreach (var b in rq.Bhp)
                await db.Database.ExecuteSqlRawAsync(
                    "insert into beri_bhp_radiologi(no_rawat,tgl_periksa,jam,kode_brng,kode_sat,jumlah,harga,total) values({0},{1},{2},{3},{4},{5},{6},{7})",
                    new object[] { rq.NoRawat, tgl, jam, b.KodeBrng, b.KodeSat, (double)b.Jumlah, (double)b.Harga, (double)(b.Harga * b.Jumlah) }, ct);

            if (rq.Hasil.Length > 0)
                await db.Database.ExecuteSqlRawAsync("insert into hasil_radiologi(no_rawat,tgl_periksa,jam,hasil) values({0},{1},{2},{3})",
                    new object[] { rq.NoRawat, tgl, jam, rq.Hasil }, ct);

            if (!string.IsNullOrWhiteSpace(rq.NoOrder))
            {
                // Marks the permintaan as answered (the permintaan list's "Hasil" column).
                await db.Database.ExecuteSqlRawAsync("update permintaan_radiologi set tgl_hasil={0}, jam_hasil={1} where noorder={2}",
                    new object[] { tgl, jam, rq.NoOrder }, ct);
                if (rq.Status == "Ralan")
                {
                    // Legacy pokes the one-row waiting-room display queue so the screen refreshes.
                    await db.Database.ExecuteSqlRawAsync("delete from antriradiologi2", Array.Empty<object>(), ct);
                    await db.Database.ExecuteSqlRawAsync("insert into antriradiologi2 values('1')", Array.Empty<object>(), ct);
                }
            }
        }, ct);
    }

    // ---- lookups ----------------------------------------------------------------------------

    private sealed class SettingRow
    {
        public string? CaraBayar { get; set; }
        public string? Kelas { get; set; }
    }

    public async Task<RadTarifSetting> GetTarifSettingAsync(CancellationToken ct = default)
    {
        var s = await db.Database.SqlQueryRaw<SettingRow>("select cara_bayar_radiologi as CaraBayar, kelas_radiologi as Kelas from set_tarif limit 1").FirstOrDefaultAsync(ct);
        return s is null ? new RadTarifSetting(true, true) : new RadTarifSetting(s.CaraBayar != "No", s.Kelas != "No");
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

    private static RadTarifRow ToTarif(TarifSql t) => new()
    {
        KdJenisPrw = t.KdJenisPrw ?? "", Nama = t.Nama ?? "", Biaya = Num(t.Biaya), BagianRs = Num(t.BagianRs), Bhp = Num(t.Bhp),
        TarifPerujuk = Num(t.TarifPerujuk), TarifDokter = Num(t.TarifDokter), TarifPetugas = Num(t.TarifPetugas), Kso = Num(t.Kso), Menejemen = Num(t.Menejemen),
    };

    public async Task<IReadOnlyList<RadTarifRow>> GetTarifAsync(LabRegInfo? reg, string? keyword, CancellationToken ct = default)
    {
        var setting = await GetTarifSettingAsync(ct);
        var ps = new SqlParams();
        // Same four cara_bayar_radiologi / kelas_radiologi variants as legacy tampil().
        var sql = $"select {TarifCols} from jns_perawatan_radiologi j inner join penjab pj on pj.kd_pj=j.kd_pj where j.status='1' ";
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

    public async Task<IReadOnlyList<RadTarifRow>> GetTarifByOrderAsync(string noOrder, CancellationToken ct = default)
    {
        var sql = $@"select {TarifCols} from jns_perawatan_radiologi j inner join penjab pj on pj.kd_pj=j.kd_pj
                     inner join permintaan_pemeriksaan_radiologi o on o.kd_jenis_prw=j.kd_jenis_prw
                     where o.noorder={{0}} order by j.kd_jenis_prw";
        return (await db.Database.SqlQueryRaw<TarifSql>(sql, noOrder).ToListAsync(ct)).Select(ToTarif).ToList();
    }

    private sealed class BhpSql
    {
        public string? Kode { get; set; }
        public string? Nama { get; set; }
        public string? Satuan { get; set; }
        public string? Harga { get; set; }
    }

    public async Task<IReadOnlyList<RadBhpRow>> GetBhpAsync(string? keyword, CancellationToken ct = default)
    {
        var ps = new SqlParams();
        var sql = "select b.kode_brng as Kode, b.nama_brng as Nama, b.kode_sat as Satuan, cast(b.harga as char) as Harga " +
                  "from ipsrsbarang b inner join ipsrsjenisbarang j on b.jenis=j.kd_jenis where j.nm_jenis like '%Rad%' ";
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = ps.Add("%" + keyword.Trim() + "%");
            sql += $"and (b.kode_brng like {k} or b.nama_brng like {k}) ";
        }
        sql += "order by b.kode_brng";
        return (await db.Database.SqlQueryRaw<BhpSql>(sql, ps.Values.ToArray()).ToListAsync(ct))
            .Select(b => new RadBhpRow { KodeBrng = b.Kode ?? "", Nama = b.Nama ?? "", Satuan = b.Satuan ?? "", Harga = Num(b.Harga) }).ToList();
    }

    private sealed class TemplateSql
    {
        public string? No { get; set; }
        public string? Nama { get; set; }
        public string? Isi { get; set; }
    }

    public async Task<IReadOnlyList<RadTemplateRow>> GetHasilTemplatesAsync(CancellationToken ct = default)
        => (await db.Database.SqlQueryRaw<TemplateSql>(
                "select no_template as No, nama_pemeriksaan as Nama, template_hasil_radiologi as Isi from template_hasil_radiologi order by no_template").ToListAsync(ct))
            .Select(t => new RadTemplateRow { NoTemplate = t.No ?? "", Nama = t.Nama ?? "", Isi = t.Isi ?? "" }).ToList();

    private sealed class OrderSql
    {
        public string? DokterPerujuk { get; set; }
        public string? KdJenisPrw { get; set; }
    }

    public async Task<RadOrderInfo?> GetOrderAsync(string noOrder, CancellationToken ct = default)
    {
        var head = await db.Database.SqlQueryRaw<OrderSql>("select dokter_perujuk as DokterPerujuk, '' as KdJenisPrw from permintaan_radiologi where noorder={0}", noOrder).FirstOrDefaultAsync(ct);
        if (head is null) return null;
        var info = new RadOrderInfo { DokterPerujuk = head.DokterPerujuk ?? "" };
        var pem = await db.Database.SqlQueryRaw<OrderSql>("select '' as DokterPerujuk, kd_jenis_prw as KdJenisPrw from permintaan_pemeriksaan_radiologi where noorder={0} order by kd_jenis_prw", noOrder).ToListAsync(ct);
        info.KdJenisPrw.AddRange(pem.Select(p => p.KdJenisPrw ?? ""));
        return info;
    }

    public async Task<string> GetDefaultDokterPjAsync(CancellationToken ct = default)
    {
        var v = await db.Database.SqlQueryRaw<LabPick>("select kd_dokterrad as Kode, '' as Nama from set_pjlab limit 1").FirstOrDefaultAsync(ct);
        return v?.Kode ?? "";
    }
}
