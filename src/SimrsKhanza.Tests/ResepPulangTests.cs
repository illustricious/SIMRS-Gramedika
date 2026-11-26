using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.MenuA;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Tests;

public sealed class ResepPulangTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_RESEP_PULANG_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis untuk resep pulang.";
        }
    }

    [FixtureFact]
    public async Task PermintaanValidasiDanSerahTerimaMencatatStokSekali()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", User = "root", Database = "simrs_resep_pulang_fixture",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_RESEP_PULANG_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=0,sql_mode=''");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM','Pasien Uji')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter,status) VALUES ('DR','Dokter Uji','1')");
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal,status) VALUES ('FA','Depo Uji','1')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas,status) VALUES ('KM','FA','Kelas 1','ISI')");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_dokter,kd_pj,status_lanjut,stts,status_bayar)
            VALUES ('RANAP-UJI','RM','DR','PJ','Ranap','Belum','Belum Bayar')
            """);
        await db.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang)
            VALUES ('RANAP-UJI','KM','2026-01-01','08:00:00','-')
            """);
        await db.ExecAsync("""
            INSERT INTO databarang(kode_brng,nama_brng,status,kelas1)
            VALUES ('OB','Obat Uji','1',5000),('ZZ','Obat Kedua','1',2500)
            """);
        await db.ExecAsync("INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES ('OB','FA',5,'','')");
        var dokter = new AuthenticatedUser { UserId = "DR", Permissions = new HashSet<string> { ResepPulangRepository.IzinPermintaan } };
        var farmasi = new AuthenticatedUser { UserId = "FARM", Permissions = new HashSet<string> { ResepPulangRepository.IzinFarmasi } };
        var repo = new ResepPulangRepository(settings);

        ResepPulangRepository.ObatDiminta[] items = [new("OB", 2, "Aturan uji"), new("ZZ", 1, "Aturan uji")];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repo.BuatPermintaanAsync(farmasi, "RANAP-UJI", "DR", items));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.BuatPermintaanAsync(dokter, "RANAP-UJI", "DR",
            [new("OB", 1, "Uji"), new("HILANG", 1, "Uji")]));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM permintaan_resep_pulang"));

        var nomor = await repo.BuatPermintaanAsync(dokter, "RANAP-UJI", "DR", items);
        Assert.Matches("^RP[0-9]{12}$", nomor);
        var antrean = Assert.Single(await repo.CariPermintaanAsync(farmasi, "RANAP-UJI"));
        var awal = await repo.MuatDrafAsync(dokter, "RANAP-UJI", nomor, antrean.S("tgl_permintaan"), antrean.S("jam"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UbahPermintaanAsync(dokter, "RANAP-UJI",
            awal, "DR", [new("HILANG", 1, "Uji rollback")]));
        await repo.UbahPermintaanAsync(dokter, "RANAP-UJI", awal, "DR",
            [new("OB", 2, "Aturan diperbaiki"), items[1]]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UbahPermintaanAsync(dokter, "RANAP-UJI", awal, "DR", items));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ProsesPermintaanAsync(farmasi, nomor, "FA",
            "RANAP-UJI", awal.Tanggal, awal.Jam, awal));
        var terbaru = await repo.MuatDrafAsync(farmasi, "RANAP-UJI", nomor, awal.Tanggal, awal.Jam, farmasi: true);
        Assert.Contains(terbaru.Obat, x => x.Isi.Dosis == "Aturan diperbaiki");
        Task ValidateAsync(AuthenticatedUser user) => repo.ProsesPermintaanAsync(user, nomor, "FA",
            "RANAP-UJI", antrean.S("tgl_permintaan"), antrean.S("jam"), terbaru);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CatatSerahAsync(farmasi, nomor, "Penerima Uji"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ValidateAsync(dokter));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ValidateAsync(farmasi));
        Assert.Equal(5, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB'"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM resep_pulang"));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM riwayat_barang_medis"));
        Assert.Equal("Belum", await db.StrAsync("SELECT status FROM permintaan_resep_pulang"));

        await db.ExecAsync("UPDATE reg_periksa SET status_bayar='Sudah Bayar' WHERE no_rawat='RANAP-UJI'");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ValidateAsync(farmasi));
        await db.ExecAsync("UPDATE reg_periksa SET status_bayar='Belum Bayar' WHERE no_rawat='RANAP-UJI'");
        await db.ExecAsync("INSERT INTO gudangbarang(kode_brng,kd_bangsal,stok,no_batch,no_faktur) VALUES ('ZZ','FA',3,'','')");
        await ValidateAsync(farmasi);
        Assert.Equal(3, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB'"));
        Assert.Equal(2, await db.CountAsync("SELECT COUNT(*) FROM riwayat_barang_medis WHERE petugas='FARM'"));
        var siap = Assert.Single(await repo.CariSiapSerahAsync(farmasi, nomor));
        Assert.Contains("Obat Uji", siap.S("obat"));

        var attempts = await Task.WhenAll(
            RecordHandoverAsync(),
            RecordHandoverAsync());
        Assert.Single(attempts, ok => ok);

        async Task<bool> RecordHandoverAsync()
        {
            try
            {
                await repo.CatatSerahAsync(farmasi, nomor, "Penerima Uji");
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message == "Serah terima resep ini sudah dicatat.")
            {
                return false;
            }
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CatatSerahAsync(farmasi, nomor, "Penerima Lain"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ValidateAsync(farmasi));
        Assert.Empty(await repo.CariSiapSerahAsync(farmasi, nomor));
        var bukti = await repo.MuatBuktiSerahAsync(dokter, "RANAP-UJI", nomor);
        Assert.Equal("Penerima Uji", bukti.S("penerima"));
        Assert.Equal("Pasien Uji", bukti.S("nm_pasien"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repo.MuatBuktiSerahAsync(farmasi, "RANAP-UJI", nomor));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.MuatBuktiSerahAsync(dokter, "RAWAT-LAIN", nomor));
        Assert.All(await repo.RiwayatPermintaanAsync(dokter, "RANAP-UJI"), row =>
        {
            Assert.Equal("Penerima Uji", row.S("penerima"));
            Assert.Equal("FARM", row.S("petugas"));
            Assert.NotNull(row["waktu"]);
        });
        Assert.Empty(await repo.RiwayatPermintaanAsync(dokter, "RAWAT-LAIN"));
        Assert.Equal(3, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB'"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.BatalkanPermintaanAsync(dokter,
            "RANAP-UJI", nomor, antrean.S("tgl_permintaan"), antrean.S("jam")));

        var nomorBaru = await repo.BuatPermintaanAsync(dokter, "RANAP-UJI", "DR", [new("OB", 1, "Uji batal")]);
        var pending = Assert.Single(await repo.CariPermintaanAsync(farmasi, nomorBaru));
        Task CancelAsync(AuthenticatedUser user, string jam) => repo.BatalkanPermintaanAsync(user,
            "RANAP-UJI", nomorBaru, pending.S("tgl_permintaan"), jam);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CancelAsync(farmasi, pending.S("jam")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CancelAsync(dokter, "25:00:00"));
        await CancelAsync(dokter, pending.S("jam"));
        Assert.Empty(await repo.CariPermintaanAsync(farmasi, nomorBaru));
        Assert.Equal(0, await db.CountAsync("SELECT COUNT(*) FROM detail_permintaan_resep_pulang WHERE no_permintaan=@p0", nomorBaru));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ProsesPermintaanAsync(farmasi, nomorBaru, "FA",
            "RANAP-UJI", pending.S("tgl_permintaan"), pending.S("jam")));
        Assert.Equal(3, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB'"));

        var raceNumber = await repo.BuatPermintaanAsync(dokter, "RANAP-UJI", "DR", [new("OB", 1, "Uji bersamaan")]);
        var raceRow = Assert.Single(await repo.CariPermintaanAsync(farmasi, raceNumber));
        var raceDate = raceRow.S("tgl_permintaan");
        var raceTime = raceRow.S("jam");
        var results = await Task.WhenAll(
            TryAsync(() => repo.BatalkanPermintaanAsync(dokter, "RANAP-UJI", raceNumber, raceDate, raceTime)),
            TryAsync(() => repo.ProsesPermintaanAsync(farmasi, raceNumber, "FA", "RANAP-UJI", raceDate, raceTime)));
        Assert.Single(results, ok => ok);
        Assert.Equal(results[0] ? 3 : 2, await db.CountAsync("SELECT stok FROM gudangbarang WHERE kode_brng='OB'"));

        static async Task<bool> TryAsync(Func<Task> action)
        {
            try { await action(); return true; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
