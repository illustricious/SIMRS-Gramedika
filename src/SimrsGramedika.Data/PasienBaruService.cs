using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Core.Entities;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

public record PilihanPasien(string Kode, string Nama)
{
    public override string ToString() => Nama;
}

public record Wilayah(string Kelurahan = "", string Kecamatan = "", string Kabupaten = "", string Propinsi = "");

public record PasienBaruData
{
    public string Nama { get; init; } = "";
    public string NoKtp { get; init; } = "";
    public JenisKelamin JenisKelamin { get; init; }
    public string TempatLahir { get; init; } = "";
    public DateOnly? TanggalLahir { get; init; }
    public string NamaIbu { get; init; } = "";
    public string Alamat { get; init; } = "";
    public Wilayah Wilayah { get; init; } = new();
    public GolonganDarah GolonganDarah { get; init; } = GolonganDarah.TidakDiketahui;
    public string Pekerjaan { get; init; } = "";
    public StatusPernikahan StatusPernikahan { get; init; }
    public string Agama { get; init; } = "-";
    public string Pendidikan { get; init; } = "-";
    public string NoTelepon { get; init; } = "";
    public string Email { get; init; } = "";
    public string KodePenjab { get; init; } = "";
    public string NoPeserta { get; init; } = "";
    public HubunganPenanggungJawab Keluarga { get; init; } = HubunganPenanggungJawab.DiriSendiri;
    public string NamaKeluarga { get; init; } = "";
    public string PekerjaanPj { get; init; } = "";
    public string AlamatPj { get; init; } = "";
    public Wilayah WilayahPj { get; init; } = new();
    public string SukuBangsa { get; init; } = "";
    public string Bahasa { get; init; } = "";
    public string CacatFisik { get; init; } = "";
    public string Perusahaan { get; init; } = "";

    /// <summary>Pasien yang menjadi penanggung jawabnya sendiri: isian P.J. kosong mengikuti data pasien.</summary>
    public PasienBaruData LengkapiPj()
    {
        if (Keluarga != HubunganPenanggungJawab.DiriSendiri) return this;
        static string Isi(string pj, string pasien) => string.IsNullOrWhiteSpace(pj) ? pasien : pj;
        var alamat = Alamat.Trim();
        return this with
        {
            NamaKeluarga = Isi(NamaKeluarga, Nama),
            PekerjaanPj = Isi(PekerjaanPj, Potong(Pekerjaan.Trim(), 35)),
            AlamatPj = Isi(AlamatPj, Potong(alamat, 100)),
            WilayahPj = new(Isi(WilayahPj.Kelurahan, Wilayah.Kelurahan), Isi(WilayahPj.Kecamatan, Wilayah.Kecamatan),
                Isi(WilayahPj.Kabupaten, Wilayah.Kabupaten), Isi(WilayahPj.Propinsi, Wilayah.Propinsi)),
        };
    }

    private static string Potong(string teks, int maks) => teks.Length <= maks ? teks : teks[..maks];
}

/// <summary>Satu aturan aktif dari set_kelengkapan_data_pasien.</summary>
public record AturanWajib(string Kolom, int Minimal);

public record ReferensiPasienBaru(
    string PerkiraanNoRm, IReadOnlyList<PilihanPasien> Penjab, IReadOnlyList<PilihanPasien> SukuBangsa,
    IReadOnlyList<PilihanPasien> Bahasa, IReadOnlyList<PilihanPasien> CacatFisik, IReadOnlyList<PilihanPasien> Perusahaan);

/// <summary>
/// Pendaftaran dan perubahan pasien seperti DlgPasien: validasi kelengkapan, kode wilayah menurut nama,
/// umur saat disimpan, dan nomor RM otomatis Khanza (lihat <see cref="PasienRepository.SaveNewAsync"/>).
/// </summary>
public class PasienBaruService(DatabaseSettings settings, AuthenticatedUser pengguna)
{
    public const string Izin = "pasien";

    /// <summary>Pilihan kolom pasien.pnd.</summary>
    public static readonly IReadOnlyList<string> Pendidikan =
        ["-", "TS", "TK", "SD", "SMP", "SMA", "SLTA/SEDERAJAT", "D1", "D2", "D3", "D4", "S1", "S2", "S3"];

    private static readonly (string Kolom, string Label, Func<PasienBaruData, DateOnly, string> Nilai)[] KolomWajib =
    [
        ("no_ktp", "No. KTP/SIM", (d, _) => d.NoKtp),
        ("tmp_lahir", "Tempat Lahir", (d, _) => d.TempatLahir),
        ("nm_ibu", "Nama Ibu", (d, _) => d.NamaIbu),
        ("alamat", "Alamat", (d, _) => d.Alamat),
        ("pekerjaan", "Pekerjaan", (d, _) => d.Pekerjaan),
        ("no_tlp", "No. Telepon", (d, _) => d.NoTelepon),
        ("umur", "Umur", (d, hari) => d.TanggalLahir is { } lahir ? HitungUmurLengkap(lahir, hari).Tahun.ToString() : ""),
        ("namakeluarga", "Nama P.J.", (d, _) => d.NamaKeluarga),
        ("no_peserta", "No. Peserta", (d, _) => d.NoPeserta),
        ("kelurahan", "Kelurahan", (d, _) => d.Wilayah.Kelurahan),
        ("kecamatan", "Kecamatan", (d, _) => d.Wilayah.Kecamatan),
        ("kabupaten", "Kabupaten", (d, _) => d.Wilayah.Kabupaten),
        ("propinsi", "Propinsi", (d, _) => d.Wilayah.Propinsi),
        ("pekerjaanpj", "Pekerjaan P.J.", (d, _) => d.PekerjaanPj),
        ("alamatpj", "Alamat P.J.", (d, _) => d.AlamatPj),
        ("kelurahanpj", "Kelurahan P.J.", (d, _) => d.WilayahPj.Kelurahan),
        ("kecamatanpj", "Kecamatan P.J.", (d, _) => d.WilayahPj.Kecamatan),
        ("kabupatenpj", "Kabupaten P.J.", (d, _) => d.WilayahPj.Kabupaten),
        ("propinsipj", "Propinsi P.J.", (d, _) => d.WilayahPj.Propinsi),
    ];

    public static (int Tahun, int Bulan, int Hari) HitungUmurLengkap(DateOnly lahir, DateOnly hariIni)
    {
        var bulan = (hariIni.Year - lahir.Year) * 12 + hariIni.Month - lahir.Month;
        if (lahir.AddMonths(bulan) > hariIni) bulan--;
        return (bulan / 12, bulan % 12, hariIni.DayNumber - lahir.AddMonths(bulan).DayNumber);
    }

    public static void Validasi(PasienBaruData data, IEnumerable<AturanWajib> aturan, DateOnly hariIni)
    {
        static void Wajib(string nilai, string label)
        {
            if (string.IsNullOrWhiteSpace(nilai)) throw new ArgumentException($"{label} wajib diisi.");
        }

        Wajib(data.Nama, "Nama Pasien");
        if (data.TanggalLahir is not { } lahir) throw new ArgumentException("Tanggal Lahir wajib diisi.");
        if (lahir > hariIni) throw new ArgumentException("Tanggal Lahir tidak boleh melewati hari ini.");
        Wajib(data.KodePenjab, "Jenis Bayar");
        Wajib(data.SukuBangsa, "Suku/Bangsa");
        Wajib(data.Bahasa, "Bahasa");
        Wajib(data.CacatFisik, "Cacat Fisik");
        Wajib(data.Perusahaan, "Perusahaan/Instansi");

        var minimal = aturan.ToDictionary(a => a.Kolom, a => a.Minimal);
        foreach (var (kolom, label, nilai) in KolomWajib)
            if (minimal.TryGetValue(kolom, out var panjang) && nilai(data, hariIni).Trim().Length < panjang)
                throw new ArgumentException($"{label} minimal {panjang} karakter.");

        // Batas lebar kolom tabel pasien, agar isian tidak terpotong diam-diam.
        foreach (var (nilai, maks, label) in new[]
                 {
                     (data.Nama, 40, "Nama Pasien"), (data.NoKtp, 20, "No. KTP/SIM"), (data.TempatLahir, 15, "Tempat Lahir"),
                     (data.NamaIbu, 40, "Nama Ibu"), (data.Alamat, 200, "Alamat"), (data.Pekerjaan, 60, "Pekerjaan"),
                     (data.NoTelepon, 40, "No. Telepon"), (data.Email, 50, "Email"), (data.NoPeserta, 25, "No. Peserta"),
                     (data.NamaKeluarga, 50, "Nama P.J."), (data.PekerjaanPj, 35, "Pekerjaan P.J."), (data.AlamatPj, 100, "Alamat P.J."),
                     (data.Wilayah.Kelurahan, 60, "Kelurahan"), (data.Wilayah.Kecamatan, 60, "Kecamatan"),
                     (data.Wilayah.Kabupaten, 60, "Kabupaten"), (data.Wilayah.Propinsi, 30, "Propinsi"),
                     (data.WilayahPj.Kelurahan, 60, "Kelurahan P.J."), (data.WilayahPj.Kecamatan, 60, "Kecamatan P.J."),
                     (data.WilayahPj.Kabupaten, 60, "Kabupaten P.J."), (data.WilayahPj.Propinsi, 30, "Propinsi P.J."),
                 })
            if (nilai.Trim().Length > maks) throw new ArgumentException($"{label} maksimal {maks} karakter.");
    }

    public async Task<ReferensiPasienBaru> ReferensiAsync()
    {
        PastikanBoleh();
        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        var nomor = await new PasienRepository(context).NextNoRekamMedisAsync();
        await using var db = await Db.OpenAsync(settings);
        async Task<IReadOnlyList<PilihanPasien>> Daftar(string sql) =>
            (await db.QueryAsync(sql)).Select(r => new PilihanPasien(r.S("kode"), r.S("nama"))).ToList();
        return new(nomor,
            await Daftar("SELECT kd_pj AS kode,png_jawab AS nama FROM penjab ORDER BY png_jawab"),
            await Daftar("SELECT id AS kode,nama_suku_bangsa AS nama FROM suku_bangsa ORDER BY nama_suku_bangsa"),
            await Daftar("SELECT id AS kode,nama_bahasa AS nama FROM bahasa_pasien ORDER BY nama_bahasa"),
            await Daftar("SELECT id AS kode,nama_cacat AS nama FROM cacat_fisik ORDER BY nama_cacat"),
            await Daftar("SELECT kode_perusahaan AS kode,nama_perusahaan AS nama FROM perusahaan_pasien ORDER BY nama_perusahaan"));
    }

    private async Task<List<AturanWajib>> AturanAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        var row = await db.FirstAsync("SELECT * FROM set_kelengkapan_data_pasien LIMIT 1");
        return row is null ? [] : KolomWajib.Where(k => row.S(k.Kolom) == "Yes")
            .Select(k => new AturanWajib(k.Kolom, (int)row.D("p_" + k.Kolom))).ToList();
    }

    /// <summary>Menyimpan pasien dan memajukan penomoran dalam satu transaksi. Mengembalikan No. RM.</summary>
    public async Task<string> SimpanAsync(PasienBaruData isian, DateOnly? hariIni = null)
    {
        var (data, today) = await SiapkanAsync(isian, hariIni);
        var pasien = new Pasien { TanggalDaftar = today };
        Terapkan(pasien, data, today);

        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        await new PasienRepository(context).SaveNewAsync(pasien, ct => TerapkanWilayahAsync(context, pasien, data.Wilayah, ct));
        return pasien.NoRekamMedis;
    }

    /// <summary>Isian pasien yang sudah terdaftar, termasuk nama wilayah.</summary>
    public async Task<PasienBaruData> MuatAsync(string noRekamMedis)
    {
        PastikanBoleh();
        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        var p = await context.PasienSet.AsNoTracking().SingleOrDefaultAsync(x => x.NoRekamMedis == noRekamMedis)
            ?? throw TidakDitemukan(noRekamMedis);
        await using var db = await Db.OpenAsync(settings);
        var nama = await db.FirstAsync("""
            SELECT k.nm_kel,c.nm_kec,b.nm_kab,r.nm_prop FROM pasien p
            LEFT JOIN kelurahan k ON k.kd_kel=p.kd_kel LEFT JOIN kecamatan c ON c.kd_kec=p.kd_kec
            LEFT JOIN kabupaten b ON b.kd_kab=p.kd_kab LEFT JOIN propinsi r ON r.kd_prop=p.kd_prop
            WHERE p.no_rkm_medis=@p0
            """, noRekamMedis) ?? throw TidakDitemukan(noRekamMedis);
        return new PasienBaruData
        {
            Nama = p.NamaPasien ?? "", NoKtp = p.NoKtp ?? "", JenisKelamin = p.JenisKelamin,
            TempatLahir = p.TempatLahir ?? "", TanggalLahir = p.TanggalLahir, NamaIbu = p.NamaIbu ?? "", Alamat = p.Alamat ?? "",
            Wilayah = new(nama.S("nm_kel"), nama.S("nm_kec"), nama.S("nm_kab"), nama.S("nm_prop")),
            GolonganDarah = p.GolonganDarah ?? GolonganDarah.TidakDiketahui, Pekerjaan = p.Pekerjaan ?? "",
            StatusPernikahan = p.StatusPernikahan ?? StatusPernikahan.Menikah, Agama = p.Agama ?? "-",
            Pendidikan = p.Pendidikan ?? "-", NoTelepon = p.NoTelepon ?? "", Email = p.Email ?? "",
            KodePenjab = p.KodePenjab, NoPeserta = p.NoPeserta ?? "", Keluarga = p.Keluarga,
            NamaKeluarga = p.NamaKeluarga ?? "", PekerjaanPj = p.PekerjaanPj ?? "", AlamatPj = p.AlamatPj ?? "",
            WilayahPj = new(p.KelurahanPj ?? "", p.KecamatanPj ?? "", p.KabupatenPj ?? "", p.PropinsiPj ?? ""),
            SukuBangsa = p.SukuBangsa.ToString(), Bahasa = p.BahasaPasien.ToString(), CacatFisik = p.CacatFisik.ToString(),
            Perusahaan = p.PerusahaanPasien ?? "",
        };
    }

    /// <summary>
    /// Mengubah identitas pasien dengan aturan yang sama seperti pendaftaran. Nomor RM, tanggal daftar, dan NIP tetap.
    /// </summary>
    public async Task UbahAsync(string noRekamMedis, PasienBaruData isian, DateOnly? hariIni = null)
    {
        var (data, today) = await SiapkanAsync(isian, hariIni);
        using var context = SimrsGramedikaDbContextFactory.Create(settings);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var pasien = await context.PasienSet.SingleOrDefaultAsync(p => p.NoRekamMedis == noRekamMedis)
            ?? throw TidakDitemukan(noRekamMedis);
        Terapkan(pasien, data, today);
        await TerapkanWilayahAsync(context, pasien, data.Wilayah, default);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static InvalidOperationException TidakDitemukan(string noRekamMedis) =>
        new($"Pasien dengan No. RM {noRekamMedis} tidak ditemukan.");

    private async Task<(PasienBaruData Data, DateOnly Hari)> SiapkanAsync(PasienBaruData isian, DateOnly? hariIni)
    {
        PastikanBoleh();
        var today = hariIni ?? DateOnly.FromDateTime(DateTime.Today);
        var data = isian.LengkapiPj();
        Validasi(data, await AturanAsync(), today);
        return (data, today);
    }

    private static string Strip(string teks) => string.IsNullOrWhiteSpace(teks) ? "-" : teks.Trim();

    private static void Terapkan(Pasien pasien, PasienBaruData data, DateOnly today)
    {
        var umur = HitungUmurLengkap(data.TanggalLahir!.Value, today);
        pasien.NamaPasien = data.Nama.Trim();
        pasien.NoKtp = data.NoKtp.Trim();
        pasien.JenisKelamin = data.JenisKelamin;
        pasien.TempatLahir = data.TempatLahir.Trim();
        pasien.TanggalLahir = data.TanggalLahir;
        pasien.NamaIbu = data.NamaIbu.Trim();
        pasien.Alamat = data.Alamat.Trim();
        pasien.GolonganDarah = data.GolonganDarah;
        pasien.Pekerjaan = data.Pekerjaan.Trim();
        pasien.StatusPernikahan = data.StatusPernikahan;
        pasien.Agama = data.Agama.Trim();
        pasien.NoTelepon = data.NoTelepon.Trim();
        pasien.Email = data.Email.Trim();
        pasien.KodePenjab = data.KodePenjab;
        pasien.Keluarga = data.Keluarga;
        pasien.NamaKeluarga = data.NamaKeluarga.Trim();
        pasien.PekerjaanPj = data.PekerjaanPj.Trim();
        pasien.AlamatPj = data.AlamatPj.Trim();
        pasien.Umur = $"{umur.Tahun} Th {umur.Bulan} Bl {umur.Hari} Hr";
        pasien.Pendidikan = data.Pendidikan;
        pasien.NoPeserta = data.NoPeserta.Trim();
        pasien.KelurahanPj = Strip(data.WilayahPj.Kelurahan);
        pasien.KecamatanPj = Strip(data.WilayahPj.Kecamatan);
        pasien.KabupatenPj = Strip(data.WilayahPj.Kabupaten);
        pasien.PropinsiPj = Strip(data.WilayahPj.Propinsi);
        pasien.PerusahaanPasien = data.Perusahaan;
        pasien.SukuBangsa = int.Parse(data.SukuBangsa);
        pasien.BahasaPasien = int.Parse(data.Bahasa);
        pasien.CacatFisik = int.Parse(data.CacatFisik);
    }

    /// <summary>Kode wilayah dicari menurut nama; nama baru ditambahkan ke master dalam transaksi pemanggil.</summary>
    internal static async Task TerapkanWilayahAsync(SimrsGramedikaDbContext context, Pasien pasien, Wilayah wilayah, CancellationToken ct)
    {
        // Nama tabel dan kolom berupa konstanta; hanya nama wilayah yang berasal dari pengguna.
#pragma warning disable EF1002 // hanya konstanta yang diinterpolasi
        async Task<int> Kode(string tabel, string kolomKode, string kolomNama, string nama)
        {
            var isi = Strip(nama);
            await context.Database.ExecuteSqlRawAsync($"INSERT IGNORE INTO {tabel}({kolomNama}) VALUES ({{0}})", [isi], ct);
            var kode = await context.Database
                .SqlQueryRaw<int>($"SELECT {kolomKode} AS `Value` FROM {tabel} WHERE {kolomNama}={{0}}", isi).ToListAsync(ct);
            return kode.Count == 1 ? kode[0] : throw new InvalidOperationException($"Kode {tabel} untuk \"{isi}\" tidak dapat ditentukan.");
        }
#pragma warning restore EF1002
        pasien.KodeKelurahan = await Kode("kelurahan", "kd_kel", "nm_kel", wilayah.Kelurahan);
        pasien.KodeKecamatan = await Kode("kecamatan", "kd_kec", "nm_kec", wilayah.Kecamatan);
        pasien.KodeKabupaten = await Kode("kabupaten", "kd_kab", "nm_kab", wilayah.Kabupaten);
        pasien.KodePropinsi = await Kode("propinsi", "kd_prop", "nm_prop", wilayah.Propinsi);
    }

    private void PastikanBoleh()
    {
        if (!pengguna.HasPermission(Izin))
            throw new InvalidOperationException("Akun tidak memiliki hak mengelola data pasien.");
    }
}
