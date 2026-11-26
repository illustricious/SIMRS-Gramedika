using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Tests;

public sealed class TindakanRanapInputTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_TINDAKAN_RANAP_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_tindakan_ranap_fixture.";
        }
    }

    [FixtureFact]
    public async Task TigaKategoriMenjagaJurnalDanMenungguKunciBilling()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", Database = "simrs_tindakan_ranap_fixture", User = "root",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_TINDAKAN_RANAP_FIXTURE_PORT")!),
            Password = Environment.GetEnvironmentVariable("SIMRS_TINDAKAN_RANAP_FIXTURE_PASSWORD") ?? "" };
        await using var sql = await Db.OpenAsync(settings);
        await sql.ExecAsync("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        await sql.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM','Pasien Uji')");
        await sql.ExecAsync("INSERT INTO penjab(kd_pj,png_jawab) VALUES ('PJ','Penjamin Uji')");
        await sql.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('DR','Dokter Uji')");
        await sql.ExecAsync("INSERT INTO petugas(nip,nama) VALUES ('PR','Petugas Uji')");
        await sql.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal) VALUES ('BG','Bangsal Uji')");
        await sql.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas) VALUES ('KM','BG','Kelas 1')");
        await sql.ExecAsync("INSERT INTO kategori_perawatan(kd_kategori,nm_kategori) VALUES ('K','Kategori Uji')");
        await sql.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_pj,status_lanjut,stts,status_bayar) VALUES
                ('RANAP-UJI','RM','PJ','Ranap','Belum','Belum Bayar'),('RANAP-BILL','RM','PJ','Ranap','Belum','Belum Bayar')
            """);
        await sql.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang) VALUES
                ('RANAP-UJI','KM','2026-01-01','00:00:00','-'),('RANAP-BILL','KM','2026-01-01','00:00:00','-')
            """);
        await sql.ExecAsync("""
            INSERT INTO jns_perawatan_inap(kd_jenis_prw,nm_perawatan,kd_kategori,kd_pj,kd_bangsal,kelas,status,material,bhp,
                tarif_tindakandr,tarif_tindakanpr,kso,menejemen,total_byrdr,total_byrpr,total_byrdrpr) VALUES
            ('T','Tindakan Uji','K','PJ','BG','Kelas 1','1',10,20,30,40,50,60,170,180,210),
            ('X','Tarif Kelas Lain','K','PJ','BG','Kelas 2','1',10,20,30,40,50,60,170,180,210)
            """);
        string[] kolom = ["Suspen_Piutang_Tindakan_Ranap", "Tindakan_Ranap",
            "Beban_Jasa_Medik_Dokter_Tindakan_Ranap", "Utang_Jasa_Medik_Dokter_Tindakan_Ranap",
            "Beban_Jasa_Medik_Paramedis_Tindakan_Ranap", "Utang_Jasa_Medik_Paramedis_Tindakan_Ranap",
            "Beban_KSO_Tindakan_Ranap", "Utang_KSO_Tindakan_Ranap", "Beban_Jasa_Sarana_Tindakan_Ranap", "Utang_Jasa_Sarana_Tindakan_Ranap",
            "HPP_BHP_Tindakan_Ranap", "Persediaan_BHP_Tindakan_Ranap", "Beban_Jasa_Menejemen_Tindakan_Ranap", "Utang_Jasa_Menejemen_Tindakan_Ranap"];
        for (int index = 0; index < kolom.Length; index++)
            await sql.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES (@p0,@p1)", $"A{index}", kolom[index]);
        await sql.ExecAsync($"INSERT INTO set_akun_ranap({string.Join(',', kolom)}) VALUES ({string.Join(',', kolom.Select((_, i) => $"@p{i}"))})",
            kolom.Select((_, i) => (object?)$"A{i}").ToArray());

        var key = new RawatInapKey("RANAP-UJI", "KM", new DateOnly(2026, 1, 1), TimeOnly.MinValue);
        var user = new AuthenticatedUser { UserId = "PR", Permissions = new HashSet<string> { "tindakan_ranap" } };
        var service = new TindakanRanapInputService(settings, key, "RM", user);
        var waktu = key.TanggalMasuk.ToDateTime(new TimeOnly(9, 0));
        var denied = new TindakanRanapInputService(settings, key, "RM", new AuthenticatedUser());
        await Assert.ThrowsAsync<InvalidOperationException>(() => denied.CariAsync(TindakanRalanJenis.Dokter, ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => denied.SimpanAsync(TindakanRalanJenis.Dokter, waktu, "DR", "", [new("T", "", "", 170)]));
        var dr = Assert.Single(await service.CariAsync(TindakanRalanJenis.Dokter, ""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(TindakanRalanJenis.Dokter, waktu, "SALAH", "", [dr]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Dokter, waktu, "DR", "", [dr with { Total = 1 }]));
        await service.SimpanAsync(TindakanRalanJenis.Dokter, waktu, "DR", "", [dr]);
        await service.SimpanAsync(TindakanRalanJenis.Paramedis, waktu, "", "PR", [Assert.Single(await service.CariAsync(TindakanRalanJenis.Paramedis, ""))]);
        await service.SimpanAsync(TindakanRalanJenis.DokterParamedis, waktu, "DR", "PR", [Assert.Single(await service.CariAsync(TindakanRalanJenis.DokterParamedis, ""))]);
        Assert.Equal(170, await sql.CountAsync("SELECT biaya_rawat FROM rawat_inap_dr"));
        Assert.Equal(180, await sql.CountAsync("SELECT biaya_rawat FROM rawat_inap_pr"));
        Assert.Equal(210, await sql.CountAsync("SELECT biaya_rawat FROM rawat_inap_drpr"));
        Assert.Equal(3, await sql.CountAsync("SELECT COUNT(*) FROM jurnal WHERE no_bukti='RANAP-UJI'"));
        Assert.Equal(560, await sql.CountAsync("SELECT SUM(debet) FROM detailjurnal WHERE kd_rek='A0'"));
        Assert.Equal(560, await sql.CountAsync("SELECT SUM(kredit) FROM detailjurnal WHERE kd_rek='A1'"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM (SELECT no_jurnal FROM detailjurnal GROUP BY no_jurnal HAVING SUM(debet)<>SUM(kredit)) x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(TindakanRalanJenis.Dokter, waktu, "DR", "", [dr]));

        await sql.ExecAsync("UPDATE set_akun_ranap SET Tindakan_Ranap='TIDAK-ADA'");
        var gagalJurnal = await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() =>
            service.SimpanAsync(TindakanRalanJenis.Dokter, waktu.AddMinutes(1), "DR", "", [dr]));
        Assert.Equal(1452, gagalJurnal.Number);
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM rawat_inap_dr"));
        Assert.Equal(3, await sql.CountAsync("SELECT COUNT(*) FROM jurnal"));
        await sql.ExecAsync("UPDATE set_akun_ranap SET Tindakan_Ranap='A1'");

        await using var billing = await Db.OpenAsync(settings);
        await billing.BeginAsync();
        await billing.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat='RANAP-BILL' FOR UPDATE");
        await billing.ExecAsync("""
            INSERT INTO billing(noindex,no_rawat,no,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya)
            VALUES(0,'RANAP-BILL','','Tagihan uji',':',0,0,0,0)
            """);
        var billService = new TindakanRanapInputService(settings, key with { NoRawat = "RANAP-BILL" }, "RM", user);
        var blocked = billService.SimpanAsync(TindakanRalanJenis.Dokter, waktu, "DR", "", [dr]);
        Assert.NotSame(blocked, await Task.WhenAny(blocked, Task.Delay(200)));
        await billing.CommitAsync();
        var reason = await Assert.ThrowsAsync<InvalidOperationException>(() => blocked.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("tagihan", reason.Message);
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM jurnal WHERE no_bukti='RANAP-BILL'"));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM rawat_inap_dr"));

        await sql.ExecAsync("UPDATE kamar_inap SET stts_pulang='Sembuh',tgl_keluar='2026-01-02',jam_keluar='00:00:00' WHERE no_rawat='RANAP-UJI'");
        await using var context = SimrsKhanzaDbContextFactory.Create(settings);
        var kasir = new KasirRepository(context);
        var tagihan = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(key.NoRawat));
        Assert.Equal(560m, tagihan.Rincian.Where(row => row.Jenis.StartsWith("Ranap", StringComparison.Ordinal)).Sum(row => row.Total));
        var alasan = await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRanapAsync(tagihan, "Tunai", user));
        Assert.Contains("hak billing rawat inap", alasan.Message);
        Assert.Equal("Belum Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat='RANAP-UJI'"));
    }
}
