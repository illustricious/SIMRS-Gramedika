using System.Globalization;
using SimrsGramedika.Data.Parkir;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.Parkir;

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
