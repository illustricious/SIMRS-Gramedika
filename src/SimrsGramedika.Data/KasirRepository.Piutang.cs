using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Data.Keuangan;

namespace SimrsGramedika.Data;

/// <summary>Tagihan disimpan sebagai piutang: uang muka dibayar sekarang, sisanya dicatat pada satu akun piutang.</summary>
public record KasirPiutang(string AkunPiutang, decimal UangMuka, DateOnly JatuhTempo);

public record KasirPiutangRincian(string AkunPiutang, decimal Jumlah, decimal Sisa, DateTime? JatuhTempo);

public partial class KasirRepository
{
    public async Task<IReadOnlyList<string>> AkunPiutangAsync(CancellationToken ct = default) =>
        await db.Database.SqlQuery<string>($"""
            SELECT nama_bayar AS Value FROM akun_piutang WHERE COALESCE(kd_rek,'')<>'' ORDER BY nama_bayar
            """).ToListAsync(ct);

    private sealed class RekeningPiutang
    {
        public string KodeRekening { get; set; } = "";
        public string? KodePenjamin { get; set; }
    }

    /// <summary>Sisi debit jurnal pelunasan beserta pembagian uang yang diterima sekarang dan piutang.</summary>
    private sealed record Penerimaan(decimal Tunai, decimal Piutang, string? KodePenjamin, List<JurnalLine> Debit);

    /// <summary>Seperti Khanza, uang muka ditambah piutang harus sama persis dengan total tagihan.</summary>
    private async Task<Penerimaan> SiapkanPenerimaanAsync(
        decimal total, string akunBayar, KasirPiutang? piutang, DateOnly hariIni, CancellationToken ct)
    {
        var tunai = total;
        if (piutang is not null)
        {
            if (total <= 0) throw new InvalidOperationException("Tidak ada sisa tagihan untuk dicatat sebagai piutang.");
            if (piutang.UangMuka < 0 || piutang.UangMuka >= total || piutang.UangMuka != Bulatkan(piutang.UangMuka))
                throw new ArgumentException("Uang muka harus 0 atau lebih, kurang dari total tagihan, dan paling banyak empat desimal.");
            if (piutang.JatuhTempo < hariIni) throw new ArgumentException("Jatuh tempo tidak boleh sebelum hari ini.");
            tunai = piutang.UangMuka;
        }

        var debit = new List<JurnalLine>();
        if (tunai > 0)
        {
            if (string.IsNullOrWhiteSpace(akunBayar)) throw new ArgumentException("Pilih akun bayar.");
            var rekening = await db.Database.SqlQuery<string>($"""
                SELECT kd_rek AS Value FROM akun_bayar
                WHERE nama_bayar={akunBayar} AND COALESCE(ppn,0)=0 AND COALESCE(kd_rek,'')<>''
                """).SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Akun bayar belum valid atau memiliki biaya admin.");
            debit.Add(new(rekening, akunBayar, (double)tunai, 0));
        }
        if (piutang is null) return new(tunai, 0, null, debit);

        var akun = await db.Database.SqlQuery<RekeningPiutang>($"""
            SELECT kd_rek AS KodeRekening,kd_pj AS KodePenjamin FROM akun_piutang
            WHERE nama_bayar={piutang.AkunPiutang} AND COALESCE(kd_rek,'')<>''
            """).SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Akun piutang tidak valid.");
        debit.Add(new(akun.KodeRekening, piutang.AkunPiutang, (double)(total - tunai), 0));
        return new(tunai, total - tunai, akun.KodePenjamin, debit);
    }

    /// <summary>Menulis piutang_pasien dan detailnya dalam transaksi pelunasan yang sedang berjalan.</summary>
    private async Task SimpanPiutangAsync(
        string noRawat, string noRekamMedis, decimal total, Penerimaan terima, KasirPiutang piutang, DateOnly tanggal, CancellationToken ct,
        decimal deposit = 0)
    {
        // Khanza mengisi piutang_pasien.tgltempo dengan tanggal nota; jatuh tempo pilihan ada di detail.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO piutang_pasien(no_rawat,tgl_piutang,no_rkm_medis,status,totalpiutang,uangmuka,sisapiutang,tgltempo)
            VALUES ({noRawat},{tanggal},{noRekamMedis},'Belum Lunas',{total},{terima.Tunai + deposit},{terima.Piutang},{tanggal})
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO detail_piutang_pasien(no_rawat,nama_bayar,kd_pj,totalpiutang,sisapiutang,tgltempo)
            VALUES ({noRawat},{piutang.AkunPiutang},{terima.KodePenjamin},{terima.Piutang},{terima.Piutang},{piutang.JatuhTempo})
            """, ct);
    }
}
