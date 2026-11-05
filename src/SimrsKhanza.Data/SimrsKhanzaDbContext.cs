using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

/// <summary>
/// EF Core mapping onto the legacy `sik` MySQL schema (structure documented in
/// docs/audit/DB_SCHEMA.md, extracted from docs/audit/schema_sik.sql — schema
/// only, no patient data). Table/column names below match the legacy snake_case
/// names exactly so this can point at an existing `sik` database without any
/// migration of old data.
/// </summary>
public class SimrsKhanzaDbContext(DbContextOptions<SimrsKhanzaDbContext> options) : DbContext(options)
{
    public DbSet<Pasien> PasienSet => Set<Pasien>();
    public DbSet<RegPeriksa> RegPeriksaSet => Set<RegPeriksa>();
    public DbSet<Dokter> DokterSet => Set<Dokter>();
    public DbSet<Pegawai> PegawaiSet => Set<Pegawai>();
    public DbSet<Petugas> PetugasSet => Set<Petugas>();
    public DbSet<Penjab> PenjabSet => Set<Penjab>();
    public DbSet<Poliklinik> PoliklinikSet => Set<Poliklinik>();
    public DbSet<Bangsal> BangsalSet => Set<Bangsal>();
    public DbSet<Kamar> KamarSet => Set<Kamar>();
    public DbSet<KamarInap> KamarInapSet => Set<KamarInap>();
    public DbSet<PemeriksaanRalan> PemeriksaanRalanSet => Set<PemeriksaanRalan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pasien>(e =>
        {
            e.ToTable("pasien");
            e.HasKey(p => p.NoRekamMedis);
            e.Property(p => p.NoRekamMedis).HasColumnName("no_rkm_medis").HasMaxLength(15);
            e.Property(p => p.NamaPasien).HasColumnName("nm_pasien").HasMaxLength(40);
            e.Property(p => p.NoKtp).HasColumnName("no_ktp").HasMaxLength(20);
            e.Property(p => p.TempatLahir).HasColumnName("tmp_lahir").HasMaxLength(15);
            e.Property(p => p.TanggalLahir).HasColumnName("tgl_lahir");
            e.Property(p => p.NamaIbu).HasColumnName("nm_ibu").HasMaxLength(40);
            e.Property(p => p.Alamat).HasColumnName("alamat").HasMaxLength(200);
            e.Property(p => p.Pekerjaan).HasColumnName("pekerjaan").HasMaxLength(60);
            e.Property(p => p.Agama).HasColumnName("agama").HasMaxLength(12);
            e.Property(p => p.TanggalDaftar).HasColumnName("tgl_daftar");
            e.Property(p => p.NoTelepon).HasColumnName("no_tlp").HasMaxLength(40);
            e.Property(p => p.KodePenjab).HasColumnName("kd_pj").HasMaxLength(3);
            e.Ignore(p => p.JenisKelamin); // enum mapping deferred — legacy column `jk` is MySQL ENUM('L','P')
            e.Ignore(p => p.GolonganDarah);
            e.Ignore(p => p.StatusPernikahan);
        });

        modelBuilder.Entity<RegPeriksa>(e =>
        {
            e.ToTable("reg_periksa");
            e.HasKey(r => r.NoRawat);
            e.Property(r => r.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(r => r.TanggalRegistrasi).HasColumnName("tgl_registrasi");
            e.Property(r => r.JamRegistrasi).HasColumnName("jam_reg");
            e.Property(r => r.NoRekamMedis).HasColumnName("no_rkm_medis").HasMaxLength(15);
            e.Property(r => r.KodePoli).HasColumnName("kd_poli").HasMaxLength(5);
            e.Property(r => r.KodeDokter).HasColumnName("kd_dokter").HasMaxLength(20);
            e.Property(r => r.KodePenjab).HasColumnName("kd_pj").HasMaxLength(3);
            e.Ignore(r => r.Status);
            e.Ignore(r => r.StatusLanjut);
            e.HasOne(r => r.Pasien).WithMany().HasForeignKey(r => r.NoRekamMedis);
            e.HasOne(r => r.Poliklinik).WithMany().HasForeignKey(r => r.KodePoli);
            e.HasOne(r => r.Dokter).WithMany().HasForeignKey(r => r.KodeDokter);
            e.HasOne(r => r.Penjab).WithMany().HasForeignKey(r => r.KodePenjab);
        });

        modelBuilder.Entity<Dokter>(e =>
        {
            e.ToTable("dokter");
            e.HasKey(d => d.KodeDokter);
            e.Property(d => d.KodeDokter).HasColumnName("kd_dokter").HasMaxLength(20);
            e.Property(d => d.NamaDokter).HasColumnName("nm_dokter").HasMaxLength(50);
            e.Property(d => d.KodeSpesialis).HasColumnName("kd_sps").HasMaxLength(5);
            e.Property(d => d.NoIjinPraktek).HasColumnName("no_ijn_praktek").HasMaxLength(120);
            e.Ignore(d => d.Aktif); // legacy column `status` is ENUM('0','1')
            e.HasOne(d => d.Pegawai).WithMany().HasForeignKey(d => d.KodeDokter).HasPrincipalKey(p => p.Nik);
        });

        modelBuilder.Entity<Pegawai>(e =>
        {
            e.ToTable("pegawai");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Nik).HasColumnName("nik").HasMaxLength(20);
            e.Property(p => p.Nama).HasColumnName("nama").HasMaxLength(50);
            e.Property(p => p.Jabatan).HasColumnName("jbtn").HasMaxLength(25);
            e.Property(p => p.MulaiKerja).HasColumnName("mulai_kerja");
            e.Ignore(p => p.StatusAktif); // legacy column `stts_aktif` is MySQL ENUM
        });

        modelBuilder.Entity<Petugas>(e =>
        {
            e.ToTable("petugas");
            e.HasKey(p => p.Nip);
            e.Property(p => p.Nip).HasColumnName("nip").HasMaxLength(20);
            e.Property(p => p.Nama).HasColumnName("nama").HasMaxLength(50);
            e.Property(p => p.KodeJabatan).HasColumnName("kd_jbtn").HasMaxLength(4);
            e.Ignore(p => p.Aktif); // legacy column `status` is ENUM('0','1')
            e.HasOne(p => p.Pegawai).WithMany().HasForeignKey(p => p.Nip).HasPrincipalKey(pg => pg.Nik);
        });

        modelBuilder.Entity<Penjab>(e =>
        {
            e.ToTable("penjab");
            e.HasKey(p => p.KodePenjab);
            e.Property(p => p.KodePenjab).HasColumnName("kd_pj").HasMaxLength(3);
            e.Property(p => p.NamaPenanggungJawab).HasColumnName("png_jawab").HasMaxLength(30);
            e.Property(p => p.NamaPerusahaan).HasColumnName("nama_perusahaan").HasMaxLength(60);
            e.Ignore(p => p.Aktif);
        });

        modelBuilder.Entity<Poliklinik>(e =>
        {
            e.ToTable("poliklinik");
            e.HasKey(p => p.KodePoli);
            e.Property(p => p.KodePoli).HasColumnName("kd_poli").HasMaxLength(5);
            e.Property(p => p.NamaPoli).HasColumnName("nm_poli").HasMaxLength(50);
            e.Property(p => p.BiayaRegistrasi).HasColumnName("registrasi");
            e.Ignore(p => p.Aktif);
        });

        modelBuilder.Entity<Bangsal>(e =>
        {
            e.ToTable("bangsal");
            e.HasKey(b => b.KodeBangsal);
            e.Property(b => b.KodeBangsal).HasColumnName("kd_bangsal").HasMaxLength(5);
            e.Property(b => b.NamaBangsal).HasColumnName("nm_bangsal").HasMaxLength(30);
            e.Ignore(b => b.Aktif);
        });

        modelBuilder.Entity<Kamar>(e =>
        {
            e.ToTable("kamar");
            e.HasKey(k => k.KodeKamar);
            e.Property(k => k.KodeKamar).HasColumnName("kd_kamar").HasMaxLength(15);
            e.Property(k => k.KodeBangsal).HasColumnName("kd_bangsal").HasMaxLength(5);
            e.Property(k => k.TarifKamar).HasColumnName("trf_kamar");
            e.Ignore(k => k.Status);
            e.Ignore(k => k.Kelas);
            e.HasOne(k => k.Bangsal).WithMany().HasForeignKey(k => k.KodeBangsal);
        });

        modelBuilder.Entity<KamarInap>(e =>
        {
            e.ToTable("kamar_inap");
            e.HasKey(k => new { k.NoRawat, k.TanggalMasuk, k.JamMasuk });
            e.Property(k => k.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(k => k.KodeKamar).HasColumnName("kd_kamar").HasMaxLength(15);
            e.Property(k => k.TanggalMasuk).HasColumnName("tgl_masuk");
            e.Property(k => k.JamMasuk).HasColumnName("jam_masuk");
            e.Property(k => k.TanggalKeluar).HasColumnName("tgl_keluar");
            e.Property(k => k.JamKeluar).HasColumnName("jam_keluar");
            e.Property(k => k.DiagnosaAwal).HasColumnName("diagnosa_awal").HasMaxLength(100);
            e.Property(k => k.DiagnosaAkhir).HasColumnName("diagnosa_akhir").HasMaxLength(100);
            e.HasOne(k => k.Kamar).WithMany().HasForeignKey(k => k.KodeKamar);
            e.HasOne(k => k.RegPeriksa).WithMany().HasForeignKey(k => k.NoRawat);
        });

        modelBuilder.Entity<PemeriksaanRalan>(e =>
        {
            e.ToTable("pemeriksaan_ralan");
            e.HasKey(p => new { p.NoRawat, p.TanggalPerawatan, p.JamRawat });
            e.Property(p => p.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(p => p.TanggalPerawatan).HasColumnName("tgl_perawatan");
            e.Property(p => p.JamRawat).HasColumnName("jam_rawat");
            e.Property(p => p.SuhuTubuh).HasColumnName("suhu_tubuh").HasMaxLength(5);
            e.Property(p => p.Tensi).HasColumnName("tensi").HasMaxLength(8);
            e.Property(p => p.Nadi).HasColumnName("nadi").HasMaxLength(3);
            e.Property(p => p.Respirasi).HasColumnName("respirasi").HasMaxLength(3);
            e.Property(p => p.Tinggi).HasColumnName("tinggi").HasMaxLength(5);
            e.Property(p => p.Berat).HasColumnName("berat").HasMaxLength(5);
            e.Property(p => p.Spo2).HasColumnName("spo2").HasMaxLength(3);
            e.Property(p => p.Gcs).HasColumnName("gcs").HasMaxLength(10);
            e.Property(p => p.Keluhan).HasColumnName("keluhan").HasMaxLength(2000);
            e.Property(p => p.Pemeriksaan).HasColumnName("pemeriksaan").HasMaxLength(2000);
            e.Property(p => p.Alergi).HasColumnName("alergi").HasMaxLength(80);
            e.Property(p => p.Rtl).HasColumnName("rtl").HasMaxLength(2000);
            e.Property(p => p.Penilaian).HasColumnName("penilaian").HasMaxLength(2000);
            e.Property(p => p.Instruksi).HasColumnName("instruksi").HasMaxLength(2000);
            e.Property(p => p.Evaluasi).HasColumnName("evaluasi").HasMaxLength(2000);
            e.Property(p => p.Nip).HasColumnName("nip").HasMaxLength(20);
            // The legacy `kesadaran` column is NOT NULL (no default), so unlike
            // the ignored enum columns elsewhere, this one must actually be
            // written on insert — mapped via a converter to the exact legacy
            // enum('Compos Mentis','Somnolence',...) string values.
            e.Property(p => p.Kesadaran).HasColumnName("kesadaran")
                .HasConversion(v => KesadaranToLegacyString(v), v => KesadaranFromLegacyString(v))
                .HasMaxLength(20);
            e.HasOne<RegPeriksa>().WithMany().HasForeignKey(p => p.NoRawat);
        });
    }

    private static readonly Dictionary<TingkatKesadaran, string> KesadaranMap = new()
    {
        [TingkatKesadaran.ComposMentis] = "Compos Mentis",
        [TingkatKesadaran.Somnolence] = "Somnolence",
        [TingkatKesadaran.Sopor] = "Sopor",
        [TingkatKesadaran.Coma] = "Coma",
        [TingkatKesadaran.Alert] = "Alert",
        [TingkatKesadaran.Confusion] = "Confusion",
        [TingkatKesadaran.Voice] = "Voice",
        [TingkatKesadaran.Pain] = "Pain",
        [TingkatKesadaran.Unresponsive] = "Unresponsive",
        [TingkatKesadaran.Apatis] = "Apatis",
        [TingkatKesadaran.Delirium] = "Delirium",
        [TingkatKesadaran.Meninggal] = "Meninggal",
    };

    private static string KesadaranToLegacyString(TingkatKesadaran value) => KesadaranMap[value];

    private static TingkatKesadaran KesadaranFromLegacyString(string value)
        => KesadaranMap.FirstOrDefault(kv => kv.Value == value, KesadaranMap.First()).Key;
}
