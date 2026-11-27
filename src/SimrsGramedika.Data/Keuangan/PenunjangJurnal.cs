using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using SimrsGramedika.Core.Auth;

namespace SimrsGramedika.Data.Keuangan;

/// <summary>Nilai satu pemeriksaan penunjang yang dijurnal, sesuai pembagian tarif Khanza.</summary>
public sealed record KomponenPenunjang(
    double Pendapatan = 0, double Dokter = 0, double Petugas = 0, double Bhp = 0, double Kso = 0, double Sarana = 0,
    double Perujuk = 0, double Menejemen = 0)
{
    public static KomponenPenunjang operator +(KomponenPenunjang a, KomponenPenunjang b) => new(
        a.Pendapatan + b.Pendapatan, a.Dokter + b.Dokter, a.Petugas + b.Petugas, a.Bhp + b.Bhp, a.Kso + b.Kso,
        a.Sarana + b.Sarana, a.Perujuk + b.Perujuk, a.Menejemen + b.Menejemen);
}

/// <summary>
/// Jurnal pemeriksaan Laboratorium dan Radiologi (DlgPeriksaLaboratorium, DlgPeriksaRadiologi) dan pembaliknya
/// (DlgCariPeriksaLab, DlgCariPeriksaRadiologi). Selalu dijalankan di dalam transaksi pemanggil.
/// </summary>
internal static class PenunjangJurnal
{
    public const string Laborat = "Laborat";
    public const string Radiologi = "Radiologi";

    public static async Task PostAsync(SimrsGramedikaDbContext db, string noRawat, bool ranap, string unit,
        KomponenPenunjang nilai, bool batal, CancellationToken ct)
    {
        var (conn, tx) = Koneksi(db);
        var sfx = ranap ? "Ranap" : "Ralan";
        var rawat = ranap ? "Rawat_Inap" : "Rawat_Jalan";
        var nama = ranap ? "Rawat Inap" : "Rawat Jalan";
        var akun = await JurnalPoster.ReadAkunAsync(conn, tx, ranap ? "set_akun_ranap" : "set_akun_ralan", ct);
        string A(string kolom) => akun.GetValueOrDefault(kolom, "");

        var baris = new List<JurnalLine>();
        // Pembatalan menukar sisi debet dan kredit.
        void Pasang(double jumlah, string debet, string namaDebet, string kredit, string namaKredit)
        {
            if (jumlah <= 0) return;
            baris.Add(new(A(debet), namaDebet, batal ? 0 : jumlah, batal ? jumlah : 0));
            baris.Add(new(A(kredit), namaKredit, batal ? jumlah : 0, batal ? 0 : jumlah));
        }
        void Jasa(double jumlah, string kolom, string label) => Pasang(jumlah,
            $"Beban_{kolom}_{unit}_{sfx}", $"Beban {label} {unit} {sfx}", $"Utang_{kolom}_{unit}_{sfx}", $"Utang {label} {unit} {sfx}");

        Pasang(nilai.Pendapatan, $"Suspen_Piutang_{unit}_{sfx}", $"Suspen Piutang {unit} {sfx}",
            $"{unit}_{sfx}", $"Pendapatan {unit} {nama}");
        Jasa(nilai.Dokter, "Jasa_Medik_Dokter", "Jasa Medik Dokter");
        Jasa(nilai.Petugas, "Jasa_Medik_Petugas", "Jasa Medik Petugas");
        Pasang(nilai.Bhp, $"HPP_Persediaan_{unit}_{rawat}", $"HPP Persediaan {unit} {nama}",
            $"Persediaan_BHP_{unit}_{rawat}", $"Persediaan BHP {unit} {nama}");
        Jasa(nilai.Kso, "Kso", "KSO");
        Jasa(nilai.Sarana, "Jasa_Sarana", "Jasa Sarana");
        Jasa(nilai.Perujuk, "Jasa_Perujuk", "Jasa Perujuk");
        Jasa(nilai.Menejemen, "Jasa_Menejemen", "Jasa Menejemen");
        if (baris.Count == 0) return;

        var pasien = await TeksAsync(conn, tx, """
            SELECT CONCAT(p.no_rkm_medis,' ',p.nm_pasien) FROM reg_periksa r
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis WHERE r.no_rawat=@p0
            """, ct, noRawat);
        var jenis = $"PEMERIKSAAN {unit.ToUpperInvariant()} {nama.ToUpperInvariant()} PASIEN {pasien}";
        await JurnalPoster.PostAsync(conn, tx, noRawat,
            batal ? $"PEMBATALAN {jenis} OLEH {AppSession.Kode}" : $"{jenis} DIPOSTING OLEH {AppSession.Kode}", baris, ct);
    }

    /// <summary>
    /// Menentukan apakah pemeriksaan yang akan dihapus pernah dijurnal. Jurnal tidak menyimpan kunci pemeriksaan,
    /// jadi dibandingkan saldo suspen kunjungan dengan nilai seluruh pemeriksaan unit itu: saldo nol berarti tidak
    /// ada yang dijurnal (hapus tanpa pembalik), saldo mencukupi berarti aman dibalik, selain itu tidak pasti.
    /// </summary>
    public static async Task<bool> PerluDibalikAsync(SimrsGramedikaDbContext db, string noRawat, bool ranap, string unit,
        CancellationToken ct)
    {
        var (conn, tx) = Koneksi(db);
        var ralan = await JurnalPoster.ReadAkunAsync(conn, tx, "set_akun_ralan", ct);
        var inap = await JurnalPoster.ReadAkunAsync(conn, tx, "set_akun_ranap", ct);
        var suspenRalan = ralan.GetValueOrDefault($"Suspen_Piutang_{unit}_Ralan", "");
        var suspenRanap = inap.GetValueOrDefault($"Suspen_Piutang_{unit}_Ranap", "");
        var suspen = ranap ? suspenRanap : suspenRalan;
        if (suspen.Length == 0) return false;

        var saldo = await AngkaAsync(conn, tx, """
            SELECT IFNULL(SUM(d.debet-d.kredit),0) FROM jurnal j JOIN detailjurnal d ON d.no_jurnal=j.no_jurnal
            WHERE j.no_bukti=@p0 AND d.kd_rek=@p1
            """, ct, noRawat, suspen);
        if (Math.Abs(saldo) < 0.5) return false;

        // Bila rawat jalan dan rawat inap memakai rekening yang sama, keduanya ikut dihitung.
        var status = new List<string>();
        if (suspenRalan == suspen) status.Add("Ralan");
        if (suspenRanap == suspen) status.Add("Ranap");
        var daftar = string.Join(",", status.Select(s => $"'{s}'"));
        var sql = unit == Laborat
            ? $"""
              SELECT IFNULL((SELECT SUM(biaya) FROM periksa_lab WHERE no_rawat=@p0 AND status IN ({daftar})),0)
                   + IFNULL((SELECT SUM(d.biaya_item) FROM detail_periksa_lab d
                             WHERE d.no_rawat=@p0 AND EXISTS(SELECT 1 FROM periksa_lab l WHERE l.no_rawat=d.no_rawat
                                 AND l.kd_jenis_prw=d.kd_jenis_prw AND l.tgl_periksa=d.tgl_periksa AND l.jam=d.jam
                                 AND l.status IN ({daftar}))),0)
              """
            : $"SELECT IFNULL(SUM(biaya),0) FROM periksa_radiologi WHERE no_rawat=@p0 AND status IN ({daftar})";
        var seluruh = await AngkaAsync(conn, tx, sql, ct, noRawat);
        if (saldo >= seluruh - 0.5) return true;
        throw new InvalidOperationException(
            $"Saldo suspen {unit} kunjungan ini (Rp {saldo:N0}) lebih kecil dari nilai pemeriksaannya (Rp {seluruh:N0}), " +
            "sehingga tidak diketahui apakah pemeriksaan ini sudah dijurnal. Periksa dan hapus lewat Khanza.");
    }

    private static (MySqlConnection, MySqlTransaction) Koneksi(SimrsGramedikaDbContext db) =>
        ((MySqlConnection)db.Database.GetDbConnection(),
            (MySqlTransaction)(db.Database.CurrentTransaction
                ?? throw new InvalidOperationException("Jurnal harus diposting di dalam transaksi.")).GetDbTransaction());

    private static async Task<object?> NilaiAsync(MySqlConnection conn, MySqlTransaction tx, string sql, CancellationToken ct,
        object[] args)
    {
        await using var cmd = new MySqlCommand(sql, conn, tx);
        for (var i = 0; i < args.Length; i++) cmd.Parameters.AddWithValue("@p" + i, args[i]);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is DBNull ? null : v;
    }

    private static async Task<double> AngkaAsync(MySqlConnection conn, MySqlTransaction tx, string sql, CancellationToken ct,
        params object[] args) => Convert.ToDouble(await NilaiAsync(conn, tx, sql, ct, args) ?? 0d);

    private static async Task<string> TeksAsync(MySqlConnection conn, MySqlTransaction tx, string sql, CancellationToken ct,
        params object[] args) => Convert.ToString(await NilaiAsync(conn, tx, sql, ct, args)) ?? "";
}
