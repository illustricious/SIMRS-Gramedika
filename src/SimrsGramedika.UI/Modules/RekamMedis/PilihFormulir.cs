using Avalonia.Controls;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.RekamMedis;
using SimrsGramedika.UI.Controls;

namespace SimrsGramedika.UI.Modules.RekamMedis;

/// <summary>Pencarian formulir rekam medis yang boleh diisi akun ini, untuk satu kunjungan yang sudah dipilih.</summary>
public static class PilihFormulir
{
    public static bool Ada(AuthenticatedUser? user) => user is not null && FormKlinisKatalog.Semua.Any(f => user.HasPermission(f.Izin));

    /// <summary>Tiap kata harus ada pada judul atau kelompoknya; urutan mengikuti kelompok pada katalog.</summary>
    public static IReadOnlyList<FormKlinis> Cari(AuthenticatedUser user, string kata)
    {
        var potongan = kata.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return [.. FormKlinisKatalog.Semua
            .Where(f => user.HasPermission(f.Izin)
                && potongan.All(p => $"{f.Judul} {f.Kelompok}".Contains(p, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(f => f.Kelompok).SelectMany(g => g)];
    }

    public static Task<FormKlinis?> TampilkanAsync(Window owner, AuthenticatedUser user, string pasien) =>
        SearchPickerDialog.ShowAsync<FormKlinis>(owner, $"Formulir Rekam Medis · {pasien}",
            kata => Task.FromResult(Cari(user, kata)), f => $"{f.Judul}{Environment.NewLine}{f.Kelompok}");
}
