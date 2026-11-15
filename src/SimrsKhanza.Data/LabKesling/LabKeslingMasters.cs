using SimrsKhanza.Config;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.LabKesling;

public enum FieldKind { Text, Number, Choice, Lookup }

/// <summary>One editable column of a master table. Column is a compile-time constant (used in SQL).</summary>
public record MasterField(string Column, string Label, FieldKind Kind = FieldKind.Text, int MaxLen = 0,
    string[]? Choices = null, string? LookupSql = null, bool Key = false, bool AutoKey = false);

public record MasterSpec(
    string Title, string Table, MasterField[] Fields,
    string ListSql, (string Column, string Header)[] Display,
    string? AutoKeySql = null, int AutoKeyLen = 0,
    string? TotalColumn = null, string[]? TotalOf = null);

/// <summary>
/// The four Lab Kesling master tables (legacy LabKeslingPelanggan, LabKeslingMasterSampelBakuMutu,
/// LabKeslingParameterPengujian, LabKeslingNilaiNormalBakuMutu) as data, edited by one generic screen.
/// </summary>
public static class LabKeslingMasters
{
    private static readonly string[] Jasa =
        ["jasa_sarana", "paket_bhp", "jasa_pj_lab", "jasa_pj_pengujian", "jasa_verifikator", "jasa_petugas", "kso", "jasa_menejemen"];

    public static readonly MasterSpec Pelanggan = new(
        "Pelanggan Lab Kesling", "labkesling_pelanggan",
        [
            new("kode_pelanggan", "Kode", MaxLen: 6, Key: true, AutoKey: true),
            new("nama_pelanggan", "Nama Pelanggan", MaxLen: 50),
            new("alamat", "Alamat", MaxLen: 50),
            new("kota", "Kota", MaxLen: 20),
            new("no_telp", "No.Telp", MaxLen: 13),
            new("kegiatan_usaha", "Kegiatan Usaha", MaxLen: 30),
            new("personal_dihubungi", "Personal Dihubungi", MaxLen: 30),
        ],
        "SELECT * FROM labkesling_pelanggan WHERE CONCAT_WS(' ',kode_pelanggan,nama_pelanggan,alamat,kota,no_telp,kegiatan_usaha,personal_dihubungi) LIKE @p0 ORDER BY kode_pelanggan",
        [("kode_pelanggan", "Kode"), ("nama_pelanggan", "Nama Pelanggan"), ("alamat", "Alamat"), ("kota", "Kota"),
         ("no_telp", "No.Telp"), ("kegiatan_usaha", "Kegiatan Usaha"), ("personal_dihubungi", "Personal Dihubungi")],
        "SELECT IFNULL(MAX(CONVERT(RIGHT(kode_pelanggan,6),SIGNED)),0) FROM labkesling_pelanggan", 6);

    public static readonly MasterSpec Sampel = new(
        "Master Sampel & Baku Mutu", "labkesling_master_sampel",
        [
            new("kode_sampel", "Kode Sampel", MaxLen: 4, Key: true),
            new("nama_sampel", "Nama Sampel", MaxLen: 40),
            new("baku_mutu", "Baku Mutu", MaxLen: 120),
        ],
        "SELECT * FROM labkesling_master_sampel WHERE CONCAT_WS(' ',kode_sampel,nama_sampel,baku_mutu) LIKE @p0 ORDER BY kode_sampel",
        [("kode_sampel", "Kode"), ("nama_sampel", "Nama Sampel"), ("baku_mutu", "Baku Mutu")]);

    public static readonly MasterSpec Parameter = new(
        "Parameter Pengujian", "labkesling_parameter_pengujian",
        [
            new("kode_parameter", "Kode", MaxLen: 5, Key: true, AutoKey: true),
            new("nama_parameter", "Nama Parameter", MaxLen: 70),
            new("metode_pengujian", "Metode", FieldKind.Choice, Choices:
                ["Spektrofotometri", "Elektrometri", "Gravimetri", "AAS", "Titrimetri", "Angka Lempeng Total",
                 "Membran Filter", "Tabung Fermentasi", "Biakan & Identifikasi", "Singlepath"]),
            new("satuan", "Satuan", MaxLen: 20),
            new("kategori", "Kategori", FieldKind.Choice, Choices: ["Fisika", "Kimia", "Mikrobiologi"]),
            new("jasa_sarana", "Jasa Sarana", FieldKind.Number),
            new("paket_bhp", "Paket BHP", FieldKind.Number),
            new("jasa_pj_lab", "Jasa PJ Lab", FieldKind.Number),
            new("jasa_pj_pengujian", "Jasa PJ Pengujian", FieldKind.Number),
            new("jasa_verifikator", "Jasa Verifikator", FieldKind.Number),
            new("jasa_petugas", "Jasa Petugas/Analis", FieldKind.Number),
            new("kso", "KSO", FieldKind.Number),
            new("jasa_menejemen", "Jasa Menejemen", FieldKind.Number),
        ],
        "SELECT * FROM labkesling_parameter_pengujian WHERE CONCAT_WS(' ',kode_parameter,nama_parameter,metode_pengujian,satuan,kategori) LIKE @p0 ORDER BY kode_parameter",
        [("kode_parameter", "Kode"), ("nama_parameter", "Nama Parameter"), ("metode_pengujian", "Metode"), ("satuan", "Satuan"),
         ("kategori", "Kategori"), ("jasa_sarana", "J.Sarana"), ("paket_bhp", "BHP"), ("jasa_pj_lab", "J.PJ Lab"),
         ("jasa_pj_pengujian", "J.PJ Uji"), ("jasa_verifikator", "J.Verif"), ("jasa_petugas", "J.Analis"), ("kso", "KSO"),
         ("jasa_menejemen", "J.Menej"), ("total", "Total")],
        "SELECT IFNULL(MAX(CONVERT(RIGHT(kode_parameter,3),SIGNED)),0) FROM labkesling_parameter_pengujian", 3,
        "total", Jasa);

    public static readonly MasterSpec NilaiNormal = new(
        "Nilai Normal Baku Mutu", "labkesling_nilai_normal_baku_mutu",
        [
            new("kode_sampel", "Sampel", FieldKind.Lookup, Key: true,
                LookupSql: "SELECT kode_sampel AS kode, nama_sampel AS nama FROM labkesling_master_sampel ORDER BY kode_sampel"),
            new("kode_parameter", "Parameter", FieldKind.Lookup, Key: true,
                LookupSql: "SELECT kode_parameter AS kode, CONCAT(nama_parameter,' (',IFNULL(satuan,''),')') AS nama FROM labkesling_parameter_pengujian ORDER BY kode_parameter"),
            new("nilai_normal", "Nilai Normal", MaxLen: 30),
        ],
        """
        SELECT n.kode_sampel, s.nama_sampel, n.kode_parameter, p.nama_parameter, p.satuan, n.nilai_normal
        FROM labkesling_nilai_normal_baku_mutu n
        JOIN labkesling_master_sampel s ON s.kode_sampel=n.kode_sampel
        JOIN labkesling_parameter_pengujian p ON p.kode_parameter=n.kode_parameter
        WHERE CONCAT_WS(' ',n.kode_sampel,s.nama_sampel,n.kode_parameter,p.nama_parameter,n.nilai_normal) LIKE @p0
        ORDER BY n.kode_sampel, n.kode_parameter
        """,
        [("kode_sampel", "Kd Sampel"), ("nama_sampel", "Sampel"), ("kode_parameter", "Kd Param"), ("nama_parameter", "Parameter"),
         ("satuan", "Satuan"), ("nilai_normal", "Nilai Normal")]);
}

/// <summary>Generic CRUD for a <see cref="MasterSpec"/>.</summary>
public class MasterRepository(DatabaseSettings settings)
{
    public async Task<List<Row>> ListAsync(MasterSpec spec, string? cari)
    {
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync(spec.ListSql, $"%{cari?.Trim()}%");
    }

    public async Task<List<(string Kode, string Nama)>> LookupAsync(string sql)
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync(sql)).Select(r => (r.S("kode"), r.S("nama"))).ToList();
    }

    public async Task<string> NextKeyAsync(MasterSpec spec)
    {
        if (spec.AutoKeySql is null) return "";
        await using var db = await Db.OpenAsync(settings);
        return await db.AutoNomorAsync(spec.AutoKeySql, "", spec.AutoKeyLen);
    }

    private static Dictionary<string, object?> WithTotal(MasterSpec spec, Dictionary<string, object?> values)
    {
        var v = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        if (spec.TotalColumn is not null && spec.TotalOf is not null)
            v[spec.TotalColumn] = spec.TotalOf.Sum(c => v.TryGetValue(c, out var x) && x is double d ? d : 0);
        return v;
    }

    /// <summary>Insert when <paramref name="originalKeys"/> is null, otherwise update the row having those keys.</summary>
    public async Task SaveAsync(MasterSpec spec, Dictionary<string, object?> values, Dictionary<string, object?>? originalKeys)
    {
        var v = WithTotal(spec, values);
        foreach (var f in spec.Fields.Where(f => f.Key))
            if (string.IsNullOrWhiteSpace(Convert.ToString(v.GetValueOrDefault(f.Column))))
                throw new ArgumentException($"{f.Label} wajib diisi.");

        var cols = v.Keys.Where(k => spec.Fields.Any(f => f.Column.Equals(k, StringComparison.OrdinalIgnoreCase))
                                     || k.Equals(spec.TotalColumn, StringComparison.OrdinalIgnoreCase)).ToList();
        var args = cols.Select(c => v[c]).ToList();
        await using var db = await Db.OpenAsync(settings);

        if (originalKeys is null)
        {
            var keyCols = spec.Fields.Where(f => f.Key).Select(f => f.Column).ToList();
            var where = string.Join(" AND ", keyCols.Select((c, i) => $"`{c}`=@p{i}"));
            if (await db.CountAsync($"SELECT COUNT(*) FROM `{spec.Table}` WHERE {where}", keyCols.Select(c => v[c]).ToArray()) > 0)
                throw new InvalidOperationException("Data dengan kode tersebut sudah ada.");
            var sql = $"INSERT INTO `{spec.Table}` ({string.Join(",", cols.Select(c => $"`{c}`"))}) VALUES ({string.Join(",", cols.Select((_, i) => $"@p{i}"))})";
            await db.ExecAsync(sql, args.ToArray());
        }
        else
        {
            var set = string.Join(",", cols.Select((c, i) => $"`{c}`=@p{i}"));
            var keys = originalKeys.Keys.ToList();
            var where = string.Join(" AND ", keys.Select((c, i) => $"`{c}`=@p{cols.Count + i}"));
            args.AddRange(keys.Select(k => originalKeys[k]));
            if (await db.ExecAsync($"UPDATE `{spec.Table}` SET {set} WHERE {where}", args.ToArray()) == 0)
                throw new InvalidOperationException("Data yang diubah tidak ditemukan.");
        }
    }

    public async Task DeleteAsync(MasterSpec spec, Dictionary<string, object?> keys)
    {
        var k = keys.Keys.ToList();
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync($"DELETE FROM `{spec.Table}` WHERE {string.Join(" AND ", k.Select((c, i) => $"`{c}`=@p{i}"))}",
            k.Select(c => keys[c]).ToArray());
    }
}
