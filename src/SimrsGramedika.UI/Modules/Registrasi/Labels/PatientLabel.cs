using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.Shared;
using SkiaSharp;

namespace SimrsGramedika.UI.Modules.Registrasi.Labels;

internal sealed record PatientLabelFormat(string Title, string Template);

internal sealed class PatientLabel(XElement layout, Row visit, Row? hospital, PatientLabelFormat format)
{
    public static IReadOnlyList<PatientLabelFormat> Formats { get; } =
    [
        new("Label Tracker 1", "rptLabelTracker"),
        new("Label Tracker 2", "rptLabelTracker2"),
        new("Label Tracker 3", "rptLabelTracker3"),
        new("Label Tracker 4", "rptLabelTracker4"),
        new("Barcode Perawatan 1", "rptBarcodeRawat"),
        new("Barcode Perawatan 2", "rptBarcodeRawat2"),
        new("Barcode RM", "rptBarcodeRawat3"),
        new("Label Rekam Medis 10", "rptBarcodeRM18"),
        new("Gelang Pasien 1", "rptBarcodeRM6"),
        new("Gelang Pasien 2", "rptBarcodeRM7"),
        new("Gelang Pasien 3", "rptBarcodeRM8"),
        new("Gelang Pasien 4", "rptBarcodeRM10"),
        new("Gelang Pasien 5", "rptBarcodeRM14"),
        new("Gelang Pasien 6", "rptBarcodeRM16"),
        new("Gelang Pasien 7", "rptBarcodeRM19"),
    ];

    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");
    private static readonly Regex ExpressionToken = new("""\$(?<kind>[FP])\{(?<key>\w+)\}|(?:"(?<literal>[^"]*)")""");
    private static readonly XNamespace Jasper = "http://jasperreports.sourceforge.net/jasperreports";
    public float Width => Number(layout, "pageWidth");
    public float Height => Number(layout, "pageHeight");

    public static async Task<PatientLabel> LoadAsync(PatientMenuTarget patient, PatientLabelFormat format)
    {
        await using var db = await Db.OpenAsync(DatabaseSettings.Load());
        var visit = await db.FirstAsync("""
            SELECT r.no_rawat, r.no_rkm_medis, r.tgl_registrasi, r.almt_pj,
                   CONCAT_WS(' ', r.umurdaftar, r.sttsumur) AS umur,
                   p.nm_pasien, p.jk, p.tgl_lahir, p.tmp_lahir, p.nm_ibu, p.no_ktp,
                   d.nm_dokter, pl.nm_poli, payer.png_jawab, patient_payer.png_jawab AS pasien_png_jawab
            FROM reg_periksa r
            JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
            LEFT JOIN dokter d ON d.kd_dokter = r.kd_dokter
            LEFT JOIN poliklinik pl ON pl.kd_poli = r.kd_poli
            LEFT JOIN penjab payer ON payer.kd_pj = r.kd_pj
            LEFT JOIN penjab patient_payer ON patient_payer.kd_pj = p.kd_pj
            WHERE r.no_rawat = @p0 AND r.no_rkm_medis = @p1
            """, patient.NoRawat, patient.NoRekamMedis)
            ?? throw new InvalidOperationException("Data registrasi pasien sudah tidak tersedia.");
        var hospital = await db.FirstAsync("""
            SELECT nama_instansi AS namars, alamat_instansi AS alamatrs, kabupaten AS kotars,
                   propinsi AS propinsirs, kontak AS kontakrs
            FROM setting ORDER BY (aktifkan = 'Yes') DESC, nama_instansi LIMIT 1
            """);

        using var resource = typeof(PatientLabel).Assembly.GetManifestResourceStream(
            $"SimrsGramedika.UI.Modules.Registrasi.Labels.Templates.{format.Template}.jrxml")
            ?? throw new InvalidOperationException("Template cetak tidak ditemukan.");
        return new PatientLabel(XElement.Load(resource), visit, hospital, format);
    }

    public SKBitmap Render(float scale)
    {
        var bitmap = new SKBitmap((int)Math.Ceiling(Width * scale), (int)Math.Ceiling(Height * scale),
            SKColorType.Bgra8888, SKAlphaType.Opaque);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Scale(scale);
            Draw(canvas);
            canvas.Flush();
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public void Draw(SKCanvas canvas)
    {
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.Translate(Number(layout, "leftMargin"), Number(layout, "topMargin"));
        foreach (var band in layout.Elements().SelectMany(section => section.Elements(Jasper + "band")))
        {
            foreach (var element in band.Elements())
                DrawElement(canvas, element);
            canvas.Translate(0, Number(band, "height"));
        }
        canvas.Restore();
    }

    private void DrawElement(SKCanvas canvas, XElement element)
    {
        var position = element.Element(Jasper + "reportElement")!;
        var condition = position.Element(Jasper + "printWhenExpression")?.Value;
        if (condition is not null && !condition.Contains($"""equals("{visit.S("jk")}")""")) return;

        var x = Number(position, "x");
        var y = Number(position, "y");
        var width = Number(position, "width");
        var height = Number(position, "height");
        using var paint = new SKPaint { Color = Color(position, "forecolor", SKColors.Black), IsAntialias = true };
        canvas.Save();
        canvas.Translate(x, y);
        var type = element.Name.LocalName;
        switch (type)
        {
            case "line":
            case "rectangle":
                var pen = element.Element(Jasper + "graphicElement")?.Element(Jasper + "pen");
                paint.StrokeWidth = Number(pen, "lineWidth", 1);
                paint.Style = SKPaintStyle.Stroke;
                if (type == "rectangle")
                    canvas.DrawRect(0, 0, width, height, paint);
                else
                {
                    var dx = width <= 1 ? 0 : width;
                    var dy = height <= 1 ? 0 : height;
                    var bottomUp = (string?)element.Attribute("direction") == "BottomUp";
                    canvas.DrawLine(0, bottomUp ? dy : 0, dx, bottomUp ? 0 : dy, paint);
                }
                break;
            case "componentElement":
                var barcode = element.Elements().Single(child => child != position);
                var value = Evaluate(barcode.Elements().Single().Value);
                Rotate(canvas, (string?)barcode.Attribute("orientation"), ref width, ref height);
                Code128.Draw(canvas, value, width, height, (string?)barcode.Attribute("textPosition") == "bottom");
                break;
            case "textField":
            case "staticText":
                if ((string?)position.Attribute("mode") == "Opaque")
                {
                    using var background = new SKPaint { Color = Color(position, "backcolor", SKColors.White) };
                    canvas.DrawRect(0, 0, width, height, background);
                }
                var textElement = element.Element(Jasper + "textElement");
                var text = type == "staticText" ? element.Element(Jasper + "text")!.Value
                    : Evaluate(element.Element(Jasper + "textFieldExpression")!.Value, (string?)element.Attribute("pattern"));
                Rotate(canvas, (string?)textElement?.Attribute("rotation"), ref width, ref height);
                var font = textElement?.Element(Jasper + "font");
                DrawText(canvas, text, width, height, paint, Number(font, "size", 10),
                    (string?)font?.Attribute("isBold") == "true",
                    (string?)textElement?.Attribute("textAlignment"), (string?)textElement?.Attribute("verticalAlignment") == "Middle");
                break;
            default:
                throw new InvalidOperationException($"Elemen template cetak tidak didukung ({type}).");
        }
        canvas.Restore();
    }

    private string Evaluate(string expression, string? pattern = null) =>
        string.Concat(ExpressionToken.Matches(expression).Select(token =>
        {
            var key = token.Groups["key"].Value;
            return token.Groups["kind"].Value switch
            {
                "P" => key switch
                {
                    "nama" => visit.S("nm_pasien"),
                    "norm" => visit.S("no_rkm_medis"),
                    "alamat" => Date("tgl_lahir", "dd/MM/yyyy"),
                    "tanggal" => Date("tgl_registrasi", "dd-MM-yyyy"),
                    _ => hospital?.S(key) ?? "",
                },
                "F" when key == "tgl_lahir" => Date(key, !string.IsNullOrEmpty(pattern) ? pattern.Replace("MMMMM", "MMMM") :
                    format.Template switch
                    {
                        "rptBarcodeRawat3" => "dd-MM-yyyy",
                        "rptBarcodeRM10" or "rptBarcodeRM14" => "dd/MM/yyyy",
                        _ => "yyyy-MM-dd",
                    }),
                "F" when key == "tgl_registrasi" && !string.IsNullOrEmpty(pattern) => Date(key, pattern),
                "F" when key == "png_jawab" && format.Template == "rptBarcodeRM18" => visit.S("pasien_png_jawab"),
                "F" => visit.S(key),
                _ => token.Groups["literal"].Value,
            };
        }));

    private string Date(string key, string pattern) => visit.Dt(key)?.ToString(pattern, Indonesian) ?? visit.S(key);

    private static void Rotate(SKCanvas canvas, string? rotation, ref float width, ref float height)
    {
        if (string.Equals(rotation, "Left", StringComparison.OrdinalIgnoreCase))
        {
            canvas.Translate(0, height);
            canvas.RotateDegrees(-90);
            (width, height) = (height, width);
        }
        else if (rotation == "Right")
        {
            canvas.Translate(width, 0);
            canvas.RotateDegrees(90);
            (width, height) = (height, width);
        }
    }

    internal static void DrawText(SKCanvas canvas, string text, float width, float height, SKPaint paint,
        float size, bool bold, string? alignment, bool middle)
    {
        using var face = SKTypeface.FromFamilyName("Tahoma", bold ? SKFontStyle.Bold : SKFontStyle.Normal);
        using var font = new SKFont(face, size) { Subpixel = true };
        var metrics = font.Metrics;
        var lineHeight = metrics.Descent - metrics.Ascent;
        var remaining = text.AsSpan();
        var lines = new List<string>();
        // Small Jasper fields can have less than a full line of font metrics.
        var maxLines = Math.Max(1, (int)(height / lineHeight));
        while (!remaining.IsEmpty && lines.Count < maxLines)
        {
            var length = (int)font.BreakText(remaining, width, out _);
            if (length == 0) break;
            if (length < remaining.Length && lines.Count + 1 < maxLines)
            {
                var space = remaining[..length].LastIndexOf(' ');
                if (space > 0) length = space;
            }
            lines.Add(remaining[..length].ToString());
            remaining = remaining[length..].TrimStart();
        }
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, width, height));
        var baseline = (middle ? (height - lines.Count * lineHeight) / 2 : 0) - metrics.Ascent;
        foreach (var line in lines)
        {
            var (x, align) = alignment switch
            {
                "Center" => (width / 2, SKTextAlign.Center),
                "Right" => (width, SKTextAlign.Right),
                _ => (0f, SKTextAlign.Left),
            };
            canvas.DrawText(line, x, baseline, align, font, paint);
            baseline += lineHeight;
        }
        canvas.Restore();
    }

    private static float Number(XElement? node, string attribute, float fallback = 0) =>
        (float?)node?.Attribute(attribute) ?? fallback;

    private static SKColor Color(XElement node, string attribute, SKColor fallback) =>
        SKColor.TryParse((string?)node.Attribute(attribute), out var color) ? color : fallback;
}
