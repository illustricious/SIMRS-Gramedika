using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;

namespace SimrsGramedika.Data;

/// <summary>Akibat membatalkan satu nota menurut data tersimpan: dipakai untuk konfirmasi, lalu dicocokkan lagi saat disimpan.</summary>
/// <param name="Tunai">Uang yang tercatat diterima saat nota disimpan.</param>
/// <param name="NoJurnal">Jurnal pelunasan yang akan dibalik; null bila nota tidak bernilai uang.</param>
public sealed record RencanaBatalNota(string NoRawat, string NoRekamMedis, string NamaPasien, bool Ranap, string NoNota,
    bool Piutang, decimal Tunai, decimal JumlahPiutang, decimal Deposit, decimal PengembalianDeposit, string? NoJurnal);

/// <summary>Batal nota pelunasan atau piutang (menu "Hapus Nota Salah" pada Billing Khanza).</summary>
public partial class KasirRepository
{
    public const string IzinBatalNota = "hapus_nota_salah";

    public static bool BolehBatalkanNota(AuthenticatedUser user, bool ranap) =>
        user.HasPermission(IzinBatalNota) && (ranap ? user.HasPermission("billing_ranap")
            : user.HasPermission("kasir_ralan") || user.HasPermission("billing_ralan"));

    private sealed class NotaDibatalkan
    {
        public string NoNota { get; set; } = "";
        public decimal Deposit { get; set; }
    }

    private sealed class PiutangTersimpan
    {
        public string Status { get; set; } = "";
    }

    private sealed class JurnalNota
    {
        public string NoJurnal { get; set; } = "";
        public string Keterangan { get; set; } = "";
    }

    private sealed class BarisJurnal
    {
        public string KodeRekening { get; set; } = "";
        public double Debet { get; set; }
        public double Kredit { get; set; }
    }

    /// <summary>Memeriksa apakah nota kunjungan ini boleh dibatalkan. Tidak mengubah data.</summary>
    public async Task<RencanaBatalNota> RencanaBatalNotaAsync(string noRawat, AuthenticatedUser user, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        return (await SusunBatalNotaAsync(noRawat, user, ct)).Rencana;
    }

    /// <summary>
    /// Menghapus nota, rincian billing, penerimaan dan piutangnya, membalik jurnal pelunasan, lalu membuka lagi tagihannya.
    /// <paramref name="dilihat"/> adalah rencana yang sudah dikonfirmasi pengguna.
    /// </summary>
    public async Task BatalkanNotaAsync(RencanaBatalNota dilihat, AuthenticatedUser user, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (rencana, bayi) = await SusunBatalNotaAsync(dilihat.NoRawat, user, ct);
        if (rencana != dilihat) throw new InvalidOperationException("Nota berubah. Muat ulang tagihan sebelum membatalkan.");
        var rawat = rencana.NoRawat;
        var rm = rencana.NoRekamMedis;
        var lanjut = rencana.Ranap ? "Ranap" : "Ralan";

        if (rencana.NoJurnal is { } noJurnal)
        {
            // Jurnal asli dibalik baris demi baris, sehingga tetap cocok walau pengaturan rekening sudah diganti.
            var asli = await db.Database.SqlQuery<BarisJurnal>($"""
                SELECT kd_rek AS KodeRekening,debet AS Debet,kredit AS Kredit FROM detailjurnal WHERE no_jurnal={noJurnal}
                """).ToListAsync(ct);
            var judul = rencana.Piutang ? "PIUTANG" : "PEMBAYARAN";
            await JurnalPoster.PostAsync((MySqlConnection)db.Database.GetDbConnection(), (MySqlTransaction)tx.GetDbTransaction(), rawat,
                $"PEMBATALAN {judul} PASIEN RAWAT {(rencana.Ranap ? "INAP" : "JALAN")} {rawat} {rm} {rencana.NamaPasien}, DIBATALKAN OLEH {user.KodeLegacy}",
                asli.Select(b => new JurnalLine(b.KodeRekening, "", b.Kredit, b.Debet)), ct);
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM detail_piutang_pasien WHERE no_rawat={rawat}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM piutang_pasien WHERE no_rawat={rawat}", ct);
        if (rencana.Ranap)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM detail_nota_inap WHERE no_rawat={rawat}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM nota_inap WHERE no_rawat={rawat}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM pengembalian_deposit WHERE no_rawat={rawat}", ct);
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM detail_nota_jalan WHERE no_rawat={rawat}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM nota_jalan WHERE no_rawat={rawat}", ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM billing WHERE no_rawat={rawat}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM tagihan_sadewa WHERE no_nota={rawat}", ct);

        // Tagihan bank yang belum dibayar ikut dihapus, seperti Khanza.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM tagihan_bpd_jateng WHERE no_rkm_medis={rm} AND no_rawat={rawat} AND status_lanjut={lanjut} AND status_bayar='Pending'
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM tagihan_bpd_papua WHERE no_rkm_medis={rm} AND no_rawat={rawat} AND status_lanjut={lanjut} AND status_bayar='Pending'
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM tagihan_bpd_jabar WHERE no_rkm_medis={rm} AND no_rawat={rawat} AND status_lanjut={lanjut} AND status_bayar='Pending'
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM tagihan_mandiri WHERE no_rkm_medis={rm} AND no_rawat={rawat} AND status_lanjut={lanjut} AND status_bayar='Pending'
            """, ct);
        var angka = rawat.Replace("/", "");
        if (angka.Length >= 14)
        {
            var noBriva = angka[2..8] + angka[10..14];
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM tagihan_briva WHERE no_rkm_medis={rm} AND no_tagihan={noBriva} AND status_tagihan={lanjut} AND status_bayar='Pending'
                """, ct);
        }

        // Bayi rawat gabung dilunasi bersama ibunya, jadi dibuka bersama; bayi yang punya tagihan sendiri dibiarkan.
        foreach (var kunjungan in bayi.Prepend(rawat))
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE reg_periksa SET status_bayar='Belum Bayar'
                WHERE no_rawat={kunjungan} AND NOT EXISTS(SELECT 1 FROM billing WHERE billing.no_rawat={kunjungan})
                """, ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>Mengunci kunjungan (dan bayinya) lalu memeriksa nota. Pemanggil sudah membuka transaksi.</summary>
    private async Task<(RencanaBatalNota Rencana, List<string> Bayi)> SusunBatalNotaAsync(
        string noRawat, AuthenticatedUser user, CancellationToken ct)
    {
        if (!user.HasPermission(IzinBatalNota)) throw new InvalidOperationException("Akun tidak memiliki hak Hapus Nota Salah.");
        db.ChangeTracker.Clear(); // baris kunjungan harus dibaca ulang dari database
        var reg = await KunjunganRanap.KunciAsync(db, noRawat, ct);
        var rawat = reg.NoRawat;
        var bayi = await db.Database.SqlQuery<string>(
            $"SELECT no_rawat2 AS Value FROM ranap_gabung WHERE no_rawat={rawat} FOR UPDATE").ToListAsync(ct);
        foreach (var anak in bayi) await KunjunganRanap.KunciAsync(db, anak, ct);

        var jalan = (await db.Database.SqlQuery<NotaDibatalkan>($"""
            SELECT COALESCE(no_nota,'') AS NoNota,CAST(0 AS DECIMAL(20,4)) AS Deposit
            FROM nota_jalan WHERE no_rawat={rawat} FOR UPDATE
            """).ToListAsync(ct)).SingleOrDefault();
        var inap = (await db.Database.SqlQuery<NotaDibatalkan>($"""
            SELECT COALESCE(no_nota,'') AS NoNota,CAST(COALESCE(Uang_Muka,0) AS DECIMAL(20,4)) AS Deposit
            FROM nota_inap WHERE no_rawat={rawat} FOR UPDATE
            """).ToListAsync(ct)).SingleOrDefault();
        if (jalan is null && inap is null)
        {
            var ibu = (await db.Database.SqlQuery<string>(
                $"SELECT no_rawat AS Value FROM ranap_gabung WHERE no_rawat2={rawat}").ToListAsync(ct)).FirstOrDefault();
            if (ibu is not null) throw new InvalidOperationException($"Rawat gabung: batalkan nota pada No. Rawat ibu {ibu}.");
            throw new InvalidOperationException("Tidak ada nota pada kunjungan ini. Billing parsial dibatalkan lewat Khanza.");
        }
        if (jalan is not null && inap is not null)
            throw new InvalidOperationException("Kunjungan ini punya nota rawat jalan dan rawat inap. Batalkan lewat Khanza.");
        var ranap = inap is not null;
        var nota = (inap ?? jalan)!;
        if (!BolehBatalkanNota(user, ranap))
            throw new InvalidOperationException($"Akun tidak memiliki hak billing {(ranap ? "rawat inap" : "rawat jalan")}.");

        var piutang = (await db.Database.SqlQuery<PiutangTersimpan>($"""
            SELECT COALESCE(status,'') AS Status FROM piutang_pasien WHERE no_rawat={rawat} FOR UPDATE
            """).ToListAsync(ct)).FirstOrDefault();
        // Khanza tidak memeriksa tiga hal ini; tanpanya cicilan dan penagihan tertinggal tanpa piutang.
        if (await db.Database.SqlQuery<bool>(
                $"SELECT EXISTS(SELECT 1 FROM bayar_piutang WHERE no_rawat={rawat}) AS Value").SingleAsync(ct))
            throw new InvalidOperationException("Sudah ada cicilan piutang. Batalkan cicilannya di Piutang Pasien lebih dulu.");
        if (await db.Database.SqlQuery<bool>(
                $"SELECT EXISTS(SELECT 1 FROM detail_penagihan_piutang WHERE no_rawat={rawat}) AS Value").SingleAsync(ct))
            throw new InvalidOperationException("Piutang sudah masuk penagihan. Selesaikan penagihannya di Khanza lebih dulu.");
        if (piutang is not null && (piutang.Status != "Belum Lunas" || await db.Database.SqlQuery<bool>($"""
                SELECT EXISTS(SELECT 1 FROM detail_piutang_pasien
                    WHERE no_rawat={rawat} AND ABS(COALESCE(sisapiutang,0)-COALESCE(totalpiutang,0))>0.001) AS Value
                """).SingleAsync(ct)))
            throw new InvalidOperationException("Piutang sudah berubah tanpa catatan cicilan. Periksa di Keuangan.");

        var tunai = await db.Database.SqlQuery<decimal>(ranap ? (FormattableString)$"""
            SELECT CAST(COALESCE(SUM(besar_bayar),0) AS DECIMAL(20,4)) AS Value FROM detail_nota_inap WHERE no_rawat={rawat}
            """ : $"""
            SELECT CAST(COALESCE(SUM(besar_bayar),0) AS DECIMAL(20,4)) AS Value FROM detail_nota_jalan WHERE no_rawat={rawat}
            """).SingleAsync(ct);
        var jumlahPiutang = await db.Database.SqlQuery<decimal>($"""
            SELECT CAST(COALESCE(SUM(totalpiutang),0) AS DECIMAL(20,4)) AS Value FROM detail_piutang_pasien WHERE no_rawat={rawat}
            """).SingleAsync(ct);
        var pengembalian = await db.Database.SqlQuery<decimal>($"""
            SELECT CAST(COALESCE(SUM(besar_pengembalian),0) AS DECIMAL(20,4)) AS Value FROM pengembalian_deposit WHERE no_rawat={rawat}
            """).SingleAsync(ct);

        // Sama dengan syarat penulisan jurnal saat pelunasan: ada deposit, uang, atau baris tagihan yang bernilai.
        var bernilai = nota.Deposit != 0 || tunai != 0 || jumlahPiutang != 0 || await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS(SELECT 1 FROM billing WHERE no_rawat={rawat} AND totalbiaya<>0) AS Value
            """).SingleAsync(ct);
        var noJurnal = bernilai ? await JurnalPelunasanAsync(rawat, ranap, tunai + jumlahPiutang, ct) : null;

        var nama = await db.PasienSet.AsNoTracking().Where(p => p.NoRekamMedis == reg.NoRekamMedis)
            .Select(p => p.NamaPasien).SingleOrDefaultAsync(ct) ?? "";
        return (new RencanaBatalNota(rawat, reg.NoRekamMedis, nama, ranap, nota.NoNota, piutang is not null,
            tunai, jumlahPiutang, nota.Deposit, pengembalian, noJurnal), bayi);
    }

    /// <summary>Jurnal pelunasan nota yang masih berlaku: yang terbaru di antara pelunasan dan pembatalan kunjungan ini.</summary>
    private async Task<string> JurnalPelunasanAsync(string noRawat, bool ranap, decimal diterima, CancellationToken ct)
    {
        var jenis = ranap ? "INAP" : "JALAN";
        // "PELUNASAN RAWAT JALAN" adalah kalimat pelunasan tunai rawat jalan dari aplikasi ini; sisanya sama dengan Khanza.
        string[] pelunasan =
        [
            $"PEMBAYARAN PASIEN RAWAT {jenis} {noRawat}", $"PIUTANG PASIEN RAWAT {jenis} {noRawat}",
            .. ranap ? Array.Empty<string>() : [$"PELUNASAN RAWAT JALAN {noRawat}"],
        ];
        string[] pembatalan =
        [
            $"PEMBATALAN PEMBAYARAN PASIEN RAWAT {jenis} {noRawat}", $"PEMBATALAN PIUTANG PASIEN RAWAT {jenis} {noRawat}",
        ];
        static bool Berawalan(string teks, string[] awalan) =>
            awalan.Any(a => teks.StartsWith(a, StringComparison.OrdinalIgnoreCase));

        var semua = await db.Database.SqlQuery<JurnalNota>($"""
            SELECT no_jurnal AS NoJurnal,COALESCE(keterangan,'') AS Keterangan FROM jurnal WHERE no_bukti={noRawat}
            ORDER BY tgl_jurnal DESC,jam_jurnal DESC,no_jurnal DESC
            """).ToListAsync(ct);
        var terakhir = semua.FirstOrDefault(j => Berawalan(j.Keterangan, pelunasan) || Berawalan(j.Keterangan, pembatalan));
        if (terakhir is null || !Berawalan(terakhir.Keterangan, pelunasan))
            throw new InvalidOperationException(
                "Jurnal pelunasan nota ini tidak ditemukan (misalnya billing parsial atau pembayaran jalur lain). Batalkan lewat Khanza.");
        var debet = await db.Database.SqlQuery<double>($"""
            SELECT COALESCE(SUM(debet),0) AS Value FROM detailjurnal WHERE no_jurnal={terakhir.NoJurnal}
            """).SingleAsync(ct);
        if (debet < (double)diterima - 1)
            throw new InvalidOperationException(
                $"Jurnal {terakhir.NoJurnal} tidak cocok dengan penerimaan pada nota ini. Periksa di Keuangan.");
        return terakhir.NoJurnal;
    }
}
