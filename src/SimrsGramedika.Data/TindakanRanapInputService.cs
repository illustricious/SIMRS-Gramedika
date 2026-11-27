using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

public sealed record TarifTindakanRanap(string Kode, string Nama, string Kategori, double Total);
/// <summary>Isi satu baris rawat_inap_dr/pr/drpr seperti dibaca terakhir; dipakai untuk menolak perubahan atas data basi.</summary>
public sealed record TindakanRanapSnapshot(TindakanRalanJenis Jenis, string NoRawat, string Kode, string Dokter, string Petugas,
    DateTime Waktu, double Biaya, double TarifDokter, double TarifPetugas, double Material, double Bhp, double Kso, double Menejemen);
public sealed record BarisTindakanRanap(TindakanRanapSnapshot Isi, string Nama, string NamaDokter, string NamaPetugas);

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

    /// <summary>Seluruh tindakan rawat inap kunjungan ini, termasuk yang dicatat saat pasien di kamar sebelumnya.</summary>
    public async Task<IReadOnlyList<BarisTindakanRanap>> DaftarAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        await KunjunganAsync(db, false);
        var hasil = new List<BarisTindakanRanap>();
        foreach (var jenis in Enum.GetValues<TindakanRalanJenis>())
        {
            var dokter = jenis != TindakanRalanJenis.Paramedis;
            var petugas = jenis != TindakanRalanJenis.Dokter;
            var rows = await db.QueryAsync($"""
                SELECT r.*,j.nm_perawatan{(dokter ? ",d.nm_dokter" : "")}{(petugas ? ",p.nama nama_petugas" : "")}
                FROM {Tabel(jenis).Nama} r JOIN jns_perawatan_inap j ON j.kd_jenis_prw=r.kd_jenis_prw
                {(dokter ? "LEFT JOIN dokter d ON d.kd_dokter=r.kd_dokter" : "")}
                {(petugas ? "LEFT JOIN petugas p ON p.nip=r.nip" : "")}
                WHERE r.no_rawat=@p0
                """, kamar.NoRawat);
            hasil.AddRange(rows.Select(r => new BarisTindakanRanap(Snapshot(jenis, r), r.S("nm_perawatan"),
                dokter ? r.S("nm_dokter") : "", petugas ? r.S("nama_petugas") : "")));
        }
        return hasil.OrderByDescending(b => b.Isi.Waktu).ThenBy(b => b.Nama, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Hapus tindakan beserta jurnal pembaliknya dalam satu transaksi, mengikuti DlgRawatInap:
    /// ditolak bila sudah ditagih, dan selain Admin Utama dibatasi 2 x 24 jam.
    /// </summary>
    public async Task HapusAsync(IReadOnlyList<TindakanRanapSnapshot> pilihan)
    {
        PastikanIzin();
        if (pilihan.Count == 0 || pilihan.Distinct().Count() != pilihan.Count)
            throw new ArgumentException("Pilih satu atau lebih tindakan yang akan dihapus.");
        if (pilihan.Any(t => t.NoRawat != kamar.NoRawat)) throw new InvalidOperationException("Tindakan berasal dari kunjungan lain.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunjunganBelumDitagihAsync(db);
        var sekarang = (await db.FirstAsync("SELECT NOW() waktu"))!.Dt("waktu")!.Value;
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun_ranap");
        var lines = new List<JurnalLine>();
        foreach (var asal in pilihan.OrderBy(t => t.Jenis).ThenBy(t => t.Kode, StringComparer.Ordinal).ThenBy(t => t.Waktu))
        {
            PastikanBatasWaktu(asal.Waktu, sekarang, "hapus");
            await PastikanTidakBerubahAsync(db, asal);
            await HapusBarisAsync(db, asal);
            TambahJurnal(lines, akun, asal, balik: true);
        }
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, kamar.NoRawat,
            $"PEMBATALAN TINDAKAN RAWAT INAP {kamar.NoRawat}, DIPOSTING OLEH {user.KodeLegacy}", lines);
        await db.CommitAsync();
    }

    /// <summary>
    /// Ubah waktu atau pelaksana, dan bila <paramref name="tarif"/> diisi ganti tindakannya memakai tarif master untuk kamar
    /// aktif. Tanpa tarif baru, nilai rupiah yang sudah tercatat dipertahankan sehingga pasien yang sudah pulang tetap bisa dikoreksi.
    /// </summary>
    public async Task UbahAsync(TindakanRanapSnapshot asal, DateTime waktu, string dokter, string petugas, TarifTindakanRanap? tarif = null)
    {
        PastikanIzin();
        if (asal.NoRawat != kamar.NoRawat) throw new InvalidOperationException("Tindakan berasal dari kunjungan lain.");
        waktu = waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await KunjunganBelumDitagihAsync(db);
        var sekarang = (await db.FirstAsync("SELECT NOW() waktu"))!.Dt("waktu")!.Value;
        PastikanBatasWaktu(asal.Waktu, sekarang, "ubah");
        await PastikanTidakBerubahAsync(db, asal);
        waktu = await KunciTanggal.WaktuAsync(db, KunciTanggal.Aktif(settings, user.IsAdminUtama), waktu, asal.Waktu);
        if (waktu > sekarang) throw new ArgumentException("Waktu tindakan tidak boleh melewati waktu server saat ini.");
        if (!user.IsAdminUtama)
        {
            if (visit.Dt("tgl_registrasi") is not { } tanggal || visit["jam_reg"] is not TimeSpan jam)
                throw new InvalidOperationException("Waktu registrasi tidak valid.");
            RawatJalanService.PastikanTidakSebelumRegistrasi(tanggal.Date + jam, waktu);
        }
        dokter = asal.Jenis == TindakanRalanJenis.Paramedis ? "" : dokter;
        petugas = asal.Jenis == TindakanRalanJenis.Dokter ? "" : petugas;
        await PastikanPelaksanaAsync(db, asal.Jenis, dokter, petugas);

        var baru = asal with { Dokter = dokter, Petugas = petugas, Waktu = waktu };
        if (tarif is not null)
        {
            var ruang = await KamarAsync(db, true);
            var row = await db.FirstAsync(SqlTarif(asal.Jenis) + " AND j.kd_jenis_prw=@p3 FOR UPDATE",
                visit.S("kd_pj"), ruang.S("kd_bangsal"), ruang.S("kelas"), tarif.Kode);
            if (row is null || row.D("total") != tarif.Total)
                throw new InvalidOperationException($"Tarif {tarif.Kode} berubah atau tidak berlaku. Cari ulang tarif.");
            baru = baru with
            {
                Kode = row.S("kd_jenis_prw"), Biaya = row.D("total"),
                TarifDokter = asal.Jenis == TindakanRalanJenis.Paramedis ? 0 : row.D("tarif_tindakandr"),
                TarifPetugas = asal.Jenis == TindakanRalanJenis.Dokter ? 0 : row.D("tarif_tindakanpr"),
                Material = row.D("material"), Bhp = row.D("bhp"), Kso = row.D("kso"), Menejemen = row.D("menejemen"),
            };
        }
        if (baru == asal) throw new InvalidOperationException("Tidak ada perubahan untuk disimpan.");

        await HapusBarisAsync(db, asal);
        try { await SisipkanAsync(db, baru); }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new InvalidOperationException("Tindakan untuk pelaksana dan waktu tersebut sudah ada.", ex);
        }
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun_ranap");
        var lines = new List<JurnalLine>();
        TambahJurnal(lines, akun, asal, balik: true);
        TambahJurnal(lines, akun, baru, balik: false);
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, kamar.NoRawat,
            $"PERUBAHAN TINDAKAN RAWAT INAP {kamar.NoRawat}, DIPOSTING OLEH {user.KodeLegacy}", lines);
        await db.CommitAsync();
    }

    public async Task SimpanAsync(TindakanRalanJenis jenis, DateTime waktu, string dokter, string petugas, IReadOnlyList<TarifTindakanRanap> pilihan)
    {
        PastikanIzin();
        if (pilihan.Count == 0 || pilihan.Select(t => t.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pilihan.Count)
            throw new ArgumentException("Pilih satu atau lebih tindakan yang berbeda.");
        waktu = waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await KunjunganBelumDitagihAsync(db);
        var ruang = await KamarAsync(db, true);
        waktu = await KunciTanggal.WaktuAsync(db, KunciTanggal.Aktif(settings, user.IsAdminUtama), waktu);
        var sekarang = (await db.FirstAsync("SELECT NOW() waktu"))!.Dt("waktu")!.Value;
        if (waktu < kamar.TanggalMasuk.ToDateTime(kamar.JamMasuk) || waktu > sekarang)
            throw new ArgumentException("Waktu tindakan harus antara waktu masuk kamar dan waktu server saat ini.");
        await PastikanPelaksanaAsync(db, jenis, dokter, petugas);

        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun_ranap");
        var lines = new List<JurnalLine>();
        foreach (var tarif in pilihan.OrderBy(t => t.Kode, StringComparer.Ordinal))
        {
            var row = await db.FirstAsync(SqlTarif(jenis) + " AND j.kd_jenis_prw=@p3 FOR UPDATE",
                visit.S("kd_pj"), ruang.S("kd_bangsal"), ruang.S("kelas"), tarif.Kode);
            if (row is null || row.D("total") != tarif.Total)
                throw new InvalidOperationException($"Tarif {tarif.Kode} berubah atau tidak berlaku. Cari ulang tarif.");
            var baru = new TindakanRanapSnapshot(jenis, kamar.NoRawat, row.S("kd_jenis_prw"), dokter, petugas, waktu, row.D("total"),
                row.D("tarif_tindakandr"), row.D("tarif_tindakanpr"), row.D("material"), row.D("bhp"), row.D("kso"), row.D("menejemen"));
            try { await SisipkanAsync(db, baru); }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                throw new InvalidOperationException($"Tindakan {tarif.Kode} sudah tercatat untuk pelaksana dan waktu itu.", ex);
            }
            TambahJurnal(lines, akun, baru, balik: false);
        }
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, kamar.NoRawat,
            $"TINDAKAN RAWAT INAP {kamar.NoRawat}, DIPOSTING OLEH {user.KodeLegacy}", lines);
        await db.CommitAsync();
    }

    private static void TambahJurnal(List<JurnalLine> lines, Dictionary<string, string> akun, TindakanRanapSnapshot t, bool balik)
    {
        Pair(t.Biaya, "Suspen_Piutang_Tindakan_Ranap", "Tindakan_Ranap");
        if (t.Jenis != TindakanRalanJenis.Paramedis) Jasa(t.TarifDokter, "Jasa_Medik_Dokter");
        if (t.Jenis != TindakanRalanJenis.Dokter) Jasa(t.TarifPetugas, "Jasa_Medik_Paramedis");
        Jasa(t.Kso, "KSO");
        Jasa(t.Material, "Jasa_Sarana");
        Pair(t.Bhp, "HPP_BHP_Tindakan_Ranap", "Persediaan_BHP_Tindakan_Ranap");
        Jasa(t.Menejemen, "Jasa_Menejemen");

        void Jasa(double nilai, string komponen) => Pair(nilai, $"Beban_{komponen}_Tindakan_Ranap", $"Utang_{komponen}_Tindakan_Ranap");
        void Pair(double nilai, string debit, string kredit)
        {
            if (!double.IsFinite(nilai) || nilai < 0) throw new InvalidOperationException("Komponen tarif tidak valid.");
            if (nilai == 0) return;
            lines.Add(new(akun.GetValueOrDefault(debit, ""), debit, balik ? 0 : nilai, balik ? nilai : 0));
            lines.Add(new(akun.GetValueOrDefault(kredit, ""), kredit, balik ? nilai : 0, balik ? 0 : nilai));
        }
    }

    private static (string Nama, string Syarat) Tabel(TindakanRalanJenis jenis) => jenis switch
    {
        TindakanRalanJenis.Dokter => ("rawat_inap_dr", "AND kd_dokter=@p4"),
        TindakanRalanJenis.Paramedis => ("rawat_inap_pr", "AND nip=@p5"),
        TindakanRalanJenis.DokterParamedis => ("rawat_inap_drpr", "AND kd_dokter=@p4 AND nip=@p5"),
        _ => throw new ArgumentOutOfRangeException(nameof(jenis)),
    };

    private static TindakanRanapSnapshot Snapshot(TindakanRalanJenis jenis, Row r) => new(jenis, r.S("no_rawat"), r.S("kd_jenis_prw"),
        jenis == TindakanRalanJenis.Paramedis ? "" : r.S("kd_dokter"), jenis == TindakanRalanJenis.Dokter ? "" : r.S("nip"),
        r.Dt("tgl_perawatan")!.Value.Date + (TimeSpan)r["jam_rawat"]!, r.D("biaya_rawat"),
        jenis == TindakanRalanJenis.Paramedis ? 0 : r.D("tarif_tindakandr"), jenis == TindakanRalanJenis.Dokter ? 0 : r.D("tarif_tindakanpr"),
        r.D("material"), r.D("bhp"), r.D("kso"), r.D("menejemen"));

    private static object?[] Kunci(TindakanRanapSnapshot t) => [t.NoRawat, t.Kode, t.Waktu.Date, t.Waktu.TimeOfDay, t.Dokter, t.Petugas];

    private static async Task PastikanTidakBerubahAsync(Db db, TindakanRanapSnapshot asal)
    {
        var (tabel, syarat) = Tabel(asal.Jenis);
        var row = await db.FirstAsync(
            $"SELECT * FROM {tabel} WHERE no_rawat=@p0 AND kd_jenis_prw=@p1 AND tgl_perawatan=@p2 AND jam_rawat=@p3 {syarat} FOR UPDATE",
            Kunci(asal));
        if (row is null || Snapshot(asal.Jenis, row) != asal)
            throw new InvalidOperationException("Tindakan sudah berubah atau dihapus. Muat ulang riwayat.");
    }

    private static async Task HapusBarisAsync(Db db, TindakanRanapSnapshot asal)
    {
        var (tabel, syarat) = Tabel(asal.Jenis);
        if (await db.ExecAsync($"DELETE FROM {tabel} WHERE no_rawat=@p0 AND kd_jenis_prw=@p1 AND tgl_perawatan=@p2 AND jam_rawat=@p3 {syarat}",
                Kunci(asal)) != 1)
            throw new InvalidOperationException("Tindakan sudah berubah atau dihapus. Muat ulang riwayat.");
    }

    private void PastikanBatasWaktu(DateTime tercatat, DateTime sekarang, string aksi)
    {
        if (!user.IsAdminUtama && RegistrasiService.MelewatiBatasEdit(tercatat, sekarang))
            throw new InvalidOperationException($"Batas {aksi} tindakan adalah 2 x 24 jam.");
    }

    private static async Task PastikanPelaksanaAsync(Db db, TindakanRalanJenis jenis, string dokter, string petugas)
    {
        if (jenis != TindakanRalanJenis.Paramedis && await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0", dokter) != 1)
            throw new ArgumentException("Pilih dokter yang terdaftar.");
        if (jenis != TindakanRalanJenis.Dokter && await db.CountAsync("SELECT COUNT(*) FROM petugas WHERE nip=@p0", petugas) != 1)
            throw new ArgumentException("Pilih petugas yang terdaftar.");
    }

    /// <summary>Kasir dan operasi kamar mengunci baris registrasi yang sama terlebih dahulu.</summary>
    private async Task<Row> KunjunganBelumDitagihAsync(Db db)
    {
        var visit = await KunjunganAsync(db, true);
        if (visit.S("stts") == "Batal" ||
            await TagihanKunjungan.SudahDitagihAsync(db, kamar.NoRawat, kunci: true))
            throw new InvalidOperationException("Registrasi batal atau tagihan sudah disimpan. Periksa di Kasir.");
        return visit;
    }

    private Task<int> SisipkanAsync(Db db, TindakanRanapSnapshot t)
    {
        var sql = t.Jenis switch
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
            _ => throw new ArgumentOutOfRangeException(nameof(t))
        };
        return db.ExecAsync(sql, t.NoRawat, t.Kode, t.Dokter, t.Petugas, t.Waktu.Date, t.Waktu.TimeOfDay,
            t.Material, t.Bhp, t.TarifDokter, t.TarifPetugas, t.Kso, t.Menejemen, t.Biaya);
    }

    private async Task<Row> KunjunganAsync(Db db, bool kunci) =>
        await db.FirstAsync("""
            SELECT kd_pj,stts,tgl_registrasi,jam_reg FROM reg_periksa WHERE no_rawat=@p0 AND no_rkm_medis=@p1 AND status_lanjut='Ranap'
            """ + (kunci ? " FOR UPDATE" : ""), kamar.NoRawat, noRm)
        ?? throw new InvalidOperationException("Kunjungan rawat inap tidak ditemukan.");

    private async Task<Row> KamarAsync(Db db, bool kunci) =>
        // Bayi rawat gabung memakai segmen kamar ibunya.
        await db.FirstAsync($"""
            SELECT k.kd_bangsal,k.kelas FROM kamar_inap s JOIN kamar k ON k.kd_kamar=s.kd_kamar
            WHERE s.no_rawat={RawatGabungService.PemilikKamar("@p0")} AND s.kd_kamar=@p1 AND s.tgl_masuk=@p2 AND s.jam_masuk=@p3 AND s.stts_pulang='-'
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
