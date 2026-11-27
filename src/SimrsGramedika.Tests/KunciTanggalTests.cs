using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

/// <summary>TANGGALMUNDUR=no pada tindakan, SOAPIE, catatan dokter dan obgyn.</summary>
public sealed class KunciTanggalTests
{
    private const string Ralan = "KUNCI-RALAN";
    private const string Ranap = "KUNCI-RANAP";
    private const string Akun = "U1";

    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_KUNCI_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema penuh kosong simrs_kunci_fixture.";
        }
    }

    [Fact]
    public void HanyaProfilTerkunciDanBukanAdminUtamaYangDikunci()
    {
        var bawaan = new DatabaseSettings();
        var terkunci = new DatabaseSettings { TanggalMundur = false };
        Assert.True(bawaan.TanggalMundur);
        Assert.False(KunciTanggal.Aktif(bawaan, false));
        Assert.True(KunciTanggal.Aktif(terkunci, false));
        Assert.False(KunciTanggal.Aktif(terkunci, true));
        // Salinan profil dan profil tersimpan tidak boleh membuka kunci.
        Assert.False(terkunci.With(name: "Salinan").TanggalMundur);
        var json = System.Text.Json.JsonSerializer.Serialize(terkunci);
        Assert.False(System.Text.Json.JsonSerializer.Deserialize<DatabaseSettings>(json)!.TanggalMundur);
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<DatabaseSettings>("{}")!.TanggalMundur);
    }

    [FixtureFact]
    public async Task CatatanBaruMemakaiWaktuServer_UbahMempertahankanWaktuLama()
    {
        DatabaseSettings Profil(bool mundur) => new()
        {
            Host = "127.0.0.1", User = "root", Database = "simrs_kunci_fixture", TanggalMundur = mundur,
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_KUNCI_FIXTURE_PORT")!),
        };
        var terkunci = Profil(false);
        await using var db = await Db.OpenAsync(terkunci);
        await SeedAsync(db);

        var sekarang = await SekarangAsync(db);
        var pilihan = sekarang.AddDays(-1);
        var user = new AuthenticatedUser
        {
            UserId = Akun,
            Permissions = new HashSet<string>
            {
                TindakanRalanInputService.Izin, "tindakan_ralan", PemeriksaanRanapService.Izin,
                TindakanRanapInputService.Izin, CatatanDokterService.Izin,
            },
        };

        async Task<DateTime> WaktuAsync(string tabel, string rawat, string tanggal = "tgl_perawatan", string jam = "jam_rawat")
        {
            var row = Assert.Single(await db.QueryAsync($"SELECT {tanggal} AS t,{jam} AS j FROM {tabel} WHERE no_rawat=@p0", rawat));
            return row.Dt("t")!.Value.Date + (TimeSpan)row["j"]!;
        }
        void WaktuServer(DateTime waktu) => Assert.InRange(waktu, sekarang, sekarang.AddMinutes(5));

        // Tindakan rawat jalan: simpan, lalu ubah dengan waktu lain.
        var tindakan = new TindakanRalanInputService(terkunci, Ralan, user);
        var tarif = Assert.Single(await tindakan.CariTarifAsync(TindakanRalanJenis.Dokter, ""));
        await tindakan.SimpanAsync(TindakanRalanJenis.Dokter, pilihan, Akun, "", [tarif]);
        var dicatat = await WaktuAsync("rawat_jl_dr", Ralan);
        WaktuServer(dicatat);
        var asal = await tindakan.MuatTindakanAsync(TindakanRalanJenis.Dokter, "T", Akun, "", dicatat);
        await tindakan.UbahAsync(asal, pilihan, Akun, "", tarif);
        Assert.Equal(dicatat, await WaktuAsync("rawat_jl_dr", Ralan));

        // Tindakan rawat inap.
        var masuk = (await db.FirstAsync("SELECT tgl_masuk FROM kamar_inap WHERE no_rawat=@p0", Ranap))!.Dt("tgl_masuk")!.Value;
        var kamar = new RawatInapKey(Ranap, "KM", DateOnly.FromDateTime(masuk), new TimeOnly(8, 0));
        var inap = new TindakanRanapInputService(terkunci, kamar, "RM", user);
        await inap.SimpanAsync(TindakanRalanJenis.Dokter, pilihan, Akun, "",
            [Assert.Single(await inap.CariAsync(TindakanRalanJenis.Dokter, ""))]);
        WaktuServer(await WaktuAsync("rawat_inap_dr", Ranap));

        // SOAPIE rawat jalan.
        var ralan = new RawatJalanService(terkunci);
        var akses = new RawatJalanAkses(false, Akun, Diizinkan: true);
        var soap = new PemeriksaanRalanInput(Ralan, pilihan.Date, pilihan.TimeOfDay, "", "", "", "", "", "", "", "",
            "Compos Mentis", "Keluhan uji", "", "", "", "", "", "", "", Akun);
        await ralan.SimpanAsync(soap, akses);
        var waktuSoap = await WaktuAsync("pemeriksaan_ralan", Ralan);
        WaktuServer(waktuSoap);
        var tersimpan = soap with { Tanggal = waktuSoap.Date, Jam = waktuSoap.TimeOfDay };
        await ralan.UbahAsync(tersimpan, soap with { Keluhan = "Diubah" }, akses);
        Assert.Equal(waktuSoap, await WaktuAsync("pemeriksaan_ralan", Ralan));
        Assert.Equal("Diubah", await db.StrAsync("SELECT keluhan FROM pemeriksaan_ralan"));

        // SOAPIE rawat inap.
        var soapRanap = new PemeriksaanRanapService(terkunci, Ranap, user);
        var isian = Enumerable.Repeat("", PemeriksaanRanapService.Isian.Count).ToArray();
        isian[PemeriksaanRanapService.AwalNarasi] = "Keluhan uji";
        await soapRanap.SimpanAsync(new SoapRanap(Ranap, pilihan, Akun, "Compos Mentis", isian));
        var catatanRanap = Assert.Single(await soapRanap.DaftarAsync());
        WaktuServer(catatanRanap.Waktu);
        await soapRanap.SimpanAsync(catatanRanap with { Waktu = pilihan }, catatanRanap);
        Assert.Equal(catatanRanap.Waktu, Assert.Single(await soapRanap.DaftarAsync()).Waktu);

        // Catatan dokter.
        var catatan = new CatatanDokterService(terkunci, Ralan, user);
        await catatan.SimpanAsync(pilihan, Akun, "Catatan uji");
        var dokter = Assert.Single(await catatan.DaftarAsync());
        WaktuServer(dokter.Waktu!.Value);
        await catatan.SimpanAsync(pilihan, Akun, "Catatan diubah", dokter);
        var diubah = Assert.Single(await catatan.DaftarAsync());
        Assert.Equal((dokter.Waktu, "Catatan diubah"), (diubah.Waktu, diubah.Isi));

        // Obstetri dan ginekologi.
        foreach (var obstetri in new[] { true, false })
        {
            var obgyn = new PemeriksaanObgynService(terkunci, Ralan, user, obstetri);
            var nilai = obgyn.Fields.Select(f => f.Choices?[0] ?? "1").ToArray();
            await obgyn.SimpanAsync(pilihan, nilai);
            var row = Assert.Single(await obgyn.DaftarAsync());
            var waktu = row.Dt("tgl_perawatan")!.Value.Date + (TimeSpan)row["jam_rawat"]!;
            WaktuServer(waktu);
            await obgyn.UbahAsync(row, pilihan, nilai);
            var sesudah = Assert.Single(await obgyn.DaftarAsync());
            Assert.Equal(waktu, sesudah.Dt("tgl_perawatan")!.Value.Date + (TimeSpan)sesudah["jam_rawat"]!);
        }

        // Profil yang mengizinkan tanggal mundur tetap memakai waktu pilihan.
        var bebas = new CatatanDokterService(Profil(true), Ralan, user);
        await bebas.SimpanAsync(pilihan, Akun, "Waktu pilihan");
        Assert.Contains(await bebas.DaftarAsync(), c => c.Waktu == pilihan);
        // Admin Utama juga, walau profilnya terkunci.
        var admin = new CatatanDokterService(terkunci, Ralan, new AuthenticatedUser { IsAdminUtama = true });
        await admin.SimpanAsync(pilihan.AddMinutes(-1), Akun, "Waktu admin");
        Assert.Contains(await admin.DaftarAsync(), c => c.Waktu == pilihan.AddMinutes(-1));
    }

    private static async Task<DateTime> SekarangAsync(Db db)
    {
        var waktu = (DateTime)(await db.ScalarAsync("SELECT NOW()"))!;
        return waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));
    }

    private static async Task SeedAsync(Db db)
    {
        await db.ExecAsync("SET SESSION sql_mode='',FOREIGN_KEY_CHECKS=0");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('U1','Dokter Uji')");
        await db.ExecAsync("INSERT INTO petugas(nip,nama) VALUES ('U1','Petugas Uji')");
        await db.ExecAsync("INSERT INTO pegawai(nik,nama,stts_aktif) VALUES ('U1','Pegawai Uji','AKTIF')");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien) VALUES ('RM','Pasien Sintetis')");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_reg,no_rawat,tgl_registrasi,jam_reg,no_rkm_medis,stts,status_lanjut,kd_pj,kd_poli,status_bayar) VALUES
            ('001',@p0,CURRENT_DATE - INTERVAL 5 DAY,'00:00:00','RM','Belum','Ralan','PJ','POL','Belum Bayar'),
            ('002',@p1,CURRENT_DATE - INTERVAL 5 DAY,'00:00:00','RM','Belum','Ranap','PJ','POL','Belum Bayar')
            """, Ralan, Ranap);
        await db.ExecAsync("INSERT INTO bangsal(kd_bangsal,nm_bangsal) VALUES ('BG','Bangsal Uji')");
        await db.ExecAsync("INSERT INTO kamar(kd_kamar,kd_bangsal,kelas) VALUES ('KM','BG','Kelas 1')");
        await db.ExecAsync("""
            INSERT INTO kamar_inap(no_rawat,kd_kamar,tgl_masuk,jam_masuk,stts_pulang)
            VALUES (@p0,'KM',CURRENT_DATE - INTERVAL 4 DAY,'08:00:00','-')
            """, Ranap);
        await db.ExecAsync("INSERT INTO set_tarif(poli_ralan,cara_bayar_ralan) VALUES('Yes','Yes')");
        await db.ExecAsync("INSERT INTO kategori_perawatan(kd_kategori,nm_kategori) VALUES ('K','Kategori Uji')");
        await db.ExecAsync("""
            INSERT INTO jns_perawatan(kd_jenis_prw,nm_perawatan,kd_kategori,kd_pj,kd_poli,status,total_byrdr,tarif_tindakandr,material,bhp,kso,menejemen)
            VALUES ('T','Tindakan Uji','K','PJ','POL','1',100,100,0,0,0,0)
            """);
        await db.ExecAsync("""
            INSERT INTO jns_perawatan_inap(kd_jenis_prw,nm_perawatan,kd_kategori,kd_pj,kd_bangsal,kelas,status,total_byrdr,tarif_tindakandr,material,bhp,kso,menejemen)
            VALUES ('T','Tindakan Uji','K','PJ','BG','Kelas 1','1',100,100,0,0,0,0)
            """);
        await db.ExecAsync("INSERT INTO rekening(kd_rek,nm_rek) VALUES ('DB','Debit'),('CR','Kredit')");
        foreach (var (tabel, akhiran) in new[] { ("set_akun_ralan", "Ralan"), ("set_akun_ranap", "Ranap") })
        {
            string[] kolom = [$"Suspen_Piutang_Tindakan_{akhiran}", $"Tindakan_{akhiran}",
                $"Beban_Jasa_Medik_Dokter_Tindakan_{akhiran}", $"Utang_Jasa_Medik_Dokter_Tindakan_{akhiran}"];
            await db.ExecAsync($"INSERT INTO {tabel}({string.Join(',', kolom)}) VALUES ('DB','CR','DB','CR')");
        }
        await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=1");
    }
}
