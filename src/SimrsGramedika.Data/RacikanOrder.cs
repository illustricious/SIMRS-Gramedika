using System.Globalization;

namespace SimrsGramedika.Data;

/// <summary>Satu obat penyusun racikan (resep_dokter_racikan_detail). Kapasitas dibaca dari databarang.</summary>
public sealed record BahanRacikan(string Kode, string Nama, decimal Jumlah, decimal P1 = 1, decimal P2 = 1,
    string Kandungan = "", decimal Kapasitas = 0);

/// <summary>Satu racikan pada resep dokter (resep_dokter_racikan). Nomor racik mengikuti urutan dalam resep.</summary>
public sealed record RacikanOrder(string Nama, string KodeMetode, string Metode, int Jumlah, string Aturan, string Keterangan,
    IReadOnlyList<BahanRacikan> Bahan);

public sealed record MetodeRacik(string Kode, string Nama)
{
    public override string ToString() => Nama;
}

/// <summary>
/// Rumus jumlah obat racikan seperti DlgPeresepanDokter Khanza, dibulatkan satu desimal: kandungan dari pecahan P1/P2,
/// jumlah dari kandungan, atau jumlah dari persen berat bahan lain.
/// </summary>
public static class HitungRacikan
{
    public static decimal Bulat(decimal nilai) => Math.Round(nilai, 1, MidpointRounding.AwayFromZero);

    /// <summary>Kandungan per bungkus bila satu racikan memakai P1/P2 bagian obat.</summary>
    public static decimal? Kandungan(decimal kapasitas, decimal p1, decimal p2) =>
        kapasitas > 0 && p1 > 0 && p2 > 0 ? Bulat(kapasitas * (p1 / p2)) : null;

    /// <summary>Angka kandungan atau persen; menerima koma maupun titik desimal.</summary>
    public static bool Baca(string teks, out decimal nilai, out bool persen)
    {
        var bersih = teks.Trim().Replace(',', '.');
        persen = bersih.EndsWith('%');
        if (persen) bersih = bersih[..^1].Trim();
        return decimal.TryParse(bersih, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out nilai);
    }

    /// <summary>
    /// Menghitung ulang jumlah semua bahan yang berkandungan. Bahan tanpa kandungan, atau yang tidak bisa dihitung
    /// karena kapasitasnya kosong, mempertahankan jumlah yang diisi dokter.
    /// </summary>
    public static IReadOnlyList<BahanRacikan> Ulang(int jumlahRacik, IReadOnlyList<BahanRacikan> bahan)
    {
        var hasil = bahan.Select(b => b.Kapasitas > 0 && Baca(b.Kandungan, out var k, out var persen) && !persen
            ? b with { Jumlah = Bulat(jumlahRacik * k / b.Kapasitas) } : b).ToList();
        // Persen dihitung terhadap berat bahan yang bukan persen, jadi menunggu jumlah mereka selesai.
        var berat = hasil.Where(b => !b.Kandungan.Contains('%')).Sum(b => b.Kapasitas * b.Jumlah);
        return hasil.Select(b => b.Kapasitas > 0 && Baca(b.Kandungan, out var p, out var persen) && persen
            ? b with { Jumlah = Bulat(berat * (p / 100) / b.Kapasitas) } : b).ToList();
    }
}
