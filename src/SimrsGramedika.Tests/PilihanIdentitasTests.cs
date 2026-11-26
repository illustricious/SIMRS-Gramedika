using SimrsGramedika.Core.Entities;
using SimrsGramedika.UI.Modules.Pasien;

namespace SimrsGramedika.Tests;

public class PilihanIdentitasTests
{
    [Fact]
    public void SetiapNilaiEnumPunyaSatuLabel()
    {
        static void Lengkap<T>(IReadOnlyList<Opsi<T>> daftar) where T : struct, Enum
        {
            Assert.Equal(Enum.GetValues<T>().Order(), daftar.Select(o => o.Nilai).Order());
            Assert.All(daftar, o => Assert.Equal(o.Label, o.ToString()));
        }

        Lengkap<GolonganDarah>(PilihanIdentitas.GolDarah);
        Lengkap<StatusPernikahan>(PilihanIdentitas.StatusNikah);
        Lengkap<HubunganPenanggungJawab>(PilihanIdentitas.Hubungan);
        // Lebar kolom pasien.agama.
        Assert.All(PilihanIdentitas.Agama, a => Assert.True(a.Length <= 12));
    }
}
