using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

public sealed record CatatanDokter(string NoRawat, DateTime? Waktu, string KodeDokter, string Dokter, string Isi);

public sealed class CatatanDokterService(DatabaseSettings settings, string noRawat, AuthenticatedUser user)
{
    public const string Izin = "catatan_perawatan";
    public const int PanjangMaksimal = 700;

    public async Task<IReadOnlyList<CatatanDokter>> DaftarAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT c.no_rawat,c.tanggal,c.jam,c.kd_dokter,d.nm_dokter,c.catatan
            FROM catatan_perawatan c LEFT JOIN dokter d ON d.kd_dokter=c.kd_dokter
            WHERE c.no_rawat=@p0 ORDER BY c.tanggal DESC,c.jam DESC,c.kd_dokter
            """, noRawat);
        return rows.Select(row => new CatatanDokter(row.S("no_rawat"), BacaWaktu(row, "tanggal", "jam"),
            row.S("kd_dokter"), row.S("nm_dokter"), row.S("catatan"))).ToList();
    }

    public async Task<IReadOnlyList<PegawaiPilihan>> DokterAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = user.IsAdminUtama
            ? await db.QueryAsync("SELECT kd_dokter,nm_dokter FROM dokter ORDER BY nm_dokter,kd_dokter")
            : await db.QueryAsync("SELECT kd_dokter,nm_dokter FROM dokter WHERE kd_dokter=@p0", user.UserId);
        return rows.Select(row => new PegawaiPilihan(row.S("kd_dokter"), row.S("nm_dokter"))).ToList();
    }

    public async Task SimpanAsync(DateTime waktu, string kodeDokter, string isi, CatatanDokter? awal = null)
    {
        PastikanIzin();
        if (awal is not null) PastikanKunjungan(awal);
        isi = isi.Trim();
        if (isi.Length is 0 or > PanjangMaksimal) throw new ArgumentException("Isi catatan antara 1 dan 700 karakter.");
        var dokter = user.IsAdminUtama ? kodeDokter : user.UserId;
        if (string.IsNullOrWhiteSpace(dokter)) throw new ArgumentException("Pilih dokter terlebih dahulu.");
        waktu = waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var kunjungan = await KunciKunjunganAsync(db);
        if (!user.IsAdminUtama)
            RawatJalanService.PastikanTidakSebelumRegistrasi(
                BacaWaktu(kunjungan, "tgl_registrasi", "jam_reg") ?? throw new InvalidOperationException("Waktu registrasi tidak valid."), waktu);
        if (awal is not null) await KunciCatatanAsync(db, awal, kunjungan);
        var kode = await db.StrAsync("SELECT kd_dokter FROM dokter WHERE kd_dokter=@p0", dokter);
        if (kode.Length == 0) throw new InvalidOperationException("Akun atau kode dokter tidak ditemukan di data dokter.");

        if (awal is null || awal.Waktu != waktu || awal.KodeDokter != kode)
        {
            if ((await CariDenganKunciAsync(db, waktu, kode)).Count != 0)
                throw new InvalidOperationException("Sudah ada catatan untuk dokter dan waktu tersebut.");
        }
        if (awal is null)
            await db.ExecAsync("""
                INSERT INTO catatan_perawatan(no_rawat,tanggal,jam,kd_dokter,catatan) VALUES (@p0,@p1,@p2,@p3,@p4)
                """, noRawat, waktu.Date, waktu.TimeOfDay, kode, isi);
        else
            await db.ExecAsync("""
                UPDATE catatan_perawatan SET tanggal=@p4,jam=@p5,kd_dokter=@p6,catatan=@p7
                WHERE no_rawat=@p0 AND tanggal=@p1 AND jam=@p2 AND kd_dokter=@p3
                """, noRawat, awal.Waktu!.Value.Date, awal.Waktu.Value.TimeOfDay, awal.KodeDokter, waktu.Date, waktu.TimeOfDay, kode, isi);
        await db.CommitAsync();
    }

    public async Task HapusAsync(CatatanDokter awal)
    {
        PastikanIzin();
        PastikanKunjungan(awal);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var kunjungan = await KunciKunjunganAsync(db);
        await KunciCatatanAsync(db, awal, kunjungan);
        await db.ExecAsync("""
            DELETE FROM catatan_perawatan WHERE no_rawat=@p0 AND tanggal=@p1 AND jam=@p2 AND kd_dokter=@p3
            """, noRawat, awal.Waktu!.Value.Date, awal.Waktu.Value.TimeOfDay, awal.KodeDokter);
        await db.CommitAsync();
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak Catatan Dokter.");
    }

    private void PastikanKunjungan(CatatanDokter catatan)
    {
        if (catatan.NoRawat != noRawat) throw new InvalidOperationException("Catatan berasal dari kunjungan lain.");
    }

    private async Task<Row> KunciKunjunganAsync(Db db) =>
        await db.FirstAsync("""
            SELECT tgl_registrasi,jam_reg,NOW() AS sekarang FROM reg_periksa
            WHERE no_rawat=@p0 AND status_lanjut='Ralan' AND stts<>'Batal' FOR UPDATE
            """, noRawat) ?? throw new InvalidOperationException("Kunjungan rawat jalan tidak ditemukan atau sudah dibatalkan.");

    private Task<List<Row>> CariDenganKunciAsync(Db db, DateTime waktu, string kode) =>
        db.QueryAsync("""
            SELECT catatan FROM catatan_perawatan
            WHERE no_rawat=@p0 AND tanggal=@p1 AND jam=@p2 AND kd_dokter=@p3 FOR UPDATE
            """, noRawat, waktu.Date, waktu.TimeOfDay, kode);

    private async Task KunciCatatanAsync(Db db, CatatanDokter catatan, Row kunjungan)
    {
        if (catatan.Waktu is not { } waktu || string.IsNullOrEmpty(catatan.KodeDokter))
            throw new InvalidOperationException("Catatan lama tidak memiliki tanggal, jam, atau kode dokter yang lengkap.");
        if (!user.IsAdminUtama)
        {
            RawatJalanService.PastikanMilikSendiri(new(false, user.UserId), catatan.KodeDokter);
            if (RegistrasiService.MelewatiBatasEdit(waktu, kunjungan.Dt("sekarang")!.Value))
                throw new InvalidOperationException("Batas ubah atau hapus catatan adalah 48 jam.");
        }
        var rows = await CariDenganKunciAsync(db, waktu, catatan.KodeDokter);
        if (rows.Count != 1 || rows[0].S("catatan") != catatan.Isi)
            throw new InvalidOperationException("Catatan berubah atau kuncinya ganda. Muat ulang daftar.");
    }

    private static DateTime? BacaWaktu(Row row, string tanggal, string jam) =>
        row.Dt(tanggal) is { Year: >= 1900 } date && row[jam] is TimeSpan time ? date.Date + time : null;
}
