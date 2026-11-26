using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

public sealed record TarifTindakanRalan(string Kode, string Nama, string Kategori, double Total);
public sealed record TindakanRalanSnapshot(TindakanRalanJenis Jenis, string NoRawat, string Kode, string Dokter, string Petugas,
    DateTime Waktu, double Biaya, double TarifDokter, double TarifPetugas, double Material, double Bhp, double Kso, double Menejemen, string StatusBayar);

public sealed class TindakanRalanInputService(DatabaseSettings settings, string noRawat, AuthenticatedUser user)
{
    public const string Izin = "tindakan_ralan";

    public async Task<IReadOnlyList<TarifTindakanRalan>> CariTarifAsync(TindakanRalanJenis jenis, string pencarian)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var visit = await KunjunganAsync(db, kunci: false);
        var aturan = await AturanTarifAsync(db);
        var kata = "%" + pencarian.Trim() + "%";
        var rows = await db.QueryAsync(SqlTarif(jenis) + """
            AND (j.kd_jenis_prw LIKE @p4 OR j.nm_perawatan LIKE @p4 OR k.nm_kategori LIKE @p4)
            ORDER BY j.nm_perawatan,j.kd_jenis_prw LIMIT 500
            """, visit.S("kd_poli"), visit.S("kd_pj"), aturan.S("poli_ralan"), aturan.S("cara_bayar_ralan"), kata);
        return rows.Select(r => new TarifTindakanRalan(r.S("kd_jenis_prw"), r.S("nm_perawatan"), r.S("nm_kategori"), r.D("total"))).ToList();
    }

    public async Task<(IReadOnlyList<PegawaiPilihan> Dokter, IReadOnlyList<PegawaiPilihan> Petugas)> PelaksanaAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var dokter = await db.QueryAsync("SELECT kd_dokter,nm_dokter FROM dokter ORDER BY nm_dokter,kd_dokter");
        var petugas = await db.QueryAsync("SELECT nip,nama FROM petugas ORDER BY nama,nip");
        return (dokter.Select(r => new PegawaiPilihan(r.S("kd_dokter"), r.S("nm_dokter"))).ToList(),
            petugas.Select(r => new PegawaiPilihan(r.S("nip"), r.S("nama"))).ToList());
    }

    public async Task<TindakanRalanSnapshot> MuatTindakanAsync(TindakanRalanJenis jenis, string kode, string dokter, string petugas, DateTime waktu)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        return await CariTindakanAsync(db, jenis, kode, dokter, petugas, waktu, false);
    }

    public async Task HapusAsync(TindakanRalanSnapshot asal)
    {
        PastikanIzin();
        if (asal.NoRawat != noRawat) throw new InvalidOperationException("Tindakan berasal dari kunjungan lain.");
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await KunjunganAsync(db, true);
        if (visit.S("stts") == "Batal" || visit.S("status_bayar") != "Belum Bayar" ||
            await db.FirstAsync("SELECT no_rawat FROM billing WHERE no_rawat=@p0 LIMIT 1 FOR UPDATE", noRawat) is not null)
            throw new InvalidOperationException("Registrasi batal atau billing sudah terverifikasi. Hubungi kasir atau keuangan.");
        var now = (await db.FirstAsync("SELECT NOW() AS waktu"))!.Dt("waktu")!.Value;
        if (!user.IsAdminUtama && RegistrasiService.MelewatiBatasEdit(asal.Waktu, now))
            throw new InvalidOperationException("Batas hapus tindakan adalah 48 jam.");
        var terbaru = await CariTindakanAsync(db, asal.Jenis, asal.Kode, asal.Dokter, asal.Petugas, asal.Waktu, true);
        if (terbaru != asal) throw new InvalidOperationException("Tindakan telah berubah. Muat ulang daftar.");
        if (terbaru.StatusBayar != "Belum") throw new InvalidOperationException("Tindakan sudah dibayar.");

        var (table, condition) = KunciTindakan(asal.Jenis);
        var deleted = await db.ExecAsync($"DELETE FROM {table} WHERE no_rawat=@p0 AND kd_jenis_prw=@p1 AND tgl_perawatan=@p2 AND jam_rawat=@p3 {condition}",
            noRawat, asal.Kode, asal.Waktu.Date, asal.Waktu.TimeOfDay, asal.Dokter, asal.Petugas);
        if (deleted != 1) throw new InvalidOperationException("Tindakan tidak ditemukan lagi. Muat ulang daftar.");
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun_ralan");
        var lines = new List<JurnalLine>();
        TambahJurnal(lines, akun, asal, true);
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noRawat,
            $"PEMBATALAN TINDAKAN RAWAT JALAN {noRawat} {asal.Kode}, DIPOSTING OLEH {user.KodeLegacy}", lines);
        await db.CommitAsync();
    }

    public async Task UbahAsync(TindakanRalanSnapshot asal, DateTime waktu, string dokter, string petugas, TarifTindakanRalan tarif)
    {
        PastikanIzin();
        if (asal.NoRawat != noRawat) throw new InvalidOperationException("Tindakan berasal dari kunjungan lain.");
        waktu = waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await KunjunganAsync(db, true);
        if (visit.S("stts") == "Batal" || visit.S("status_bayar") != "Belum Bayar" ||
            await db.FirstAsync("SELECT no_rawat FROM billing WHERE no_rawat=@p0 LIMIT 1 FOR UPDATE", noRawat) is not null)
            throw new InvalidOperationException("Registrasi batal atau billing sudah terverifikasi. Hubungi kasir atau keuangan.");
        var now = (await db.FirstAsync("SELECT NOW() AS waktu"))!.Dt("waktu")!.Value;
        if (!user.IsAdminUtama)
        {
            if (RegistrasiService.MelewatiBatasEdit(asal.Waktu, now)) throw new InvalidOperationException("Batas ubah tindakan adalah 48 jam.");
            if (visit.Dt("tgl_registrasi") is not { } tanggal || visit["jam_reg"] is not TimeSpan jam)
                throw new InvalidOperationException("Waktu registrasi tidak valid.");
            RawatJalanService.PastikanTidakSebelumRegistrasi(tanggal.Date + jam, waktu);
        }
        var terbaru = await CariTindakanAsync(db, asal.Jenis, asal.Kode, asal.Dokter, asal.Petugas, asal.Waktu, true);
        if (terbaru != asal) throw new InvalidOperationException("Tindakan telah berubah. Muat ulang daftar.");
        if (terbaru.StatusBayar != "Belum") throw new InvalidOperationException("Tindakan sudah dibayar.");
        if (asal.Jenis != TindakanRalanJenis.Paramedis &&
            (string.IsNullOrWhiteSpace(dokter) || await db.FirstAsync("SELECT kd_dokter FROM dokter WHERE kd_dokter=@p0", dokter) is null))
            throw new ArgumentException("Pilih dokter yang terdaftar.");
        if (asal.Jenis != TindakanRalanJenis.Dokter &&
            (string.IsNullOrWhiteSpace(petugas) || await db.FirstAsync("SELECT nip FROM petugas WHERE nip=@p0", petugas) is null))
            throw new ArgumentException("Pilih petugas yang terdaftar.");
        var aturan = await AturanTarifAsync(db);
        var row = await db.FirstAsync(SqlTarif(asal.Jenis) + " AND j.kd_jenis_prw=@p4 FOR UPDATE",
            visit.S("kd_poli"), visit.S("kd_pj"), aturan.S("poli_ralan"), aturan.S("cara_bayar_ralan"), tarif.Kode);
        if (row is null || row.D("total") != tarif.Total) throw new InvalidOperationException("Tarif berubah atau tidak berlaku. Cari ulang tarif.");
        var (table, condition) = KunciTindakan(asal.Jenis);
        await db.ExecAsync($"DELETE FROM {table} WHERE no_rawat=@p0 AND kd_jenis_prw=@p1 AND tgl_perawatan=@p2 AND jam_rawat=@p3 {condition}",
            noRawat, asal.Kode, asal.Waktu.Date, asal.Waktu.TimeOfDay, asal.Dokter, asal.Petugas);
        try { await SisipkanAsync(db, asal.Jenis, tarif.Kode, waktu, dokter, petugas, row); }
        catch (MySqlException ex) when (ex.Number == 1062) { throw new InvalidOperationException("Tindakan untuk pelaksana dan waktu tersebut sudah ada.", ex); }
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun_ralan");
        var baru = new TindakanRalanSnapshot(asal.Jenis, noRawat, tarif.Kode, dokter, petugas, waktu,
            row.D("total"), row.D("tarif_tindakandr"), row.D("tarif_tindakanpr"), row.D("material"), row.D("bhp"), row.D("kso"), row.D("menejemen"), "Belum");
        var lines = new List<JurnalLine>();
        TambahJurnal(lines, akun, asal, true);
        TambahJurnal(lines, akun, baru, false);
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noRawat, $"PERUBAHAN TINDAKAN RAWAT JALAN {noRawat}, DIPOSTING OLEH {user.KodeLegacy}", lines);
        await db.CommitAsync();
    }

    private static void TambahJurnal(List<JurnalLine> lines, Dictionary<string, string> akun, TindakanRalanSnapshot t, bool balik)
    {
        void Pair(double nilai, string debit, string kredit)
        {
            if (nilai <= 0) return;
            lines.Add(new JurnalLine(akun.GetValueOrDefault(debit, ""), debit, balik ? 0 : nilai, balik ? nilai : 0));
            lines.Add(new JurnalLine(akun.GetValueOrDefault(kredit, ""), kredit, balik ? nilai : 0, balik ? 0 : nilai));
        }
        void Jasa(double nilai, string jasa) => Pair(nilai, $"Beban_{jasa}_Tindakan_Ralan", $"Utang_{jasa}_Tindakan_Ralan");
        Pair(t.Biaya, "Suspen_Piutang_Tindakan_Ralan", "Tindakan_Ralan");
        if (t.Jenis != TindakanRalanJenis.Paramedis) Jasa(t.TarifDokter, "Jasa_Medik_Dokter");
        if (t.Jenis != TindakanRalanJenis.Dokter) Jasa(t.TarifPetugas, "Jasa_Medik_Paramedis");
        Jasa(t.Kso, "KSO");
        Jasa(t.Material, "Jasa_Sarana");
        Pair(t.Bhp, "HPP_BHP_Tindakan_Ralan", "Persediaan_BHP_Tindakan_Ralan");
        Jasa(t.Menejemen, "Jasa_Menejemen");
    }

    private static (string Table, string Condition) KunciTindakan(TindakanRalanJenis jenis) => jenis switch
    {
        TindakanRalanJenis.Dokter => ("rawat_jl_dr", "AND kd_dokter=@p4"),
        TindakanRalanJenis.Paramedis => ("rawat_jl_pr", "AND nip=@p5"),
        TindakanRalanJenis.DokterParamedis => ("rawat_jl_drpr", "AND kd_dokter=@p4 AND nip=@p5"),
        _ => throw new ArgumentOutOfRangeException(nameof(jenis)),
    };

    private async Task<TindakanRalanSnapshot> CariTindakanAsync(Db db, TindakanRalanJenis jenis, string kode, string dokter, string petugas, DateTime waktu, bool kunci)
    {
        var (table, condition) = KunciTindakan(jenis);
        var row = await db.FirstAsync($"SELECT * FROM {table} WHERE no_rawat=@p0 AND kd_jenis_prw=@p1 AND tgl_perawatan=@p2 AND jam_rawat=@p3 {condition}" + (kunci ? " FOR UPDATE" : ""),
            noRawat, kode, waktu.Date, waktu.TimeOfDay, dokter, petugas)
            ?? throw new InvalidOperationException("Tindakan tidak ditemukan lagi. Muat ulang daftar.");
        return new(jenis, noRawat, kode, jenis == TindakanRalanJenis.Paramedis ? "" : row.S("kd_dokter"),
            jenis == TindakanRalanJenis.Dokter ? "" : row.S("nip"), waktu, row.D("biaya_rawat"), row.D("tarif_tindakandr"),
            row.D("tarif_tindakanpr"), row.D("material"), row.D("bhp"), row.D("kso"), row.D("menejemen"), row.S("stts_bayar"));
    }

    public async Task SimpanAsync(TindakanRalanJenis jenis, DateTime waktu, string dokter, string petugas,
        IReadOnlyList<TarifTindakanRalan> pilihan)
    {
        PastikanIzin();
        if (pilihan.Count == 0 || pilihan.Select(t => t.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pilihan.Count)
            throw new ArgumentException("Pilih satu atau lebih tindakan yang berbeda.");
        waktu = waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await KunjunganAsync(db, kunci: true);
        if (visit.S("stts") == "Batal" || visit.S("status_bayar") != "Belum Bayar" ||
            await db.FirstAsync("SELECT no_rawat FROM billing WHERE no_rawat=@p0 LIMIT 1 FOR UPDATE", noRawat) is not null)
            throw new InvalidOperationException("Registrasi batal atau billing sudah terverifikasi. Hubungi kasir atau keuangan.");

        if (!user.IsAdminUtama)
        {
            if (visit.Dt("tgl_registrasi") is not { } tanggal || visit["jam_reg"] is not TimeSpan jam)
                throw new InvalidOperationException("Waktu registrasi tidak valid.");
            RawatJalanService.PastikanTidakSebelumRegistrasi(tanggal.Date + jam, waktu);
        }
        if (jenis != TindakanRalanJenis.Paramedis &&
            (string.IsNullOrWhiteSpace(dokter) || await db.FirstAsync("SELECT kd_dokter FROM dokter WHERE kd_dokter=@p0", dokter) is null))
            throw new ArgumentException("Pilih dokter yang terdaftar.");
        if (jenis != TindakanRalanJenis.Dokter &&
            (string.IsNullOrWhiteSpace(petugas) || await db.FirstAsync("SELECT nip FROM petugas WHERE nip=@p0", petugas) is null))
            throw new ArgumentException("Pilih petugas yang terdaftar.");

        var aturan = await AturanTarifAsync(db);
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun_ralan");
        var lines = new List<JurnalLine>();
        foreach (var tarif in pilihan.OrderBy(t => t.Kode, StringComparer.Ordinal))
        {
            var r = await db.FirstAsync(SqlTarif(jenis) + " AND j.kd_jenis_prw=@p4 FOR UPDATE",
                visit.S("kd_poli"), visit.S("kd_pj"), aturan.S("poli_ralan"), aturan.S("cara_bayar_ralan"), tarif.Kode);
            if (r is null || r.D("total") != tarif.Total)
                throw new InvalidOperationException($"Tarif {tarif.Kode} berubah atau sudah tidak berlaku. Cari ulang tarif sebelum menyimpan.");

            try
            {
                await SisipkanAsync(db, jenis, tarif.Kode, waktu, dokter, petugas, r);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                throw new InvalidOperationException($"Tindakan {tarif.Kode} sudah tercatat untuk pelaksana dan waktu tersebut.", ex);
            }

            Pair(r.D("total"), "Suspen_Piutang_Tindakan_Ralan", "Tindakan_Ralan");
            if (jenis != TindakanRalanJenis.Paramedis)
                Jasa(r.D("tarif_tindakandr"), "Jasa_Medik_Dokter");
            if (jenis != TindakanRalanJenis.Dokter)
                Jasa(r.D("tarif_tindakanpr"), "Jasa_Medik_Paramedis");
            Jasa(r.D("kso"), "KSO");
            Jasa(r.D("material"), "Jasa_Sarana");
            Pair(r.D("bhp"), "HPP_BHP_Tindakan_Ralan", "Persediaan_BHP_Tindakan_Ralan");
            Jasa(r.D("menejemen"), "Jasa_Menejemen");
        }

        var pasien = await db.StrAsync("SELECT nm_pasien FROM pasien WHERE no_rkm_medis=@p0", visit.S("no_rkm_medis"));
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noRawat,
            $"TINDAKAN RAWAT JALAN PASIEN {visit.S("no_rkm_medis")} {pasien}, DIPOSTING OLEH {user.KodeLegacy}", lines);
        await db.CommitAsync();

        void Jasa(double nilai, string jasa) => Pair(nilai, $"Beban_{jasa}_Tindakan_Ralan", $"Utang_{jasa}_Tindakan_Ralan");
        void Pair(double nilai, string debit, string kredit)
        {
            if (nilai <= 0) return;
            lines.Add(new JurnalLine(akun.GetValueOrDefault(debit, ""), debit, nilai, 0));
            lines.Add(new JurnalLine(akun.GetValueOrDefault(kredit, ""), kredit, 0, nilai));
        }
    }

    private Task<int> SisipkanAsync(Db db, TindakanRalanJenis jenis, string kode, DateTime waktu, string dokter, string petugas, Row r)
    {
        object?[] args = [noRawat, kode, dokter, petugas, waktu.Date, waktu.TimeOfDay, r.D("material"), r.D("bhp"),
            r.D("tarif_tindakandr"), r.D("tarif_tindakanpr"), r.D("kso"), r.D("menejemen"), r.D("total")];
        var sql = jenis switch
        {
            TindakanRalanJenis.Dokter => """
                INSERT INTO rawat_jl_dr(no_rawat,kd_jenis_prw,kd_dokter,tgl_perawatan,jam_rawat,material,bhp,tarif_tindakandr,kso,menejemen,biaya_rawat,stts_bayar)
                VALUES(@p0,@p1,@p2,@p4,@p5,@p6,@p7,@p8,@p10,@p11,@p12,'Belum')
                """,
            TindakanRalanJenis.Paramedis => """
                INSERT INTO rawat_jl_pr(no_rawat,kd_jenis_prw,nip,tgl_perawatan,jam_rawat,material,bhp,tarif_tindakanpr,kso,menejemen,biaya_rawat,stts_bayar)
                VALUES(@p0,@p1,@p3,@p4,@p5,@p6,@p7,@p9,@p10,@p11,@p12,'Belum')
                """,
            TindakanRalanJenis.DokterParamedis => """
                INSERT INTO rawat_jl_drpr(no_rawat,kd_jenis_prw,kd_dokter,nip,tgl_perawatan,jam_rawat,material,bhp,tarif_tindakandr,tarif_tindakanpr,kso,menejemen,biaya_rawat,stts_bayar)
                VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,'Belum')
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(jenis)),
        };
        return db.ExecAsync(sql, args);
    }

    private async Task<Row> KunjunganAsync(Db db, bool kunci) =>
        await db.FirstAsync("""
            SELECT kd_poli,kd_pj,no_rkm_medis,tgl_registrasi,jam_reg,stts,status_bayar
            FROM reg_periksa WHERE no_rawat=@p0 AND status_lanjut='Ralan'
            """ + (kunci ? " FOR UPDATE" : ""), noRawat)
        ?? throw new InvalidOperationException("Kunjungan rawat jalan tidak ditemukan.");

    private static async Task<Row> AturanTarifAsync(Db db) =>
        await db.FirstAsync("SELECT poli_ralan,cara_bayar_ralan FROM set_tarif LIMIT 1")
        ?? throw new InvalidOperationException("Pengaturan tarif rawat jalan belum tersedia.");

    private static string SqlTarif(TindakanRalanJenis jenis)
    {
        var total = jenis switch
        {
            TindakanRalanJenis.Dokter => "total_byrdr",
            TindakanRalanJenis.Paramedis => "total_byrpr",
            TindakanRalanJenis.DokterParamedis => "total_byrdrpr",
            _ => throw new ArgumentOutOfRangeException(nameof(jenis)),
        };
        return $"""
            SELECT j.kd_jenis_prw,j.nm_perawatan,k.nm_kategori,j.material,j.bhp,j.tarif_tindakandr,j.tarif_tindakanpr,
                   j.kso,j.menejemen,j.{total} AS total
            FROM jns_perawatan j JOIN kategori_perawatan k ON k.kd_kategori=j.kd_kategori
            WHERE j.status='1' AND j.{total}>0
              AND (@p2='No' OR j.kd_poli IN (@p0,'-'))
              AND (@p3='No' OR j.kd_pj IN (@p1,'-'))

            """;
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak Tindakan Rawat Jalan.");
    }
}
