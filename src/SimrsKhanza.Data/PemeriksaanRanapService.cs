using MySqlConnector;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data;

public sealed record SoapRanap(string NoRawat, DateTime Waktu, string Nip, string Kesadaran, IReadOnlyList<string> Isian, string Nama = "");

public sealed class PemeriksaanRanapService(DatabaseSettings settings, string noRawat, AuthenticatedUser user)
{
    public const string Izin = "tindakan_ranap";
    public const int AwalNarasi = 9;

    public static IReadOnlyList<(string Kolom, string Label, int Maks)> Isian { get; } = Array.AsReadOnly(
        new (string, string, int)[]
        {
            ("suhu_tubuh", "Suhu", 5), ("tensi", "Tensi", 8), ("nadi", "Nadi", 3), ("respirasi", "Respirasi", 3),
            ("tinggi", "Tinggi", 5), ("berat", "Berat", 5), ("spo2", "SpO₂", 3), ("gcs", "GCS", 10), ("alergi", "Alergi", 80),
            ("keluhan", "Subjektif / Keluhan", 2000), ("pemeriksaan", "Objektif / Pemeriksaan", 2000),
            ("penilaian", "Asesmen", 2000), ("rtl", "Rencana", 2000), ("instruksi", "Instruksi", 2000), ("evaluasi", "Evaluasi", 2000),
        });

    private static readonly string KolomIsian = string.Join(",", Isian.Select(f => f.Kolom));
    private static readonly string ParameterIsian = string.Join(",", Isian.Select((_, i) => $"@p{i + 5}"));
    private static readonly string SetIsian = string.Join(",", Isian.Select((f, i) => $"{f.Kolom}=@p{i + 5}"));

    public async Task<IReadOnlyList<SoapRanap>> DaftarAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT e.*,g.nama FROM pemeriksaan_ranap e LEFT JOIN pegawai g ON g.nik=e.nip
            WHERE e.no_rawat=@p0 ORDER BY e.tgl_perawatan DESC,e.jam_rawat DESC
            """, noRawat);
        return rows.Select(Baca).ToList();
    }

    public async Task<IReadOnlyList<SoapRanap>> LimaTerakhirAsync(string nip, string? cari)
    {
        PastikanIzin();
        var penulis = user.IsAdminUtama ? nip : user.UserId;
        if (string.IsNullOrWhiteSpace(penulis))
            throw new ArgumentException("Pilih dokter atau petugas terlebih dahulu.");

        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT e.*,g.nama
            FROM reg_periksa aktif
            JOIN reg_periksa riwayat ON riwayat.no_rkm_medis=aktif.no_rkm_medis
            JOIN pemeriksaan_ranap e ON e.no_rawat=riwayat.no_rawat
            LEFT JOIN pegawai g ON g.nik=e.nip
            WHERE aktif.no_rawat=@p0 AND e.nip=@p1
              AND (@p2='' OR LOCATE(@p2,e.keluhan)>0 OR LOCATE(@p2,e.pemeriksaan)>0)
            ORDER BY e.tgl_perawatan DESC,e.jam_rawat DESC,e.no_rawat DESC
            LIMIT 5
            """, noRawat, penulis, cari?.Trim() ?? "");
        return rows.Select(Baca).ToList();
    }

    public async Task<IReadOnlyList<PegawaiPilihan>> PegawaiAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = user.IsAdminUtama
            ? await db.QueryAsync("SELECT nik,nama FROM pegawai ORDER BY nama,nik")
            : await db.QueryAsync("SELECT nik,nama FROM pegawai WHERE nik=@p0", user.UserId);
        return rows.Select(r => new PegawaiPilihan(r.S("nik"), r.S("nama"))).ToList();
    }

    public async Task SimpanAsync(SoapRanap input, SoapRanap? asal = null)
    {
        PastikanIzin();
        PastikanKunjungan(input);
        if (asal is not null) PastikanKunjungan(asal);
        var nip = user.IsAdminUtama ? input.Nip : user.UserId;
        ValidasiIsian(input);
        if (string.IsNullOrWhiteSpace(nip)) throw new ArgumentException("Pilih dokter atau petugas.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var kunjungan = await KunciKunjunganAsync(db);
        if (!user.IsAdminUtama)
            RawatJalanService.PastikanTidakSebelumRegistrasi(Waktu(kunjungan, "tgl_registrasi", "jam_reg"), input.Waktu);
        if (asal is not null) await KunciCatatanAsync(db, asal, kunjungan);
        var nik = await db.StrAsync("SELECT nik FROM pegawai WHERE nik=@p0", nip);
        if (nik.Length == 0) throw new InvalidOperationException("NIK dokter atau petugas tidak ditemukan.");
        object?[] args = [noRawat, input.Waktu.Date, input.Waktu.TimeOfDay, nik, input.Kesadaran, .. input.Isian];
        try
        {
            if (asal is null)
                await db.ExecAsync($"""
                    INSERT INTO pemeriksaan_ranap(no_rawat,tgl_perawatan,jam_rawat,nip,kesadaran,{KolomIsian})
                    VALUES(@p0,@p1,@p2,@p3,@p4,{ParameterIsian})
                    """, args);
            else
                await db.ExecAsync($"""
                    UPDATE pemeriksaan_ranap SET tgl_perawatan=@p1,jam_rawat=@p2,nip=@p3,kesadaran=@p4,{SetIsian}
                    WHERE no_rawat=@p0 AND tgl_perawatan=@p{args.Length} AND jam_rawat=@p{args.Length + 1}
                    """, [.. args, asal.Waktu.Date, asal.Waktu.TimeOfDay]);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new InvalidOperationException("Sudah ada pemeriksaan pada tanggal dan jam itu. Pilih waktu lain.", ex);
        }
        await db.CommitAsync();
    }

    public async Task HapusAsync(SoapRanap asal)
    {
        PastikanIzin();
        PastikanKunjungan(asal);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var kunjungan = await KunciKunjunganAsync(db);
        await KunciCatatanAsync(db, asal, kunjungan);
        await db.ExecAsync("""
            DELETE FROM pemeriksaan_ranap WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam_rawat=@p2
            """, noRawat, asal.Waktu.Date, asal.Waktu.TimeOfDay);
        await db.CommitAsync();
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun ini tidak memiliki hak pemeriksaan rawat inap.");
    }

    private void PastikanKunjungan(SoapRanap catatan)
    {
        if (catatan.NoRawat != noRawat) throw new InvalidOperationException("Catatan berasal dari kunjungan lain.");
    }

    private async Task<Row> KunciKunjunganAsync(Db db)
    {
        var row = await db.FirstAsync("""
            SELECT tgl_registrasi,jam_reg,stts,status_lanjut,NOW() AS sekarang
            FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE
            """, noRawat);
        if (row is null || row.S("status_lanjut") != "Ranap" || row.S("stts") == "Batal")
            throw new InvalidOperationException("Kunjungan rawat inap tidak ditemukan atau telah dibatalkan.");
        if (await db.CountAsync("SELECT EXISTS(SELECT 1 FROM billing WHERE no_rawat=@p0)", noRawat) != 0)
            throw new InvalidOperationException("Billing telah diverifikasi. Pemeriksaan hanya dapat dilihat.");
        return row;
    }

    private async Task KunciCatatanAsync(Db db, SoapRanap asal, Row kunjungan)
    {
        var row = await db.FirstAsync("""
            SELECT * FROM pemeriksaan_ranap WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam_rawat=@p2 FOR UPDATE
            """, noRawat, asal.Waktu.Date, asal.Waktu.TimeOfDay);
        if (row is null) throw Konflik();
        var aktual = Baca(row);
        if (aktual.Nip != asal.Nip || aktual.Kesadaran != asal.Kesadaran || !aktual.Isian.SequenceEqual(asal.Isian))
            throw Konflik();
        if (!user.IsAdminUtama)
        {
            if (!string.Equals(aktual.Nip, user.UserId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Hanya penulis yang dapat mengubah atau menghapus pemeriksaan ini.");
            if (RegistrasiService.MelewatiBatasEdit(asal.Waktu, kunjungan.Dt("sekarang")!.Value))
                throw new InvalidOperationException("Batas ubah atau hapus pemeriksaan adalah 48 jam.");
        }
    }

    private static InvalidOperationException Konflik() =>
        new("Catatan telah berubah. Muat ulang riwayat dan pilih ulang catatan.");

    public static void ValidasiIsian(SoapRanap input)
    {
        if (!RawatJalanService.Kesadaran.Contains(input.Kesadaran))
            throw new ArgumentException("Pilih tingkat kesadaran.");
        if (input.Isian.Count != Isian.Count || input.Isian.All(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Isi minimal satu data pemeriksaan.");
        for (var i = 0; i < Isian.Count; i++)
            if (input.Isian[i].Length > Isian[i].Maks)
                throw new ArgumentException($"{Isian[i].Label} melebihi {Isian[i].Maks} karakter.");
    }

    private static SoapRanap Baca(Row row) =>
        new(row.S("no_rawat"), Waktu(row, "tgl_perawatan", "jam_rawat"), row.S("nip"), row.S("kesadaran"),
            Isian.Select(f => row.S(f.Kolom)).ToArray(), row.S("nama"));

    private static DateTime Waktu(Row row, string tanggal, string jam) =>
        row.Dt(tanggal)?.Date.Add(row[jam] is TimeSpan t ? t : throw new InvalidOperationException("Jam tidak valid."))
        ?? throw new InvalidOperationException("Tanggal tidak valid.");
}
