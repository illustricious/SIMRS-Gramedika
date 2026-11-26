using SimrsGramedika.Data.Farmasi;

namespace SimrsGramedika.Tests;

public class PetugasPilihanTests
{
    private static readonly List<(string Kode, string Nama)> Petugas = [("P01", "Ani"), ("P02", "Budi")];

    [Fact]
    public void RegularAccount_IsLockedToOwnPetugas()
    {
        Assert.Equal(("P02", true), PetugasPilihan.Tentukan(Petugas, false, "P02"));
    }

    [Fact]
    public void RegularAccountWithoutPetugasRecord_HasNoSelection()
    {
        Assert.Equal((null, true), PetugasPilihan.Tentukan(Petugas, false, "D0000099"));
    }

    [Fact]
    public void AdminUtama_PicksFromList()
    {
        Assert.Equal((null, false), PetugasPilihan.Tentukan(Petugas, true, "Admin Utama"));
    }
}
