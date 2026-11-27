using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>Satu baris rujuk (DlgRujuk Khanza). NoRujuk kosong berarti dibuatkan nomor saat disimpan.</summary>
public sealed record RujukanKeluarData(
    string NoRujuk, DateTime Waktu, string RujukKe, string Diagnosa, string KodeDokter,
    string Kategori = "-", string Ambulance = "-", string Keterangan = "");

/// <summary>Isi Surat Rujukan (rptSuratRujukan Khanza).</summary>
public sealed record SuratRujukanData(
    string NoRujuk, DateTime? TanggalRujuk, string RujukKe, string Diagnosa, string Dokter, string NoRekamMedis,
    string Pasien, string Alamat, DateTime? TanggalRegistrasi, string Tindakan, string Terapi)
{
    /// <summary>ID penanda tangan pada QR.</summary>
    public string IdDokter { get; init; } = "";
}

public sealed class RujukanKeluarService(DatabaseSettings settings, AuthenticatedUser user)
{
    public const string Izin = "rujukan_keluar";

    /// <summary>Nilai enum kat_rujuk dan ambulance.</summary>
    public static readonly IReadOnlyList<string> Kategori = ["-", "Bedah", "Non Bedah", "Kebidanan", "Anak"];
    public static readonly IReadOnlyList<string> Ambulance = ["-", "AGD", "SENDIRI", "SWASTA"];

    /// <summary>Rujukan keluar milik satu kunjungan, yang terbaru di atas.</summary>
    public async Task<IReadOnlyList<RujukanKeluarData>> DaftarAsync(string noRawat)
    {
        Periksa(noRawat);
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT no_rujuk,tgl_rujuk,jam,rujuk_ke,keterangan_diagnosa,kd_dokter,kat_rujuk,ambulance,keterangan
            FROM rujuk WHERE no_rawat=@p0 ORDER BY tgl_rujuk DESC,jam DESC,no_rujuk DESC
            """, noRawat);
        return rows.Select(r => new RujukanKeluarData(r.S("no_rujuk"),
            (r.Dt("tgl_rujuk") ?? DateTime.Today).Date + (r["jam"] is TimeSpan t ? t : TimeSpan.Zero),
            r.S("rujuk_ke"), r.S("keterangan_diagnosa"), r.S("kd_dokter"), r.S("kat_rujuk"), r.S("ambulance"),
            r.S("keterangan"))).ToList();
    }

    /// <summary>
    /// Menambah rujukan, atau mengubah rujukan bernomor <paramref name="noRujukLama"/>. Mengembalikan nomor rujukan.
    /// Tabel rujuk tidak punya kunci unik, jadi penomoran dan pemeriksaan nomor dilakukan di dalam kunci bernama.
    /// </summary>
    public async Task<string> SimpanAsync(string noRawat, RujukanKeluarData data, string? noRujukLama = null)
    {
        Periksa(noRawat);
        var nomor = noRujukLama ?? data.NoRujuk.Trim();
        var tujuan = data.RujukKe.Trim();
        if (tujuan.Length == 0) throw new ArgumentException("Tempat rujuk wajib diisi.");
        if (tujuan.Length > 150) throw new ArgumentException("Tempat rujuk paling banyak 150 karakter.");
        if (string.IsNullOrWhiteSpace(data.KodeDokter)) throw new ArgumentException("Dokter wajib dipilih.");
        if (nomor.Length > 40) throw new ArgumentException("No. rujuk paling banyak 40 karakter.");
        if (!Kategori.Contains(data.Kategori)) throw new ArgumentException("Kategori rujuk tidak dikenal.");
        if (!Ambulance.Contains(data.Ambulance)) throw new ArgumentException("Pilihan ambulance tidak dikenal.");

        await using var db = await Db.OpenAsync(settings);
        await db.WithNamedLockAsync($"simrs.rujuk.{db.Conn.Database}", "Rujukan sedang disimpan oleh pengguna lain.", async () =>
        {
            object?[] isi =
            [
                noRawat, tujuan, data.Waktu.Date, data.Diagnosa.Trim(), data.KodeDokter, data.Kategori, data.Ambulance,
                data.Keterangan.Trim(), data.Waktu.TimeOfDay,
            ];
            if (noRujukLama is not null)
            {
                var berubah = await db.ExecAsync("""
                    UPDATE rujuk SET rujuk_ke=@p1,tgl_rujuk=@p2,keterangan_diagnosa=@p3,kd_dokter=@p4,kat_rujuk=@p5,
                        ambulance=@p6,keterangan=@p7,jam=@p8
                    WHERE no_rawat=@p0 AND no_rujuk=@p9
                    """, [.. isi, nomor]);
                if (berubah == 0) throw new InvalidOperationException("Rujukan ini sudah tidak ada. Muat ulang daftar.");
                return;
            }

            if (nomor.Length == 0)
                nomor = await db.AutoNomorAsync(
                    "SELECT IFNULL(MAX(CAST(SUBSTRING(no_rujuk,2) AS UNSIGNED)),0) FROM rujuk WHERE no_rujuk REGEXP '^R[0-9]+$'",
                    "R", 9);
            if (await db.CountAsync("SELECT COUNT(*) FROM rujuk WHERE no_rujuk=@p0", nomor) > 0)
                throw new InvalidOperationException($"No. rujuk {nomor} sudah dipakai.");
            await db.ExecAsync("""
                INSERT INTO rujuk(no_rawat,rujuk_ke,tgl_rujuk,keterangan_diagnosa,kd_dokter,kat_rujuk,ambulance,keterangan,jam,no_rujuk)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)
                """, [.. isi, nomor]);
        });
        return nomor;
    }

    /// <summary>
    /// Data cetak satu rujukan tersimpan. Seperti DlgRujuk, Terapi adalah nama obat yang diberikan dan Tindakan
    /// menyebut jenis layanan yang tercatat (rawat inap, laboratorium, radiologi, operasi).
    /// </summary>
    public async Task<SuratRujukanData> SuratAsync(string noRawat, string noRujuk)
    {
        Periksa(noRawat);
        await using var db = await Db.OpenAsync(settings);
        var r = await db.FirstAsync("""
            SELECT rujuk.no_rujuk,rujuk.tgl_rujuk,rujuk.rujuk_ke,rujuk.keterangan_diagnosa,rujuk.kd_dokter,dokter.nm_dokter,
                   reg_periksa.no_rkm_medis,reg_periksa.tgl_registrasi,pasien.nm_pasien,pasien.alamat
            FROM rujuk
            JOIN reg_periksa ON reg_periksa.no_rawat=rujuk.no_rawat
            JOIN pasien ON pasien.no_rkm_medis=reg_periksa.no_rkm_medis
            JOIN dokter ON dokter.kd_dokter=rujuk.kd_dokter
            WHERE rujuk.no_rawat=@p0 AND rujuk.no_rujuk=@p1
            """, noRawat, noRujuk) ?? throw new InvalidOperationException("Data rujukan sudah tidak tersedia.");

        var obat = await db.QueryAsync("""
            SELECT databarang.nama_brng FROM detail_pemberian_obat
            JOIN databarang ON databarang.kode_brng=detail_pemberian_obat.kode_brng
            WHERE detail_pemberian_obat.no_rawat=@p0 GROUP BY databarang.nama_brng ORDER BY databarang.nama_brng
            """, noRawat);
        var tindakan = new List<string>();
        foreach (var (tabel, nama) in new[]
        {
            ("rawat_inap_dr", "rawat inap"), ("periksa_lab", "pemeriksaan laboratorium"),
            ("periksa_radiologi", "pemeriksaan radiologi"), ("operasi", "operasi"),
        })
            if (await db.CountAsync($"SELECT COUNT(*) FROM {tabel} WHERE no_rawat=@p0", noRawat) > 0) tindakan.Add(nama);

        return new(r.S("no_rujuk"), r.Dt("tgl_rujuk"), r.S("rujuk_ke"), r.S("keterangan_diagnosa"), r.S("nm_dokter"),
            r.S("no_rkm_medis"), r.S("nm_pasien"), r.S("alamat"), r.Dt("tgl_registrasi"),
            string.Join(", ", tindakan), string.Join(", ", obat.Select(o => o.S("nama_brng"))))
        {
            IdDokter = await TandaTanganSurat.IdAsync(db, r.S("kd_dokter")),
        };
    }

    private void Periksa(string noRawat)
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Hak akses Rujukan Keluar diperlukan.");
        if (string.IsNullOrWhiteSpace(noRawat)) throw new ArgumentException("Pilih kunjungan terlebih dahulu.");
    }
}
