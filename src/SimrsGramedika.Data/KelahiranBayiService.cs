using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <summary>Nilai APGAR pada satu waktu penilaian; tiap aspek 0, 1 atau 2. Kosong berarti belum diisi.</summary>
public sealed record Apgar(int? Jantung = null, int? Nafas = null, int? Tonus = null, int? Refleks = null, int? Warna = null)
{
    public IReadOnlyList<int?> Nilai => [Jantung, Nafas, Tonus, Refleks, Warna];
    public int? Total => Nilai.All(n => n is >= 0 and <= 2) ? Nilai.Sum() : null;
}

/// <summary>Satu data kelahiran: kolom pasien_bayi ditambah identitas bayi pada tabel pasien.</summary>
public sealed record KelahiranBayi
{
    /// <summary>Kosong untuk bayi yang belum terdaftar; No. RM dibuat saat disimpan.</summary>
    public string NoRekamMedis { get; init; } = "";
    public string Nama { get; init; } = "";
    public JenisKelamin JenisKelamin { get; init; }
    public DateOnly TanggalLahir { get; init; }
    public TimeOnly JamLahir { get; init; }
    public DateOnly TanggalDaftar { get; init; }
    public string NamaIbu { get; init; } = "";
    public string UmurIbu { get; init; } = "";
    public string NamaAyah { get; init; } = "";
    public string UmurAyah { get; init; } = "";
    public string Alamat { get; init; } = "";
    public string BeratBadan { get; init; } = "";
    public string PanjangBadan { get; init; } = "";
    public string LingkarKepala { get; init; } = "";
    public string LingkarDada { get; init; } = "";
    public string LingkarPerut { get; init; } = "";
    public string AnakKe { get; init; } = "";
    public string Gravida { get; init; } = "";
    public string Para { get; init; } = "";
    public string Abortus { get; init; } = "";
    public string ProsesLahir { get; init; } = "";
    public string Diagnosa { get; init; } = "";
    public string PenyulitKehamilan { get; init; } = "";
    public string Ketuban { get; init; } = "";
    public string Keterangan { get; init; } = "";
    public string Resusitasi { get; init; } = "";
    public string ObatDiberikan { get; init; } = "";
    public string Miksi { get; init; } = "";
    public string Mekonium { get; init; } = "";
    /// <summary>NIK pegawai penolong persalinan.</summary>
    public string Penolong { get; init; } = "";
    public string NamaPenolong { get; init; } = "";
    /// <summary>Kosong saat disimpan berarti memakai nomor otomatis.</summary>
    public string NoSkl { get; init; } = "";
    public Apgar Menit1 { get; init; } = new();
    public Apgar Menit5 { get; init; } = new();
    public Apgar Menit10 { get; init; } = new();
}

public sealed record BarisKelahiran(string NoRekamMedis, string Nama, string JenisKelamin, DateTime? TanggalLahir, string JamLahir,
    string NamaIbu, string BeratBadan, string PanjangBadan, string NoSkl, string Penolong);

/// <summary>Pasien yang bisa dipilih sebagai bayi atau sebagai ibu.</summary>
public sealed record PasienKelahiran(string NoRekamMedis, string Nama, JenisKelamin JenisKelamin, DateOnly? TanggalLahir,
    DateOnly? TanggalDaftar, string NamaIbu, string Alamat, string Keluarga, string NamaKeluarga)
{
    public override string ToString() => $"{NoRekamMedis} · {Nama} · {TanggalLahir:dd/MM/yyyy} · {Alamat}";
}

/// <summary>Data kelahiran bayi (DlgIKBBayi) tanpa bridging Dukcapil.</summary>
public sealed class KelahiranBayiService(DatabaseSettings settings, AuthenticatedUser user)
{
    public const string Izin = "kelahiran_bayi";

    // Kolom pasien_bayi: nama, nilai, panjang kolom dan label untuk pesan.
    private static readonly (string Kolom, string Label, int Maks, Func<KelahiranBayi, string> Nilai, Func<KelahiranBayi, string, KelahiranBayi> Isi)[] Teks =
    [
        ("umur_ibu", "Umur Ibu", 8, d => d.UmurIbu, (d, v) => d with { UmurIbu = v }),
        ("nama_ayah", "Nama Ayah", 50, d => d.NamaAyah, (d, v) => d with { NamaAyah = v }),
        ("umur_ayah", "Umur Ayah", 8, d => d.UmurAyah, (d, v) => d with { UmurAyah = v }),
        ("berat_badan", "Berat Badan", 10, d => d.BeratBadan, (d, v) => d with { BeratBadan = v }),
        ("panjang_badan", "Panjang Badan", 10, d => d.PanjangBadan, (d, v) => d with { PanjangBadan = v }),
        ("lingkar_kepala", "Lingkar Kepala", 10, d => d.LingkarKepala, (d, v) => d with { LingkarKepala = v }),
        ("lingkar_dada", "Lingkar Dada", 10, d => d.LingkarDada, (d, v) => d with { LingkarDada = v }),
        ("lingkar_perut", "Lingkar Perut", 10, d => d.LingkarPerut, (d, v) => d with { LingkarPerut = v }),
        ("anakke", "Anak Ke", 2, d => d.AnakKe, (d, v) => d with { AnakKe = v }),
        ("g", "Gravida", 10, d => d.Gravida, (d, v) => d with { Gravida = v }),
        ("p", "Para", 10, d => d.Para, (d, v) => d with { Para = v }),
        ("a", "Abortus", 10, d => d.Abortus, (d, v) => d with { Abortus = v }),
        ("proses_lahir", "Proses Lahir", 60, d => d.ProsesLahir, (d, v) => d with { ProsesLahir = v }),
        ("diagnosa", "Diagnosa", 60, d => d.Diagnosa, (d, v) => d with { Diagnosa = v }),
        ("penyulit_kehamilan", "Penyulit Kehamilan", 60, d => d.PenyulitKehamilan, (d, v) => d with { PenyulitKehamilan = v }),
        ("ketuban", "Ketuban", 60, d => d.Ketuban, (d, v) => d with { Ketuban = v }),
        ("keterangan", "Keterangan", 50, d => d.Keterangan, (d, v) => d with { Keterangan = v }),
        ("resusitas", "Resusitasi", 100, d => d.Resusitasi, (d, v) => d with { Resusitasi = v }),
        ("obat_diberikan", "Obat Diberikan", 300, d => d.ObatDiberikan, (d, v) => d with { ObatDiberikan = v }),
        ("mikasi", "Miksi", 100, d => d.Miksi, (d, v) => d with { Miksi = v }),
        ("mikonium", "Mekonium", 100, d => d.Mekonium, (d, v) => d with { Mekonium = v }),
    ];

    public async Task<IReadOnlyList<BarisKelahiran>> DaftarAsync(string cari, DateOnly dari, DateOnly sampai)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT b.no_rkm_medis,p.nm_pasien,p.jk,p.tgl_lahir,CAST(b.jam_lahir AS CHAR) jam,p.nm_ibu,b.berat_badan,b.panjang_badan,
                   COALESCE(b.no_skl,'') no_skl,COALESCE(g.nama,b.penolong,'') penolong
            FROM pasien_bayi b JOIN pasien p ON p.no_rkm_medis=b.no_rkm_medis LEFT JOIN pegawai g ON g.nik=b.penolong
            WHERE p.tgl_lahir BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',b.no_rkm_medis,p.nm_pasien,p.nm_ibu,b.nama_ayah,b.no_skl,g.nama) LIKE @p0
            ORDER BY p.tgl_lahir DESC,b.jam_lahir DESC LIMIT 500
            """, Mirip(cari), dari.ToDateTime(TimeOnly.MinValue), sampai.ToDateTime(TimeOnly.MinValue));
        return rows.Select(r => new BarisKelahiran(r.S("no_rkm_medis"), r.S("nm_pasien"), r.S("jk"), r.Dt("tgl_lahir"), r.S("jam"),
            r.S("nm_ibu"), r.S("berat_badan"), r.S("panjang_badan"), r.S("no_skl"), r.S("penolong"))).ToList();
    }

    public async Task<KelahiranBayi> MuatAsync(string noRekamMedis)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var r = await db.FirstAsync("""
            SELECT b.*,CAST(b.jam_lahir AS CHAR) jam,p.nm_pasien,p.jk,p.tgl_lahir,p.tgl_daftar,p.nm_ibu,p.alamat,
                   COALESCE(g.nama,'') nama_penolong
            FROM pasien_bayi b JOIN pasien p ON p.no_rkm_medis=b.no_rkm_medis LEFT JOIN pegawai g ON g.nik=b.penolong
            WHERE b.no_rkm_medis=@p0
            """, noRekamMedis) ?? throw new InvalidOperationException("Data kelahiran tidak ditemukan. Muat ulang daftar.");
        var data = new KelahiranBayi
        {
            NoRekamMedis = r.S("no_rkm_medis"), Nama = r.S("nm_pasien"), JenisKelamin = Kelamin(r.S("jk")),
            TanggalLahir = Tanggal(r.Dt("tgl_lahir")) ?? default, TanggalDaftar = Tanggal(r.Dt("tgl_daftar")) ?? default,
            JamLahir = TimeOnly.TryParse(r.S("jam"), out var jam) ? jam : default,
            NamaIbu = r.S("nm_ibu"), Alamat = r.S("alamat"), Penolong = r.S("penolong"), NamaPenolong = r.S("nama_penolong"),
            NoSkl = r.S("no_skl"), Menit1 = BacaApgar(r, "1"), Menit5 = BacaApgar(r, "5"), Menit10 = BacaApgar(r, "10"),
        };
        return Teks.Aggregate(data, (d, t) => t.Isi(d, r.S(t.Kolom)));
    }

    /// <summary>Pasien terdaftar yang belum punya data kelahiran.</summary>
    public Task<IReadOnlyList<PasienKelahiran>> CariBayiAsync(string cari) =>
        CariPasienAsync(cari, "NOT EXISTS(SELECT 1 FROM pasien_bayi b WHERE b.no_rkm_medis=p.no_rkm_medis)", "p.tgl_lahir DESC");

    public Task<IReadOnlyList<PasienKelahiran>> CariIbuAsync(string cari) => CariPasienAsync(cari, "p.jk='P'", "p.nm_pasien");

    private async Task<IReadOnlyList<PasienKelahiran>> CariPasienAsync(string cari, string syarat, string urut)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync($"""
            SELECT p.no_rkm_medis,p.nm_pasien,p.jk,p.tgl_lahir,p.tgl_daftar,p.nm_ibu,p.alamat,p.keluarga,p.namakeluarga FROM pasien p
            WHERE {syarat} AND CONCAT_WS(' ',p.no_rkm_medis,p.nm_pasien,p.alamat) LIKE @p0
            ORDER BY {urut},p.no_rkm_medis LIMIT 100
            """, Mirip(cari));
        return rows.Select(r => new PasienKelahiran(r.S("no_rkm_medis"), r.S("nm_pasien"), Kelamin(r.S("jk")),
            Tanggal(r.Dt("tgl_lahir")), Tanggal(r.Dt("tgl_daftar")), r.S("nm_ibu"), r.S("alamat"), r.S("keluarga"),
            r.S("namakeluarga"))).ToList();
    }

    public async Task<IReadOnlyList<PilihanPasien>> CariPenolongAsync(string cari)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT nik,nama FROM pegawai WHERE stts_aktif<>'KELUAR' AND CONCAT_WS(' ',nik,nama) LIKE @p0 ORDER BY nama LIMIT 100
            """, Mirip(cari));
        return rows.Select(r => new PilihanPasien(r.S("nik"), r.S("nama"))).ToList();
    }

    public async Task<(string NoRekamMedis, string NoSkl)> PerkiraanNomorAsync(DateOnly tanggalLahir)
    {
        PastikanIzin();
        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        return (await new PasienRepository(context).NextNoRekamMedisAsync(), await NoSklBerikutAsync(context, tanggalLahir, default));
    }

    /// <summary>
    /// Menyimpan data kelahiran. Bayi yang belum terdaftar didaftarkan sebagai pasien dalam transaksi yang sama;
    /// untuk pasien yang sudah ada, identitasnya tidak diubah.
    /// </summary>
    public async Task<(string NoRekamMedis, string NoSkl)> SimpanAsync(KelahiranBayi isian, DateOnly? hariIni = null)
    {
        PastikanIzin();
        var today = hariIni ?? DateOnly.FromDateTime(DateTime.Today);
        var data = Rapikan(isian);
        var baru = data.NoRekamMedis.Length == 0;
        if (baru) PeriksaIdentitas(data, today);
        PeriksaKelahiran(data);
        var noSkl = "";
        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        await PastikanPenolongAsync(context, data.Penolong);

        if (baru)
        {
            var umur = PasienBaruService.HitungUmurLengkap(data.TanggalLahir, today);
            var alamat = Strip(data.Alamat);
            // Isian "-" mengikuti pendaftaran bayi pada Khanza.
            var pasien = new Pasien
            {
                NamaPasien = data.Nama, NoKtp = "-", JenisKelamin = data.JenisKelamin, TempatLahir = "-",
                TanggalLahir = data.TanggalLahir, NamaIbu = data.NamaIbu, Alamat = alamat,
                GolonganDarah = GolonganDarah.TidakDiketahui, Pekerjaan = "-", StatusPernikahan = StatusPernikahan.BelumMenikah,
                Agama = "-", TanggalDaftar = data.TanggalDaftar, NoTelepon = "0", Email = "-", KodePenjab = "-", NoPeserta = "-",
                Keluarga = HubunganPenanggungJawab.Ayah, NamaKeluarga = data.NamaAyah,
                AlamatPj = alamat.Length > 100 ? alamat[..100] : alamat,
                Umur = $"{umur.Tahun} Th {umur.Bulan} Bl {umur.Hari} Hr", Pendidikan = "-", PerusahaanPasien = "-",
            };
            await new PasienRepository(context).SaveNewAsync(pasien,
                ct => IsiMasterKosongAsync(context, pasien, ct),
                sesudahSimpan: async ct => noSkl = await SisipkanAsync(context, data with { NoRekamMedis = pasien.NoRekamMedis }, ct));
            return (pasien.NoRekamMedis, noSkl);
        }

        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await KunciPasienAsync(context, data.NoRekamMedis)) throw new InvalidOperationException("Pasien tidak ditemukan.");
        if (await AdaDataAsync(context, data.NoRekamMedis))
            throw new InvalidOperationException("Pasien ini sudah punya data kelahiran. Pilih dari daftar untuk mengubahnya.");
        noSkl = await SisipkanAsync(context, data, default);
        await tx.CommitAsync();
        return (data.NoRekamMedis, noSkl);
    }

    /// <param name="dilihat">Data yang dimuat ke layar; perubahan ditolak bila isinya sudah berbeda.</param>
    public async Task UbahAsync(KelahiranBayi dilihat, KelahiranBayi isian, DateOnly? hariIni = null)
    {
        PastikanIzin();
        var today = hariIni ?? DateOnly.FromDateTime(DateTime.Today);
        var data = Rapikan(isian) with { NoRekamMedis = dilihat.NoRekamMedis };
        if (data.NoSkl.Length == 0) data = data with { NoSkl = dilihat.NoSkl };
        PeriksaIdentitas(data, today);
        PeriksaKelahiran(data);
        if (data.NoSkl.Length == 0) throw new ArgumentException("No. SKL wajib diisi.");

        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        await PastikanPenolongAsync(context, data.Penolong);
        await using var tx = await context.Database.BeginTransactionAsync();
        await KunciDanCocokkanAsync(context, dilihat);
        var umur = PasienBaruService.HitungUmurLengkap(data.TanggalLahir, today);
        // Nama keluarga hanya mengikuti nama ayah bila penanggung jawab pasien memang ayahnya.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE pasien SET nm_pasien={data.Nama},jk={Huruf(data.JenisKelamin)},tgl_lahir={data.TanggalLahir},
                tgl_daftar={data.TanggalDaftar},umur={$"{umur.Tahun} Th {umur.Bulan} Bl {umur.Hari} Hr"},nm_ibu={data.NamaIbu},
                namakeluarga=IF(keluarga='AYAH' AND {data.NamaAyah}<>'',{data.NamaAyah},namakeluarga)
            WHERE no_rkm_medis={data.NoRekamMedis}
            """);
        var kolom = Kolom(data, data.NoSkl);
        var set = string.Join(",", kolom.Select((k, i) => $"{k.Nama}={{{i}}}"));
        var args = kolom.Select(k => k.Nilai).Append(data.NoRekamMedis).ToArray();
        try
        {
#pragma warning disable EF1002 // nama kolom konstanta; semua nilai lewat parameter
            await context.Database.ExecuteSqlRawAsync($"UPDATE pasien_bayi SET {set} WHERE no_rkm_medis={{{kolom.Count}}}", args);
#pragma warning restore EF1002
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new InvalidOperationException($"No. SKL {data.NoSkl} sudah dipakai data kelahiran lain.", ex);
        }
        await tx.CommitAsync();
    }

    /// <summary>Menghapus data kelahiran; data pasien bayinya tetap ada, seperti Khanza.</summary>
    public async Task HapusAsync(KelahiranBayi dilihat)
    {
        PastikanIzin();
        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        await using var tx = await context.Database.BeginTransactionAsync();
        await KunciDanCocokkanAsync(context, dilihat);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM pasien_bayi WHERE no_rkm_medis={dilihat.NoRekamMedis}");
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM bridging_dukcapil WHERE no_rkm_medis={dilihat.NoRekamMedis}");
        await tx.CommitAsync();
    }

    private async Task KunciDanCocokkanAsync(SimrsGramedikaDbContext context, KelahiranBayi dilihat)
    {
        var rm = dilihat.NoRekamMedis;
        await KunciPasienAsync(context, rm);
        await context.Database.SqlQuery<string>($"SELECT no_rkm_medis AS Value FROM pasien_bayi WHERE no_rkm_medis={rm} FOR UPDATE")
            .ToListAsync();
        // Baris sudah terkunci, sehingga pembacaan lewat koneksi lain melihat isi yang tidak akan berubah lagi.
        if (await MuatAsync(rm) != dilihat)
            throw new InvalidOperationException("Data kelahiran sudah berubah. Pilih lagi dari daftar.");
    }

    private static async Task<bool> KunciPasienAsync(SimrsGramedikaDbContext context, string noRekamMedis) =>
        (await context.Database.SqlQuery<string>(
            $"SELECT no_rkm_medis AS Value FROM pasien WHERE no_rkm_medis={noRekamMedis} FOR UPDATE").ToListAsync()).Count == 1;

    private static async Task<bool> AdaDataAsync(SimrsGramedikaDbContext context, string noRekamMedis) =>
        await context.Database.SqlQuery<bool>(
            $"SELECT EXISTS(SELECT 1 FROM pasien_bayi WHERE no_rkm_medis={noRekamMedis}) AS Value").SingleAsync();

    private static async Task PastikanPenolongAsync(SimrsGramedikaDbContext context, string nik)
    {
        if (!await context.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM pegawai WHERE nik={nik}) AS Value").SingleAsync())
            throw new ArgumentException("Penolong tidak ditemukan pada data pegawai.");
    }

    private static async Task<string> NoSklBerikutAsync(SimrsGramedikaDbContext context, DateOnly lahir, CancellationToken ct)
    {
        var awal = new DateOnly(lahir.Year, lahir.Month, 1);
        var akhir = awal.AddMonths(1).AddDays(-1);
        var terakhir = await context.Database.SqlQuery<long>($"""
            SELECT CAST(COALESCE(MAX(CAST(LEFT(b.no_skl,4) AS SIGNED)),0) AS SIGNED) AS Value
            FROM pasien_bayi b JOIN pasien p ON p.no_rkm_medis=b.no_rkm_medis WHERE p.tgl_lahir BETWEEN {awal} AND {akhir}
            """).SingleAsync(ct);
        return $"{terakhir + 1:0000}/RM-SKL/{lahir:MM'/'yyyy}";
    }

    private static List<(string Nama, object Nilai)> Kolom(KelahiranBayi d, string noSkl)
    {
        var kolom = Teks.Select(t => (t.Kolom, (object)t.Nilai(d))).ToList();
        kolom.Add(("jam_lahir", d.JamLahir.ToTimeSpan()));
        kolom.Add(("penolong", d.Penolong));
        kolom.Add(("no_skl", noSkl));
        foreach (var (menit, apgar) in new[] { ("1", d.Menit1), ("5", d.Menit5), ("10", d.Menit10) })
        {
            foreach (var (huruf, nilai) in new[] { "f", "u", "t", "r", "w" }.Zip(apgar.Nilai))
                kolom.Add((huruf + menit, nilai!.Value.ToString()));
            kolom.Add(("n" + menit, apgar.Total!.Value.ToString()));
        }
        return kolom;
    }

    /// <summary>Menulis baris pasien_bayi dalam transaksi pemanggil. Mengembalikan No. SKL yang dipakai.</summary>
    private static async Task<string> SisipkanAsync(SimrsGramedikaDbContext context, KelahiranBayi data, CancellationToken ct)
    {
        // Nomor SKL otomatis dihitung dari tanggal lahir yang tersimpan pada pasien.
        var lahir = data.NoSkl.Length > 0 ? data.TanggalLahir : DateOnly.FromDateTime(await context.Database.SqlQuery<DateTime>(
            $"SELECT CAST(tgl_lahir AS DATETIME) AS Value FROM pasien WHERE no_rkm_medis={data.NoRekamMedis}").SingleAsync(ct));
        var noSkl = data.NoSkl.Length > 0 ? data.NoSkl : await NoSklBerikutAsync(context, lahir, ct);
        var kolom = Kolom(data, noSkl);
        kolom.Insert(0, ("no_rkm_medis", data.NoRekamMedis));
        var nama = string.Join(",", kolom.Select(k => k.Nama));
        var tempat = string.Join(",", kolom.Select((_, i) => $"{{{i}}}"));
        try
        {
#pragma warning disable EF1002 // nama kolom konstanta; semua nilai lewat parameter
            await context.Database.ExecuteSqlRawAsync($"INSERT INTO pasien_bayi({nama}) VALUES ({tempat})",
                kolom.Select(k => k.Nilai), ct);
#pragma warning restore EF1002
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new InvalidOperationException($"No. SKL {noSkl} sudah dipakai data kelahiran lain.", ex);
        }
        return noSkl;
    }

    /// <summary>Kode "-" untuk wilayah, suku, bahasa, cacat fisik, perusahaan dan penjamin, ditambahkan bila belum ada.</summary>
    private static async Task IsiMasterKosongAsync(SimrsGramedikaDbContext context, Pasien pasien, CancellationToken ct)
    {
        await context.Database.ExecuteSqlRawAsync("""
            INSERT IGNORE INTO penjab(kd_pj,png_jawab,nama_perusahaan,alamat_asuransi,no_telp,attn,status)
            VALUES ('-','-','-','-','-','-','0')
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT IGNORE INTO perusahaan_pasien(kode_perusahaan,nama_perusahaan,alamat,kota,no_telp) VALUES ('-','-','-','-','-')", ct);
        await context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO suku_bangsa(nama_suku_bangsa) VALUES ('-')", ct);
        await context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO bahasa_pasien(nama_bahasa) VALUES ('-')", ct);
        await context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO cacat_fisik(nama_cacat) VALUES ('-')", ct);
        pasien.SukuBangsa = await context.Database
            .SqlQuery<int>($"SELECT id AS Value FROM suku_bangsa WHERE nama_suku_bangsa='-'").SingleAsync(ct);
        pasien.BahasaPasien = await context.Database
            .SqlQuery<int>($"SELECT id AS Value FROM bahasa_pasien WHERE nama_bahasa='-'").SingleAsync(ct);
        pasien.CacatFisik = await context.Database
            .SqlQuery<int>($"SELECT id AS Value FROM cacat_fisik WHERE nama_cacat='-'").SingleAsync(ct);
        await PasienBaruService.TerapkanWilayahAsync(context, pasien, new Wilayah(), ct);
    }

    private static KelahiranBayi Rapikan(KelahiranBayi d)
    {
        var hasil = d with
        {
            NoRekamMedis = d.NoRekamMedis.Trim(), Nama = d.Nama.Trim(), NamaIbu = d.NamaIbu.Trim(), Alamat = d.Alamat.Trim(),
            Penolong = d.Penolong.Trim(), NoSkl = d.NoSkl.Trim(),
        };
        return Teks.Aggregate(hasil, (data, t) => t.Isi(data, t.Nilai(data).Trim()));
    }

    private static void PeriksaIdentitas(KelahiranBayi d, DateOnly hariIni)
    {
        if (d.Nama.Length == 0) throw new ArgumentException("Nama bayi wajib diisi.");
        if (d.TanggalLahir.Year < 1900 || d.TanggalLahir > hariIni)
            throw new ArgumentException("Tanggal lahir belum diisi atau melewati hari ini.");
        if (d.TanggalDaftar < d.TanggalLahir || d.TanggalDaftar > hariIni)
            throw new ArgumentException("Tanggal daftar harus di antara tanggal lahir dan hari ini.");
        foreach (var (nilai, maks, label) in new[] { (d.Nama, 40, "Nama bayi"), (d.NamaIbu, 40, "Nama Ibu"), (d.Alamat, 200, "Alamat") })
            if (nilai.Length > maks) throw new ArgumentException($"{label} maksimal {maks} karakter.");
    }

    private static void PeriksaKelahiran(KelahiranBayi d)
    {
        if (d.Penolong.Length == 0) throw new ArgumentException("Penolong wajib dipilih.");
        if (d.NoSkl.Length > 30) throw new ArgumentException("No. SKL maksimal 30 karakter.");
        foreach (var t in Teks)
            if (t.Nilai(d).Length > t.Maks) throw new ArgumentException($"{t.Label} maksimal {t.Maks} karakter.");
        if (d.Menit1.Total is null || d.Menit5.Total is null || d.Menit10.Total is null)
            throw new ArgumentException("Nilai APGAR menit 1, 5 dan 10 harus lengkap, masing-masing 0, 1 atau 2.");
    }

    private static Apgar BacaApgar(Row r, string menit)
    {
        int? Nilai(string huruf) => int.TryParse(r.S(huruf + menit), out var n) && n is >= 0 and <= 2 ? n : null;
        return new(Nilai("f"), Nilai("u"), Nilai("t"), Nilai("r"), Nilai("w"));
    }

    private static DateOnly? Tanggal(DateTime? nilai) => nilai is { } t ? DateOnly.FromDateTime(t) : null;
    private static JenisKelamin Kelamin(string jk) => jk == "P" ? JenisKelamin.Perempuan : JenisKelamin.LakiLaki;
    private static string Huruf(JenisKelamin jk) => jk == JenisKelamin.Perempuan ? "P" : "L";
    private static string Strip(string teks) => teks.Length == 0 ? "-" : teks;
    private static string Mirip(string cari) => "%" + cari.Trim() + "%";

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak Kelahiran Bayi.");
    }
}
