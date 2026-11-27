using SimrsGramedika.Config;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

/// <summary>
/// Jurnal pemeriksaan Laboratorium dan Radiologi pada skema sintetis simrs_penunjang_fixture
/// (tabel dari schema_sik.sql, kosong) di MariaDB lokal.
/// </summary>
public class JurnalPenunjangTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_PENUNJANG_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_penunjang_fixture.";
        }
    }

    private const string Rawat = "2026/11/27/000001";
    private static readonly DateOnly Tgl = new(2026, 11, 27);

    private static DatabaseSettings Settings => new()
    {
        Host = "127.0.0.1", Database = "simrs_penunjang_fixture", User = "root",
        Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_PENUNJANG_FIXTURE_PORT")!),
    };

    private static readonly string[] Jenis =
    [
        "Jasa_Medik_Dokter", "Jasa_Medik_Petugas", "Kso", "Jasa_Sarana", "Jasa_Perujuk", "Jasa_Menejemen",
    ];

    /// <summary>Setiap kolom rekening diisi rekening sendiri bernama sama dengan perannya.</summary>
    private static async Task<Db> SiapkanAsync()
    {
        var db = await Db.OpenAsync(Settings);
        await db.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        foreach (var t in new[]
                 {
                     "detailjurnal", "jurnal", "rekening", "set_akun_ralan", "set_akun_ranap", "periksa_lab", "detail_periksa_lab",
                     "detail_periksa_labpa", "detail_periksa_labpa_gambar", "saran_kesan_lab", "periksa_radiologi", "hasil_radiologi",
                     "gambar_radiologi", "beri_bhp_radiologi", "permintaan_lab", "permintaan_pemeriksaan_lab",
                     "permintaan_detail_permintaan_lab", "permintaan_radiologi", "permintaan_pemeriksaan_radiologi", "billing",
                     "kamar_inap", "reg_periksa", "pasien",
                 })
            await db.ExecAsync($"DELETE FROM {t}");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES('000001','PASIEN SINTETIS')");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,status_lanjut,stts,status_bayar,tgl_registrasi)
            VALUES(@p0,'000001','Ralan','Sudah','Belum Bayar','2026-11-27')
            """, Rawat);

        var nomor = 0;
        foreach (var (tabel, sfx, rawat) in new[] { ("set_akun_ralan", "Ralan", "Rawat_Jalan"), ("set_akun_ranap", "Ranap", "Rawat_Inap") })
        {
            await db.ExecAsync($"INSERT INTO {tabel}() VALUES()");
            foreach (var unit in new[] { "Laborat", "Radiologi" })
            {
                var kolom = new List<string> { $"Suspen_Piutang_{unit}_{sfx}", $"{unit}_{sfx}",
                    $"HPP_Persediaan_{unit}_{rawat}", $"Persediaan_BHP_{unit}_{rawat}" };
                foreach (var j in Jenis) kolom.AddRange([$"Beban_{j}_{unit}_{sfx}", $"Utang_{j}_{unit}_{sfx}"]);
                foreach (var k in kolom)
                {
                    var kode = $"R{++nomor:000}";
                    await db.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES(@p0,@p1)", kode, k);
                    await db.ExecAsync($"UPDATE {tabel} SET {k}=@p0", kode);
                }
            }
        }
        return db;
    }

    /// <summary>Debet dikurangi kredit pada rekening dengan peran tersebut.</summary>
    private static async Task<double> SaldoAsync(Db db, string peran) => Convert.ToDouble(await db.ScalarAsync("""
        SELECT IFNULL(SUM(d.debet-d.kredit),0) FROM detailjurnal d JOIN rekening r ON r.kd_rek=d.kd_rek WHERE LOWER(r.nm_rek)=LOWER(@p0)
        """, peran));

    private static LabTarifRow Tarif(string kode) => new()
    {
        KdJenisPrw = kode, Nama = kode, Biaya = 10000, BagianRs = 1000, Bhp = 900, TarifPerujuk = 800, TarifDokter = 700,
        TarifPetugas = 600, Kso = 500, Menejemen = 400,
    };

    private static LabSaveItem Item(string kode, int id) => new(kode, id, "1", "-", "-", 100, 90, 80, 70, 60, 50, 40, 1000);

    private static LabSaveRequest Lab(string jam, string status = "Ralan", string? order = null) => new(
        LabCategory.PK, Rawat, "P1", "D1", "D1", Tgl, TimeOnly.Parse(jam), status, order, [Tarif("L1")], [Item("L1", 1)], []);

    private static async Task SimpanLabAsync(LabSaveRequest rq)
    {
        await using var context = SimrsGramedikaDbContextFactory.Create(Settings);
        await new LabPeriksaRepository(context).SaveAsync(rq);
    }

    private static async Task HapusLabAsync(string jam, LabCategory kategori = LabCategory.PK, string? kode = null)
    {
        await using var context = SimrsGramedikaDbContextFactory.Create(Settings);
        await new LabPeriksaRepository(context).DeleteAsync(kategori, new(Rawat, "2026-11-27", jam), kode);
    }

    private static async Task SemuaRekeningNolAsync(Db db) => Assert.Equal(0, await db.CountAsync(
        "SELECT COUNT(*) FROM (SELECT kd_rek FROM detailjurnal GROUP BY kd_rek HAVING ABS(SUM(debet)-SUM(kredit))>0.001) x"));

    [FixtureFact]
    public async Task LaboratoriumDijurnalDanDibalikSaatDihapus()
    {
        await using var db = await SiapkanAsync();
        await SimpanLabAsync(Lab("09:00:00"));

        Assert.Equal(1, await db.CountAsync(
            "SELECT COUNT(*) FROM jurnal WHERE no_bukti=@p0 AND keterangan LIKE 'PEMERIKSAAN LABORAT RAWAT JALAN PASIEN 000001%'", Rawat));
        // Tarif pemeriksaan ditambah item hasil.
        Assert.Equal(11000, await SaldoAsync(db, "Suspen_Piutang_Laborat_Ralan"));
        Assert.Equal(-11000, await SaldoAsync(db, "Laborat_Ralan"));
        Assert.Equal(770, await SaldoAsync(db, "Beban_Jasa_Medik_Dokter_Laborat_Ralan"));
        Assert.Equal(-660, await SaldoAsync(db, "Utang_Jasa_Medik_Petugas_Laborat_Ralan"));
        Assert.Equal(990, await SaldoAsync(db, "HPP_Persediaan_Laborat_Rawat_Jalan"));
        Assert.Equal(-990, await SaldoAsync(db, "Persediaan_BHP_Laborat_Rawat_Jalan"));
        Assert.Equal(550, await SaldoAsync(db, "Beban_Kso_Laborat_Ralan"));
        Assert.Equal(1100, await SaldoAsync(db, "Beban_Jasa_Sarana_Laborat_Ralan"));
        Assert.Equal(880, await SaldoAsync(db, "Beban_Jasa_Perujuk_Laborat_Ralan"));
        Assert.Equal(440, await SaldoAsync(db, "Beban_Jasa_Menejemen_Laborat_Ralan"));
        Assert.Equal(0, await SaldoAsync(db, "Suspen_Piutang_Laborat_Ranap"));

        await HapusLabAsync("09:00:00");
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM jurnal WHERE keterangan LIKE 'PEMBATALAN PEMERIKSAAN LABORAT%'"));
        await SemuaRekeningNolAsync(db);
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM periksa_lab") + await db.CountAsync("SELECT COUNT(*) FROM detail_periksa_lab"));
    }

    [FixtureFact]
    public async Task RekeningKosongMembatalkanSimpanDanHapus()
    {
        await using var db = await SiapkanAsync();
        await db.ExecAsync("UPDATE set_akun_ralan SET Laborat_Ralan=''");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SimpanLabAsync(Lab("09:00:00")));
        Assert.Contains("Rekening", ex.Message);
        foreach (var tabel in new[] { "periksa_lab", "detail_periksa_lab", "jurnal", "detailjurnal" })
            Assert.Equal(0, await db.CountAsync($"SELECT COUNT(*) FROM {tabel}"));

        await db.ExecAsync("UPDATE set_akun_ralan SET Laborat_Ralan=(SELECT kd_rek FROM rekening WHERE nm_rek='Laborat_Ralan')");
        await SimpanLabAsync(Lab("09:00:00"));
        await db.ExecAsync("UPDATE set_akun_ralan SET Beban_Kso_Laborat_Ralan=''");
        await Assert.ThrowsAsync<InvalidOperationException>(() => HapusLabAsync("09:00:00"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM periksa_lab"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM detail_periksa_lab"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
    }

    [FixtureFact]
    public async Task PemeriksaanTanpaJurnalDihapusTanpaPembalikDanCampuranDitolak()
    {
        await using var db = await SiapkanAsync();
        // Data dari versi aplikasi sebelumnya: baris ada, jurnal tidak ada.
        const string lama = """
            INSERT INTO periksa_lab(no_rawat,kd_jenis_prw,tgl_periksa,jam,biaya,status,kategori) VALUES(@p0,'L1','2026-11-27',@p1,5000,'Ralan','PK')
            """;
        await db.ExecAsync(lama, Rawat, "07:00:00");
        await HapusLabAsync("07:00:00");
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM periksa_lab"));

        await db.ExecAsync(lama, Rawat, "07:00:00");
        await SimpanLabAsync(Lab("09:00:00"));
        foreach (var jam in new[] { "07:00:00", "09:00:00" })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => HapusLabAsync(jam));
            Assert.Contains("Saldo suspen", ex.Message);
        }
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM periksa_lab"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
    }

    [FixtureFact]
    public async Task ItemPermintaanYangSudahDibayarTidakDijurnal()
    {
        await using var db = await SiapkanAsync();
        await db.ExecAsync("INSERT INTO permintaan_lab(noorder,no_rawat,status) VALUES('PK1',@p0,'ralan')", Rawat);
        await db.ExecAsync("INSERT INTO permintaan_pemeriksaan_lab(noorder,kd_jenis_prw,stts_bayar) VALUES('PK1','L1','Sudah')");
        await db.ExecAsync("INSERT INTO permintaan_detail_permintaan_lab(noorder,kd_jenis_prw,id_template,stts_bayar) VALUES('PK1','L1',1,'Belum')");
        await SimpanLabAsync(Lab("09:00:00", order: "PK1"));
        Assert.Equal(1000, await SaldoAsync(db, "Suspen_Piutang_Laborat_Ralan"));
        Assert.Equal(1, await db.CountAsync("SELECT COUNT(*) FROM periksa_lab"));
    }

    [FixtureFact]
    public async Task PatologiAnatomiDibalikPerPemeriksaan()
    {
        await using var db = await SiapkanAsync();
        await SimpanLabAsync(new LabSaveRequest(LabCategory.PA, Rawat, "P1", "D1", "D1", Tgl, new TimeOnly(10, 0), "Ralan", null,
            [Tarif("A1"), Tarif("A2")], [], [new("A1", "-", "-", "-", "-", "-"), new("A2", "-", "-", "-", "-", "-")]));
        Assert.Equal(20000, await SaldoAsync(db, "Suspen_Piutang_Laborat_Ralan"));
        await HapusLabAsync("10:00:00", LabCategory.PA, "A1");
        Assert.Equal(10000, await SaldoAsync(db, "Suspen_Piutang_Laborat_Ralan"));
        await HapusLabAsync("10:00:00", LabCategory.PA, "A2");
        await SemuaRekeningNolAsync(db);
    }

    [FixtureFact]
    public async Task RadiologiRawatInapMemasukkanBhpLaluDibalik()
    {
        await using var db = await SiapkanAsync();
        var tarif = new RadTarifRow
        {
            KdJenisPrw = "R1", Nama = "Thorax", Biaya = 20000, BagianRs = 2000, Bhp = 1500, TarifPerujuk = 1200, TarifDokter = 1100,
            TarifPetugas = 1000, Kso = 900, Menejemen = 800,
        };
        await using (var context = SimrsGramedikaDbContextFactory.Create(Settings))
            await new RadiologiPeriksaRepository(context).SaveAsync(new RadSaveRequest(Rawat, "P1", "D1", "D1", Tgl, new TimeOnly(11, 0),
                "Ranap", null, [new(tarif, "", "", "", "", "", "", "", "")], [new("B1", "PCS", 2, 250)], "Normal"));

        Assert.Equal(20000, await SaldoAsync(db, "Suspen_Piutang_Radiologi_Ranap"));
        Assert.Equal(-20000, await SaldoAsync(db, "Radiologi_Ranap"));
        // BHP tarif 1500 ditambah BHP terpakai 2 x 250.
        Assert.Equal(2000, await SaldoAsync(db, "HPP_Persediaan_Radiologi_Rawat_Inap"));
        Assert.Equal(-1100, await SaldoAsync(db, "Utang_Jasa_Medik_Dokter_Radiologi_Ranap"));
        Assert.Equal(0, await SaldoAsync(db, "Suspen_Piutang_Radiologi_Ralan"));

        await using (var context = SimrsGramedikaDbContextFactory.Create(Settings))
            await new RadiologiPeriksaRepository(context).DeleteAsync(new(Rawat, "2026-11-27", "11:00:00"));
        await SemuaRekeningNolAsync(db);
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM jurnal"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM beri_bhp_radiologi"));
    }
}
