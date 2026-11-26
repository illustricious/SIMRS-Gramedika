using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Core.Entities;

namespace SimrsGramedika.Data;

/// <summary>
/// EF Core mapping onto the legacy `sik` MySQL schema (structure documented in
/// docs/audit/DB_SCHEMA.md, extracted from docs/audit/schema_sik.sql — schema
/// only, no patient data). Table/column names below match the legacy snake_case
/// names exactly so this can point at an existing `sik` database without any
/// migration of old data.
/// </summary>
public class SimrsGramedikaDbContext(DbContextOptions<SimrsGramedikaDbContext> options) : DbContext(options)
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
    public DbSet<PermintaanLab> PermintaanLabSet => Set<PermintaanLab>();
    public DbSet<PermintaanRadiologi> PermintaanRadiologiSet => Set<PermintaanRadiologi>();
    public DbSet<HasilLab> HasilLabSet => Set<HasilLab>();
    public DbSet<HasilRadiologi> HasilRadiologiSet => Set<HasilRadiologi>();
    public DbSet<DataBarang> DataBarangSet => Set<DataBarang>();
    public DbSet<JenisBarang> JenisBarangSet => Set<JenisBarang>();
    public DbSet<GudangBarang> GudangBarangSet => Set<GudangBarang>();
    public DbSet<ResepObat> ResepObatSet => Set<ResepObat>();
    public DbSet<ResepDokterItem> ResepDokterItemSet => Set<ResepDokterItem>();
    public DbSet<PengeluaranObatBhp> PengeluaranObatBhpSet => Set<PengeluaranObatBhp>();
    public DbSet<DetailPengeluaranObatBhp> DetailPengeluaranObatBhpSet => Set<DetailPengeluaranObatBhp>();
    public DbSet<MasterTriaseKasus> MasterTriaseKasusSet => Set<MasterTriaseKasus>();
    public DbSet<TriaseIgd> TriaseIgdSet => Set<TriaseIgd>();
    public DbSet<UtdStokDarah> UtdStokDarahSet => Set<UtdStokDarah>();

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
            e.Property(p => p.Email).HasColumnName("email").HasMaxLength(50);
            e.Property(p => p.NamaKeluarga).HasColumnName("namakeluarga").HasMaxLength(50);
            e.Property(p => p.PekerjaanPj).HasColumnName("pekerjaanpj").HasMaxLength(35);
            e.Property(p => p.AlamatPj).HasColumnName("alamatpj").HasMaxLength(100);
            e.Property(p => p.Umur).HasColumnName("umur").HasMaxLength(30);
            e.Property(p => p.Pendidikan).HasColumnName("pnd").HasMaxLength(20);
            e.Property(p => p.KodeKelurahan).HasColumnName("kd_kel");
            e.Property(p => p.KodeKecamatan).HasColumnName("kd_kec");
            e.Property(p => p.KodeKabupaten).HasColumnName("kd_kab");
            e.Property(p => p.KodePropinsi).HasColumnName("kd_prop");
            e.Property(p => p.KelurahanPj).HasColumnName("kelurahanpj").HasMaxLength(60);
            e.Property(p => p.KecamatanPj).HasColumnName("kecamatanpj").HasMaxLength(60);
            e.Property(p => p.KabupatenPj).HasColumnName("kabupatenpj").HasMaxLength(60);
            e.Property(p => p.PropinsiPj).HasColumnName("propinsipj").HasMaxLength(30);
            e.Property(p => p.PerusahaanPasien).HasColumnName("perusahaan_pasien").HasMaxLength(8);
            e.Property(p => p.SukuBangsa).HasColumnName("suku_bangsa");
            e.Property(p => p.BahasaPasien).HasColumnName("bahasa_pasien");
            e.Property(p => p.CacatFisik).HasColumnName("cacat_fisik");
            e.Property(p => p.Nip).HasColumnName("nip").HasMaxLength(30);
            e.Property(p => p.NoPeserta).HasColumnName("no_peserta").HasMaxLength(25);

            e.Property(p => p.JenisKelamin).HasColumnName("jk")
                .HasConversion(v => v == JenisKelamin.LakiLaki ? "L" : "P",
                               v => v == "L" ? JenisKelamin.LakiLaki : JenisKelamin.Perempuan)
                .HasMaxLength(1);
            e.Property(p => p.GolonganDarah).HasColumnName("gol_darah")
                .HasConversion(v => v == null ? null : GolDarahToDb(v.Value),
                               v => v == null ? (GolonganDarah?)null : GolDarahFromDb(v))
                .HasMaxLength(2);
            e.Property(p => p.StatusPernikahan).HasColumnName("stts_nikah")
                .HasConversion(v => v == null ? null : StatusNikahToDb(v.Value),
                               v => v == null ? (StatusPernikahan?)null : StatusNikahFromDb(v))
                .HasMaxLength(20);
            e.Property(p => p.Keluarga).HasColumnName("keluarga")
                .HasConversion(v => KeluargaToDb(v), v => KeluargaFromDb(v))
                .HasMaxLength(20);
            // Without this, EF infers its own shadow FK ("PenjabKodePenjab")
            // for the Penjab navigation instead of reusing KodePenjab, and
            // INSERTs fail with "Unknown column 'PenjabKodePenjab'".
            e.HasOne(p => p.Penjab).WithMany().HasForeignKey(p => p.KodePenjab);
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
            e.Property(r => r.NoReg).HasColumnName("no_reg").HasMaxLength(8);
            e.Property(r => r.PenanggungJawab).HasColumnName("p_jawab").HasMaxLength(100);
            e.Property(r => r.AlamatPj).HasColumnName("almt_pj").HasMaxLength(200);
            e.Property(r => r.HubunganPj).HasColumnName("hubunganpj").HasMaxLength(20);
            e.Property(r => r.BiayaReg).HasColumnName("biaya_reg");

            e.Property(r => r.Status).HasColumnName("stts")
                .HasConversion(v => v == null ? null : StatusRegToDb(v.Value),
                               v => v == null ? (StatusRegistrasi?)null : StatusRegFromDb(v))
                .HasMaxLength(20);
            e.Property(r => r.StatusLanjut).HasColumnName("status_lanjut")
                .HasConversion(v => v == StatusLanjut.RawatJalan ? "Ralan" : "Ranap",
                               v => v == "Ralan" ? StatusLanjut.RawatJalan : StatusLanjut.RawatInap)
                .HasMaxLength(10);
            e.Property(r => r.StatusPoli).HasColumnName("status_poli")
                .HasConversion(v => v == null ? null : (v == StatusPendaftaran.Baru ? "Baru" : "Lama"),
                               v => v == null ? (StatusPendaftaran?)null : (v == "Baru" ? StatusPendaftaran.Baru : StatusPendaftaran.Lama))
                .HasMaxLength(10);
            e.Property(r => r.StatusBayar).HasColumnName("status_bayar")
                .HasConversion(v => v == null ? null : (v == StatusBayar.SudahBayar ? "Sudah Bayar" : "Belum Bayar"),
                               v => v == null ? (StatusBayar?)null : (v == "Sudah Bayar" ? StatusBayar.SudahBayar : StatusBayar.BelumBayar))
                .HasMaxLength(20);

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
            e.Property(k => k.Status).HasColumnName("status")
                .HasConversion(v => StatusKamarMap[v], v => StatusKamarMap.FirstOrDefault(kv => kv.Value == v, StatusKamarMap.First()).Key)
                .HasMaxLength(20);
            e.Property(k => k.Kelas).HasColumnName("kelas")
                .HasConversion(v => KelasKamarMap[v], v => KelasKamarMap.FirstOrDefault(kv => kv.Value == v, KelasKamarMap.First()).Key)
                .HasMaxLength(12);
            e.HasOne(k => k.Bangsal).WithMany().HasForeignKey(k => k.KodeBangsal);
        });

        modelBuilder.Entity<KamarInap>(e =>
        {
            e.ToTable("kamar_inap");
            e.HasKey(k => new { k.NoRawat, k.TanggalMasuk, k.JamMasuk });
            e.Property(k => k.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(k => k.KodeKamar).HasColumnName("kd_kamar").HasMaxLength(15);
            e.Property(k => k.TarifKamar).HasColumnName("trf_kamar");
            e.Property(k => k.TanggalMasuk).HasColumnName("tgl_masuk");
            e.Property(k => k.JamMasuk).HasColumnName("jam_masuk");
            e.Property(k => k.TanggalKeluar).HasColumnName("tgl_keluar");
            e.Property(k => k.JamKeluar).HasColumnName("jam_keluar");
            e.Property(k => k.DiagnosaAwal).HasColumnName("diagnosa_awal").HasMaxLength(100);
            e.Property(k => k.DiagnosaAkhir).HasColumnName("diagnosa_akhir").HasMaxLength(100);
            e.Property(k => k.Lama).HasColumnName("lama");
            e.Property(k => k.TotalBiaya).HasColumnName("ttl_biaya");
            e.Property(k => k.StatusPulang).HasColumnName("stts_pulang")
                .HasConversion(v => StatusPulangToDb(v), v => StatusPulangFromDb(v))
                .HasMaxLength(30);
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

        modelBuilder.Entity<PermintaanLab>(e =>
        {
            e.ToTable("permintaan_lab");
            e.HasKey(p => p.NoOrder);
            e.Property(p => p.NoOrder).HasColumnName("noorder").HasMaxLength(15);
            e.Property(p => p.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(p => p.TanggalPermintaan).HasColumnName("tgl_permintaan");
            e.Property(p => p.JamPermintaan).HasColumnName("jam_permintaan");
            e.Property(p => p.TanggalSampel).HasColumnName("tgl_sampel");
            e.Property(p => p.JamSampel).HasColumnName("jam_sampel");
            e.Property(p => p.TanggalHasil).HasColumnName("tgl_hasil");
            e.Property(p => p.JamHasil).HasColumnName("jam_hasil");
            e.Property(p => p.DokterPerujuk).HasColumnName("dokter_perujuk").HasMaxLength(20);
            e.Property(p => p.InformasiTambahan).HasColumnName("informasi_tambahan").HasMaxLength(60);
            e.Property(p => p.DiagnosaKlinis).HasColumnName("diagnosa_klinis").HasMaxLength(80);
            e.Property(p => p.Status).HasColumnName("status")
                .HasConversion(v => StatusPenunjangToDb(v), v => StatusPenunjangFromDb(v))
                .HasMaxLength(10);
            e.HasOne(p => p.RegPeriksa).WithMany().HasForeignKey(p => p.NoRawat);
        });

        modelBuilder.Entity<PermintaanRadiologi>(e =>
        {
            e.ToTable("permintaan_radiologi");
            e.HasKey(p => p.NoOrder);
            e.Property(p => p.NoOrder).HasColumnName("noorder").HasMaxLength(15);
            e.Property(p => p.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(p => p.TanggalPermintaan).HasColumnName("tgl_permintaan");
            e.Property(p => p.JamPermintaan).HasColumnName("jam_permintaan");
            e.Property(p => p.TanggalSampel).HasColumnName("tgl_sampel");
            e.Property(p => p.JamSampel).HasColumnName("jam_sampel");
            e.Property(p => p.TanggalHasil).HasColumnName("tgl_hasil");
            e.Property(p => p.JamHasil).HasColumnName("jam_hasil");
            e.Property(p => p.DokterPerujuk).HasColumnName("dokter_perujuk").HasMaxLength(20);
            e.Property(p => p.InformasiTambahan).HasColumnName("informasi_tambahan").HasMaxLength(60);
            e.Property(p => p.DiagnosaKlinis).HasColumnName("diagnosa_klinis").HasMaxLength(80);
            e.Property(p => p.Status).HasColumnName("status")
                .HasConversion(v => StatusPenunjangToDb(v), v => StatusPenunjangFromDb(v))
                .HasMaxLength(10);
            e.HasOne(p => p.RegPeriksa).WithMany().HasForeignKey(p => p.NoRawat);
        });

        modelBuilder.Entity<HasilLab>(e =>
        {
            e.ToTable("saran_kesan_lab");
            e.HasKey(h => new { h.NoRawat, h.TanggalPeriksa, h.Jam });
            e.Property(h => h.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(h => h.TanggalPeriksa).HasColumnName("tgl_periksa");
            e.Property(h => h.Jam).HasColumnName("jam");
            e.Property(h => h.Saran).HasColumnName("saran").HasMaxLength(700);
            e.Property(h => h.Kesan).HasColumnName("kesan").HasMaxLength(700);
            e.HasOne<RegPeriksa>().WithMany().HasForeignKey(h => h.NoRawat);
        });

        modelBuilder.Entity<HasilRadiologi>(e =>
        {
            e.ToTable("hasil_radiologi");
            e.HasKey(h => new { h.NoRawat, h.TanggalPeriksa, h.Jam });
            e.Property(h => h.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(h => h.TanggalPeriksa).HasColumnName("tgl_periksa");
            e.Property(h => h.Jam).HasColumnName("jam");
            e.Property(h => h.Hasil).HasColumnName("hasil");
            e.HasOne<RegPeriksa>().WithMany().HasForeignKey(h => h.NoRawat);
        });

        modelBuilder.Entity<DataBarang>(e =>
        {
            e.ToTable("databarang");
            e.HasKey(d => d.KodeBarang);
            e.Property(d => d.KodeBarang).HasColumnName("kode_brng").HasMaxLength(15);
            e.Property(d => d.NamaBarang).HasColumnName("nama_brng").HasMaxLength(80);
            e.Property(d => d.KodeSatuanBesar).HasColumnName("kode_satbesar").HasMaxLength(4);
            e.Property(d => d.Dasar).HasColumnName("dasar");
            e.Property(d => d.JualBebas).HasColumnName("jualbebas");
            e.Property(d => d.StokMinimal).HasColumnName("stokminimal");
            e.Property(d => d.Aktif).HasColumnName("status")
                .HasConversion(v => v ? "1" : "0", v => v == "1")
                .HasMaxLength(1);
            e.Property(d => d.KodeJenis).HasColumnName("kdjns").HasMaxLength(4);
        });

        modelBuilder.Entity<JenisBarang>(e =>
        {
            e.ToTable("jenis");
            e.HasKey(j => j.KodeJenis);
            e.Property(j => j.KodeJenis).HasColumnName("kdjns").HasMaxLength(4);
            e.Property(j => j.Nama).HasColumnName("nama").HasMaxLength(30);
        });

        modelBuilder.Entity<GudangBarang>(e =>
        {
            e.ToTable("gudangbarang");
            e.HasKey(g => new { g.KodeBarang, g.KodeBangsal, g.NoBatch, g.NoFaktur });
            e.Property(g => g.KodeBarang).HasColumnName("kode_brng").HasMaxLength(15);
            e.Property(g => g.KodeBangsal).HasColumnName("kd_bangsal").HasMaxLength(5);
            e.Property(g => g.Stok).HasColumnName("stok");
            e.Property(g => g.NoBatch).HasColumnName("no_batch").HasMaxLength(20);
            e.Property(g => g.NoFaktur).HasColumnName("no_faktur").HasMaxLength(20);
        });

        modelBuilder.Entity<ResepObat>(e =>
        {
            e.ToTable("resep_obat");
            e.HasKey(r => r.NoResep);
            e.Property(r => r.NoResep).HasColumnName("no_resep").HasMaxLength(14);
            e.Property(r => r.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(r => r.KodeDokter).HasColumnName("kd_dokter").HasMaxLength(20);
            e.Property(r => r.TanggalPeresepan).HasColumnName("tgl_peresepan");
            e.Property(r => r.JamPeresepan).HasColumnName("jam_peresepan");
            e.Property(r => r.Status).HasColumnName("status")
                .HasConversion(v => v == null ? null : StatusPenunjangToDb(v.Value),
                               v => v == null ? (StatusKunjunganPenunjang?)null : StatusPenunjangFromDb(v))
                .HasMaxLength(10);
            e.HasOne(r => r.RegPeriksa).WithMany().HasForeignKey(r => r.NoRawat);
            e.HasOne(r => r.Dokter).WithMany().HasForeignKey(r => r.KodeDokter);
        });

        modelBuilder.Entity<ResepDokterItem>(e =>
        {
            e.ToTable("resep_dokter");
            e.HasKey(r => new { r.NoResep, r.KodeBarang }); // no declared PK in legacy schema — see entity doc comment
            e.Property(r => r.NoResep).HasColumnName("no_resep").HasMaxLength(14);
            e.Property(r => r.KodeBarang).HasColumnName("kode_brng").HasMaxLength(15);
            e.Property(r => r.Jumlah).HasColumnName("jml");
            e.Property(r => r.AturanPakai).HasColumnName("aturan_pakai").HasMaxLength(150);
            e.HasOne<ResepObat>().WithMany().HasForeignKey(r => r.NoResep);
        });

        modelBuilder.Entity<PengeluaranObatBhp>(e =>
        {
            e.ToTable("pengeluaran_obat_bhp");
            e.HasKey(p => p.NoKeluar);
            e.Property(p => p.NoKeluar).HasColumnName("no_keluar").HasMaxLength(15);
            e.Property(p => p.Tanggal).HasColumnName("tanggal");
            e.Property(p => p.Nip).HasColumnName("nip").HasMaxLength(20);
            e.Property(p => p.Keterangan).HasColumnName("keterangan").HasMaxLength(200);
            e.Property(p => p.KodeBangsal).HasColumnName("kd_bangsal").HasMaxLength(5);
        });

        modelBuilder.Entity<DetailPengeluaranObatBhp>(e =>
        {
            e.ToTable("detail_pengeluaran_obat_bhp");
            e.HasKey(d => new { d.NoKeluar, d.KodeBarang }); // no declared PK in legacy schema — see entity doc comment
            e.Property(d => d.NoKeluar).HasColumnName("no_keluar").HasMaxLength(15);
            e.Property(d => d.KodeBarang).HasColumnName("kode_brng").HasMaxLength(15);
            e.Property(d => d.KodeSatuan).HasColumnName("kode_sat").HasMaxLength(4);
            e.Property(d => d.NoBatch).HasColumnName("no_batch").HasMaxLength(20);
            e.Property(d => d.Jumlah).HasColumnName("jumlah");
            e.Property(d => d.HargaBeli).HasColumnName("harga_beli");
            e.Property(d => d.Total).HasColumnName("total");
            e.Property(d => d.NoFaktur).HasColumnName("no_faktur").HasMaxLength(20);
            e.HasOne<PengeluaranObatBhp>().WithMany().HasForeignKey(d => d.NoKeluar);
        });

        modelBuilder.Entity<MasterTriaseKasus>(e =>
        {
            e.ToTable("master_triase_macam_kasus");
            e.HasKey(m => m.KodeKasus);
            e.Property(m => m.KodeKasus).HasColumnName("kode_kasus").HasMaxLength(3);
            e.Property(m => m.MacamKasus).HasColumnName("macam_kasus").HasMaxLength(150);
        });

        modelBuilder.Entity<TriaseIgd>(e =>
        {
            e.ToTable("data_triase_igd");
            e.HasKey(t => t.NoRawat);
            e.Property(t => t.NoRawat).HasColumnName("no_rawat").HasMaxLength(17);
            e.Property(t => t.TanggalKunjungan).HasColumnName("tgl_kunjungan");
            e.Property(t => t.KeteranganKedatangan).HasColumnName("keterangan_kedatangan").HasMaxLength(100);
            e.Property(t => t.KodeKasus).HasColumnName("kode_kasus").HasMaxLength(3);
            e.Property(t => t.TekananDarah).HasColumnName("tekanan_darah").HasMaxLength(8);
            e.Property(t => t.Nadi).HasColumnName("nadi").HasMaxLength(3);
            e.Property(t => t.Pernapasan).HasColumnName("pernapasan").HasMaxLength(3);
            e.Property(t => t.Suhu).HasColumnName("suhu").HasMaxLength(5);
            e.Property(t => t.SaturasiO2).HasColumnName("saturasi_o2").HasMaxLength(3);
            e.Property(t => t.Nyeri).HasColumnName("nyeri").HasMaxLength(5);

            e.Property(t => t.CaraMasuk).HasColumnName("cara_masuk")
                .HasConversion(v => CaraMasukMap[v], v => CaraMasukMap.FirstOrDefault(kv => kv.Value == v, CaraMasukMap.First()).Key)
                .HasMaxLength(10);
            e.Property(t => t.AlatTransportasi).HasColumnName("alat_transportasi")
                .HasConversion(v => AlatTransportasiMap[v], v => AlatTransportasiMap.FirstOrDefault(kv => kv.Value == v, AlatTransportasiMap.First()).Key)
                .HasMaxLength(10);
            e.Property(t => t.AlasanKedatangan).HasColumnName("alasan_kedatangan")
                .HasConversion(v => AlasanKedatanganMap[v], v => AlasanKedatanganMap.FirstOrDefault(kv => kv.Value == v, AlasanKedatanganMap.First()).Key)
                .HasMaxLength(20);

            e.HasOne(t => t.RegPeriksa).WithMany().HasForeignKey(t => t.NoRawat);
            e.HasOne(t => t.Kasus).WithMany().HasForeignKey(t => t.KodeKasus);
        });

        modelBuilder.Entity<UtdStokDarah>(e =>
        {
            e.ToTable("utd_stok_darah");
            e.HasKey(u => u.NoKantong);
            e.Property(u => u.NoKantong).HasColumnName("no_kantong").HasMaxLength(20);
            e.Property(u => u.KodeKomponen).HasColumnName("kode_komponen").HasMaxLength(5);
            e.Property(u => u.TanggalAftap).HasColumnName("tanggal_aftap");
            e.Property(u => u.TanggalKadaluarsa).HasColumnName("tanggal_kadaluarsa");

            e.Property(u => u.GolonganDarah).HasColumnName("golongan_darah")
                .HasConversion(v => v == null ? null : GolDarahUtdMap[v.Value],
                               v => v == null ? (GolonganDarahUtd?)null : GolDarahUtdMap.FirstOrDefault(kv => kv.Value == v, GolDarahUtdMap.First()).Key)
                .HasMaxLength(2);
            e.Property(u => u.Resus).HasColumnName("resus")
                .HasConversion(v => v == null ? null : (v == ResusDarah.Positif ? "(+)" : "(-)"),
                               v => v == null ? (ResusDarah?)null : (v == "(+)" ? ResusDarah.Positif : ResusDarah.Negatif))
                .HasMaxLength(3);
            e.Property(u => u.Status).HasColumnName("status")
                .HasConversion(v => v == null ? null : StatusKantongMap[v.Value],
                               v => v == null ? (StatusKantongDarah?)null : StatusKantongMap.FirstOrDefault(kv => kv.Value == v, StatusKantongMap.First()).Key)
                .HasMaxLength(15);
        });
    }

    private static readonly Dictionary<GolonganDarahUtd, string> GolDarahUtdMap = new()
    {
        [GolonganDarahUtd.A] = "A", [GolonganDarahUtd.B] = "B",
        [GolonganDarahUtd.Ab] = "AB", [GolonganDarahUtd.O] = "O",
    };

    private static readonly Dictionary<StatusKantongDarah, string> StatusKantongMap = new()
    {
        [StatusKantongDarah.Ada] = "Ada", [StatusKantongDarah.Diambil] = "Diambil",
        [StatusKantongDarah.Dimusnahkan] = "Dimusnahkan",
    };

    private static readonly Dictionary<CaraMasukIgd, string> CaraMasukMap = new()
    {
        [CaraMasukIgd.Jalan] = "Jalan", [CaraMasukIgd.Brankar] = "Brankar",
        [CaraMasukIgd.KursiRoda] = "Kursi Roda", [CaraMasukIgd.Digendong] = "Digendong",
    };

    private static readonly Dictionary<AlatTransportasiIgd, string> AlatTransportasiMap = new()
    {
        [AlatTransportasiIgd.Tidak] = "-", [AlatTransportasiIgd.Agd] = "AGD",
        [AlatTransportasiIgd.Sendiri] = "Sendiri", [AlatTransportasiIgd.Swasta] = "Swasta",
    };

    private static readonly Dictionary<AlasanKedatanganIgd, string> AlasanKedatanganMap = new()
    {
        [AlasanKedatanganIgd.DatangSendiri] = "Datang Sendiri", [AlasanKedatanganIgd.Polisi] = "Polisi",
        [AlasanKedatanganIgd.Rujukan] = "Rujukan", [AlasanKedatanganIgd.Bidan] = "Bidan",
        [AlasanKedatanganIgd.Puskesmas] = "Puskesmas", [AlasanKedatanganIgd.RumahSakit] = "Rumah Sakit",
        [AlasanKedatanganIgd.Poliklinik] = "Poliklinik", [AlasanKedatanganIgd.FaskesLain] = "Faskes Lain",
        [AlasanKedatanganIgd.TidakDiketahui] = "-",
    };

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

    private static readonly Dictionary<GolonganDarah, string> GolDarahMap = new()
    {
        [GolonganDarah.A] = "A", [GolonganDarah.B] = "B", [GolonganDarah.O] = "O",
        [GolonganDarah.AB] = "AB", [GolonganDarah.TidakDiketahui] = "-",
    };

    private static string GolDarahToDb(GolonganDarah v) => GolDarahMap[v];
    private static GolonganDarah GolDarahFromDb(string v)
        => GolDarahMap.FirstOrDefault(kv => kv.Value == v, GolDarahMap.First()).Key;

    private static readonly Dictionary<StatusPernikahan, string> StatusNikahMap = new()
    {
        [StatusPernikahan.BelumMenikah] = "BELUM MENIKAH", [StatusPernikahan.Menikah] = "MENIKAH",
        [StatusPernikahan.Janda] = "JANDA", [StatusPernikahan.Duda] = "DUDHA",
    };

    private static string StatusNikahToDb(StatusPernikahan v) => StatusNikahMap[v];
    private static StatusPernikahan StatusNikahFromDb(string v)
        => StatusNikahMap.FirstOrDefault(kv => kv.Value == v, StatusNikahMap.First()).Key;

    private static readonly Dictionary<HubunganPenanggungJawab, string> KeluargaMap = new()
    {
        [HubunganPenanggungJawab.Ayah] = "AYAH", [HubunganPenanggungJawab.Ibu] = "IBU",
        [HubunganPenanggungJawab.Istri] = "ISTRI", [HubunganPenanggungJawab.Suami] = "SUAMI",
        [HubunganPenanggungJawab.Saudara] = "SAUDARA", [HubunganPenanggungJawab.Anak] = "ANAK",
        [HubunganPenanggungJawab.DiriSendiri] = "DIRI SENDIRI", [HubunganPenanggungJawab.LainLain] = "LAIN-LAIN",
    };

    private static string KeluargaToDb(HubunganPenanggungJawab v) => KeluargaMap[v];
    private static HubunganPenanggungJawab KeluargaFromDb(string v)
        => KeluargaMap.FirstOrDefault(kv => kv.Value == v, KeluargaMap.First()).Key;

    private static readonly Dictionary<StatusRegistrasi, string> StatusRegMap = new()
    {
        [StatusRegistrasi.Belum] = "Belum", [StatusRegistrasi.Sudah] = "Sudah",
        [StatusRegistrasi.Batal] = "Batal", [StatusRegistrasi.BerkasDiterima] = "Berkas Diterima",
        [StatusRegistrasi.Dirujuk] = "Dirujuk", [StatusRegistrasi.Meninggal] = "Meninggal",
        [StatusRegistrasi.Dirawat] = "Dirawat", [StatusRegistrasi.PulangPaksa] = "Pulang Paksa",
    };

    private static string StatusRegToDb(StatusRegistrasi v) => StatusRegMap[v];
    private static StatusRegistrasi StatusRegFromDb(string v)
        => StatusRegMap.FirstOrDefault(kv => kv.Value == v, StatusRegMap.First()).Key;

    private static readonly Dictionary<StatusKamar, string> StatusKamarMap = new()
    {
        [StatusKamar.Isi] = "ISI", [StatusKamar.Kosong] = "KOSONG",
        [StatusKamar.Dibersihkan] = "DIBERSIHKAN", [StatusKamar.Dibooking] = "DIBOOKING",
        [StatusKamar.Perbaikan] = "PERBAIKAN",
    };

    private static readonly Dictionary<KelasKamar, string> KelasKamarMap = new()
    {
        [KelasKamar.Kelas1] = "Kelas 1", [KelasKamar.Kelas2] = "Kelas 2", [KelasKamar.Kelas3] = "Kelas 3",
        [KelasKamar.KelasUtama] = "Kelas Utama", [KelasKamar.Vip] = "Kelas VIP", [KelasKamar.Vvip] = "Kelas VVIP",
    };

    // kamar_inap.stts_pulang is NOT NULL in the legacy schema (no default), so
    // the "not discharged yet" state is represented by "-" (one of the legacy
    // enum's own values) rather than a NULL column.
    private static readonly Dictionary<StatusPulang, string> StatusPulangMap = new(StatusPulangLabels.Map);

    private static string StatusPulangToDb(StatusPulang? v) => StatusPulangMap[v ?? StatusPulang.BelumPulang];
    private static StatusPulang? StatusPulangFromDb(string v)
        => StatusPulangMap.FirstOrDefault(kv => kv.Value == v, StatusPulangMap.First()).Key;

    // permintaan_lab/permintaan_radiologi.status — lowercase in the legacy
    // schema, unlike every other Ralan/Ranap-flavored column elsewhere.
    private static readonly Dictionary<StatusKunjunganPenunjang, string> StatusPenunjangMap = new()
    {
        [StatusKunjunganPenunjang.RawatJalan] = "ralan", [StatusKunjunganPenunjang.RawatInap] = "ranap",
    };

    private static string StatusPenunjangToDb(StatusKunjunganPenunjang v) => StatusPenunjangMap[v];
    private static StatusKunjunganPenunjang StatusPenunjangFromDb(string v)
        => StatusPenunjangMap.FirstOrDefault(kv => kv.Value == v, StatusPenunjangMap.First()).Key;
}
