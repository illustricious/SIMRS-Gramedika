using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data;

public sealed record IsiResumeRalan(string Keluhan, string Perjalanan, string Penunjang, string Laborat, string Kondisi, string Obat);
public sealed record ResumeRalan(string Dokter, IsiResumeRalan Isi);
public sealed record RingkasanKodeResume(string Kode, string Nama, int Prioritas, bool Prosedur);

public sealed class ResumeRalanService(DatabaseSettings settings, string noRawat, string noRm, AuthenticatedUser user)
{
    private const string Select = """
        SELECT s.kd_dokter,s.keluhan_utama,s.jalannya_penyakit,s.pemeriksaan_penunjang,s.hasil_laborat,s.kondisi_pulang,s.obat_pulang
        FROM resume_pasien s JOIN reg_periksa r ON r.no_rawat=s.no_rawat WHERE s.no_rawat=@p0 AND r.no_rkm_medis=@p1
        """;
    private static readonly string[] KolomDiagnosa = ["diagnosa_utama", "diagnosa_sekunder", "diagnosa_sekunder2", "diagnosa_sekunder3", "diagnosa_sekunder4"];
    private static readonly string[] KolomProsedur = ["prosedur_utama", "prosedur_sekunder", "prosedur_sekunder2", "prosedur_sekunder3"];

    public async Task<(ResumeRalan? Resume, IReadOnlyList<RingkasanKodeResume> Kode)> MuatAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        if (await db.CountAsync("SELECT COUNT(*) FROM reg_periksa WHERE no_rawat=@p0 AND no_rkm_medis=@p1", noRawat, noRm) != 1)
            throw new InvalidOperationException("Kunjungan tidak ditemukan.");
        return (Baca(await db.FirstAsync(Select, noRawat, noRm)), await KodeAsync(db));
    }

    public async Task SimpanAsync(IsiResumeRalan data, ResumeRalan? sebelumnya)
    {
        PastikanIzin();
        if (string.IsNullOrWhiteSpace(data.Keluhan) || data.Kondisi is not ("Hidup" or "Meninggal"))
            throw new ArgumentException("Keluhan utama dan kondisi pulang wajib diisi.");
        string[] narasi = [data.Keluhan, data.Perjalanan, data.Penunjang, data.Laborat, data.Obat];
        if (narasi.Any(text => System.Text.Encoding.UTF8.GetByteCount(text) > 65000))
            throw new ArgumentException("Salah satu narasi melebihi batas penyimpanan.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await db.FirstAsync("""
            SELECT kd_dokter FROM reg_periksa WHERE no_rawat=@p0 AND no_rkm_medis=@p1
                AND status_lanjut='Ralan' AND stts<>'Batal' FOR UPDATE
            """, noRawat, noRm) ?? throw new InvalidOperationException("Kunjungan rawat jalan tidak aktif.");
        var tersimpan = Baca(await db.FirstAsync(Select + " FOR UPDATE", noRawat, noRm));
        if (tersimpan != sebelumnya) throw new InvalidOperationException("Resume sudah berubah. Muat ulang sebelum menyimpan.");
        if (tersimpan is not null)
        {
            if (!user.IsAdminUtama && !user.UserId.Equals(tersimpan.Dokter, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Resume hanya dapat diubah oleh dokter yang mencatatnya atau Admin Utama.");
            await db.ExecAsync("""
                UPDATE resume_pasien SET keluhan_utama=@p1,jalannya_penyakit=@p2,pemeriksaan_penunjang=@p3,
                    hasil_laborat=@p4,kondisi_pulang=@p5,obat_pulang=@p6 WHERE no_rawat=@p0
                """, noRawat, data.Keluhan, data.Perjalanan, data.Penunjang, data.Laborat, data.Kondisi, data.Obat);
        }
        else
        {
            var dokter = user.IsAdminUtama ? visit.S("kd_dokter") : user.UserId;
            if (await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0", dokter) != 1)
                throw new InvalidOperationException("Akun harus terhubung ke dokter yang terdaftar.");
            var codes = await KodeAsync(db);
            List<string> columns = ["no_rawat", "kd_dokter", "keluhan_utama", "jalannya_penyakit", "pemeriksaan_penunjang", "hasil_laborat", "kondisi_pulang", "obat_pulang"];
            List<object?> values = [noRawat, dokter, data.Keluhan, data.Perjalanan, data.Penunjang, data.Laborat, data.Kondisi, data.Obat];
            for (var index = 0; index < KolomDiagnosa.Length + KolomProsedur.Length; index++)
            {
                var prosedur = index >= KolomDiagnosa.Length;
                var slot = prosedur ? index - KolomDiagnosa.Length : index;
                var column = (prosedur ? KolomProsedur : KolomDiagnosa)[slot];
                var code = codes.FirstOrDefault(c => c.Prosedur == prosedur && c.Prioritas == slot + 1);
                if (code?.Kode.Length > (prosedur ? 8 : 10))
                    throw new InvalidOperationException($"Kode {code.Kode} terlalu panjang untuk kolom resume.");
                columns.Add(column);
                columns.Add("kd_" + column);
                values.Add(code is null ? "" : code.Nama[..Math.Min(code.Nama.Length, 80)]);
                values.Add(code?.Kode ?? "");
            }
            var placeholders = string.Join(',', columns.Select((_, index) => $"@p{index}"));
            await db.ExecAsync($"INSERT INTO resume_pasien({string.Join(',', columns)}) VALUES({placeholders})", values.ToArray());
        }
        await db.CommitAsync();
    }

    private async Task<IReadOnlyList<RingkasanKodeResume>> KodeAsync(Db db)
    {
        var rows = await db.QueryAsync("""
            SELECT d.kd_penyakit kode,m.nm_penyakit nama,d.prioritas,0 prosedur
            FROM diagnosa_pasien d LEFT JOIN penyakit m ON m.kd_penyakit=d.kd_penyakit
            WHERE d.no_rawat=@p0 AND d.status='Ralan'
            UNION ALL
            SELECT d.kode,m.deskripsi_panjang,d.prioritas,1
            FROM prosedur_pasien d LEFT JOIN icd9 m ON m.kode=d.kode WHERE d.no_rawat=@p0 AND d.status='Ralan'
            ORDER BY prosedur,prioritas,kode
            """, noRawat);
        return rows.Select(row => new RingkasanKodeResume(row.S("kode"), row.S("nama"), (int)row.D("prioritas"), row.D("prosedur") == 1)).ToList();
    }

    private static ResumeRalan? Baca(Row? row) => row is null ? null : new(row.S("kd_dokter"), new(
        row.S("keluhan_utama"), row.S("jalannya_penyakit"), row.S("pemeriksaan_penunjang"), row.S("hasil_laborat"),
        row.S("kondisi_pulang"), row.S("obat_pulang")));

    private void PastikanIzin()
    {
        if (!user.HasPermission("data_resume_pasien")) throw new InvalidOperationException("Akun tidak memiliki hak mengakses resume pasien.");
    }
}
