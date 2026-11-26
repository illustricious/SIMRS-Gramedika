using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SimrsGramedika.Data.Keuangan;

namespace SimrsGramedika.Data;

public partial class KasirRepository
{
    private const string ResepPulang = "Resep Pulang";

    private async Task<KasirBillItem> LengkapiTagihanRanapAsync(KasirBillItem bill, CancellationToken ct)
    {
        var rawat = bill.NoRawat;
        var rows = bill.Rincian.ToList();
        rows.AddRange(await MuatRincianTindakanObatAsync(rawat, true, ct));
        rows.AddRange(await MuatPenunjangAsync(rawat, true, ct));
        rows.AddRange(await db.Database.SqlQuery<KasirRincian>($"""
            SELECT 'Resep Pulang' AS Jenis,b.nama_brng AS Nama,CAST(r.harga AS DECIMAL(20,4)) AS Tarif,
                   CAST(SUM(r.jml_barang) AS DECIMAL(20,4)) AS Jumlah,CAST(SUM(r.total) AS DECIMAL(20,4)) AS Total
            FROM resep_pulang r JOIN databarang b ON b.kode_brng=r.kode_brng
            WHERE r.no_rawat={rawat}
            GROUP BY b.kode_brng,b.nama_brng,r.harga
            ORDER BY b.nama_brng,r.harga,b.kode_brng
            """).ToListAsync(ct));

        var obat = rows.Where(r => r.Jenis == Obat).Sum(r => r.Total);
        if (obat > 0 && await db.Database.SqlQuery<bool>($"""
            SELECT (tampilkan_ppnobat_ranap='Yes') IS TRUE AS Value FROM set_nota LIMIT 1
            """).FirstOrDefaultAsync(ct))
        {
            var ppn = decimal.Round(obat * 0.11m, 0, MidpointRounding.AwayFromZero);
            if (ppn > 0) rows.Add(new(Obat, PpnObat, ppn, 1, ppn));
        }

        var alasan = bill.AlasanBelumBisaBayar ?? await CekKomponenLainAsync(rawat, ct);
        if (alasan is null && (await AdaPermintaanTertundaAsync(rawat, true, ct) || await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS(SELECT 1 FROM permintaan_resep_pulang WHERE no_rawat={rawat} AND status='Belum') AS Value
            """).SingleAsync(ct)))
            alasan = "Masih ada permintaan penunjang atau resep yang menunggu. Selesaikan atau batalkan sebelum pelunasan.";

        return bill with { Rincian = rows, Total = rows.Sum(r => r.Total), AlasanBelumBisaBayar = alasan };
    }

    private async Task TambahJurnalLayananRanapAsync(KasirBillItem bill, List<JurnalLine> lines,
        Dictionary<string, string> akun, MySqlConnection conn, MySqlTransaction tx, CancellationToken ct)
    {
        var ppn = bill.Rincian.Where(r => r.Jenis == Obat && r.Nama == PpnObat).Sum(r => r.Total);
        if (ppn > 0)
        {
            var akunUmum = await JurnalPoster.ReadAkunAsync(conn, tx, "set_akun", ct);
            TambahKredit(lines, akunUmum.GetValueOrDefault("PPN_Keluaran", ""), PpnObat, ppn);
        }

        TambahKredit(lines, akun.GetValueOrDefault("Resep_Pulang_Ranap", ""), ResepPulang,
            bill.Rincian.Where(r => r.Jenis == ResepPulang).Sum(r => r.Total));

        var akunRalan = await JurnalPoster.ReadAkunAsync(conn, tx, "set_akun_ralan", ct);
        var layanan = new (string Nama, decimal Biaya)[]
        {
            ("Tindakan", bill.Rincian.Where(r => r.Jenis is "Ranap Dokter" or "Ranap Paramedis" or "Ranap Dokter Paramedis"
                or "Ralan Dokter" or "Ralan Paramedis" or RalanDokterParamedis).Sum(r => r.Total)),
            (Laborat, bill.Rincian.Where(r => r.Jenis == Laborat).Sum(r => r.Total)),
            (Radiologi, bill.Rincian.Where(r => r.Jenis == Radiologi).Sum(r => r.Total)),
            (Obat, bill.Rincian.Where(r => r.Jenis == Obat).Sum(r => r.Total) - ppn)
        };
        var suspenLayanan = layanan.SelectMany(item => new[]
        {
            (item.Nama, Rekening: akun.GetValueOrDefault($"Suspen_Piutang_{item.Nama}_Ranap", "")),
            (item.Nama, Rekening: akunRalan.GetValueOrDefault($"Suspen_Piutang_{item.Nama}_Ralan", ""))
        }).Where(x => !string.IsNullOrWhiteSpace(x.Rekening))
            .GroupBy(x => x.Rekening, StringComparer.OrdinalIgnoreCase);
        var saldoLayanan = new Dictionary<string, decimal>();
        foreach (var rekening in suspenLayanan)
        {
            var saldo = await db.Database.SqlQuery<decimal>($"""
                SELECT CAST(COALESCE(SUM(d.debet-d.kredit),0) AS DECIMAL(20,4)) AS Value
                FROM jurnal j JOIN detailjurnal d ON d.no_jurnal=j.no_jurnal
                WHERE j.no_bukti={bill.NoRawat} AND d.kd_rek={rekening.Key}
                """).SingleAsync(ct);
            if (saldo == 0) continue;

            var jenis = rekening.Select(x => x.Nama).Distinct().ToArray();
            if (saldo < 0 || jenis.Length != 1)
                throw new InvalidOperationException("Saldo suspen Rawat Inap ambigu. Rekonsiliasi di Keuangan sebelum melunasi.");
            saldoLayanan[jenis[0]] = saldoLayanan.GetValueOrDefault(jenis[0]) + saldo;
            TambahKredit(lines, rekening.Key, "Pelunasan Suspen Rawat Inap", saldo);
        }

        foreach (var item in layanan)
        {
            var belumDijurnal = item.Biaya - saldoLayanan.GetValueOrDefault(item.Nama);
            if (belumDijurnal < 0)
                throw new InvalidOperationException($"Saldo suspen {item.Nama} melebihi tagihan. Rekonsiliasi di Keuangan sebelum melunasi.");
            TambahKredit(lines, akun.GetValueOrDefault($"{item.Nama}_Ranap", ""), item.Nama, belumDijurnal);
        }
    }
}
