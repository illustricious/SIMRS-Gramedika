using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data;

public sealed class DiagnosaRalanService(DatabaseSettings settings, string noRawat, AuthenticatedUser user)
{
    private void Izin() { if (!user.HasPermission("diagnosa_pasien")) throw new InvalidOperationException("Akun tidak memiliki hak diagnosa pasien."); }

    public async Task<List<Row>> DaftarAsync(bool prosedur)
    {
        Izin();
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync(prosedur
            ? "SELECT p.kode AS kode,i.deskripsi_panjang AS nama,p.prioritas,p.jumlah AS keterangan FROM prosedur_pasien p LEFT JOIN icd9 i ON i.kode=p.kode WHERE p.no_rawat=@p0 AND p.status='Ralan' ORDER BY p.prioritas,p.kode"
            : "SELECT d.kd_penyakit AS kode,p.nm_penyakit AS nama,d.prioritas,d.status_penyakit AS keterangan FROM diagnosa_pasien d LEFT JOIN penyakit p ON p.kd_penyakit=d.kd_penyakit WHERE d.no_rawat=@p0 AND d.status='Ralan' ORDER BY d.prioritas,d.kd_penyakit", noRawat);
    }

    public async Task<List<Row>> CariAsync(bool prosedur, string kata)
    {
        Izin();
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync(prosedur
            ? "SELECT kode,deskripsi_panjang AS nama FROM icd9 WHERE validcode='1' AND (kode LIKE @p0 OR deskripsi_panjang LIKE @p0) ORDER BY kode LIMIT 100"
            : "SELECT kd_penyakit AS kode,nm_penyakit AS nama FROM penyakit WHERE validcode='1' AND (kd_penyakit LIKE @p0 OR nm_penyakit LIKE @p0) ORDER BY kd_penyakit LIMIT 100", "%" + kata.Trim() + "%");
    }

    public async Task TambahAsync(bool prosedur, string kode, int prioritas, string jumlah)
    {
        Izin();
        if (string.IsNullOrWhiteSpace(kode) || prioritas < 1 || prioritas > (prosedur ? 4 : 5)) throw new ArgumentException("Pilih kode dan prioritas yang valid.");
        if (prosedur && (!int.TryParse(jumlah, out var n) || n < 1 || n > 999)) throw new ArgumentException("Jumlah prosedur harus 1 sampai 999.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await db.FirstAsync("SELECT no_rkm_medis FROM reg_periksa WHERE no_rawat=@p0 AND status_lanjut='Ralan' AND stts<>'Batal' FOR UPDATE", noRawat)
            ?? throw new InvalidOperationException("Kunjungan tidak ditemukan atau dibatalkan.");
        var table = prosedur ? "prosedur_pasien" : "diagnosa_pasien";
        if (await db.CountAsync($"SELECT COUNT(*) FROM {table} WHERE no_rawat=@p0 AND status='Ralan' AND prioritas=@p1", noRawat, prioritas) != 0)
            throw new InvalidOperationException("Prioritas tersebut sudah digunakan.");
        var exists = await db.CountAsync(prosedur ? "SELECT COUNT(*) FROM icd9 WHERE kode=@p0 AND validcode='1'" : "SELECT COUNT(*) FROM penyakit WHERE kd_penyakit=@p0 AND validcode='1'", kode);
        if (exists != 1) throw new InvalidOperationException("Kode tidak ditemukan atau tidak aktif.");
        if (prosedur)
            await db.ExecAsync("INSERT INTO prosedur_pasien(no_rawat,kode,status,prioritas,jumlah) VALUES(@p0,@p1,'Ralan',@p2,@p3)", noRawat, kode, prioritas, jumlah);
        else
        {
            var lama = await db.CountAsync("SELECT COUNT(*) FROM diagnosa_pasien d JOIN reg_periksa r ON r.no_rawat=d.no_rawat WHERE r.no_rkm_medis=@p0 AND d.kd_penyakit=@p1", visit.S("no_rkm_medis"), kode);
            await db.ExecAsync("INSERT INTO diagnosa_pasien(no_rawat,kd_penyakit,status,prioritas,status_penyakit) VALUES(@p0,@p1,'Ralan',@p2,@p3)", noRawat, kode, prioritas, lama > 0 ? "Lama" : "Baru");
        }
        string[] columns = prosedur ? ["kd_prosedur_utama", "kd_prosedur_sekunder", "kd_prosedur_sekunder2", "kd_prosedur_sekunder3"] : ["kd_diagnosa_utama", "kd_diagnosa_sekunder", "kd_diagnosa_sekunder2", "kd_diagnosa_sekunder3", "kd_diagnosa_sekunder4"];
        if (!prosedur && kode.Length > 10 && await db.CountAsync("SELECT COUNT(*) FROM resume_pasien WHERE no_rawat=@p0", noRawat) > 0)
            throw new InvalidOperationException("Kode diagnosis terlalu panjang untuk resume pasien.");
        await db.ExecAsync($"UPDATE resume_pasien SET {columns[prioritas - 1]}=@p1 WHERE no_rawat=@p0", noRawat, kode);
        await db.CommitAsync();
    }

    public async Task HapusAsync(bool prosedur, string kode, int prioritas)
    {
        Izin();
        var columns = prosedur ? new[] { "kd_prosedur_utama", "kd_prosedur_sekunder", "kd_prosedur_sekunder2", "kd_prosedur_sekunder3" }
            : new[] { "kd_diagnosa_utama", "kd_diagnosa_sekunder", "kd_diagnosa_sekunder2", "kd_diagnosa_sekunder3", "kd_diagnosa_sekunder4" };
        if (string.IsNullOrWhiteSpace(kode) || prioritas < 1 || prioritas > columns.Length) throw new ArgumentException("Kode atau prioritas tidak valid.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        if (await db.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat=@p0 AND status_lanjut='Ralan' AND stts<>'Batal' FOR UPDATE", noRawat) is null)
            throw new InvalidOperationException("Kunjungan tidak ditemukan atau dibatalkan.");
        var sql = prosedur ? "DELETE FROM prosedur_pasien WHERE no_rawat=@p0 AND kode=@p1 AND status='Ralan' AND prioritas=@p2"
            : "DELETE FROM diagnosa_pasien WHERE no_rawat=@p0 AND kd_penyakit=@p1 AND status='Ralan' AND prioritas=@p2";
        if (await db.ExecAsync(sql, noRawat, kode, prioritas) != 1) throw new InvalidOperationException("Data berubah atau tidak ditemukan. Muat ulang daftar.");
        await db.ExecAsync($"UPDATE resume_pasien SET {columns[prioritas - 1]}='' WHERE no_rawat=@p0 AND {columns[prioritas - 1]}=@p1", noRawat, kode);
        await db.CommitAsync();
    }
}
