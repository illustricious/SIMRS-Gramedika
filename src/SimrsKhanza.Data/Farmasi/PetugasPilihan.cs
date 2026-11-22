namespace SimrsKhanza.Data.Farmasi;

/// <summary>
/// Petugas pencatat transaksi farmasi (kolom nip berelasi ke petugas.nip). Akun biasa terkunci pada NIP-nya sendiri
/// (akses.getkode()), Admin Utama memilih dari daftar petugas aktif.
/// </summary>
public static class PetugasPilihan
{
    public static (string? Kode, bool Terkunci) Tentukan(IReadOnlyList<(string Kode, string Nama)> petugas, bool isAdminUtama, string userId)
    {
        if (isAdminUtama) return (null, false);
        var own = petugas.FirstOrDefault(p => p.Kode == userId);
        return (own.Kode, true);
    }
}
