using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data;

public sealed record IsianResumePulang(string Kolom, string Label, int Max, string[]? Pilihan = null);
public sealed record ResumePulangRanap(string Dokter, IReadOnlyList<string> Nilai, DateTime? Kontrol, IReadOnlyList<string> KodeKlinis);

public sealed class ResumePulangRanapService(DatabaseSettings settings, string noRawat, string noRm, AuthenticatedUser user)
{
    public const string Izin = "data_resume_pasien";
    public static IReadOnlyList<IsianResumePulang> Isian { get; } = Array.AsReadOnly<IsianResumePulang>([
        new("diagnosa_awal","Diagnosis awal",100), new("alasan","Alasan dirawat",100),
        new("keluhan_utama","Keluhan utama",16000), new("pemeriksaan_fisik","Pemeriksaan fisik",16000),
        new("jalannya_penyakit","Perjalanan penyakit",16000), new("pemeriksaan_penunjang","Pemeriksaan penunjang",16000),
        new("hasil_laborat","Hasil laboratorium",16000), new("tindakan_dan_operasi","Tindakan dan operasi",16000),
        new("obat_di_rs","Obat selama dirawat",16000), new("alergi","Alergi",100),
        new("diet","Diet",16000), new("lab_belum","Hasil lab yang masih ditunggu",16000), new("edukasi","Edukasi pulang",16000),
        new("cara_keluar","Cara keluar",50,["Atas Izin Dokter","Pindah RS","Pulang Atas Permintaan Sendiri","Lainnya"]),
        new("ket_keluar","Keterangan cara keluar",50),
        new("keadaan","Keadaan saat keluar",50,["Membaik","Sembuh","Keadaan Khusus","Meninggal"]),
        new("ket_keadaan","Keterangan keadaan",50),
        new("dilanjutkan","Tindak lanjut",50,["Kembali Ke RS","RS Lain","Dokter Luar","Puskesmes","Lainnya"]),
        new("ket_dilanjutkan","Keterangan tindak lanjut",50), new("obat_pulang","Obat pulang dan aturan pakai",16000)
    ]);
    private static readonly string[] KodeDiagnosa = ["diagnosa_utama","diagnosa_sekunder","diagnosa_sekunder2","diagnosa_sekunder3","diagnosa_sekunder4"];
    private static readonly string[] KodeProsedur = ["prosedur_utama","prosedur_sekunder","prosedur_sekunder2","prosedur_sekunder3"];
    private static readonly string SqlBaca = $"""
        SELECT s.kd_dokter,s.kontrol,{string.Join(',', Isian.Select(x => "s." + x.Kolom))},
            {string.Join(',', KodeDiagnosa.Concat(KodeProsedur).SelectMany(col => new[] { "s." + col, "s.kd_" + col }))}
        FROM resume_pasien_ranap s JOIN reg_periksa r ON r.no_rawat=s.no_rawat WHERE s.no_rawat=@p0 AND r.no_rkm_medis=@p1
        """;

    public async Task<ResumePulangRanap?> MuatAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        return Baca(await db.FirstAsync(SqlBaca, noRawat, noRm));
    }

    public async Task SimpanAsync(IReadOnlyList<string> nilai, DateTime? kontrol, ResumePulangRanap? snapshot)
    {
        PastikanIzin();
        if (nilai.Count != Isian.Count) throw new ArgumentException("Isian resume tidak lengkap.");
        for (int i = 0; i < Isian.Count; i++)
        {
            var field = Isian[i];
            if (nilai[i].Length > field.Max || (field.Pilihan is { } allowed && !allowed.Contains(nilai[i])))
                throw new ArgumentException($"Isi {field.Label} sesuai pilihan atau batas isian.");
        }
        if (string.IsNullOrWhiteSpace(nilai[2])) throw new ArgumentException("Keluhan utama wajib diisi.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await db.FirstAsync("""
            SELECT kd_dokter FROM reg_periksa WHERE no_rawat=@p0 AND no_rkm_medis=@p1 AND status_lanjut='Ranap' AND stts<>'Batal' FOR UPDATE
            """, noRawat, noRm) ?? throw new InvalidOperationException("Kunjungan rawat inap tidak ditemukan.");
        var saved = Baca(await db.FirstAsync(SqlBaca + " FOR UPDATE", noRawat, noRm));
        if (saved?.Dokter != snapshot?.Dokter || saved?.Kontrol != snapshot?.Kontrol ||
            (saved is not null && (snapshot is null || !saved.Nilai.SequenceEqual(snapshot.Nilai))))
            throw new InvalidOperationException("Resume sudah berubah. Muat ulang sebelum menyimpan.");
        var dokter = saved?.Dokter ?? (user.IsAdminUtama ? visit.S("kd_dokter") : user.UserId);
        if ((!user.IsAdminUtama && !string.Equals(dokter, user.UserId, StringComparison.OrdinalIgnoreCase)) ||
            await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0", dokter) != 1)
            throw new InvalidOperationException("Resume hanya dapat disimpan oleh dokter penulis atau Admin Utama.");
        List<string> cols = ["no_rawat","kd_dokter","kontrol", .. Isian.Select(f => f.Kolom)];
        List<object?> args = [noRawat, dokter, kontrol, .. nilai];
        if (saved is null)
        {
            var codes = await db.QueryAsync("""
                SELECT d.kd_penyakit kode, LEFT(m.nm_penyakit,80) nama,d.prioritas,0 prosedur
                FROM diagnosa_pasien d JOIN penyakit m ON m.kd_penyakit=d.kd_penyakit WHERE d.no_rawat=@p0 AND d.status='Ranap'
                UNION ALL
                SELECT d.kode,LEFT(m.deskripsi_panjang,80),d.prioritas,1
                FROM prosedur_pasien d JOIN icd9 m ON m.kode=d.kode WHERE d.no_rawat=@p0 AND d.status='Ranap'
                ORDER BY prosedur,prioritas,kode
                """, noRawat);
            TambahKode(KodeDiagnosa, 0, 10);
            TambahKode(KodeProsedur, 1, 8);
            await db.ExecAsync($"INSERT INTO resume_pasien_ranap({string.Join(',', cols)}) VALUES({string.Join(',', cols.Select((_, i) => "@p" + i))})", args.ToArray());

            void TambahKode(string[] columns, int prosedur, int max)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    var row = codes.FirstOrDefault(x => x.D("prosedur") == prosedur && x.D("prioritas") == i + 1);
                    var code = row?.S("kode") ?? "";
                    if (code.Length > max) throw new InvalidOperationException("Kode diagnosis atau prosedur terlalu panjang untuk resume.");
                    cols.Add(columns[i]);
                    args.Add(row?.S("nama") ?? "");
                    cols.Add("kd_" + columns[i]);
                    args.Add(code);
                }
            }
        }
        else
        {
            var edits = string.Join(',', cols.Select((name, i) => $"{name}=@p{i}").Skip(2));
            await db.ExecAsync($"UPDATE resume_pasien_ranap SET {edits} WHERE no_rawat=@p0", args.ToArray());
        }
        await db.CommitAsync();
    }

    private static ResumePulangRanap? Baca(Row? row) => row is null ? null :
        new(row.S("kd_dokter"), Isian.Select(x => row.S(x.Kolom)).ToArray(), row.Dt("kontrol"),
            KodeDiagnosa.Concat(KodeProsedur).Select(col => $"{row.S("kd_" + col)} {row.S(col)}".Trim())
                .Where(value => value.Length > 0).ToArray());

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak resume pasien.");
    }
}
