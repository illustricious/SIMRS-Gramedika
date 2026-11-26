using System.Runtime.InteropServices;

namespace SimrsGramedika.UI.Modules.Registrasi.Labels;

internal static class WindowsLabelPrinting
{
    public static void Print(nint owner, PatientLabel label, string title)
    {
        const uint ReturnDc = 0x100, NoSelection = 0x4, NoPageNumbers = 0x8,
            DriverCopies = 0x40000, NoCurrentPage = 0x800000, NoPrintToFile = 0x80000;
        const uint StartPageGeneral = uint.MaxValue, ResultPrint = 1;
        var dialog = new PrintDialog
        {
            Size = (uint)Marshal.SizeOf<PrintDialog>(), Owner = owner,
            Flags = ReturnDc | NoSelection | NoPageNumbers | NoCurrentPage | DriverCopies | NoPrintToFile,
            Copies = 1, StartPage = StartPageGeneral,
        };

        try
        {
            var result = PrintDlgExW(ref dialog);
            if (result < 0)
                throw new InvalidOperationException($"Dialog printer Windows gagal dibuka (0x{result:X8}).");
            if (dialog.ResultAction != ResultPrint) return;
            if (dialog.DeviceContext == 0)
                throw new InvalidOperationException("Windows tidak mengembalikan printer yang dipilih.");
            SendPage(dialog.DeviceContext, label, title);
        }
        finally
        {
            if (dialog.DeviceContext != 0) DeleteDC(dialog.DeviceContext);
            if (dialog.DeviceMode != 0) GlobalFree(dialog.DeviceMode);
            if (dialog.DeviceNames != 0) GlobalFree(dialog.DeviceNames);
        }
    }

    private static void SendPage(nint dc, PatientLabel label, string title)
    {
        const int DpiX = 88, DpiY = 90, PaperWidth = 110, PaperHeight = 111, OffsetX = 112, OffsetY = 113;
        const int HorizontalResolution = 8, VerticalResolution = 10;
        var width = (int)Math.Round(label.Width * GetDeviceCaps(dc, DpiX) / 72.0);
        var height = (int)Math.Round(label.Height * GetDeviceCaps(dc, DpiY) / 72.0);
        // A tolerance of 1 mm accommodates rounding of custom media sizes by printer drivers.
        var toleranceX = GetDeviceCaps(dc, DpiX) / 25.4;
        var toleranceY = GetDeviceCaps(dc, DpiY) / 25.4;
        if (width <= 0 || height <= 0 ||
            width > GetDeviceCaps(dc, PaperWidth) + toleranceX ||
            height > GetDeviceCaps(dc, PaperHeight) + toleranceY)
            throw new InvalidOperationException("Sesuaikan ukuran dan orientasi kertas di pengaturan printer.");

        var printableWidth = GetDeviceCaps(dc, HorizontalResolution);
        var printableHeight = GetDeviceCaps(dc, VerticalResolution);
        // On larger paper use the printable origin. On matching stock preserve the physical offsets.
        var x = width <= printableWidth ? 0 : -GetDeviceCaps(dc, OffsetX);
        var y = height <= printableHeight ? 0 : -GetDeviceCaps(dc, OffsetY);
        using var bitmap = label.Render(600f / 72);
        var header = new BitmapInfo
        {
            Size = 40, Width = bitmap.Width, Height = -bitmap.Height, Planes = 1, BitsPerPixel = 32,
        };
        var document = new DocumentInfo { Size = Marshal.SizeOf<DocumentInfo>(), Name = title };
        Check(StartDocW(dc, ref document));
        try
        {
            Check(StartPage(dc));
            SetStretchBltMode(dc, 4); // HALFTONE
            SetBrushOrgEx(dc, 0, 0, 0);
            Check(StretchDIBits(dc, x, y, width, height, 0, 0, bitmap.Width, bitmap.Height,
                bitmap.GetPixels(), ref header, 0, 0x00CC0020)); // SRCCOPY
            Check(EndPage(dc));
            Check(EndDoc(dc));
        }
        catch
        {
            AbortDoc(dc);
            throw;
        }
    }

    private static void Check(int result)
    {
        if (result <= 0) throw new InvalidOperationException("Printer tidak dapat menerima dokumen.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PrintDialog
    {
        public uint Size;
        public nint Owner, DeviceMode, DeviceNames, DeviceContext;
        public uint Flags, Flags2, ExclusionFlags, PageRangeCount, MaxPageRangeCount;
        public nint PageRanges;
        public uint MinPage, MaxPage, Copies;
        public nint Instance, TemplateName, Callback;
        public uint PropertyPageCount;
        public nint PropertyPages;
        public uint StartPage, ResultAction;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DocumentInfo
    {
        public int Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Name;
        public nint Output, DataType;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitsPerPixel;
        public uint Compression, ImageSize;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ColorsUsed, ColorsImportant;
    }

    [DllImport("comdlg32.dll", ExactSpelling = true)]
    private static extern int PrintDlgExW(ref PrintDialog dialog);
    [DllImport("kernel32.dll")]
    private static extern nint GlobalFree(nint memory);
    [DllImport("gdi32.dll")]
    private static extern int DeleteDC(nint dc);
    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(nint dc, int index);
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern int StartDocW(nint dc, ref DocumentInfo info);
    [DllImport("gdi32.dll")]
    private static extern int StartPage(nint dc);
    [DllImport("gdi32.dll")]
    private static extern int EndPage(nint dc);
    [DllImport("gdi32.dll")]
    private static extern int EndDoc(nint dc);
    [DllImport("gdi32.dll")]
    private static extern int AbortDoc(nint dc);
    [DllImport("gdi32.dll")]
    private static extern int SetStretchBltMode(nint dc, int mode);
    [DllImport("gdi32.dll")]
    private static extern int SetBrushOrgEx(nint dc, int x, int y, nint previous);
    [DllImport("gdi32.dll")]
    private static extern int StretchDIBits(nint dc, int x, int y, int width, int height,
        int sourceX, int sourceY, int sourceWidth, int sourceHeight, nint pixels,
        ref BitmapInfo info, uint usage, uint rasterOperation);
}
