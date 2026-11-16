using System.Diagnostics;
using Avalonia.Controls;
using SimrsKhanza.Config;
using SimrsKhanza.UI.Modules.Shared;

namespace SimrsKhanza.UI.Modules.Kepegawaian;

/// <summary>
/// Legacy DlgPenggajian: "Kepegawaian &amp; Gaji" is the PHP web app (webapps/penggajian) shown in an
/// embedded browser. Here it opens in the system browser; the user signs in there (the old app put
/// the web password in the URL, which we deliberately don't do).
/// </summary>
public class PenggajianView : PageView
{
    private readonly TextBox _url = Ui.Text();

    public PenggajianView()
    {
        _url.Text = DatabaseSettings.Load().WebBase + "/penggajian/";
        _url.MinWidth = 420;
        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Ui.Hint("Modul penggajian SIMRS Khanza berupa aplikasi web (folder webapps/penggajian di server web Khanza). Alamat dasarnya diatur di Koneksi Database → Aplikasi Web Khanza."),
                Ui.Bar(Ui.L("Alamat"), _url, Ui.Btn("🌐 Buka di Browser", (_, _) => Open())),
            },
        };
        Build("Kepegawaian & Gaji", body);
    }

    private void Open()
    {
        var url = (_url.Text ?? "").Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
        {
            Ui.Err(StatusText, "Alamat harus diawali http:// atau https://");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
            Ui.Ok(StatusText, "Dibuka di browser.");
        }
        catch (Exception ex)
        {
            Ui.Err(StatusText, "Gagal membuka browser: " + ex.Message);
        }
    }

    public override Task ReloadAsync() => Task.CompletedTask;
}
