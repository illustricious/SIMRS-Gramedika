using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>Identitas rumah sakit untuk kop cetakan, dari tabel setting Khanza.</summary>
public sealed record KopInstansi(string Nama, string Alamat, string Kontak, string Email, byte[]? Logo)
{
    public static async Task<KopInstansi> MuatAsync(DatabaseSettings settings)
    {
        await using var db = await Db.OpenAsync(settings);
        var row = await db.FirstAsync("""
            SELECT nama_instansi,alamat_instansi,kabupaten,propinsi,kontak,email,logo
            FROM setting ORDER BY (aktifkan = 'Yes') DESC, nama_instansi LIMIT 1
            """) ?? throw new InvalidOperationException("Identitas rumah sakit belum diisi pada pengaturan (tabel setting).");
        var alamat = string.Join(", ",
            new[] { row.S("alamat_instansi"), row.S("kabupaten"), row.S("propinsi") }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new(row.S("nama_instansi"), alamat, row.S("kontak"), row.S("email"), row["logo"] as byte[]);
    }
}
