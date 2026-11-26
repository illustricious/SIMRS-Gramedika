using SkiaSharp;

namespace SimrsGramedika.UI.Modules.Registrasi.Labels;

internal static class Code128
{
    // Module widths for symbols 0–106, in Code 128 symbol order.
    private static readonly string[] Patterns = (
        "212222 222122 222221 121223 121322 131222 122213 122312 132212 221213 " +
        "221312 231212 112232 122132 122231 113222 123122 123221 223211 221132 " +
        "221231 213212 223112 312131 311222 321122 321221 312212 322112 322211 " +
        "212123 212321 232121 111323 131123 131321 112313 132113 132311 211313 " +
        "231113 231311 112133 112331 132131 113123 113321 133121 313121 211331 " +
        "231131 213113 213311 213131 311123 311321 331121 312113 312311 332111 " +
        "314111 221411 431111 111224 111422 121124 121421 141122 141221 112214 " +
        "112412 122114 122411 142112 142211 241211 221114 413111 241112 134111 " +
        "111242 121142 121241 114212 124112 124211 411212 421112 421211 212141 " +
        "214121 412121 111143 111341 131141 114113 114311 411113 411311 113141 " +
        "114131 311141 411131 211412 211214 211232 2331112").Split(' ');

    public static void Draw(SKCanvas canvas, string value, float width, float height, bool caption)
    {
        if (value.Length == 0 || value.Any(c => c < ' ' || c > '~'))
            throw new InvalidOperationException("Nomor untuk barcode Code 128 tidak valid.");

        var symbols = Encode(value);
        const int quietZone = 10;
        var modules = 2 * quietZone + symbols.Sum(symbol => Patterns[symbol].Sum(c => c - '0'));
        var moduleWidth = Math.Min(1, width / modules);
        var x = (width - modules * moduleWidth) / 2 + quietZone * moduleWidth;
        var barHeight = caption ? height - 10 : height;
        using var paint = new SKPaint { Color = SKColors.Black };
        foreach (var symbol in symbols)
        {
            var pattern = Patterns[symbol];
            for (var i = 0; i < pattern.Length; i++)
            {
                var segment = (pattern[i] - '0') * moduleWidth;
                if (i % 2 == 0) canvas.DrawRect(x, 0, segment, barHeight, paint);
                x += segment;
            }
        }
        if (caption)
        {
            canvas.Translate(0, barHeight);
            paint.IsAntialias = true;
            PatientLabel.DrawText(canvas, value, width, 10, paint, 8, false, "Center", true);
        }
    }

    private static List<int> Encode(string value)
    {
        // Set C packs pairs of digits. Set B covers separators and other printable characters.
        var numeric = DigitRun(value, 0) >= 4;
        var symbols = new List<int> { numeric ? 105 : 104 };
        for (var i = 0; i < value.Length;)
        {
            var digits = DigitRun(value, i);
            if (numeric && digits < 2)
            {
                symbols.Add(100);
                numeric = false;
            }
            if (!numeric && digits >= 4 && digits % 2 == 0)
            {
                symbols.Add(99);
                numeric = true;
            }
            if (numeric)
            {
                symbols.Add((value[i] - '0') * 10 + value[i + 1] - '0');
                i += 2;
            }
            else
            {
                symbols.Add(value[i++] - ' ');
            }
        }
        var checksum = symbols[0];
        for (var i = 1; i < symbols.Count; i++) checksum += symbols[i] * i;
        symbols.Add(checksum % 103);
        symbols.Add(106);
        return symbols;
    }

    private static int DigitRun(string value, int start)
    {
        var end = start;
        while (end < value.Length && char.IsAsciiDigit(value[end])) end++;
        return end - start;
    }
}
