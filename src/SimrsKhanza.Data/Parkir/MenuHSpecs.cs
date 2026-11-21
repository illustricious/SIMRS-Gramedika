using SimrsKhanza.Data.Master;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.Parkir;

public static class MenuHSpecs
{
    public const string JenisParkirKey = "btnJenisParkir";
    public const string ParkirMasukKey = "btnParkirMasuk";

    public static IReadOnlyDictionary<string, MasterSpec> Masters { get; } = new Dictionary<string, MasterSpec>
    {
        [JenisParkirKey] = new("Jenis Parkir", "parkir_jenis",
            [
                new("kd_parkir", "Kode Jenis", MaxLen: 5, Key: true),
                new("jns_parkir", "Jenis Parkir", MaxLen: 50, Required: true),
                new("biaya", "Tarif (Rp)", FieldKind.Number, Required: true) { NullWhenEmpty = true, Default = 0d },
                new("jenis", "Sistem Tarif", FieldKind.Choice, Choices: ["Harian", "Jam"], Required: true),
            ],
            """
            SELECT kd_parkir, jns_parkir, biaya, jenis
            FROM parkir_jenis
            WHERE kd_parkir LIKE @p0 OR jns_parkir LIKE @p0
            ORDER BY kd_parkir
            """,
            [("kd_parkir", "Kode"), ("jns_parkir", "Jenis Parkir"), ("biaya", "Tarif (Rp)"), ("jenis", "Sistem Tarif")])
        {
            Hint = "Kode maksimal 5 karakter, misalnya P0001. Isi tarif tanpa pemisah ribuan, misalnya 15000. Jenis yang sudah dipakai tidak bisa dihapus.",
            BeforeSave = ValidateJenisAsync,
            BeforeDelete = DeleteJenisAsync,
        },
        ["btnBarcodeParkir"] = new("Barcode Parkir", "parkir_barcode",
            [
                new("kode_barcode", "Barcode", MaxLen: 15, Key: true),
                new("nomer_kartu", "Nomor Kartu", MaxLen: 5, Required: true),
            ],
            """
            SELECT kode_barcode, nomer_kartu
            FROM parkir_barcode
            WHERE kode_barcode LIKE @p0 OR nomer_kartu LIKE @p0
            ORDER BY kode_barcode
            """,
            [("kode_barcode", "Barcode"), ("nomer_kartu", "Nomor Kartu")])
        {
            Hint = "Pindai barcode atau ketik manual. Nomor kartu maksimal 5 karakter, misalnya K0001.",
            BeforeSave = ValidateBarcodeAsync,
            BeforeDelete = DeleteBarcodeAsync,
        },
    };

    private static async Task ValidateJenisAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? original)
    {
        if (values["biaya"] is not double biaya || !double.IsFinite(biaya) || biaya < 0)
            throw new InvalidOperationException("Tarif parkir harus berupa angka nol atau lebih.");
        if (original is null || Equals(original["kd_parkir"], values["kd_parkir"])) return;
        await DeleteJenisAsync(db, original);
    }

    private static async Task DeleteJenisAsync(Db db, Dictionary<string, object?> keys)
    {
        var kode = keys["kd_parkir"];
        // Lock the parent first, as the check-in does, to prevent a new visit before deletion.
        await db.ScalarAsync("SELECT kd_parkir FROM parkir_jenis WHERE kd_parkir=@p0 FOR UPDATE", kode);
        if (await db.ScalarAsync("SELECT no_kendaraan FROM parkir WHERE kd_parkir=@p0 LIMIT 1 FOR UPDATE", kode) is not null)
            throw new InvalidOperationException("Kode jenis parkir sudah dipakai dalam riwayat parkir.");
    }

    private static async Task ValidateBarcodeAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? original)
    {
        if (original is null) return;
        var kartu = await LockCardAsync(db, original);
        if (kartu.Length > 0 && !string.Equals(kartu, values["nomer_kartu"] as string, StringComparison.Ordinal))
            await EnsureCardUnusedAsync(db, kartu);
    }

    private static async Task DeleteBarcodeAsync(Db db, Dictionary<string, object?> keys)
    {
        var kartu = await LockCardAsync(db, keys);
        if (kartu.Length > 0) await EnsureCardUnusedAsync(db, kartu);
    }

    private static Task<string> LockCardAsync(Db db, Dictionary<string, object?> keys) =>
        db.StrAsync("SELECT nomer_kartu FROM parkir_barcode WHERE kode_barcode=@p0 FOR UPDATE", keys["kode_barcode"]);

    private static async Task EnsureCardUnusedAsync(Db db, string kartu)
    {
        if (await db.ScalarAsync("SELECT no_kendaraan FROM parkir WHERE nomer_kartu=@p0 LIMIT 1 FOR UPDATE", kartu) is not null)
            throw new InvalidOperationException("Nomor kartu sudah dipakai dalam riwayat parkir. Kode barcode kartu masih bisa diubah.");
    }
}
