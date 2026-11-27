using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>
/// Satu komponen biaya operasi. <paramref name="Peran"/> adalah kolom pelaksana di tabel operasi (null untuk komponen tanpa orang),
/// <paramref name="Paket"/> kolom tarif di paket_operasi, dan <paramref name="Biaya"/> kolom nilai di tabel operasi.
/// </summary>
public sealed record KomponenOperasi(string Label, string? Peran, string Paket, string Biaya, bool Dokter = false);
public sealed record PaketOperasi(string Kode, string Nama, string Kategori, IReadOnlyList<double> Biaya)
{
    public double Total => Biaya.Sum();
}
public sealed record ObatOperasi(string Kode, string Nama, string Satuan, double Harga);
public sealed record PakaiObatOperasi(ObatOperasi Obat, double Jumlah);
public sealed record LaporanOperasi(string DiagnosaPreOp, string DiagnosaPostOp, string Jaringan, DateTime Selesai,
    bool PermintaanPa, string NomorImplan, string Isi);
/// <param name="Tim">Kolom peran ke kode dokter atau NIP petugas; peran yang tidak diisi disimpan "-".</param>
public sealed record InputOperasi(DateTime Mulai, DateTime Selesai, string JenisAnestesi, string Kategori,
    IReadOnlyDictionary<string, string> Tim, IReadOnlyList<PaketOperasi> Paket, IReadOnlyList<PakaiObatOperasi> Obat,
    LaporanOperasi? Laporan);
/// <summary>Semua baris operasi pada satu waktu, seperti dihapus bersama oleh DlgCariTagihanOperasi.</summary>
public sealed record CatatanOperasi(DateTime Waktu, string Status, string Paket, double BiayaTindakan, double BiayaObat);

/// <summary>Tagihan operasi/VK rawat inap (DlgTagihanOperasi dan DlgCariTagihanOperasi) beserta jurnalnya.</summary>
public sealed class OperasiService(DatabaseSettings settings, string noRawat, AuthenticatedUser user)
{
    public const string Izin = "operasi";

    /// <summary>Urutan mengikuti rincian operasi pada billing rawat inap.</summary>
    public static readonly IReadOnlyList<KomponenOperasi> Komponen =
    [
        new("Operator 1", "operator1", "operator1", "biayaoperator1", true),
        new("Operator 2", "operator2", "operator2", "biayaoperator2", true),
        new("Operator 3", "operator3", "operator3", "biayaoperator3", true),
        new("Asisten Operator 1", "asisten_operator1", "asisten_operator1", "biayaasisten_operator1"),
        new("Asisten Operator 2", "asisten_operator2", "asisten_operator2", "biayaasisten_operator2"),
        new("Asisten Operator 3", "asisten_operator3", "asisten_operator3", "biayaasisten_operator3"),
        new("Instrumen", "instrumen", "instrumen", "biayainstrumen"),
        new("Dokter Anak", "dokter_anak", "dokter_anak", "biayadokter_anak", true),
        new("Perawat Resusitasi", "perawaat_resusitas", "perawaat_resusitas", "biayaperawaat_resusitas"),
        new("Dokter Anestesi", "dokter_anestesi", "dokter_anestesi", "biayadokter_anestesi", true),
        new("Asisten Anestesi 1", "asisten_anestesi", "asisten_anestesi", "biayaasisten_anestesi"),
        new("Asisten Anestesi 2", "asisten_anestesi2", "asisten_anestesi2", "biayaasisten_anestesi2"),
        new("Bidan 1", "bidan", "bidan", "biayabidan"),
        new("Bidan 2", "bidan2", "bidan2", "biayabidan2"),
        new("Bidan 3", "bidan3", "bidan3", "biayabidan3"),
        new("Perawat Luar", "perawat_luar", "perawat_luar", "biayaperawat_luar"),
        new("Alat", null, "alat", "biayaalat"),
        new("Sewa OK/VK", null, "sewa_ok", "biayasewaok"),
        new("Akomodasi", null, "akomodasi", "akomodasi"),
        new("Onloop 1", "omloop", "omloop", "biaya_omloop"),
        new("Onloop 2", "omloop2", "omloop2", "biaya_omloop2"),
        new("Onloop 3", "omloop3", "omloop3", "biaya_omloop3"),
        new("Onloop 4", "omloop4", "omloop4", "biaya_omloop4"),
        new("Onloop 5", "omloop5", "omloop5", "biaya_omloop5"),
        new("N.M.S.", null, "bagian_rs", "bagian_rs"),
        new("Sarpras", null, "sarpras", "biayasarpras"),
        new("Dokter PJ Anak", "dokter_pjanak", "dokter_pjanak", "biaya_dokter_pjanak", true),
        new("Dokter Umum", "dokter_umum", "dokter_umum", "biaya_dokter_umum", true),
    ];

    /// <summary>Isi enum operasi.kategori.</summary>
    public static readonly IReadOnlyList<string> Kategori =
    [
        "-", "Kecil", "Sedang", "Besar", "Khusus", "Elektive", "Emergency", "Ringan", "Ringan Cito", "Ringan Khusus",
        "Ringan Khusus Cito", "Ringan Khusus Cito Ganda", "Sedang Cito", "Sedang Ganda", "Sedang Khusus", "Sedang Khusus Cito",
        "Sedang Khusus Cito Ganda", "Sedang Khusus Ganda", "Besar Cito", "Besar Ganda", "Besar Khusus", "Besar Khusus Cito",
        "Besar Khusus Cito Ganda", "Besar Khusus Ganda",
    ];

    public async Task<(IReadOnlyList<PegawaiPilihan> Dokter, IReadOnlyList<PegawaiPilihan> Petugas)> PelaksanaAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var dokter = await db.QueryAsync("SELECT kd_dokter,nm_dokter FROM dokter WHERE kd_dokter<>'-' ORDER BY nm_dokter,kd_dokter");
        var petugas = await db.QueryAsync("SELECT nip,nama FROM petugas WHERE nip<>'-' ORDER BY nama,nip");
        return (dokter.Select(r => new PegawaiPilihan(r.S("kd_dokter"), r.S("nm_dokter"))).ToList(),
            petugas.Select(r => new PegawaiPilihan(r.S("nip"), r.S("nama"))).ToList());
    }

    /// <summary>Paket aktif sesuai cara bayar dan kelas kamar menurut set_tarif.</summary>
    public async Task<IReadOnlyList<PaketOperasi>> CariPaketAsync(string pencarian)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var visit = await KunjunganAsync(db, false);
        var (sql, kelas) = await SaringPaketAsync(db);
        var rows = await db.QueryAsync(sql + " AND (kode_paket LIKE @p2 OR nm_perawatan LIKE @p2) ORDER BY nm_perawatan,kode_paket LIMIT 500",
            visit.S("kd_pj"), kelas, "%" + pencarian.Trim() + "%");
        return rows.Select(Paket).ToList();
    }

    public async Task<IReadOnlyList<ObatOperasi>> CariObatAsync(string pencarian)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT o.kd_obat,o.nm_obat,COALESCE(s.satuan,o.kode_sat) satuan,o.hargasatuan
            FROM obatbhp_ok o LEFT JOIN kodesatuan s ON s.kode_sat=o.kode_sat
            WHERE o.kd_obat LIKE @p0 OR o.nm_obat LIKE @p0 ORDER BY o.nm_obat,o.kd_obat LIMIT 500
            """, "%" + pencarian.Trim() + "%");
        return rows.Select(r => new ObatOperasi(r.S("kd_obat"), r.S("nm_obat"), r.S("satuan"), r.D("hargasatuan"))).ToList();
    }

    public async Task<IReadOnlyList<CatatanOperasi>> DaftarAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        await KunjunganAsync(db, false);
        var rows = await db.QueryAsync("""
            SELECT o.*,COALESCE(p.nm_perawatan,o.kode_paket) nama_paket FROM operasi o
            LEFT JOIN paket_operasi p ON p.kode_paket=o.kode_paket
            WHERE o.no_rawat=@p0 ORDER BY o.tgl_operasi DESC,o.kode_paket
            """, noRawat);
        var obat = await db.QueryAsync(
            "SELECT tanggal,SUM(hargasatuan*jumlah) total FROM beri_obat_operasi WHERE no_rawat=@p0 GROUP BY tanggal", noRawat);
        var perWaktu = obat.ToDictionary(r => r.Dt("tanggal")!.Value, r => r.D("total"));
        return rows.GroupBy(r => r.Dt("tgl_operasi")!.Value).Select(g => new CatatanOperasi(g.Key, g.First().S("status"),
            string.Join(", ", g.Select(r => r.S("nama_paket"))), g.Sum(BiayaBaris), perWaktu.GetValueOrDefault(g.Key))).ToList();
    }

    public async Task<LaporanOperasi?> MuatLaporanAsync(DateTime waktu)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var r = await db.FirstAsync("SELECT * FROM laporan_operasi WHERE no_rawat=@p0 AND tanggal=@p1", noRawat, waktu);
        return r is null ? null : new(r.S("diagnosa_preop"), r.S("diagnosa_postop"), r.S("jaringan_dieksekusi"),
            r.Dt("selesaioperasi") ?? waktu, r.S("permintaan_pa") == "Ya", r.S("nomor_implan"), r.S("laporan_operasi"));
    }

    /// <summary>Ganti laporan operasi yang sudah tercatat. Seperti Khanza, tidak dibatasi status tagihan.</summary>
    public async Task SimpanLaporanAsync(DateTime waktu, LaporanOperasi laporan)
    {
        PastikanIzin();
        Validasi(laporan, waktu);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunjunganAsync(db, true);
        if (await db.FirstAsync("SELECT no_rawat FROM operasi WHERE no_rawat=@p0 AND tgl_operasi=@p1 LIMIT 1 FOR UPDATE", noRawat, waktu) is null)
            throw new InvalidOperationException("Operasi pada waktu tersebut sudah tidak ada. Muat ulang riwayat.");
        await db.ExecAsync("DELETE FROM laporan_operasi WHERE no_rawat=@p0 AND tanggal=@p1", noRawat, waktu);
        await SisipkanLaporanAsync(db, waktu, laporan);
        await db.CommitAsync();
    }

    public async Task SimpanAsync(InputOperasi input)
    {
        PastikanIzin();
        var mulai = Bulatkan(input.Mulai);
        if (Bulatkan(input.Selesai) <= mulai) throw new ArgumentException("Waktu selesai harus sesudah waktu mulai operasi.");
        var jenis = input.JenisAnestesi.Trim();
        if (jenis.Length is 0 or > 8) throw new ArgumentException("Isi jenis anestesi, maksimum 8 karakter.");
        if (!Kategori.Contains(input.Kategori)) throw new ArgumentException("Pilih kategori operasi.");
        if (input.Paket.Count == 0 || input.Paket.Select(p => p.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Paket.Count)
            throw new ArgumentException("Pilih satu atau lebih paket operasi yang berbeda.");
        if (input.Obat.Any(o => !double.IsFinite(o.Jumlah) || o.Jumlah <= 0)
            || input.Obat.Select(o => o.Obat.Kode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Obat.Count)
            throw new ArgumentException("Jumlah obat harus lebih dari nol dan tiap obat dicantumkan sekali.");
        var laporan = string.IsNullOrWhiteSpace(input.Laporan?.Isi) ? null : input.Laporan with { Selesai = input.Selesai };
        if (laporan is not null) Validasi(laporan, mulai);
        var tim = Komponen.Where(k => k.Peran is not null).ToDictionary(k => k.Peran!,
            k => input.Tim.TryGetValue(k.Peran!, out var kode) && !string.IsNullOrWhiteSpace(kode) ? kode : "-");

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var visit = await KunjunganBelumDitagihAsync(db);
        if (visit.S("status_lanjut") != "Ranap") throw new InvalidOperationException("Input operasi di sini hanya untuk pasien rawat inap.");
        if (await db.FirstAsync("""
                SELECT no_rawat FROM operasi WHERE no_rawat=@p0 AND tgl_operasi=@p1
                UNION ALL SELECT no_rawat FROM beri_obat_operasi WHERE no_rawat=@p0 AND tanggal=@p1 LIMIT 1
                """, noRawat, mulai) is not null)
            throw new InvalidOperationException("Sudah ada operasi atau obat operasi pada waktu itu. Hapus dulu atau ubah waktunya.");
        foreach (var komponen in Komponen.Where(k => k.Peran is not null && tim[k.Peran!] != "-"))
        {
            var sqlOrang = komponen.Dokter ? "SELECT COUNT(*) FROM dokter WHERE kd_dokter=@p0" : "SELECT COUNT(*) FROM petugas WHERE nip=@p0";
            if (await db.CountAsync(sqlOrang, tim[komponen.Peran!]) != 1)
                throw new ArgumentException($"{komponen.Label} tidak terdaftar.");
        }

        var (sql, kelas) = await SaringPaketAsync(db);
        double tindakan = 0, jasaDokter = 0, jasaPetugas = 0, bhp = 0;
        foreach (var pilihan in input.Paket.OrderBy(p => p.Kode, StringComparer.Ordinal))
        {
            var row = await db.FirstAsync(sql + " AND kode_paket=@p2 FOR UPDATE", visit.S("kd_pj"), kelas, pilihan.Kode);
            if (row is null || !Paket(row).Biaya.SequenceEqual(pilihan.Biaya))
                throw new InvalidOperationException($"Tarif paket {pilihan.Kode} berubah atau tidak berlaku. Cari ulang paket.");
            var biaya = Paket(row).Biaya;
            if (biaya.Any(b => !double.IsFinite(b) || b < 0)) throw new InvalidOperationException("Komponen tarif tidak valid.");
            var kolom = new List<string> { "no_rawat", "tgl_operasi", "jenis_anasthesi", "kategori", "kode_paket", "status" };
            var nilai = new List<object?> { noRawat, mulai, jenis, input.Kategori, pilihan.Kode, "Ranap" };
            for (var i = 0; i < Komponen.Count; i++)
            {
                var k = Komponen[i];
                if (k.Peran is not null)
                {
                    kolom.Add(k.Peran);
                    nilai.Add(tim[k.Peran]);
                    if (k.Dokter) jasaDokter += biaya[i]; else jasaPetugas += biaya[i];
                }
                kolom.Add(k.Biaya);
                nilai.Add(biaya[i]);
                tindakan += biaya[i];
            }
            await db.ExecAsync($"INSERT INTO operasi({string.Join(',', kolom)}) VALUES({string.Join(',', nilai.Select((_, i) => $"@p{i}"))})",
                nilai.ToArray());
        }
        foreach (var pakai in input.Obat.OrderBy(o => o.Obat.Kode, StringComparer.Ordinal))
        {
            var harga = await db.FirstAsync("SELECT hargasatuan FROM obatbhp_ok WHERE kd_obat=@p0 FOR UPDATE", pakai.Obat.Kode);
            if (harga is null || harga.D("hargasatuan") != pakai.Obat.Harga)
                throw new InvalidOperationException($"Harga {pakai.Obat.Nama} berubah. Cari ulang obat.");
            await db.ExecAsync("INSERT INTO beri_obat_operasi(no_rawat,tanggal,kd_obat,hargasatuan,jumlah) VALUES(@p0,@p1,@p2,@p3,@p4)",
                noRawat, mulai, pakai.Obat.Kode, pakai.Obat.Harga, pakai.Jumlah);
            bhp += pakai.Obat.Harga * pakai.Jumlah;
        }
        if (laporan is not null)
        {
            try { await SisipkanLaporanAsync(db, mulai, laporan); }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                throw new InvalidOperationException("Laporan operasi untuk waktu itu sudah ada.", ex);
            }
        }
        await PostingAsync(db, "Ranap", tindakan, jasaDokter, jasaPetugas, bhp, balik: false,
            $"OPERASI RAWAT INAP {noRawat}, DIPOSTING OLEH {user.KodeLegacy}");
        await db.CommitAsync();
    }

    /// <summary>Hapus seluruh operasi, obat dan laporan pada satu waktu, dengan jurnal pembalik. Ditolak bila sudah ditagih.</summary>
    public async Task HapusAsync(CatatanOperasi asal)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunjunganBelumDitagihAsync(db);
        var rows = await db.QueryAsync("SELECT * FROM operasi WHERE no_rawat=@p0 AND tgl_operasi=@p1 FOR UPDATE", noRawat, asal.Waktu);
        var obat = await db.QueryAsync("SELECT hargasatuan,jumlah FROM beri_obat_operasi WHERE no_rawat=@p0 AND tanggal=@p1 FOR UPDATE",
            noRawat, asal.Waktu);
        var tindakan = rows.Sum(BiayaBaris);
        var bhp = obat.Sum(r => r.D("hargasatuan") * r.D("jumlah"));
        if (rows.Count == 0 || tindakan != asal.BiayaTindakan || Math.Abs(bhp - asal.BiayaObat) > 0.001
            || rows.Any(r => r.S("status") != asal.Status))
            throw new InvalidOperationException("Data operasi sudah berubah atau dihapus. Muat ulang riwayat.");
        double Jasa(bool dokter) => rows.Sum(r => Komponen.Where(k => k.Peran is not null && k.Dokter == dokter).Sum(k => r.D(k.Biaya)));

        await db.ExecAsync("DELETE FROM operasi WHERE no_rawat=@p0 AND tgl_operasi=@p1", noRawat, asal.Waktu);
        await db.ExecAsync("DELETE FROM beri_obat_operasi WHERE no_rawat=@p0 AND tanggal=@p1", noRawat, asal.Waktu);
        await db.ExecAsync("DELETE FROM laporan_operasi WHERE no_rawat=@p0 AND tanggal=@p1", noRawat, asal.Waktu);
        var rawat = asal.Status == "Ralan" ? "JALAN" : "INAP";
        await PostingAsync(db, asal.Status, tindakan, Jasa(true), Jasa(false), bhp, balik: true,
            $"PEMBATALAN OPERASI RAWAT {rawat} {noRawat}, DIPOSTING OLEH {user.KodeLegacy}");
        await db.CommitAsync();
    }

    private async Task PostingAsync(Db db, string status, double tindakan, double jasaDokter, double jasaPetugas, double bhp,
        bool balik, string keterangan)
    {
        var ranap = status != "Ralan";
        var akhiran = ranap ? "Ranap" : "Ralan";
        var akun = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, ranap ? "set_akun_ranap" : "set_akun_ralan");
        if (ranap)
            foreach (var (kolom, rekening) in await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun_ranap2"))
                akun.TryAdd(kolom, rekening);
        var lines = new List<JurnalLine>();
        Pasang(tindakan + bhp, "Suspen_Piutang_Operasi", "Operasi");
        Pasang(jasaDokter, "Beban_Jasa_Medik_Dokter_Operasi", "Utang_Jasa_Medik_Dokter_Operasi");
        Pasang(jasaPetugas, "Beban_Jasa_Medik_Paramedis_Operasi", "Utang_Jasa_Medik_Paramedis_Operasi");
        Pasang(bhp, "HPP_Obat_Operasi", "Persediaan_Obat_Kamar_Operasi");
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noRawat, keterangan, lines);

        void Pasang(double nilai, string debit, string kredit)
        {
            if (nilai == 0) return;
            debit += "_" + akhiran;
            kredit += "_" + akhiran;
            lines.Add(new(akun.GetValueOrDefault(debit, ""), debit, balik ? 0 : nilai, balik ? nilai : 0));
            lines.Add(new(akun.GetValueOrDefault(kredit, ""), kredit, balik ? nilai : 0, balik ? 0 : nilai));
        }
    }

    private Task<int> SisipkanLaporanAsync(Db db, DateTime waktu, LaporanOperasi l) => db.ExecAsync("""
        INSERT INTO laporan_operasi(no_rawat,tanggal,diagnosa_preop,diagnosa_postop,jaringan_dieksekusi,selesaioperasi,permintaan_pa,nomor_implan,laporan_operasi)
        VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)
        """, noRawat, waktu, l.DiagnosaPreOp.Trim(), l.DiagnosaPostOp.Trim(), l.Jaringan.Trim(), Bulatkan(l.Selesai),
        l.PermintaanPa ? "Ya" : "Tidak", l.NomorImplan.Trim(), l.Isi.Trim());

    private static void Validasi(LaporanOperasi l, DateTime mulai)
    {
        if (string.IsNullOrWhiteSpace(l.Isi)) throw new ArgumentException("Isi laporan operasi.");
        if (l.DiagnosaPreOp.Length > 100 || l.DiagnosaPostOp.Length > 100 || l.Jaringan.Length > 100 || l.NomorImplan.Length > 50)
            throw new ArgumentException("Diagnosis dan jaringan maksimum 100 karakter, nomor implan 50 karakter.");
        if (l.Selesai < mulai) throw new ArgumentException("Waktu selesai tidak boleh sebelum waktu mulai operasi.");
    }

    private static DateTime Bulatkan(DateTime waktu) => waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));
    private static double BiayaBaris(Row r) => Komponen.Sum(k => r.D(k.Biaya));
    private static PaketOperasi Paket(Row r) =>
        new(r.S("kode_paket"), r.S("nm_perawatan"), r.S("kategori"), Komponen.Select(k => r.D(k.Paket)).ToList());

    /// <summary>Query paket dengan @p0 = cara bayar dan @p1 = kelas, serta nilai kelas kamar yang berlaku.</summary>
    private async Task<(string Sql, string Kelas)> SaringPaketAsync(Db db)
    {
        var aturan = await db.FirstAsync("SELECT cara_bayar_operasi,kelas_operasi FROM set_tarif LIMIT 1");
        var kelas = await db.StrAsync($"""
            SELECT k.kelas FROM kamar_inap i JOIN kamar k ON k.kd_kamar=i.kd_kamar
            WHERE i.no_rawat={RawatGabungService.PemilikKamar("@p0")} AND i.stts_pulang='-'
            ORDER BY i.tgl_masuk DESC,i.jam_masuk DESC LIMIT 1
            """, noRawat);
        var sql = $"SELECT kode_paket,nm_perawatan,kategori,{string.Join(',', Komponen.Select(k => k.Paket))} FROM paket_operasi WHERE status='1'";
        if (aturan?.S("cara_bayar_operasi") != "No") sql += " AND kd_pj IN (@p0,'-')";
        if (aturan?.S("kelas_operasi") != "No") sql += " AND kelas IN (@p1,'-')";
        return (sql, kelas);
    }

    private async Task<Row> KunjunganAsync(Db db, bool kunci) =>
        await db.FirstAsync("SELECT kd_pj,stts,status_lanjut FROM reg_periksa WHERE no_rawat=@p0" + (kunci ? " FOR UPDATE" : ""), noRawat)
        ?? throw new InvalidOperationException("Kunjungan tidak ditemukan.");

    private async Task<Row> KunjunganBelumDitagihAsync(Db db)
    {
        var visit = await KunjunganAsync(db, true);
        if (visit.S("stts") == "Batal" || await TagihanKunjungan.SudahDitagihAsync(db, noRawat, kunci: true))
            throw new InvalidOperationException("Registrasi batal atau tagihan sudah disimpan. Periksa di Kasir.");
        return visit;
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak Operasi.");
    }
}
