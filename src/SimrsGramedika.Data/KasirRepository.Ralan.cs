using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SimrsGramedika.Data.Keuangan;

namespace SimrsGramedika.Data;

public partial class KasirRepository
{
    private const string Laborat = "Laborat";
    private const string Radiologi = "Radiologi";
    private const string Obat = "Obat";
    private const string PpnObat = "PPN Obat";
    private const string RalanDokterParamedis = "Ralan Dokter Paramedis";

    private Task<bool> AdaNotaAtauBillingRalanAsync(string noRawat, CancellationToken ct) =>
        db.Database.SqlQuery<bool>($"""
            SELECT (EXISTS(SELECT 1 FROM billing WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM nota_jalan WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM detail_nota_jalan WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM tagihan_sadewa WHERE no_nota={noRawat})) AS Value
            """).SingleAsync(ct);

    private async Task<KasirBillItem> MuatNotaRalanAsync(KasirBillItem bill, CancellationToken ct)
    {
        var nota = await db.Database.SqlQuery<string>($"""
            SELECT COALESCE(no_nota,'') AS Value FROM nota_jalan WHERE no_rawat={bill.NoRawat}
            """).SingleOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(nota)) return bill;

        var tersimpan = await db.Database.SqlQuery<KasirRincian>($"""
            SELECT status AS Jenis,nm_perawatan AS Nama,CAST(biaya AS DECIMAL(20,4)) AS Tarif,
                   CAST(jumlah AS DECIMAL(20,4)) AS Jumlah,CAST(totalbiaya AS DECIMAL(20,4)) AS Total
            FROM billing WHERE no_rawat={bill.NoRawat} AND status IS NOT NULL
                AND status<>'-' AND status NOT LIKE 'Ttl%'
            ORDER BY noindex
            """).ToListAsync(ct);
        var rincian = tersimpan.Where(r => r.Jenis != "Tagihan" && r.Jumlah != 0).ToList();
        var total = tersimpan.Where(r => r.Jenis == "Tagihan").ToArray();
        if (rincian.Count == 0 || total.Length != 1 || rincian.Sum(r => r.Total) != total[0].Total)
            return bill with { NoNota = nota };

        return bill with
        {
            NoNota = nota, RincianTersimpan = true, Rincian = rincian,
            BiayaReg = rincian.Where(r => r.Jenis == Registrasi).Sum(r => r.Total), Total = total[0].Total
        };
    }

    private async Task<KasirBillItem> LengkapiTagihanRalanAsync(KasirBillItem bill, CancellationToken ct)
    {
        var noRawat = bill.NoRawat;
        var rows = bill.Rincian.ToList();
        rows.AddRange(await MuatRincianTindakanObatAsync(noRawat, false, ct));
        rows.AddRange(await MuatPenunjangAsync(noRawat, false, ct));

        var obat = rows.Where(r => r.Jenis == Obat).Sum(r => r.Total);
        if (obat > 0 && await db.Database.SqlQuery<bool>($"""
            SELECT (tampilkan_ppnobat_ralan='Yes') IS TRUE AS Value FROM set_nota LIMIT 1
            """).FirstOrDefaultAsync(ct))
        {
            // Java billing rounds 11% VAT to whole rupiah when this legacy setting is enabled.
            var ppn = decimal.Round(obat * 0.11m, 0, MidpointRounding.AwayFromZero);
            if (ppn > 0) rows.Add(new(Obat, PpnObat, ppn, 1, ppn));
        }

        var pending = await AdaPermintaanTertundaAsync(noRawat, false, ct);

        var unsupported = await db.Database.SqlQuery<bool>($"""
            SELECT (
                EXISTS(SELECT 1 FROM deposit WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM piutang_pasien WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_inap_dr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_inap_pr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM rawat_inap_drpr WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM operasi WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM beri_obat_operasi WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM tagihan_obat_langsung WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM returpasien WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM tambahan_biaya WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM pengurangan_biaya WHERE no_rawat={noRawat}) OR
                EXISTS(SELECT 1 FROM temporary_tambahan_potongan WHERE no_rawat={noRawat})
            ) AS Value
            """).SingleAsync(ct);
        var alasan = unsupported || rows.Any(r => r.Total < 0) ? BayarDiKhanza
            : pending ? "Resep atau permintaan penunjang masih menunggu proses. Selesaikan atau batalkan permintaan sebelum pelunasan."
            : null;
        return bill with { Rincian = rows, Total = rows.Sum(r => r.Total), AlasanBelumBisaBayar = alasan };
    }

    private async Task<List<JurnalLine>> BuatJurnalRalanAsync(
        KasirBillItem bill, string rekening, string akunBayar, MySqlConnection conn, MySqlTransaction tx, CancellationToken ct)
    {
        var akun = await JurnalPoster.ReadAkunAsync(conn, tx, "set_akun_ralan", ct);
        var lines = new List<JurnalLine> { new(rekening, akunBayar, (double)bill.Total, 0) };
        TambahKredit(lines, akun.GetValueOrDefault("Registrasi_Ralan", ""), Registrasi, bill.BiayaReg);
        var ppn = bill.Rincian.Where(r => r.Jenis == Obat && r.Nama == PpnObat).Sum(r => r.Total);
        if (ppn > 0)
        {
            var akunUmum = await JurnalPoster.ReadAkunAsync(conn, tx, "set_akun", ct);
            TambahKredit(lines, akunUmum.GetValueOrDefault("PPN_Keluaran", ""), PpnObat, ppn);
        }

        var layanan = new (string Nama, decimal Biaya)[]
        {
            ("Tindakan", bill.Rincian.Where(r => r.Jenis is "Ralan Dokter" or "Ralan Paramedis" or RalanDokterParamedis).Sum(r => r.Total)),
            (Laborat, bill.Rincian.Where(r => r.Jenis == Laborat).Sum(r => r.Total)),
            (Radiologi, bill.Rincian.Where(r => r.Jenis == Radiologi).Sum(r => r.Total)),
            (Obat, bill.Rincian.Where(r => r.Jenis == Obat).Sum(r => r.Total) - ppn)
        };
        foreach (var group in layanan.Where(x => x.Biaya > 0)
                     .GroupBy(x => akun.GetValueOrDefault($"Suspen_Piutang_{x.Nama}_Ralan", ""), StringComparer.OrdinalIgnoreCase))
        {
            var suspen = group.Key;
            // Already-posted service revenue must not be credited a second time.
            var saldo = string.IsNullOrWhiteSpace(suspen) ? 0 : await db.Database.SqlQuery<decimal>($"""
                SELECT CAST(COALESCE(SUM(d.debet-d.kredit),0) AS DECIMAL(20,4)) AS Value
                FROM jurnal j JOIN detailjurnal d ON d.no_jurnal=j.no_jurnal
                WHERE j.no_bukti={bill.NoRawat} AND d.kd_rek={suspen}
                """).SingleAsync(ct);
            if (saldo < 0 || saldo > group.Sum(x => x.Biaya) || (saldo > 0 && group.Count() > 1 && saldo != group.Sum(x => x.Biaya)))
                throw new InvalidOperationException("Saldo suspen layanan tidak cocok dengan rincian tagihan. Rekonsiliasi di Keuangan sebelum melunasi.");
            TambahKredit(lines, suspen, "Pelunasan Suspen Rawat Jalan", saldo);
            foreach (var item in group)
            {
                var belumDijurnal = group.Count() == 1 ? item.Biaya - saldo : saldo == 0 ? item.Biaya : 0;
                TambahKredit(lines, akun.GetValueOrDefault($"{item.Nama}_Ralan", ""), item.Nama, belumDijurnal);
            }
        }
        return lines;
    }
}
