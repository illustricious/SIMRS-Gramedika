using SimrsGramedika.Core.Auth;
using SimrsGramedika.Config;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.MenuA;

public sealed class ResepPulangRepository(DatabaseSettings settings)
{
    public const string IzinPermintaan = "permintaan_resep_pulang";

    public const string IzinFarmasi = "resep_pulang";

    public Task<IReadOnlyList<Row>> CariDokterAsync(AuthenticatedUser user, string query)
        => CariMasterAsync(user, """
            SELECT kd_dokter AS kode,nm_dokter AS nama FROM dokter
            WHERE status='1' AND CONCAT_WS(' ',kd_dokter,nm_dokter) LIKE @p0
            ORDER BY nm_dokter,kd_dokter LIMIT 100
            """, query);

    public Task<IReadOnlyList<Row>> CariObatAsync(AuthenticatedUser user, string query)
        => CariMasterAsync(user, """
            SELECT kode_brng AS kode,nama_brng AS nama,kode_sat AS satuan FROM databarang
            WHERE status='1' AND CONCAT_WS(' ',kode_brng,nama_brng) LIKE @p0
            ORDER BY nama_brng,kode_brng LIMIT 100
            """, query);

    private async Task<IReadOnlyList<Row>> CariMasterAsync(AuthenticatedUser user, string sql, string query)
    {
        PastikanIzin(user, IzinPermintaan);
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync(sql, $"%{query.Trim()}%");
    }

    public async Task<string> BuatPermintaanAsync(AuthenticatedUser user, string noRawat, string dokter,
        IReadOnlyList<ObatDiminta> obat)
    {
        PastikanIzin(user, IzinPermintaan);
        var items = ValidasiObat(obat);

        await using var db = await Db.OpenAsync(settings);
        var nomor = "";
        await db.WithNamedLockAsync($"simrs.resep-pulang.{db.Conn.Database}", "Penomoran resep sedang digunakan. Coba lagi.", async () =>
        {
            for (var attempt = 0; ; attempt++)
            {
                await db.BeginAsync();
                try
                {
                    await MenuAWrites.KunciKunjunganAsync(db, noRawat, cekBilling: true);
                    if (await db.CountAsync("SELECT COUNT(*) FROM kamar_inap WHERE no_rawat=@p0", noRawat) == 0)
                        throw new InvalidOperationException("Kunjungan tidak memiliki riwayat kamar rawat inap.");
                    if (await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0 AND status='1'", dokter) != 1)
                        throw new InvalidOperationException("Pilih dokter yang masih aktif.");
                    var now = (DateTime)(await db.ScalarAsync("SELECT NOW()"))!;
                    nomor = await db.AutoNomorAsync("""
                        SELECT COALESCE(MAX(CAST(RIGHT(no_permintaan,4) AS UNSIGNED)),0)
                        FROM permintaan_resep_pulang WHERE tgl_permintaan=@p0
                        """, "RP" + now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture), 4, now.Date);
                    if (nomor.Length != 14)
                        throw new InvalidOperationException("Nomor permintaan resep hari ini sudah habis.");
                    await db.ExecAsync("""
                        INSERT INTO permintaan_resep_pulang
                            (no_permintaan,tgl_permintaan,jam,no_rawat,kd_dokter,status,tgl_validasi,jam_validasi)
                        VALUES (@p0,@p1,@p2,@p3,@p4,'Belum','1000-01-01','00:00:00')
                        """, nomor, now.Date, now.TimeOfDay, noRawat, dokter);
                    await TambahDetailAsync(db, nomor, items);
                    await db.CommitAsync();
                    return;
                }
                catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
                {
                    await db.DisposeTxAsync();
                    if (attempt >= 2)
                        throw new InvalidOperationException("Nomor atau waktu permintaan bertabrakan. Coba simpan lagi.", ex);
                    await Task.Delay(550);
                }
                catch
                {
                    await db.DisposeTxAsync();
                    throw;
                }
            }
        });
        return nomor;
    }

    public sealed record ObatResep(ObatDiminta Isi, string Nama, string Satuan);
    public sealed record DrafResep(string Nomor, string Tanggal, string Jam, string KodeDokter, string NamaDokter,
        IReadOnlyList<ObatResep> Obat);

    public async Task<DrafResep> MuatDrafAsync(AuthenticatedUser user, string noRawat, string nomor, string tanggal, string jam,
        bool farmasi = false)
    {
        PastikanIzin(user, farmasi ? IzinFarmasi : IzinPermintaan);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await MenuAWrites.KunciKunjunganAsync(db, noRawat, cekBilling: true);
        var draf = await BacaDrafAsync(db, noRawat, nomor, tanggal, jam);
        await db.CommitAsync();
        return draf;
    }

    public async Task UbahPermintaanAsync(AuthenticatedUser user, string noRawat, DrafResep awal,
        string dokter, IReadOnlyList<ObatDiminta> obat)
    {
        PastikanIzin(user, IzinPermintaan);
        var items = ValidasiObat(obat);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await MenuAWrites.KunciKunjunganAsync(db, noRawat, cekBilling: true);
        await PastikanDrafSamaAsync(db, noRawat, awal);
        if (await db.CountAsync("SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0 AND status='1'", dokter) != 1)
            throw new InvalidOperationException("Pilih dokter yang masih aktif.");
        await db.ExecAsync("UPDATE permintaan_resep_pulang SET kd_dokter=@p1 WHERE no_permintaan=@p0", awal.Nomor, dokter);
        await db.ExecAsync("DELETE FROM detail_permintaan_resep_pulang WHERE no_permintaan=@p0", awal.Nomor);
        await TambahDetailAsync(db, awal.Nomor, items);
        await db.CommitAsync();
    }

    private static async Task PastikanDrafSamaAsync(Db db, string noRawat, DrafResep awal)
    {
        var kini = await BacaDrafAsync(db, noRawat, awal.Nomor, awal.Tanggal, awal.Jam);
        if (kini.KodeDokter != awal.KodeDokter || !kini.Obat.Select(x => x.Isi).SequenceEqual(awal.Obat.Select(x => x.Isi)))
            throw new InvalidOperationException("Permintaan sudah diubah. Buka ulang untuk melihat isi terbaru.");
    }

    private static async Task<DrafResep> BacaDrafAsync(Db db, string noRawat, string nomor, string tanggal, string jam)
    {
        var header = await db.FirstAsync("""
            SELECT h.kd_dokter,d.nm_dokter
            FROM permintaan_resep_pulang h LEFT JOIN dokter d ON d.kd_dokter=h.kd_dokter
            WHERE h.no_permintaan=@p0 AND h.no_rawat=@p1 AND h.tgl_permintaan=@p2 AND h.jam=@p3 AND h.status='Belum'
            FOR UPDATE
            """, nomor, noRawat, tanggal, jam)
            ?? throw new InvalidOperationException("Permintaan sudah divalidasi, dibatalkan, atau berubah. Muat ulang daftar.");
        var rows = await db.QueryAsync("""
            SELECT d.kode_brng,d.jml,d.dosis,b.nama_brng,b.kode_sat
            FROM detail_permintaan_resep_pulang d LEFT JOIN databarang b ON b.kode_brng=d.kode_brng
            WHERE d.no_permintaan=@p0 ORDER BY d.kode_brng,d.dosis,d.jml FOR UPDATE
            """, nomor);
        return new(nomor, tanggal, jam, header.S("kd_dokter"), header.S("nm_dokter"),
            rows.Select(row => new ObatResep(new(row.S("kode_brng"), row.D("jml"), row.S("dosis")),
                row.S("nama_brng"), row.S("kode_sat"))).ToArray());
    }

    private static ObatDiminta[] ValidasiObat(IReadOnlyList<ObatDiminta> obat)
    {
        var items = obat.Select(x => x with { Kode = x.Kode.Trim(), Dosis = x.Dosis.Trim() }).ToArray();
        if (items.Length == 0 || items.Any(x => x.Kode.Length == 0 || x.Dosis.Length is 0 or > 150
            || !double.IsFinite(x.Jumlah) || x.Jumlah <= 0)
            || items.Select(x => x.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw new InvalidOperationException("Isi obat, jumlah positif dan aturan pakai. Setiap obat hanya boleh muncul sekali.");
        return items;
    }

    private static async Task TambahDetailAsync(Db db, string nomor, IReadOnlyList<ObatDiminta> items)
    {
        foreach (var item in items)
        {
            if (await db.ExecAsync("""
                INSERT INTO detail_permintaan_resep_pulang (no_permintaan,kode_brng,jml,dosis)
                SELECT @p0,kode_brng,@p2,@p3 FROM databarang WHERE kode_brng=@p1 AND status='1'
                """, nomor, item.Kode, item.Jumlah, item.Dosis) != 1)
                throw new InvalidOperationException($"Obat {item.Kode} sudah tidak aktif.");
        }
    }

    public async Task<IReadOnlyList<Row>> CariSiapSerahAsync(AuthenticatedUser user, string query)
    {
        PastikanIzin(user, IzinFarmasi);
        await using var db = await Db.OpenAsync(settings);
        await PastikanTabelSerahAsync(db);
        return await db.QueryAsync("""
            SELECT h.no_permintaan,h.no_rawat,p.nm_pasien,
                   (SELECT GROUP_CONCAT(CONCAT(b.nama_brng,' × ',d.jml_barang) ORDER BY b.nama_brng SEPARATOR ', ')
                    FROM resep_pulang d JOIN databarang b ON b.kode_brng=d.kode_brng
                    WHERE d.no_rawat=h.no_rawat AND d.tanggal=h.tgl_validasi AND d.jam=h.jam_validasi
                      AND d.jml_barang>0 AND EXISTS (SELECT 1 FROM detail_permintaan_resep_pulang i
                        WHERE i.no_permintaan=h.no_permintaan AND i.kode_brng=d.kode_brng)) AS obat
            FROM permintaan_resep_pulang h
            JOIN reg_periksa r ON r.no_rawat=h.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            LEFT JOIN gramedika_serah_resep_pulang s ON s.no_permintaan=h.no_permintaan
            WHERE h.status='Sudah' AND r.stts<>'Batal' AND s.no_permintaan IS NULL
              AND CONCAT_WS(' ',h.no_permintaan,h.no_rawat,r.no_rkm_medis,p.nm_pasien) LIKE @p0
            HAVING obat IS NOT NULL
            ORDER BY h.tgl_validasi DESC,h.jam_validasi DESC,h.no_permintaan DESC LIMIT 100
            """, $"%{query.Trim()}%");
    }

    public async Task CatatSerahAsync(AuthenticatedUser user, string nomor, string penerima)
    {
        PastikanIzin(user, IzinFarmasi);
        penerima = penerima.Trim();
        if (penerima.Length is 0 or > 100) throw new InvalidOperationException("Isi nama penerima obat.");
        var petugas = user.KodeLegacy.Trim();
        if (petugas.Length is 0 or > 100) throw new InvalidOperationException("Identitas akun petugas tidak valid.");

        await using var db = await Db.OpenAsync(settings);
        await PastikanTabelSerahAsync(db);
        // Dibaca sebelum transaksi: baca biasa di dalamnya membekukan snapshot sebelum kunjungan terkunci.
        var rawat = await db.StrAsync("SELECT no_rawat FROM permintaan_resep_pulang WHERE no_permintaan=@p0", nomor);
        await db.BeginAsync();
        await MenuAWrites.KunciKunjunganAsync(db, rawat);
        var header = await db.FirstAsync("""
            SELECT no_rawat,status FROM permintaan_resep_pulang
            WHERE no_permintaan=@p0 FOR UPDATE
            """, nomor);
        if (header is null || header.S("no_rawat") != rawat || header.S("status") != "Sudah")
            throw new InvalidOperationException("Validasi resep di Farmasi sebelum mencatat serah terima.");
        if (await db.CountAsync("SELECT COUNT(*) FROM gramedika_serah_resep_pulang WHERE no_permintaan=@p0", nomor) != 0)
            throw new InvalidOperationException("Serah terima resep ini sudah dicatat.");
        if (await db.CountAsync("""
            SELECT COUNT(*) FROM resep_pulang d
            JOIN permintaan_resep_pulang h ON h.no_rawat=d.no_rawat AND h.tgl_validasi=d.tanggal AND h.jam_validasi=d.jam
            WHERE h.no_permintaan=@p0 AND d.jml_barang>0
              AND EXISTS (SELECT 1 FROM detail_permintaan_resep_pulang i
                WHERE i.no_permintaan=h.no_permintaan AND i.kode_brng=d.kode_brng)
            """, nomor) == 0)
            throw new InvalidOperationException("Data pengeluaran obat untuk validasi ini tidak ditemukan.");
        await db.ExecAsync("""
            INSERT INTO gramedika_serah_resep_pulang (no_permintaan,no_rawat,waktu,petugas,penerima)
            VALUES (@p0,@p1,NOW(),@p2,@p3)
            """, nomor, rawat, petugas, penerima);
        await db.CommitAsync();
    }

    private static async Task<bool> AdaTabelSerahAsync(Db db) => await db.CountAsync("""
        SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()
        AND table_name='gramedika_serah_resep_pulang'
        """) == 1;

    private static async Task PastikanTabelSerahAsync(Db db)
    {
        if (!await AdaTabelSerahAsync(db))
            throw new InvalidOperationException("Tabel serah terima belum terpasang. Hubungi IT untuk memasang migrasi obat pulang.");
    }

    private static void PastikanIzin(AuthenticatedUser user, string izin)
    {
        if (!user.HasPermission(izin)) throw new UnauthorizedAccessException("Tidak memiliki hak akses resep pulang.");
    }

    public sealed record ObatDiminta(string Kode, double Jumlah, string Dosis);

    /// <summary>Permintaan tervalidasi yang obatnya belum dicatat diserahkan, jadi masih bisa diretur ke depo.</summary>
    public async Task<IReadOnlyList<Row>> CariBisaDireturAsync(AuthenticatedUser user, string query)
    {
        PastikanIzin(user, IzinFarmasi);
        await using var db = await Db.OpenAsync(settings);
        var belumSerah = await AdaTabelSerahAsync(db)
            ? "AND NOT EXISTS (SELECT 1 FROM gramedika_serah_resep_pulang s WHERE s.no_permintaan=h.no_permintaan)"
            : "";
        return await db.QueryAsync($"""
            SELECT h.no_permintaan,h.no_rawat,h.tgl_validasi,h.jam_validasi,p.nm_pasien,
                   GROUP_CONCAT(CONCAT(b.nama_brng,' × ',d.jml_barang) ORDER BY b.nama_brng SEPARATOR ', ') AS obat
            FROM permintaan_resep_pulang h
            JOIN reg_periksa r ON r.no_rawat=h.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN resep_pulang d ON d.no_rawat=h.no_rawat AND d.tanggal=h.tgl_validasi AND d.jam=h.jam_validasi
            JOIN databarang b ON b.kode_brng=d.kode_brng
            WHERE h.status='Sudah' AND r.stts<>'Batal' AND r.status_bayar<>'Sudah Bayar' {belumSerah}
              AND CONCAT_WS(' ',h.no_permintaan,h.no_rawat,r.no_rkm_medis,p.nm_pasien) LIKE @p0
            GROUP BY h.no_permintaan,h.no_rawat,h.tgl_validasi,h.jam_validasi,p.nm_pasien
            ORDER BY h.tgl_validasi DESC,h.jam_validasi DESC,h.no_permintaan DESC LIMIT 100
            """, $"%{query.Trim()}%");
    }

    /// <summary>
    /// Retur seluruh obat satu validasi: stok kembali ke depo/batch asal, baris pengeluaran dihapus, dan permintaan
    /// kembali berstatus "Belum" agar bisa dikoreksi, dibatalkan, atau divalidasi ulang. Seperti Khanza, tanpa jurnal.
    /// </summary>
    public async Task ReturValidasiAsync(AuthenticatedUser user, string noRawat, string nomor,
        string tanggalValidasi, string jamValidasi)
    {
        PastikanIzin(user, IzinFarmasi);
        await using var db = await Db.OpenAsync(settings);
        var adaSerah = await AdaTabelSerahAsync(db);
        await db.BeginAsync();
        var pasien = await MenuAWrites.KunciKunjunganAsync(db, noRawat, cekBilling: true);
        if (await db.FirstAsync("""
            SELECT no_permintaan FROM permintaan_resep_pulang
            WHERE no_permintaan=@p0 AND no_rawat=@p1 AND status='Sudah' AND tgl_validasi=@p2 AND jam_validasi=@p3 FOR UPDATE
            """, nomor, noRawat, tanggalValidasi, jamValidasi) is null)
            throw new InvalidOperationException("Validasi sudah diretur atau berubah. Muat ulang daftar.");
        if (adaSerah && await db.CountAsync("SELECT COUNT(*) FROM gramedika_serah_resep_pulang WHERE no_permintaan=@p0", nomor) != 0)
            throw new InvalidOperationException("Obat sudah dicatat diserahkan kepada penerima. Proses retur lewat Khanza.");
        var sewaktu = await db.QueryAsync("""
            SELECT no_permintaan FROM permintaan_resep_pulang
            WHERE no_rawat=@p0 AND status='Sudah' AND tgl_validasi=@p1 AND jam_validasi=@p2 FOR UPDATE
            """, noRawat, tanggalValidasi, jamValidasi);
        if (sewaktu.Count != 1)
            throw new InvalidOperationException("Ada permintaan lain yang divalidasi pada waktu yang sama, sehingga obatnya tidak bisa dipisahkan. Periksa lewat Khanza.");

        var baris = await db.QueryAsync("""
            SELECT kode_brng,jml_barang,kd_bangsal,no_batch,no_faktur FROM resep_pulang
            WHERE no_rawat=@p0 AND tanggal=@p1 AND jam=@p2 ORDER BY kode_brng,kd_bangsal,no_batch,no_faktur FOR UPDATE
            """, noRawat, tanggalValidasi, jamValidasi);
        if (baris.Count == 0)
            throw new InvalidOperationException("Data pengeluaran obat untuk validasi ini tidak ditemukan. Periksa lewat Khanza.");

        foreach (var row in baris)
        {
            var item = new StokItem(row.S("kode_brng"), "", "", "", row.S("no_batch"), row.S("no_faktur"), 0, 0, null);
            await MenuAWrites.MutasiResepAsync(db, item, row.S("kd_bangsal"), row.D("jml_barang"), masuk: true,
                $"{noRawat} {pasien.S("nm_pasien")}", user.KodeLegacy);
        }

        await db.ExecAsync("DELETE FROM resep_pulang WHERE no_rawat=@p0 AND tanggal=@p1 AND jam=@p2",
            noRawat, tanggalValidasi, jamValidasi);
        await db.ExecAsync("""
            UPDATE permintaan_resep_pulang SET status='Belum',tgl_validasi='1000-01-01',jam_validasi='00:00:00'
            WHERE no_permintaan=@p0
            """, nomor);
        await db.CommitAsync();
    }

    public async Task BatalkanPermintaanAsync(AuthenticatedUser user, string noRawat, string nomor,
        string tanggal, string jam)
    {
        PastikanIzin(user, IzinPermintaan);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await MenuAWrites.KunciKunjunganAsync(db, noRawat, cekBilling: true);
        var header = await db.FirstAsync("""
            SELECT status FROM permintaan_resep_pulang
            WHERE no_permintaan=@p0 AND no_rawat=@p1 AND tgl_permintaan=@p2 AND jam=@p3
            FOR UPDATE
            """, nomor, noRawat, tanggal, jam);
        if (header is null)
            throw new InvalidOperationException("Permintaan sudah berubah atau tidak ditemukan. Muat ulang daftar.");
        if (header.S("status") != "Belum")
            throw new InvalidOperationException("Permintaan sudah divalidasi Farmasi dan tidak bisa dibatalkan dari Rawat Inap.");

        await db.ExecAsync("DELETE FROM detail_permintaan_resep_pulang WHERE no_permintaan=@p0", nomor);
        await db.ExecAsync("DELETE FROM permintaan_resep_pulang WHERE no_permintaan=@p0", nomor);
        await db.CommitAsync();
    }

    public async Task<Row> MuatBuktiSerahAsync(AuthenticatedUser user, string noRawat, string nomor)
    {
        PastikanIzin(user, IzinPermintaan);
        await using var db = await Db.OpenAsync(settings);
        await PastikanTabelSerahAsync(db);
        return await db.FirstAsync("""
            SELECT p.nm_pasien,r.no_rkm_medis,h.no_rawat,h.no_permintaan,s.waktu,s.petugas,s.penerima
            FROM gramedika_serah_resep_pulang s
            JOIN permintaan_resep_pulang h ON h.no_permintaan=s.no_permintaan AND h.no_rawat=s.no_rawat
            JOIN reg_periksa r ON r.no_rawat=h.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            WHERE h.no_permintaan=@p0 AND h.no_rawat=@p1 AND h.status='Sudah'
            """, nomor, noRawat)
            ?? throw new InvalidOperationException("Bukti serah terima tidak ditemukan untuk kunjungan ini.");
    }

    public async Task<IReadOnlyList<Row>> RiwayatPermintaanAsync(AuthenticatedUser user, string noRawat)
    {
        if (!user.HasPermission(IzinPermintaan))
            throw new UnauthorizedAccessException("Tidak memiliki hak akses permintaan resep pulang.");

        await using var db = await Db.OpenAsync(settings);
        var adaSerah = await AdaTabelSerahAsync(db);
        var serah = adaSerah ? "s.waktu,s.petugas,s.penerima" : "NULL AS waktu,NULL AS petugas,NULL AS penerima";
        var joinSerah = adaSerah
            ? "LEFT JOIN gramedika_serah_resep_pulang s ON s.no_permintaan=h.no_permintaan AND s.no_rawat=h.no_rawat"
            : "";
        return await db.QueryAsync($"""
            SELECT h.no_permintaan,h.tgl_permintaan,h.jam,h.status,b.nama_brng,d.jml,d.dosis,{serah}
            FROM permintaan_resep_pulang h
            LEFT JOIN detail_permintaan_resep_pulang d ON d.no_permintaan=h.no_permintaan
            LEFT JOIN databarang b ON b.kode_brng=d.kode_brng
            {joinSerah}
            WHERE h.no_rawat=@p0
            ORDER BY h.tgl_permintaan DESC,h.jam DESC,h.no_permintaan DESC,b.nama_brng,d.dosis
            """, noRawat);
    }

    public async Task<IReadOnlyList<Row>> CariPermintaanAsync(AuthenticatedUser user, string query)
    {
        PastikanIzin(user, IzinFarmasi);
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT x.no_permintaan,x.no_rawat,x.tgl_permintaan,x.jam,p.nm_pasien,
                   GROUP_CONCAT(CONCAT(b.nama_brng,' × ',d.jml) ORDER BY b.nama_brng SEPARATOR ', ') AS obat
            FROM permintaan_resep_pulang x JOIN reg_periksa r ON r.no_rawat=x.no_rawat
            JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            JOIN detail_permintaan_resep_pulang d ON d.no_permintaan=x.no_permintaan
            JOIN databarang b ON b.kode_brng=d.kode_brng
            WHERE x.status='Belum' AND (x.no_permintaan LIKE @p0 OR x.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0)
            GROUP BY x.no_permintaan,x.no_rawat,x.tgl_permintaan,x.jam,p.nm_pasien
            ORDER BY x.tgl_permintaan DESC,x.no_permintaan DESC LIMIT 100
            """, $"%{query.Trim()}%");
    }

    public async Task<IReadOnlyList<Row>> CariStokAsync(string noRawat, string depo, string query)
    {
        await using var db = await Db.OpenAsync(settings);
        return await StokAsync(db, noRawat, depo, query.Trim(), perKode: false);
    }

    private Task<List<Row>> StokAsync(Db db, string noRawat, string depo, string query, bool perKode)
    {
        var harga = settings.AktifkanBatchObat ? "d" : "b";
        var batch = settings.AktifkanBatchObat
            ? "JOIN data_batch d ON d.kode_brng=g.kode_brng AND d.no_batch=g.no_batch AND d.no_faktur=g.no_faktur AND d.sisa>0"
            : "";
        var tersedia = settings.AktifkanBatchObat ? "LEAST(g.stok,d.sisa)" : "g.stok";
        var penanda = settings.AktifkanBatchObat ? "g.no_batch<>'' AND g.no_faktur<>''" : "g.no_batch='' AND g.no_faktur=''";
        var kadaluarsa = settings.AktifkanBatchObat ? "d.tgl_kadaluarsa" : "b.expire";
        var filter = perKode ? "g.kode_brng=@p2" : "CONCAT_WS(' ',g.kode_brng,b.nama_brng,g.no_batch,g.no_faktur) LIKE @p2";
        var limit = perKode ? "" : "LIMIT 100";
        return db.QueryAsync($"""
            SELECT g.kode_brng,b.nama_brng,b.kode_sat,g.no_batch,g.no_faktur,{tersedia} AS stok,
                   CEIL((CASE WHEN COALESCE(h.hargajual,0)>0 THEN {harga}.h_beli*(1+h.hargajual/100)
                       ELSE CASE k.kelas
                           WHEN 'Kelas 1' THEN {harga}.kelas1 WHEN 'Kelas 2' THEN {harga}.kelas2
                           WHEN 'Kelas 3' THEN {harga}.kelas3 WHEN 'Kelas Utama' THEN {harga}.utama
                           WHEN 'Kelas VIP' THEN {harga}.vip WHEN 'Kelas VVIP' THEN {harga}.vvip ELSE {harga}.h_beli
                       END END)/100)*100 AS harga
            FROM reg_periksa r
            LEFT JOIN kamar k ON k.kd_kamar=(
                SELECT i.kd_kamar FROM kamar_inap i WHERE i.no_rawat=r.no_rawat
                ORDER BY (i.stts_pulang='-') DESC,i.tgl_masuk DESC,i.jam_masuk DESC LIMIT 1)
            LEFT JOIN set_harga_obat_ranap h ON h.kd_pj=r.kd_pj AND h.kelas=k.kelas
            JOIN gudangbarang g ON g.kd_bangsal=@p1 JOIN databarang b ON b.kode_brng=g.kode_brng
            {batch}
            WHERE r.no_rawat=@p0 AND g.stok>0 AND b.status='1' AND {penanda} AND {filter}
            ORDER BY b.nama_brng,{kadaluarsa} IS NULL,{kadaluarsa},g.no_batch,g.no_faktur {limit}
            """, noRawat, depo, perKode ? query : $"%{query}%");
    }

    public async Task ProsesPermintaanAsync(AuthenticatedUser user, string nomor, string depo,
        string noRawat, string tanggal, string jam, DrafResep? snapshot = null)
    {
        PastikanIzin(user, IzinFarmasi);
        await using var db = await Db.OpenAsync(settings);
        // Dibaca sebelum transaksi: baca biasa di dalamnya membekukan snapshot sebelum kunjungan terkunci.
        var rawat = await db.StrAsync("SELECT no_rawat FROM permintaan_resep_pulang WHERE no_permintaan=@p0", nomor);
        await db.BeginAsync();
        if (rawat != noRawat) throw new InvalidOperationException("Permintaan sudah berubah. Muat ulang antrean.");
        var pasien = await MenuAWrites.KunciKunjunganAsync(db, rawat, cekBilling: true);
        var header = await db.FirstAsync("""
            SELECT no_rawat,status FROM permintaan_resep_pulang
            WHERE no_permintaan=@p0 AND tgl_permintaan=@p1 AND jam=@p2 FOR UPDATE
            """, nomor, tanggal, jam);
        if (header is null || header.S("no_rawat") != rawat || header.S("status") != "Belum")
            throw new InvalidOperationException("Permintaan resep tidak ditemukan atau sudah dilayani.");

        if (snapshot is not null) await PastikanDrafSamaAsync(db, noRawat, snapshot);

        var details = await db.QueryAsync("""
            SELECT kode_brng,jml,dosis FROM detail_permintaan_resep_pulang
            WHERE no_permintaan=@p0 ORDER BY kode_brng,dosis FOR UPDATE
            """, nomor);
        if (details.Count == 0) throw new InvalidOperationException("Permintaan belum berisi obat.");

        var sekarang = (DateTime)(await db.ScalarAsync("SELECT NOW()"))!;
        if (await db.CountAsync("SELECT COUNT(*) FROM resep_pulang WHERE no_rawat=@p0 AND tanggal=@p1 AND jam=@p2",
            rawat, sekarang.Date, sekarang.TimeOfDay) != 0)
            throw new InvalidOperationException("Ada pengeluaran obat lain pada waktu yang sama. Coba validasi kembali.");

        foreach (var group in details.GroupBy(r => r.S("kode_brng"), StringComparer.OrdinalIgnoreCase))
        {
            var dosis = group.Select(r => r.S("dosis")).Distinct().ToArray();
            if (group.Key.Length == 0 || dosis.Length != 1 || dosis[0].Length == 0 || group.Any(r => !double.IsFinite(r.D("jml")) || r.D("jml") <= 0))
                throw new InvalidOperationException("Periksa kode obat, jumlah, dan aturan pakai pada permintaan resep.");
            var kurang = group.Sum(r => r.D("jml"));
            foreach (var row in await StokAsync(db, rawat, depo, group.Key, perKode: true))
            {
                var jumlah = Math.Min(kurang, row.D("stok"));
                if (jumlah <= 0) continue;
                var item = new StokItem(group.Key, row.S("nama_brng"), row.S("kode_sat"), "", row.S("no_batch"), row.S("no_faktur"),
                    row.D("stok"), row.D("harga"), null);
                if (row["harga"] is null || !double.IsFinite(item.Harga * jumlah) || item.Harga < 0)
                    throw new InvalidOperationException($"Harga {item.Nama} belum valid.");
                await MenuAWrites.MutasiResepAsync(db, item, depo, jumlah, masuk: false, $"{rawat} {pasien.S("nm_pasien")}", user.KodeLegacy);
                await db.ExecAsync("""
                    INSERT INTO resep_pulang (no_rawat,kode_brng,jml_barang,harga,total,dosis,tanggal,jam,kd_bangsal,no_batch,no_faktur)
                    VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10)
                    """, rawat, item.Kode, jumlah, item.Harga, item.Harga * jumlah, dosis[0], sekarang.Date, sekarang.TimeOfDay, depo, item.NoBatch, item.NoFaktur);
                kurang -= jumlah;
                if (kurang <= 0.0000001) break;
            }
            if (kurang > 0.0000001) throw new InvalidOperationException($"Stok obat {group.Key} di depo terpilih tidak cukup.");
        }

        await db.ExecAsync("""
            UPDATE permintaan_resep_pulang SET status='Sudah',tgl_validasi=@p0,jam_validasi=@p1 WHERE no_permintaan=@p2
            """, sekarang.Date, sekarang.TimeOfDay, nomor);
        await db.CommitAsync();
    }
}
