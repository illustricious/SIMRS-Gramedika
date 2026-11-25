using System.Globalization;
using MySqlConnector;
using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data;

public enum JenisOrderRalan { Resep, LabPk, LabPa, LabMb, Radiologi }
public sealed record BarisOrderRalan(string Kode, string Nama, decimal Jumlah = 0, string Aturan = "", IReadOnlyList<int>? Template = null);
public sealed record BahanPaRalan(DateTime? Diambil, string Cara, string Lokasi, string Pengawet, string TempatSebelumnya,
    DateTime? TanggalSebelumnya, string NomorSebelumnya, string DiagnosisSebelumnya);

public sealed class OrderRalanService(DatabaseSettings settings, string noRawat, string noRm, AuthenticatedUser user, JenisOrderRalan jenis)
{
    private sealed record FormatOrder(string Izin, string Header, string Detail, string Prefix, int Digit, string Kategori = "", string? Template = null);
    private readonly FormatOrder _format = jenis switch
    {
        JenisOrderRalan.Resep => new("resep_dokter", "resep_obat", "resep_dokter", "", 4),
        JenisOrderRalan.LabPk => new("permintaan_lab", "permintaan_lab", "permintaan_pemeriksaan_lab", "PK", 4, "PK", "permintaan_detail_permintaan_lab"),
        JenisOrderRalan.LabPa => new("permintaan_lab", "permintaan_labpa", "permintaan_pemeriksaan_labpa", "PA", 4, "PA"),
        JenisOrderRalan.LabMb => new("permintaan_lab", "permintaan_labmb", "permintaan_pemeriksaan_labmb", "MB", 4, "MB", "permintaan_detail_permintaan_labmb"),
        JenisOrderRalan.Radiologi => new("permintaan_radiologi", "permintaan_radiologi", "permintaan_pemeriksaan_radiologi", "PR", 3),
        _ => throw new ArgumentOutOfRangeException(nameof(jenis))
    };

    private bool Resep => jenis == JenisOrderRalan.Resep;
    private string SyaratTarif
    {
        get
        {
            var lab = jenis != JenisOrderRalan.Radiologi;
            var bayar = lab ? "cara_bayar_lab" : "cara_bayar_radiologi";
            var kelas = lab ? "kelas_lab" : "kelas_radiologi";
            var kategori = lab ? "m.kategori=@p2" : "@p2=''";
            return $"""
                m.status='1' AND {kategori}
                AND (COALESCE((SELECT {bayar} FROM set_tarif LIMIT 1),'Yes')<>'Yes' OR m.kd_pj IN ('-',r.kd_pj))
                AND (COALESCE((SELECT {kelas} FROM set_tarif LIMIT 1),'Yes')<>'Yes' OR m.kelas IN ('-','Rawat Jalan'))
                """;
        }
    }

    public async Task<List<Row>> TemplateAsync(string kode)
    {
        PastikanIzin();
        if (_format.Template is null) return [];
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT id_template,pemeriksaan FROM template_laboratorium WHERE kd_jenis_prw=@p0 ORDER BY urut,id_template
            """, kode);
    }

    public async Task<IReadOnlyList<BarisOrderRalan>> CariAsync(string kata)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var keyword = "%" + kata.Trim() + "%";
        var rows = Resep
            ? await db.QueryAsync("""
                SELECT kode_brng kode,nama_brng nama FROM databarang
                WHERE status='1' AND (kode_brng LIKE @p0 OR nama_brng LIKE @p0) ORDER BY nama_brng,kode_brng LIMIT 100
                """, keyword)
            : await db.QueryAsync($"""
                SELECT m.kd_jenis_prw kode,m.nm_perawatan nama FROM {MasterPemeriksaan} m
                JOIN reg_periksa r ON r.no_rawat=@p0 AND r.no_rkm_medis=@p1
                WHERE {SyaratTarif} AND (m.kd_jenis_prw LIKE @p3 OR m.nm_perawatan LIKE @p3)
                ORDER BY m.nm_perawatan,m.kd_jenis_prw LIMIT 100
                """, noRawat, noRm, _format.Kategori, keyword);
        return rows.Select(r => new BarisOrderRalan(r.S("kode"), r.S("nama"))).ToList();
    }

    public async Task<List<Row>> RiwayatAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        if (Resep)
            return await db.QueryAsync("""
                SELECT h.no_resep nomor,h.tgl_peresepan tanggal,m.nama_brng nama,d.jml jumlah,d.aturan_pakai aturan,
                    IF(h.tgl_penyerahan>'0000-00-00','Diserahkan',IF(h.tgl_perawatan>'0000-00-00','Diverifikasi','Menunggu farmasi')) status
                FROM resep_obat h JOIN reg_periksa r ON r.no_rawat=h.no_rawat
                LEFT JOIN resep_dokter d ON d.no_resep=h.no_resep LEFT JOIN databarang m ON m.kode_brng=d.kode_brng
                WHERE h.no_rawat=@p0 AND r.no_rkm_medis=@p1 AND h.status='ralan' AND h.tgl_peresepan>'0000-00-00'
                ORDER BY h.no_resep DESC,m.nama_brng
                """, noRawat, noRm);
        return await db.QueryAsync($"""
            SELECT h.noorder nomor,h.tgl_permintaan tanggal,m.nm_perawatan nama,h.diagnosa_klinis diagnosa,
                h.informasi_tambahan informasi,IF(h.tgl_hasil>'0000-00-00','Hasil dicatat',IF(h.tgl_sampel>'0000-00-00','Diproses','Menunggu')) status
            FROM {_format.Header} h JOIN reg_periksa r ON r.no_rawat=h.no_rawat
            LEFT JOIN {_format.Detail} d ON d.noorder=h.noorder LEFT JOIN {MasterPemeriksaan} m ON m.kd_jenis_prw=d.kd_jenis_prw
            WHERE h.no_rawat=@p0 AND r.no_rkm_medis=@p1 AND h.status='ralan' ORDER BY h.noorder DESC,m.nm_perawatan
            """, noRawat, noRm);
    }

    public async Task<string> SimpanAsync(IReadOnlyList<BarisOrderRalan> baris, string diagnosa, string informasi, BahanPaRalan? bahan = null)
    {
        PastikanIzin();
        if (baris.Count == 0 || baris.Any(r => string.IsNullOrWhiteSpace(r.Kode)) ||
            baris.Select(r => r.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != baris.Count)
            throw new ArgumentException("Pilih paling sedikit satu item dan hindari kode ganda.");
        if (Resep && baris.Any(r => r.Jumlah <= 0 || string.IsNullOrWhiteSpace(r.Aturan) || r.Aturan.Length > 150))
            throw new ArgumentException("Isi jumlah positif dan aturan pakai untuk setiap obat.");
        if (!Resep && (diagnosa.Trim().Length is 0 or > 80 || informasi.Length > 60))
            throw new ArgumentException("Diagnosis klinis wajib diisi (maksimum 80 karakter). Informasi tambahan maksimum 60 karakter.");
        if (jenis == JenisOrderRalan.LabPa && (bahan is null || bahan.Cara.Length > 40 || bahan.Lokasi.Length > 40 ||
            bahan.Pengawet.Length > 40 || bahan.TempatSebelumnya.Length > 100 || bahan.NomorSebelumnya.Length > 20 || bahan.DiagnosisSebelumnya.Length > 100))
            throw new ArgumentException("Lengkapi informasi bahan PA sesuai panjang kolom.");

        await using var db = await Db.OpenAsync(settings);
        // Kolom waktu layanan Khanza memakai tanggal nol sampai unit penunjang memproses order.
        await db.ExecAsync("""
            SET SESSION sql_mode=TRIM(BOTH ',' FROM REPLACE(REPLACE(CONCAT(',',@@SESSION.sql_mode,','),
                ',NO_ZERO_DATE,',','),',NO_ZERO_IN_DATE,',','))
            """);
        for (int percobaan = 1; ; percobaan++)
        {
            await db.BeginAsync();
            try
            {
                var visit = await db.FirstAsync("""
                    SELECT kd_dokter,status_bayar,NOW() waktu FROM reg_periksa
                    WHERE no_rawat=@p0 AND no_rkm_medis=@p1 AND status_lanjut='Ralan' AND stts<>'Batal' FOR UPDATE
                    """, noRawat, noRm) ?? throw new InvalidOperationException("Kunjungan rawat jalan tidak aktif.");
                if (visit.S("status_bayar") != "Belum Bayar" ||
                    await db.CountAsync("SELECT COUNT(*) FROM billing WHERE no_rawat=@p0", noRawat) > 0 ||
                    await db.CountAsync("SELECT COUNT(*) FROM kamar_inap WHERE no_rawat=@p0", noRawat) > 0)
                    throw new InvalidOperationException("Kunjungan telah masuk billing atau rawat inap.");
                var dokter = user.IsAdminUtama ? visit.S("kd_dokter") : user.UserId;
                if (await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0", dokter) != 1)
                    throw new InvalidOperationException("Akun harus terhubung ke dokter yang terdaftar.");
                var waktu = visit.Dt("waktu") ?? throw new InvalidOperationException("Waktu server tidak tersedia.");
                var nomor = await NomorAsync(db, waktu);
                await SimpanHeaderAsync(db, nomor, waktu, dokter, diagnosa.Trim(), informasi.Trim());
                if (jenis == JenisOrderRalan.LabPa && bahan is not null)
                    await db.ExecAsync("""
                        UPDATE permintaan_labpa SET pengambilan_bahan=COALESCE(@p1,'0000-00-00'),diperoleh_dengan=@p2,
                        lokasi_jaringan=@p3,diawetkan_dengan=@p4,pernah_dilakukan_di=@p5,tanggal_pa_sebelumnya=COALESCE(@p6,'0000-00-00'),
                        nomor_pa_sebelumnya=@p7,diagnosa_pa_sebelumnya=@p8 WHERE noorder=@p0
                        """, nomor, bahan.Diambil?.Date, bahan.Cara, bahan.Lokasi, bahan.Pengawet, bahan.TempatSebelumnya,
                        bahan.TanggalSebelumnya?.Date, bahan.NomorSebelumnya, bahan.DiagnosisSebelumnya);
                foreach (var row in baris)
                    await SimpanBarisAsync(db, nomor, row);
                await db.CommitAsync();
                return nomor;
            }
            catch (MySqlException ex) when (percobaan < 5 && ex.Number is 1062 or 1213 or 1205)
            {
                await db.DisposeTxAsync();
            }
        }
    }

    private string MasterPemeriksaan => jenis == JenisOrderRalan.Radiologi ? "jns_perawatan_radiologi" : "jns_perawatan_lab";

    private async Task<string> NomorAsync(Db db, DateTime waktu)
    {
        var prefix = _format.Prefix + waktu.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var key = Resep ? "no_resep" : "noorder";
        var last = await db.CountAsync($"""
            SELECT COALESCE(MAX(CAST(RIGHT({key},{_format.Digit}) AS UNSIGNED)),0)
            FROM {_format.Header} WHERE {key} LIKE @p0
            """, prefix + "%");
        if (last >= (_format.Digit == 3 ? 999 : 9999))
            throw new InvalidOperationException("Nomor order untuk tanggal ini sudah penuh.");
        return prefix + (last + 1).ToString($"D{_format.Digit}", CultureInfo.InvariantCulture);
    }

    private Task<int> SimpanHeaderAsync(Db db, string nomor, DateTime waktu, string dokter, string diagnosa, string informasi)
    {
        var jam = waktu.TimeOfDay;
        return Resep
            ? db.ExecAsync("""
                INSERT INTO resep_obat(no_resep,tgl_perawatan,jam,no_rawat,kd_dokter,tgl_peresepan,jam_peresepan,status,tgl_penyerahan,jam_penyerahan)
                VALUES(@p0,'0000-00-00','00:00:00',@p1,@p2,@p3,@p4,'ralan','0000-00-00','00:00:00')
                """, nomor, noRawat, dokter, waktu.Date, jam)
            : db.ExecAsync($"""
                INSERT INTO {_format.Header}(noorder,no_rawat,tgl_permintaan,jam_permintaan,tgl_sampel,jam_sampel,tgl_hasil,jam_hasil,
                    dokter_perujuk,status,informasi_tambahan,diagnosa_klinis)
                VALUES(@p0,@p1,@p3,@p4,'0000-00-00','00:00:00','0000-00-00','00:00:00',@p2,'ralan',@p5,@p6)
                """, nomor, noRawat, dokter, waktu.Date, jam, informasi, diagnosa);
    }

    private async Task SimpanBarisAsync(Db db, string nomor, BarisOrderRalan row)
    {
        var affected = Resep
            ? await db.ExecAsync("""
                INSERT INTO resep_dokter(no_resep,kode_brng,jml,aturan_pakai)
                SELECT @p0,kode_brng,@p2,@p3 FROM databarang WHERE kode_brng=@p1 AND status='1'
                """, nomor, row.Kode, row.Jumlah, row.Aturan.Trim())
            : await db.ExecAsync($"""
                INSERT INTO {_format.Detail}(noorder,kd_jenis_prw,stts_bayar)
                SELECT @p3,m.kd_jenis_prw,'Belum' FROM {MasterPemeriksaan} m
                JOIN reg_periksa r ON r.no_rawat=@p0 AND r.no_rkm_medis=@p1
                WHERE {SyaratTarif} AND m.kd_jenis_prw=@p4
                """, noRawat, noRm, _format.Kategori, nomor, row.Kode);
        if (affected != 1) throw new InvalidOperationException($"Item {row.Kode} sudah tidak tersedia untuk kunjungan ini.");
        if (_format.Template is { } template && row.Template is not null)
        {
            foreach (var id in row.Template.Distinct())
            {
                var inserted = await db.ExecAsync($"""
                    INSERT INTO {template}(noorder,kd_jenis_prw,id_template,stts_bayar)
                    SELECT @p0,kd_jenis_prw,id_template,'Belum' FROM template_laboratorium WHERE kd_jenis_prw=@p1 AND id_template=@p2
                    """, nomor, row.Kode, id);
                if (inserted != 1) throw new InvalidOperationException("Rincian pemeriksaan lab sudah berubah. Pilih kembali item.");
            }
        }
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(_format.Izin)) throw new InvalidOperationException("Akun tidak memiliki hak mengakses alur ini.");
    }
}
