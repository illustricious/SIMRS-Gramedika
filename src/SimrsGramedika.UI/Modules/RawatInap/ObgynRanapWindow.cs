using Avalonia.Controls;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.RawatJalan;
using SimrsGramedika.UI.Modules.Shared;

namespace SimrsGramedika.UI.Modules.RawatInap;

internal sealed class ObgynRanapWindow : Window
{
    public ObgynRanapWindow(RawatInapListItem visit, AuthenticatedUser user)
    {
        Title = "Obstetri / Ginekologi Rawat Inap";
        Width = 860;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var identitas = Ui.Hint($"{visit.NamaPasien} · No. RM {visit.NoRekamMedis} · No. Rawat {visit.NoRawat}");
        identitas.Margin = new Avalonia.Thickness(14, 10, 14, 0);
        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "Obstetri", Content = new ObgynRalanView(visit.NoRawat, user, obstetri: true, ranap: true) },
                new TabItem { Header = "Ginekologi", Content = new ObgynRalanView(visit.NoRawat, user, obstetri: false, ranap: true) },
            },
        };
        var layout = new Grid { RowDefinitions = new("Auto,*") };
        Grid.SetRow(tabs, 1);
        layout.Children.Add(identitas);
        layout.Children.Add(tabs);
        Content = layout;
    }
}
