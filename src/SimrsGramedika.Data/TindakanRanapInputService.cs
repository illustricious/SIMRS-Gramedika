using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

public sealed record TarifTindakanRanap(string Kode, string Nama, string Kategori, double Total);

public sealed class TindakanRanapInputService(DatabaseSettings settings, RawatInapKey kamar, string noRm, AuthenticatedUser user)
{
    public const string Izin = "tindakan_ranap";

    public async Task<IReadOnlyList<TarifTindakanRanap>> CariAsync(TindakanRalanJenis jenis, string query)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var visit = await KunjunganAsync(db, false);
        var ruang = await KamarAsync(db, false);
        var rows = await db.QueryAsync(SqlTarif(jenis) + """
            AND (j.kd_jenis_prw LIKE @p3 OR j.nm_perawatan LIKE @p3 OR k.nm_kategori LIKE @p3)
            ORDER BY j.nm_perawatan,j.kd_jenis_prw LIMIT 500
            """, visit.S("kd_pj"), ruang.S("kd_bangsal"), ruang.S("kelas"), "%" + query.Trim() + "%");
        return rows.Select(r => new TarifTindakanRanap(r.S("kd_jenis_prw"), r.S("nm_perawatan"), r.S("nm_kategori"), r.D("total"))).ToList();
    }

    public async Task<(IReadOnlyList<PegawaiPilihan> Dokter, IReadOnlyList<PegawaiPilihan> Petugas)> PelaksanaAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var dokter = await db.QueryAsync("SELECT kd_dokter,nm_dokter FROM dokter ORDER BY nm_dokter,kd_dokter");
        var petugas = await db.QueryAsync("SELECT nip,nama FROM petugas ORDER BY nama,nip");
        return (dokter.Select(r => new PegawaiPilihan(r.S("kd_dokter"),r.S("nm_dokter"))).ToList(),
            petugas.Select(r => new PegawaiPilihan(r.S("nip"),r.S("nama"))).ToList());
    }

    public async Task SimpanAsync(TindakanRalanJenis jenis, DateTime waktu, string dokter, string petugas, IReadOnlyList<TarifTindakanRanap> pilihan)
    {
        PastikanIzin();
        if (pilihan.Count == 0 || pilihan.Select(t => t.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pilihan.Count)
            throw new ArgumentException("Pilih satu atau lebih tindakan yang berbeda.");
        waktu = waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        // Kasir dan operasi kamar mengunci baris registrasi yang sama terlebih dahulu.
        var visit = await KunjunganAsync(db, true);
        if (visit.S("stts") == "Batal" || visit.S("status_bayar") != "Belum Bayar"
            || await db.FirstAsync("SELECT no_rawat FROM billing WHERE no_rawat=@p0 LIMIT 1 FOR UPDATE", kamar.NoRawat) is not null
            || await db.FirstAsync("SELECT no_rawat FROM nota_inap WHERE no_rawat=@p0 LIMIT 1 FOR UPDATE", kamar.NoRawat) is not null)
            throw new InvalidOperationException("Registrasi batal atau tagihan sudah disimpan. Periksa di Kasir.");
        var ruang = await KamarAsync(db, true);
        var sekarang = (await db.FirstAsync("SELECT NOW() waktu"))!.Dt("waktu")!.Value;
        if (waktu < kamar.TanggalMasuk.ToDateTime(kamar.JamMasuk) || waktu > sekarang)
            throw new ArgumentException("Waktu tindakan harus antara waktu masuk kamar dan waktu server saat ini.");
        if (jenis != TindakanRalanJenis.Paramedis && await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0", dokter) != 1)
            throw new ArgumentException("Pilih dokter yang terdaftar.");
        if (jenis != TindakanRalanJenis.Dokter && await db.CountAsync("SELECT COUNT(*) FROM petugas WHERE nip=@p0", petugas) != 1)
            throw new ArgumentException("Pilih petugas yang terdaftar.");

        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun_ranap");
        var lines = new List<JurnalLine>();
        foreach (var tarif in pilihan.OrderBy(t => t.Kode, StringComparer.Ordinal))
        {
            var row = await db.FirstAsync(SqlTarif(jenis) + " AND j.kd_jenis_prw=@p3 FOR UPDATE",
                visit.S("kd_pj"), ruang.S("kd_bangsal"), ruang.S("kelas"), tarif.Kode);
            if (row is null || row.D("total") != tarif.Total)
                throw new InvalidOperationException($"Tarif {tarif.Kode} berubah atau tidak berlaku. Cari ulang tarif.");
            try { await SisipkanAsync(db, jenis, waktu, dokter, petugas, row); }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                throw new InvalidOperationException($"Tindakan {tarif.Kode} sudah tercatat untuk pelaksana dan waktu itu.", ex);
            }
            Pair(row.D("total"), "Suspen_Piutang_Tindakan_Ranap", "Tindakan_Ranap");
            if (jenis != TindakanRalanJenis.Paramedis) Jasa(row.D("tarif_tindakandr"), "Jasa_Medik_Dokter");
            if (jenis != TindakanRalanJenis.Dokter) Jasa(row.D("tarif_tindakanpr"), "Jasa_Medik_Paramedis");
            Jasa(row.D("kso"), "KSO");
            Jasa(row.D("material"), "Jasa_Sarana");
            Pair(row.D("bhp"), "HPP_BHP_Tindakan_Ranap", "Persediaan_BHP_Tindakan_Ranap");
            Jasa(row.D("menejemen"), "Jasa_Menejemen");
        }
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, kamar.NoRawat,
            $"TINDAKAN RAWAT INAP {kamar.NoRawat}, DIPOSTING OLEH {user.KodeLegacy}", lines);
        await db.CommitAsync();

        void Jasa(double nilai, string komponen) => Pair(nilai, $"Beban_{komponen}_Tindakan_Ranap", $"Utang_{komponen}_Tindakan_Ranap");
        void Pair(double nilai, string debit, string kredit)
        {
            if (!double.IsFinite(nilai) || nilai < 0) throw new InvalidOperationException("Komponen tarif tidak valid.");
            if (nilai == 0) return;
            lines.Add(new(akun.GetValueOrDefault(debit, ""), debit, nilai, 0));
            lines.Add(new(akun.GetValueOrDefault(kredit, ""), kredit, 0, nilai));
        }
    }

    private Task<int> SisipkanAsync(Db db, TindakanRalanJenis jenis, DateTime waktu, string dokter, string petugas, Row row)
    {
        var sql = jenis switch
        {
            TindakanRalanJenis.Dokter => """
                INSERT INTO rawat_inap_dr(no_rawat,kd_jenis_prw,kd_dokter,tgl_perawatan,jam_rawat,material,bhp,tarif_tindakandr,kso,menejemen,biaya_rawat)
                VALUES(@p0,@p1,@p2,@p4,@p5,@p6,@p7,@p8,@p10,@p11,@p12)
                """,
            TindakanRalanJenis.Paramedis => """
                INSERT INTO rawat_inap_pr(no_rawat,kd_jenis_prw,nip,tgl_perawatan,jam_rawat,material,bhp,tarif_tindakanpr,kso,menejemen,biaya_rawat)
                VALUES(@p0,@p1,@p3,@p4,@p5,@p6,@p7,@p9,@p10,@p11,@p12)
                """,
            TindakanRalanJenis.DokterParamedis => """
                INSERT INTO rawat_inap_drpr(no_rawat,kd_jenis_prw,kd_dokter,nip,tgl_perawatan,jam_rawat,material,bhp,tarif_tindakandr,tarif_tindakanpr,kso,menejemen,biaya_rawat)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12)
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(jenis))
        };
        return db.ExecAsync(sql, kamar.NoRawat, row.S("kd_jenis_prw"), dokter, petugas, waktu.Date, waktu.TimeOfDay,
            row.D("material"), row.D("bhp"), row.D("tarif_tindakandr"), row.D("tarif_tindakanpr"), row.D("kso"), row.D("menejemen"), row.D("total"));
    }

    private async Task<Row> KunjunganAsync(Db db, bool kunci) =>
        await db.FirstAsync("""
            SELECT kd_pj,stts,status_bayar FROM reg_periksa WHERE no_rawat=@p0 AND no_rkm_medis=@p1 AND status_lanjut='Ranap'
            """ + (kunci ? " FOR UPDATE" : ""), kamar.NoRawat, noRm)
        ?? throw new InvalidOperationException("Kunjungan rawat inap tidak ditemukan.");

    private async Task<Row> KamarAsync(Db db, bool kunci) =>
        await db.FirstAsync("""
            SELECT k.kd_bangsal,k.kelas FROM kamar_inap s JOIN kamar k ON k.kd_kamar=s.kd_kamar
            WHERE s.no_rawat=@p0 AND s.kd_kamar=@p1 AND s.tgl_masuk=@p2 AND s.jam_masuk=@p3 AND s.stts_pulang='-'
            """ + (kunci ? " FOR UPDATE" : ""), kamar.NoRawat, kamar.KodeKamar, kamar.TanggalMasuk.ToDateTime(TimeOnly.MinValue), kamar.JamMasuk.ToTimeSpan())
        ?? throw new InvalidOperationException("Segmen kamar sudah ditutup atau pasien telah pindah. Muat ulang daftar.");

    private static string SqlTarif(TindakanRalanJenis jenis)
    {
        var total = jenis switch
        {
            TindakanRalanJenis.Dokter => "total_byrdr",
            TindakanRalanJenis.Paramedis => "total_byrpr",
            TindakanRalanJenis.DokterParamedis => "total_byrdrpr",
            _ => throw new ArgumentOutOfRangeException(nameof(jenis))
        };
        return $"""
            SELECT j.kd_jenis_prw,j.nm_perawatan,k.nm_kategori,j.material,j.bhp,j.tarif_tindakandr,j.tarif_tindakanpr,j.kso,j.menejemen,j.{total} total
            FROM jns_perawatan_inap j JOIN kategori_perawatan k ON k.kd_kategori=j.kd_kategori
            WHERE j.status='1' AND j.{total}>0
                AND (COALESCE((SELECT cara_bayar_ranap FROM set_tarif LIMIT 1),'Yes')='No' OR j.kd_pj IN (@p0,'-'))
                AND (COALESCE((SELECT ruang_ranap FROM set_tarif LIMIT 1),'Yes')='No' OR j.kd_bangsal IN (@p1,'-'))
                AND (COALESCE((SELECT kelas_ranap FROM set_tarif LIMIT 1),'Yes')='No' OR j.kelas IN (@p2,'-'))

            """;
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak Tindakan Rawat Inap.");
    }
}
