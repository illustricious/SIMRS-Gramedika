using Microsoft.EntityFrameworkCore;

// EF1002: the only interpolated parts are constant table names picked from AntrianBoardKind;
// every runtime value goes through {n} parameters.
#pragma warning disable EF1002

namespace SimrsGramedika.Data;

/// <summary>
/// Display boards that legacy serves as PHP pages under webapps/ (each page polls a
/// data_antrian*.php table and a data_antrian*judul.php "panggilan" panel).
/// </summary>
public enum AntrianBoardKind
{
    /// <summary>poli.php — "Jadwal Praktek Dokter" for today (menu: Antrian Unit/Poliklinik 2).</summary>
    JadwalPoli,
    /// <summary>antrian.php — queue of one dokter/poli (opened from a JadwalPoli row).</summary>
    PoliDokter,
    Apotek2,
    Apotek2Racikan,
    Apotek2NonRacikan,
    LabPk,
    LabPa,
    LabMb,
    Radiologi,
}

/// <summary>One "Panggilan ..." line of a board's header panel.</summary>
public record AntrianCall(string Label, string Text, bool Ring, int Sound);

public record AntrianBoardSnapshot(
    string Title,
    string Subtitle,
    string Instansi,
    string AlamatInstansi,
    IReadOnlyList<string> Columns,
    IReadOnlyList<AntrianBoardRow> Rows,
    IReadOnlyList<AntrianCall> Calls);

/// <summary>Cells are display strings; Key1/Key2 carry kd_poli/kd_dokter for the JadwalPoli drill-down.</summary>
public record AntrianBoardRow(IReadOnlyList<string> Cells, string? Key1 = null, string? Key2 = null)
{
    public string this[int i] => i < Cells.Count ? Cells[i] : "";
}

public class AntrianBoardRaw
{
    public string C0 { get; set; } = "";
    public string C1 { get; set; } = "";
    public string C2 { get; set; } = "";
    public string C3 { get; set; } = "";
    public string C4 { get; set; } = "";
    public string C5 { get; set; } = "";
    public string C6 { get; set; } = "";
    public string C7 { get; set; } = "";
}

public class AntrianFlagRaw
{
    public string NoResep { get; set; } = "";
    public string Status { get; set; } = "";
    public string NoRawat { get; set; } = "";
    public string Nama { get; set; } = "";
}

/// <summary>The single "now calling" row of antriloket (loket int, antrian int).</summary>
public record AntrianLoketCall(int Loket, int Antrian);

public interface IAntrianRepository
{
    Task<AntrianLoketCall?> GetLoketAsync(CancellationToken ct = default);

    /// <summary>Legacy "Antri": delete from antriloket, then insert (loket, antrian).</summary>
    Task SetLoketAsync(int loket, int antrian, CancellationToken ct = default);

    /// <summary>Legacy "Stop": delete from antriloket.</summary>
    Task ClearLoketAsync(CancellationToken ct = default);

    /// <summary>
    /// One refresh of a board: today's rows plus the "panggilan" calls. When
    /// <paramref name="resetFlags"/> is true a raised flag (status '1') is consumed exactly
    /// like the legacy PHP pages do — flag tables are SHARED with any legacy board that is
    /// open at the same time, so whichever polls first rings and resets.
    /// </summary>
    Task<AntrianBoardSnapshot> LoadAsync(AntrianBoardKind kind, string? kdPoli = null, string? kdDokter = null,
        bool resetFlags = true, CancellationToken ct = default);
}

public class AntrianRepository(SimrsGramedikaDbContext db) : IAntrianRepository
{
    private const int MaxRows = 500;

    // Table names are fixed constants chosen from the enum, never user text.
    private sealed record RequestBoard(string Title, string Table, string FlagSampel, string FlagHasil, string LabelHasil);

    private static RequestBoard RequestFor(AntrianBoardKind kind) => kind switch
    {
        AntrianBoardKind.LabPk => new("Antrian Laboratorium Patologi Klinis", "permintaan_lab", "antrilabpk", "antrilabpk2", "Panggilan Hasil Laborat"),
        AntrianBoardKind.LabPa => new("Antrian Laboratorium Patologi Anatomi", "permintaan_labpa", "antrilabpa", "antrilabpa2", "Panggilan Hasil Laborat"),
        AntrianBoardKind.LabMb => new("Antrian Laboratorium Mikrobiologi & Bio Molekuler", "permintaan_labmb", "antrilabmb", "antrilabmb2", "Panggilan Hasil Laborat"),
        AntrianBoardKind.Radiologi => new("Antrian Radiologi", "permintaan_radiologi", "antriradiologi", "antriradiologi2", "Panggilan Hasil Radiologi"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public async Task<AntrianBoardSnapshot> LoadAsync(AntrianBoardKind kind, string? kdPoli = null, string? kdDokter = null,
        bool resetFlags = true, CancellationToken ct = default)
    {
        var (instansi, alamat) = await LoadInstansiAsync(ct);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        return kind switch
        {
            AntrianBoardKind.JadwalPoli => await LoadJadwalAsync(instansi, alamat, today, ct),
            AntrianBoardKind.PoliDokter => await LoadPoliDokterAsync(instansi, alamat, today, kdPoli ?? "", kdDokter ?? "", resetFlags, ct),
            AntrianBoardKind.Apotek2 or AntrianBoardKind.Apotek2Racikan or AntrianBoardKind.Apotek2NonRacikan
                => await LoadApotekAsync(kind, instansi, alamat, today, resetFlags, ct),
            _ => await LoadRequestAsync(kind, instansi, alamat, today, resetFlags, ct),
        };
    }

    public async Task<AntrianLoketCall?> GetLoketAsync(CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQueryRaw<AntrianBoardRaw>(
            "select cast(antrian as char) as C0, cast(loket as char) as C1, '' as C2, '' as C3, '' as C4, '' as C5, '' as C6, '' as C7 from antriloket limit 1")
            .ToListAsync(ct);
        if (rows.Count == 0) return null;
        return int.TryParse(rows[0].C1, out var loket) && int.TryParse(rows[0].C0, out var antrian)
            ? new AntrianLoketCall(loket, antrian) : null;
    }

    public async Task SetLoketAsync(int loket, int antrian, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("delete from antriloket", ct);
        await db.Database.ExecuteSqlRawAsync("insert into antriloket values({0},{1})", loket, antrian);
        await tx.CommitAsync(ct);
    }

    public Task ClearLoketAsync(CancellationToken ct = default)
        => db.Database.ExecuteSqlRawAsync("delete from antriloket", ct);

    private async Task<(string Instansi, string Alamat)> LoadInstansiAsync(CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<AntrianBoardRaw>(
            "select ifnull(nama_instansi,'') as C0, concat_ws(', ', alamat_instansi, kabupaten, propinsi) as C1, '' as C2, '' as C3, '' as C4, '' as C5, '' as C6, '' as C7 from setting limit 1")
            .ToListAsync(ct);
        return rows.Count > 0 ? (rows[0].C0, rows[0].C1) : ("", "");
    }

    // ---- Lab (PK/PA/MB) and Radiologi: data_antrian{laborat,radiologi}.php + ...judul.php ----

    private async Task<AntrianBoardSnapshot> LoadRequestAsync(
        AntrianBoardKind kind, string instansi, string alamat, string today, bool resetFlags, CancellationToken ct)
    {
        var b = RequestFor(kind);

        var rows = await db.Database.SqlQueryRaw<AntrianBoardRaw>(
            $@"select p.no_rawat as C0, ps.nm_pasien as C1, d.nm_dokter as C2,
                      if(p.jam_permintaan='00:00:00','',cast(p.jam_permintaan as char)) as C3,
                      if(p.jam_sampel='00:00:00','',cast(p.jam_sampel as char)) as C4,
                      if(p.jam_hasil='00:00:00','',cast(p.jam_hasil as char)) as C5,
                      '' as C6, '' as C7
               from {b.Table} p
               inner join reg_periksa r on p.no_rawat=r.no_rawat
               inner join pasien ps on r.no_rkm_medis=ps.no_rkm_medis
               inner join dokter d on p.dokter_perujuk=d.kd_dokter
               where p.status='ralan' and p.tgl_permintaan={{0}}
               order by p.jam_permintaan desc
               limit {MaxRows}", today).ToListAsync(ct);

        var sampel = await ScalarAsync(
            $@"select concat(p.no_rawat,' ',ps.nm_pasien) as Value from {b.Table} p
               inner join reg_periksa r on p.no_rawat=r.no_rawat
               inner join pasien ps on r.no_rkm_medis=ps.no_rkm_medis
               where p.status='ralan' and p.tgl_sampel={{0}} order by p.jam_sampel desc limit 1", today, ct);
        var hasil = await ScalarAsync(
            $@"select concat(p.no_rawat,' ',ps.nm_pasien) as Value from {b.Table} p
               inner join reg_periksa r on p.no_rawat=r.no_rawat
               inner join pasien ps on r.no_rkm_medis=ps.no_rkm_medis
               where p.status='ralan' and p.tgl_hasil={{0}} order by p.jam_hasil desc limit 1", today, ct);

        var ringSampel = await ConsumeSingleRowFlagAsync(b.FlagSampel, resetFlags, ct);
        var ringHasil = await ConsumeSingleRowFlagAsync(b.FlagHasil, resetFlags, ct);

        return new AntrianBoardSnapshot(
            b.Title, "", instansi, alamat,
            ["No.Rawat", "Nama Pasien", "Perujuk", "Jam Permintaan", "Ambil Sampel", "Keluar Hasil"],
            rows.Select(r => new AntrianBoardRow([r.C0, r.C1, r.C2, r.C3, r.C4, r.C5])).ToList(),
            [
                new AntrianCall("Panggilan Pengambilan Sampel", sampel, ringSampel, 1),
                new AntrianCall(b.LabelHasil, hasil, ringHasil, 2),
            ]);
    }

    /// <summary>
    /// Legacy flag tables (antrilabpk, antriradiologi2, …) hold a single row with status '0'/'1'.
    /// When it is '1' the page rings and rewrites the table as delete + insert('0').
    /// </summary>
    private async Task<bool> ConsumeSingleRowFlagAsync(string flagTable, bool reset, CancellationToken ct)
    {
        var status = await ScalarAsync($"select status as Value from {flagTable} limit 1", null, ct);
        if (status != "1") return false;
        if (reset)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync($"delete from {flagTable}", ct);
            await db.Database.ExecuteSqlRawAsync($"insert into {flagTable} values('0')", ct);
            await tx.CommitAsync(ct);
        }
        return true;
    }

    // ---- Apotek 2 (+ racikan / non racikan): data_antrianfarmasi*.php + ...judul*.php ----

    private async Task<AntrianBoardSnapshot> LoadApotekAsync(
        AntrianBoardKind kind, string instansi, string alamat, string today, bool resetFlags, CancellationToken ct)
    {
        var (title, racikanFilter, racikanFilterOnResep) = kind switch
        {
            AntrianBoardKind.Apotek2Racikan => ("Antrian Apotek/Farmasi Racikan",
                " and a.no_resep in(select distinct no_resep from resep_dokter_racikan)",
                " and resep_obat.no_resep in(select distinct resep_dokter_racikan.no_resep from resep_dokter_racikan)"),
            AntrianBoardKind.Apotek2NonRacikan => ("Antrian Apotek/Farmasi Non Racikan",
                " and a.no_resep not in(select distinct no_resep from resep_dokter_racikan)",
                " and resep_obat.no_resep not in(select distinct resep_dokter_racikan.no_resep from resep_dokter_racikan)"),
            _ => ("Antrian Apotek/Farmasi", "", ""),
        };
        var withJenis = kind == AntrianBoardKind.Apotek2;

        var rows = await db.Database.SqlQueryRaw<AntrianBoardRaw>(
            $@"select resep_obat.no_resep as C0, resep_obat.no_rawat as C1, pasien.nm_pasien as C2,
                      if(exists(select 1 from resep_dokter_racikan x where x.no_resep=resep_obat.no_resep),'Racikan','Non Racikan') as C3,
                      dokter.nm_dokter as C4,
                      cast(resep_obat.jam_peresepan as char) as C5,
                      if(resep_obat.jam='00:00:00','',cast(resep_obat.jam as char)) as C6,
                      if(resep_obat.jam_penyerahan='00:00:00','',cast(resep_obat.jam_penyerahan as char)) as C7
               from resep_obat
               inner join reg_periksa on resep_obat.no_rawat=reg_periksa.no_rawat
               inner join pasien on reg_periksa.no_rkm_medis=pasien.no_rkm_medis
               inner join dokter on resep_obat.kd_dokter=dokter.kd_dokter
               where resep_obat.jam_peresepan<>'00:00:00' and resep_obat.status='ralan'
                 {racikanFilterOnResep}
                 and resep_obat.tgl_peresepan={{0}}
               order by resep_obat.jam_peresepan desc
               limit {MaxRows}", today).ToListAsync(ct);

        var validasi = await ReadApotekFlagAsync("antriapotek2", racikanFilter, resetFlags, ct);
        var penyerahan = await ReadApotekFlagAsync("antriapotek3", racikanFilter, resetFlags, ct);

        IReadOnlyList<string> cols = withJenis
            ? ["No.Resep", "No.Rawat", "Nama Pasien", "Jenis Resep", "Dokter Peresep", "Peresepan", "Validasi", "Penyerahan"]
            : ["No.Resep", "No.Rawat", "Nama Pasien", "Dokter Peresep", "Peresepan", "Validasi", "Penyerahan"];

        return new AntrianBoardSnapshot(
            title, "", instansi, alamat, cols,
            rows.Select(r => new AntrianBoardRow(withJenis
                ? [r.C0, r.C1, r.C2, r.C3, r.C4, r.C5, r.C6, r.C7]
                : [r.C0, r.C1, r.C2, r.C4, r.C5, r.C6, r.C7])).ToList(),
            [
                new AntrianCall("Panggilan Validasi Resep", validasi.Text, validasi.Ring, 1),
                new AntrianCall("Panggilan Penyerahan Resep", penyerahan.Text, penyerahan.Ring, 2),
            ]);
    }

    private async Task<(string Text, bool Ring)> ReadApotekFlagAsync(string flagTable, string racikanFilter, bool reset, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<AntrianFlagRaw>(
            $@"select ifnull(a.no_resep,'') as NoResep, cast(a.status as char) as Status, a.no_rawat as NoRawat,
                      concat(a.no_rawat,' ',ifnull(p.nm_pasien,'')) as Nama
               from {flagTable} a
               left join reg_periksa r on a.no_rawat=r.no_rawat
               left join pasien p on r.no_rkm_medis=p.no_rkm_medis
               where 1=1 {racikanFilter}")
            .ToListAsync(ct);

        var text = string.Join(" ", rows.Select(r => $"{r.NoResep} {r.Nama}"));
        var ring = rows.Any(r => r.Status == "1");

        // Legacy resets with an unfiltered "update <table> set status='0'", kept as-is.
        if (ring && reset)
            await db.Database.ExecuteSqlRawAsync($"update {flagTable} set status='0'", ct);

        return (text, ring);
    }

    // ---- Jadwal Praktek Dokter (poli.php) ----

    private static string HariKerja(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday => "AKHAD",
        DayOfWeek.Monday => "SENIN",
        DayOfWeek.Tuesday => "SELASA",
        DayOfWeek.Wednesday => "RABU",
        DayOfWeek.Thursday => "KAMIS",
        DayOfWeek.Friday => "JUMAT",
        _ => "SABTU",
    };

    private async Task<AntrianBoardSnapshot> LoadJadwalAsync(string instansi, string alamat, string today, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<AntrianBoardRaw>(
            $@"select dokter.nm_dokter as C0, poliklinik.nm_poli as C1,
                      cast(jadwal.jam_mulai as char) as C2, ifnull(cast(jadwal.jam_selesai as char),'') as C3,
                      cast((select count(*) from reg_periksa rp
                            where rp.kd_poli=jadwal.kd_poli and rp.kd_dokter=jadwal.kd_dokter and rp.tgl_registrasi={{1}}) as char) as C4,
                      poliklinik.kd_poli as C5, dokter.kd_dokter as C6, '' as C7
               from jadwal
               inner join dokter on dokter.kd_dokter=jadwal.kd_dokter
               inner join poliklinik on jadwal.kd_poli=poliklinik.kd_poli
               where jadwal.hari_kerja={{0}}
               order by poliklinik.nm_poli, dokter.nm_dokter, jadwal.jam_mulai
               limit {MaxRows}", HariKerja(DateTime.Today.DayOfWeek), today).ToListAsync(ct);

        return new AntrianBoardSnapshot(
            "Jadwal Praktek Dokter", "Klik dua kali nama dokter untuk melihat antrian poli", instansi, alamat,
            ["NAMA DOKTER", "POLIKLINIK", "JAM MULAI", "JAM SELESAI", "REGISTER"],
            rows.Select(r => new AntrianBoardRow([r.C0, r.C1, r.C2, r.C3, r.C4], r.C5, r.C6)).ToList(),
            []);
    }

    // ---- Antrian satu dokter/poli (antrian.php) ----

    private async Task<AntrianBoardSnapshot> LoadPoliDokterAsync(
        string instansi, string alamat, string today, string kdPoli, string kdDokter, bool resetFlags, CancellationToken ct)
    {
        var nmPoli = await ScalarAsync("select nm_poli as Value from poliklinik where kd_poli={0}", kdPoli, ct);
        var nmDokter = await ScalarAsync("select nm_dokter as Value from dokter where kd_dokter={0}", kdDokter, ct);

        var rows = await db.Database.SqlQueryRaw<AntrianBoardRaw>(
            $@"select r.no_reg as C0, r.no_rawat as C1, p.nm_pasien as C2, '' as C3, '' as C4, '' as C5, '' as C6, '' as C7
               from reg_periksa r inner join pasien p on r.no_rkm_medis=p.no_rkm_medis
               where r.kd_poli={{0}} and r.kd_dokter={{1}} and r.tgl_registrasi={{2}} and r.stts='Belum'
               order by r.no_reg
               limit {MaxRows}", kdPoli, kdDokter, today).ToListAsync(ct);

        var flags = await db.Database.SqlQueryRaw<AntrianFlagRaw>(
            @"select '' as NoResep, cast(a.status as char) as Status, a.no_rawat as NoRawat,
                     concat(ifnull(r.no_reg,''),' ',a.no_rawat,' ',ifnull(p.nm_pasien,'')) as Nama
              from antripoli a
              left join reg_periksa r on a.no_rawat=r.no_rawat
              left join pasien p on r.no_rkm_medis=p.no_rkm_medis
              where a.kd_poli={0} and a.kd_dokter={1}", kdPoli, kdDokter).ToListAsync(ct);

        var ring = flags.Any(f => f.Status == "1");
        if (ring && resetFlags)
            await db.Database.ExecuteSqlRawAsync("update antripoli set status='0' where kd_poli={0} and kd_dokter={1}", kdPoli, kdDokter);

        return new AntrianBoardSnapshot(
            $"Antrian Poli {nmPoli}, Dokter {nmDokter}", "", instansi, alamat,
            ["NO", "NO.RAWAT", "NAMA PASIEN"],
            rows.Select(r => new AntrianBoardRow([r.C0, r.C1, r.C2])).ToList(),
            [new AntrianCall("Panggilan Poli", string.Join(" ", flags.Select(f => f.Nama)), ring, 1)]);
    }

    private async Task<string> ScalarAsync(string sql, object? arg, CancellationToken ct)
    {
        var q = arg is null
            ? db.Database.SqlQueryRaw<string>(sql)
            : db.Database.SqlQueryRaw<string>(sql, arg);
        var list = await q.ToListAsync(ct);
        return list.FirstOrDefault() ?? "";
    }
}
