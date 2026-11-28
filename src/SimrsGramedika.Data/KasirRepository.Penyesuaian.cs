using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data.Keuangan;

namespace SimrsGramedika.Data;

/// <summary>Biaya yang ditambahkan atau dipotong Kasir pada satu kunjungan yang belum ditagih.</summary>
public enum JenisPenyesuaian { Tambahan, Potongan }

public record PenyesuaianBiaya(string Nama, decimal Besar);

public partial class KasirRepository
{
    private const string Tambahan = "Tambahan";
    private const string Potongan = "Potongan";
    private const int PanjangNamaBiaya = 60;
    private const decimal BiayaTerbesar = 999_999_999_999m;

    public static string IzinPenyesuaian(JenisPenyesuaian jenis) =>
        jenis == JenisPenyesuaian.Tambahan ? "tambahan_biaya" : "potongan_biaya";

    /// <summary>Hanya baris potongan yang boleh bernilai negatif pada tagihan.</summary>
    private static bool NilaiTidakWajar(KasirRincian baris) => baris.Jenis == Potongan ? baris.Total > 0 : baris.Total < 0;

    public async Task<IReadOnlyList<PenyesuaianBiaya>> DaftarPenyesuaianAsync(
        string noRawat, JenisPenyesuaian jenis, CancellationToken ct = default) =>
        await db.Database.SqlQueryRaw<PenyesuaianBiaya>(jenis == JenisPenyesuaian.Tambahan
            ? """
              SELECT nama_biaya AS Nama,CAST(besar_biaya AS DECIMAL(20,4)) AS Besar
              FROM tambahan_biaya WHERE no_rawat={0} ORDER BY nama_biaya
              """
            : """
              SELECT nama_pengurangan AS Nama,CAST(COALESCE(besar_pengurangan,0) AS DECIMAL(20,4)) AS Besar
              FROM pengurangan_biaya WHERE no_rawat={0} ORDER BY nama_pengurangan
              """, noRawat).ToListAsync(ct);

    public async Task TambahPenyesuaianAsync(string noRawat, JenisPenyesuaian jenis, string nama, decimal besar,
        AuthenticatedUser user, CancellationToken ct = default)
    {
        nama = nama.Trim();
        if (nama.Length is 0 or > PanjangNamaBiaya)
            throw new ArgumentException($"Nama biaya wajib diisi, paling panjang {PanjangNamaBiaya} huruf.");
        if (besar <= 0 || besar > BiayaTerbesar || besar != Bulatkan(besar))
            throw new ArgumentException("Besar biaya harus lebih dari nol dan paling banyak empat desimal.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await KunciUntukPenyesuaianAsync(noRawat, jenis, user, ct);
        var ada = await db.Database.SqlQueryRaw<bool>(jenis == JenisPenyesuaian.Tambahan
            ? "SELECT EXISTS(SELECT 1 FROM tambahan_biaya WHERE no_rawat={0} AND nama_biaya={1}) AS Value"
            : "SELECT EXISTS(SELECT 1 FROM pengurangan_biaya WHERE no_rawat={0} AND nama_pengurangan={1}) AS Value",
            noRawat, nama).SingleAsync(ct);
        if (ada) throw new InvalidOperationException($"\"{nama}\" sudah tercatat. Hapus dulu atau pakai nama lain.");
        await db.Database.ExecuteSqlRawAsync(jenis == JenisPenyesuaian.Tambahan
            ? "INSERT INTO tambahan_biaya(no_rawat,nama_biaya,besar_biaya) VALUES ({0},{1},{2})"
            : "INSERT INTO pengurangan_biaya(no_rawat,nama_pengurangan,besar_pengurangan) VALUES ({0},{1},{2})",
            [noRawat, nama, besar], ct);
        await tx.CommitAsync(ct);
    }

    public async Task HapusPenyesuaianAsync(string noRawat, JenisPenyesuaian jenis, string nama,
        AuthenticatedUser user, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await KunciUntukPenyesuaianAsync(noRawat, jenis, user, ct);
        var terhapus = await db.Database.ExecuteSqlRawAsync(jenis == JenisPenyesuaian.Tambahan
            ? "DELETE FROM tambahan_biaya WHERE no_rawat={0} AND nama_biaya={1}"
            : "DELETE FROM pengurangan_biaya WHERE no_rawat={0} AND nama_pengurangan={1}",
            [noRawat, nama], ct);
        if (terhapus != 1) throw new InvalidOperationException("Data biaya sudah berubah. Muat ulang daftarnya.");
        await tx.CommitAsync(ct);
    }

    /// <summary>Kunci yang sama dengan pelunasan, sehingga biaya tidak berubah di tengah penyimpanan nota.</summary>
    private async Task KunciUntukPenyesuaianAsync(
        string noRawat, JenisPenyesuaian jenis, AuthenticatedUser user, CancellationToken ct)
    {
        if (!user.HasPermission(IzinPenyesuaian(jenis)))
            throw new InvalidOperationException($"Akun tidak memiliki hak {(jenis == JenisPenyesuaian.Tambahan ? "tambahan" : "potongan")} biaya.");
        var reg = await KunjunganRanap.KunciAsync(db, noRawat, ct);
        if (reg.Status == StatusRegistrasi.Batal) throw new InvalidOperationException("Registrasi pasien sudah dibatalkan.");
        var ibu = await db.Database.SqlQuery<string>($"SELECT no_rawat AS Value FROM ranap_gabung WHERE no_rawat2={noRawat}")
            .ToListAsync(ct);
        if (ibu.Count > 0)
            throw new InvalidOperationException($"Tagihan bayi rawat gabung ada pada No. Rawat ibu {ibu[0]}. Catat biaya di sana.");
        if (reg.StatusBayar == StatusBayar.SudahBayar || await TagihanKunjungan.SudahDitagihAsync(db, noRawat, ct)
            || await db.Database.SqlQuery<bool>($"""
                SELECT (EXISTS(SELECT 1 FROM nota_jalan WHERE no_rawat={noRawat}) OR
                    EXISTS(SELECT 1 FROM nota_inap WHERE no_rawat={noRawat})) AS Value
                """).SingleAsync(ct))
            throw new InvalidOperationException("Tagihan kunjungan ini sudah disimpan, biayanya tidak bisa diubah.");
    }

    /// <summary>Baris tagihan dari tambahan dan potongan biaya; potongan bernilai negatif seperti billing Khanza.</summary>
    private async Task<List<KasirRincian>> MuatPenyesuaianAsync(string noRawat, CancellationToken ct)
    {
        var rows = new List<KasirRincian>();
        foreach (var item in await DaftarPenyesuaianAsync(noRawat, JenisPenyesuaian.Tambahan, ct))
            if (item.Besar != 0) rows.Add(new(Tambahan, item.Nama, item.Besar, 1, item.Besar));
        foreach (var item in await DaftarPenyesuaianAsync(noRawat, JenisPenyesuaian.Potongan, ct))
            if (item.Besar != 0) rows.Add(new(Potongan, item.Nama, item.Besar, 1, -item.Besar));
        return rows;
    }

    /// <summary>Selisih antara total baris dan tarif kali jumlah; Khanza menyimpan nol untuk baris potongan.</summary>
    private static decimal SelisihBaris(KasirRincian baris) =>
        baris.Jenis == Potongan ? 0 : baris.Total - baris.Tarif * baris.Jumlah;

    /// <summary>Tambahan dikredit ke pendapatan, potongan didebit sebagai pengurang.</summary>
    private static void TambahJurnalPenyesuaian(
        List<JurnalLine> lines, KasirBillItem bill, Dictionary<string, string> akun, string akhiran)
    {
        TambahKredit(lines, akun.GetValueOrDefault($"Tambahan_{akhiran}", ""), $"Tambahan {akhiran}",
            bill.Rincian.Where(r => r.Jenis == Tambahan).Sum(r => r.Total));
        TambahDebit(lines, akun.GetValueOrDefault($"Potongan_{akhiran}", ""), $"Potongan {akhiran}",
            -bill.Rincian.Where(r => r.Jenis == Potongan).Sum(r => r.Total));
    }

    private static void TambahDebit(List<JurnalLine> lines, string akun, string nama, decimal jumlah)
    {
        if (jumlah > 0) lines.Add(new(akun, nama, (double)jumlah, 0));
    }
}
