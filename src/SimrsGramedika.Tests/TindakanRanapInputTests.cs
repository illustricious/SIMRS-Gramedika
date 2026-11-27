using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

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
        await using var context = SimrsGramedikaDbContextFactory.Create(settings);
        var kasir = new KasirRepository(context);
        var tagihan = Assert.IsType<KasirBillItem>(await kasir.GetBillAsync(key.NoRawat));
        Assert.Equal(560m, tagihan.Rincian.Where(row => row.Jenis.StartsWith("Ranap", StringComparison.Ordinal)).Sum(row => row.Total));
        var alasan = await Assert.ThrowsAsync<InvalidOperationException>(() => kasir.BayarRanapAsync(tagihan, "Tunai", user));
        Assert.Contains("hak billing rawat inap", alasan.Message);
        Assert.Equal("Belum Bayar", await sql.StrAsync("SELECT status_bayar FROM reg_periksa WHERE no_rawat='RANAP-UJI'"));

        await KoreksiPasienPulangAsync(sql, settings, key, user, dr);
        await KoreksiDanObgynPasienDirawatAsync(sql, settings, user);
    }

    private static Task<long> SaldoPiutangAsync(Db sql, string noRawat) => sql.CountAsync("""
        SELECT COALESCE(SUM(d.debet-d.kredit),0) FROM detailjurnal d JOIN jurnal j ON j.no_jurnal=d.no_jurnal
        WHERE d.kd_rek='A0' AND j.no_bukti=@p0
        """, noRawat);

    private static async Task KoreksiPasienPulangAsync(Db sql, DatabaseSettings settings, RawatInapKey key, AuthenticatedUser user,
        TarifTindakanRanap tarif)
    {
        var admin = new AuthenticatedUser { IsAdminUtama = true };
        var biasa = new TindakanRanapInputService(settings, key, "RM", user);
        var utama = new TindakanRanapInputService(settings, key, "RM", admin);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TindakanRanapInputService(settings, key, "RM", new AuthenticatedUser()).DaftarAsync());
        var daftar = await biasa.DaftarAsync();
        Assert.Equal(3, daftar.Count);
        var dokter = daftar.Single(b => b.Isi.Jenis == TindakanRalanJenis.Dokter);
        Assert.Equal(("Tindakan Uji", "Dokter Uji", ""), (dokter.Nama, dokter.NamaDokter, dokter.NamaPetugas));

        var lewat = await Assert.ThrowsAsync<InvalidOperationException>(() => biasa.HapusAsync([dokter.Isi]));
        Assert.Contains("2 x 24 jam", lewat.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => biasa.UbahAsync(dokter.Isi, dokter.Isi.Waktu.AddMinutes(5), "DR", ""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => utama.UbahAsync(dokter.Isi, dokter.Isi.Waktu, "DR", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => utama.UbahAsync(dokter.Isi, dokter.Isi.Waktu, "SALAH", ""));
        var tutup = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            utama.UbahAsync(dokter.Isi, dokter.Isi.Waktu.AddMinutes(5), "DR", "", tarif));
        Assert.Contains("Segmen kamar", tutup.Message);
        await utama.UbahAsync(dokter.Isi, dokter.Isi.Waktu.AddMinutes(5), "DR", "");
        Assert.Equal("09:05:00", await sql.StrAsync("SELECT jam_rawat FROM rawat_inap_dr WHERE no_rawat='RANAP-UJI'"));
        Assert.Equal(170, await sql.CountAsync("SELECT biaya_rawat FROM rawat_inap_dr WHERE no_rawat='RANAP-UJI'"));
        Assert.Equal(560, await SaldoPiutangAsync(sql, "RANAP-UJI"));
        var basi = await Assert.ThrowsAsync<InvalidOperationException>(() => utama.HapusAsync([dokter.Isi]));
        Assert.Contains("sudah berubah", basi.Message);

        // Satu baris basi membatalkan seluruh pilihan.
        var baru = (await utama.DaftarAsync()).Select(b => b.Isi).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => utama.HapusAsync([.. baru, dokter.Isi]));
        Assert.Equal(560, await SaldoPiutangAsync(sql, "RANAP-UJI"));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM rawat_inap_pr WHERE no_rawat='RANAP-UJI'"));

        await utama.HapusAsync(baru);
        foreach (var tabel in new[] { "rawat_inap_dr", "rawat_inap_pr", "rawat_inap_drpr" })
            Assert.Equal(0, await sql.CountAsync($"SELECT COUNT(*) FROM {tabel} WHERE no_rawat='RANAP-UJI'"));
        Assert.Equal(0, await SaldoPiutangAsync(sql, "RANAP-UJI"));
        Assert.Equal(0, await sql.CountAsync("SELECT COUNT(*) FROM (SELECT no_jurnal FROM detailjurnal GROUP BY no_jurnal HAVING SUM(debet)<>SUM(kredit)) x"));

        await sql.ExecAsync("""
            INSERT INTO rawat_inap_dr(no_rawat,kd_jenis_prw,kd_dokter,tgl_perawatan,jam_rawat,material,bhp,tarif_tindakandr,kso,menejemen,biaya_rawat)
            VALUES('RANAP-BILL','T','DR','2026-01-01','09:00:00',10,20,30,50,60,170)
            """);
        var ditagih = new TindakanRanapInputService(settings, key with { NoRawat = "RANAP-BILL" }, "RM", admin);
        var baris = Assert.Single(await ditagih.DaftarAsync()).Isi;
        var alasan = await Assert.ThrowsAsync<InvalidOperationException>(() => ditagih.HapusAsync([baris]));
        Assert.Contains("tagihan", alasan.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ditagih.UbahAsync(baris, baris.Waktu.AddMinutes(1), "DR", ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => utama.HapusAsync([baris]));
        Assert.Equal(1, await sql.CountAsync("SELECT COUNT(*) FROM rawat_inap_dr WHERE no_rawat='RANAP-BILL'"));
    }

    private static async Task KoreksiDanObgynPasienDirawatAsync(Db sql, DatabaseSettings settings, AuthenticatedUser user)
    {
        var masuk = DateTime.Today.AddDays(-1).AddHours(8);
        await sql.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,kd_pj,status_lanjut,stts,status_bayar,tgl_registrasi,jam_reg)
            VALUES ('RANAP-BARU','RM','PJ','Ranap','Belum','Belum Bayar',@p0,@p1)
            """, masuk.Date, masuk.TimeOfDay);
        await sql.ExecAsync("INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang) VALUES ('RANAP-BARU','KM',@p0,@p1,'-')",
            masuk.Date, masuk.TimeOfDay);
        await sql.ExecAsync("""
            INSERT INTO jns_perawatan_inap(kd_jenis_prw,nm_perawatan,kd_kategori,kd_pj,kd_bangsal,kelas,status,material,bhp,
                tarif_tindakandr,tarif_tindakanpr,kso,menejemen,total_byrdr,total_byrpr,total_byrdrpr) VALUES
            ('U','Tindakan Lain','K','PJ','BG','Kelas 1','1',1,2,3,0,4,5,15,0,0)
            """);
        var key = new RawatInapKey("RANAP-BARU", "KM", DateOnly.FromDateTime(masuk), TimeOnly.FromDateTime(masuk));
        var terkunci = new DatabaseSettings { Host = settings.Host, Database = settings.Database, User = settings.User,
            Port = settings.Port, Password = settings.Password, TanggalMundur = false };
        var service = new TindakanRanapInputService(terkunci, key, "RM", user);
        var tarif = (await service.CariAsync(TindakanRalanJenis.Dokter, "")).ToDictionary(t => t.Kode);
        await service.SimpanAsync(TindakanRalanJenis.Dokter, masuk, "DR", "", [tarif["T"]]);
        var asal = Assert.Single(await service.DaftarAsync()).Isi;

        // Non-admin dengan kunci tanggal: waktu yang diminta diabaikan, tindakan boleh diganti.
        await service.UbahAsync(asal, asal.Waktu.AddHours(-3), "DR", "", tarif["U"]);
        var ganti = Assert.Single(await service.DaftarAsync()).Isi;
        Assert.Equal(("U", 15d, asal.Waktu), (ganti.Kode, ganti.Biaya, ganti.Waktu));
        Assert.Equal(15, await SaldoPiutangAsync(sql, "RANAP-BARU"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(ganti, ganti.Waktu, "DR", "", tarif["T"] with { Total = 1 }));
        await service.HapusAsync([ganti]);
        Assert.Empty(await service.DaftarAsync());
        Assert.Equal(0, await SaldoPiutangAsync(sql, "RANAP-BARU"));

        foreach (var obstetri in new[] { true, false })
        {
            var tabel = obstetri ? "pemeriksaan_obstetri_ranap" : "pemeriksaan_ginekologi_ranap";
            var periksa = new PemeriksaanObgynService(settings, "RANAP-BARU", user, obstetri, ranap: true);
            var isian = periksa.Fields.Select(f => f.Choices?[0] ?? "uji").ToArray();
            await Assert.ThrowsAsync<InvalidOperationException>(() => new PemeriksaanObgynService(settings, "RANAP-BARU",
                new AuthenticatedUser { Permissions = new HashSet<string> { "tindakan_ralan" } }, obstetri, ranap: true).DaftarAsync());
            // Layanan rawat jalan tidak menulis ke kunjungan rawat inap.
            await Assert.ThrowsAsync<InvalidOperationException>(() => new PemeriksaanObgynService(settings, "RANAP-BARU",
                new AuthenticatedUser { IsAdminUtama = true }, obstetri).SimpanAsync(DateTime.Now, isian));
            await periksa.SimpanAsync(DateTime.Now, isian);
            var tersimpan = Assert.Single(await periksa.DaftarAsync());
            isian[0] = "ubah";
            await periksa.UbahAsync(tersimpan, DateTime.Now, isian);
            tersimpan = Assert.Single(await periksa.DaftarAsync());
            Assert.Equal("ubah", tersimpan.S(periksa.Fields[0].Name));
            Assert.Equal(1, await sql.CountAsync($"SELECT COUNT(*) FROM {tabel} WHERE no_rawat='RANAP-BARU'"));
            await periksa.HapusAsync(tersimpan);
            Assert.Empty(await periksa.DaftarAsync());
        }
    }
}
