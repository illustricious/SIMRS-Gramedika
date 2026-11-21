using System.Globalization;
using SimrsKhanza.Data.Parkir;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Parkir;

public sealed class JenisParkirView() : MasterView(MenuHSpecs.Masters[MenuHSpecs.JenisParkirKey])
{
    protected override double ParseNumber(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tarif)
            && double.IsFinite(tarif))
            return tarif;
        throw new FormatException("Isi tarif dalam angka tanpa pemisah ribuan, misalnya 15000.");
    }
}
