using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimrsGramedika.Data;
using SimrsGramedika.UI.Modules.Registrasi;

namespace SimrsGramedika.UI.Modules.RawatJalan;

/// <summary>Resume medis rawat jalan dengan isi rptLaporanResume Khanza, termasuk QR identitas dokter.</summary>
public static class ResumeRalanReport
{
    public static string GeneratePdf(ResumeRalanCetak resume, KopInstansi kop)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimrsGramedika_Resume_{Guid.NewGuid():N}.pdf");
        Dokumen(resume, kop).GeneratePdf(path);
        return path;
    }

    internal static Document Dokumen(ResumeRalanCetak resume, KopInstansi kop)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        static string Tanggal(DateTime? t) => t?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";
        var kunjungan = Tanggal(resume.TanggalRegistrasi);
        var qr = KodeQr.Png(TandaTanganSurat.Teks(kop.Nama, kop.Kota, resume.Dokter, resume.IdDokter, kunjungan));

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Element(c => RegistrasiReport.Kop(c, kop));
            page.Footer().AlignRight().Text(t =>
            {
                t.Span($"{resume.NoRawat} · halaman ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Item().PaddingBottom(6).AlignCenter().Text("RESUME MEDIS PASIEN").FontSize(12).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(85);
                        c.RelativeColumn(3);
                        c.ConstantColumn(85);
                        c.RelativeColumn(2);
                    });
                    foreach (var isi in new[]
                    {
                        "Nama Pasien", resume.Pasien, "No. Rekam Medis", resume.NoRekamMedis,
                        "Umur", resume.Umur, "Tgl Lahir", Tanggal(resume.TanggalLahir),
                        "Jenis Kelamin", resume.JenisKelamin, "Tanggal Masuk", kunjungan,
                        "Pekerjaan", resume.Pekerjaan, "Tanggal Keluar", kunjungan,
                        "Ruang", resume.Ruang, "No. Rawat", resume.NoRawat,
                        "Alamat", resume.Alamat, "", "",
                    })
                        table.Cell().PaddingVertical(1.5f).PaddingRight(4).Text(isi);
                });

                void Bagian(string judul, string isi)
                {
                    col.Item().PaddingTop(8).Text(judul).Bold();
                    col.Item().PaddingTop(2).Text(isi.Length == 0 ? "-" : isi);
                }

                Bagian("Keluhan utama dari riwayat penyakit yang positif :", resume.Isi.Keluhan);
                Bagian("Jalannya penyakit selama perawatan :", resume.Isi.Perjalanan);
                Bagian("Pemeriksaan penunjang yang positif :", resume.Isi.Penunjang);
                Bagian("Hasil laboratorium yang positif :", resume.Isi.Laborat);
                string Kode(bool prosedur) => string.Join("\n", resume.Kode.Where(k => k.Prosedur == prosedur).Select(k =>
                    $"{(k.Prioritas == 1 ? "Utama" : $"Sekunder {k.Prioritas - 1}")} : {k.Nama}{(k.Kode.Length == 0 ? "" : $" ({k.Kode})")}"));
                Bagian("Diagnosa Akhir :", Kode(false));
                Bagian("Prosedur/Tindakan :", Kode(true));
                Bagian("Kondisi pasien pulang :", resume.Isi.Kondisi);
                Bagian("Obat-obatan waktu pulang/nasihat :", resume.Isi.Obat);

                col.Item().PaddingTop(18).ShowEntire().AlignRight().Width(190).Column(ttd =>
                {
                    ttd.Item().AlignCenter().Text("Dokter Penanggung Jawab");
                    ttd.Item().PaddingVertical(4).AlignCenter().Width(66).Image(qr);
                    ttd.Item().AlignCenter().Text($"( {resume.Dokter} )");
                });
            });
        }));
    }
}
