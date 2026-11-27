using System.Text.RegularExpressions;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data;
using SimrsGramedika.Data.RekamMedis;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.RekamMedis;

namespace SimrsGramedika.Tests;

public sealed class FormKlinisTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_FORMKLINIS_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_formklinis_fixture.";
        }
    }

    [Fact]
    public void SkorSkriningGiziMengikutiPilihan()
    {
        var isi = new Dictionary<string, string>
        {
            ["parameter_imt"] = "IMT < 18,5/z score < -2",
            ["parameter_bb"] = "BB Hilang 5 - 10 %",
            ["parameter_penyakit"] = "Tidak ada asupan nutrisi > 5 hari",
        };
        FormKlinisKatalog.SkriningGizi.Hitung!(isi);
        Assert.Equal(("2", "1", "2", "5"), (isi["skor_imt"], isi["skor_bb"], isi["skor_penyakit"], isi["skor_total"]));
        Assert.StartsWith("Beresiko tinggi", isi["parameter_total"]);

        isi["parameter_imt"] = "IMT > 20/z score > 2";
        isi["parameter_penyakit"] = "Ada asupan nutrisi > 5 hari";
        FormKlinisKatalog.SkriningGizi.Hitung!(isi);
        Assert.Equal("1", isi["skor_total"]);
        Assert.StartsWith("Beresiko menengah", isi["parameter_total"]);
    }

    [Theory]
    [InlineData(0, "Risiko Rendah")]
    [InlineData(1, "Risiko Sedang")]   // riwayat jatuh 25
    [InlineData(3, "Risiko Tinggi")]   // 25 + 15 + 30
    public void SkorMorseMenjumlahkanSkala(int jumlahSkalaBerisiko, string tingkat)
    {
        var form = FormKlinisKatalog.RisikoJatuhDewasa;
        var pilihan = form.Isian.Where(i => i.Pilihan is not null).ToArray();
        var isi = pilihan.Select((i, n) => (i.Kolom, Nilai: n < jumlahSkalaBerisiko ? i.Pilihan![^1] : i.Pilihan![0]))
            .ToDictionary(x => x.Kolom, x => x.Nilai);
        form.Hitung!(isi);
        var harapan = new[] { 25, 15, 30, 20, 20, 15 }.Take(jumlahSkalaBerisiko).Sum();
        Assert.Equal(harapan.ToString(), isi["penilaian_jatuhmorse_totalnilai"]);
        Assert.Contains(tingkat, form.Keterangan!(isi));
    }

    /// <summary>Definisi formulir harus sama dengan DDL Khanza: kolom lengkap, pilihan sama dengan enum, panjang muat.</summary>
    [Fact]
    public void DefinisiCocokDenganSkemaKhanza()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "docs", "audit", "schema_sik.sql"))) folder = folder.Parent;
        Assert.NotNull(folder);
        var skema = File.ReadAllText(Path.Combine(folder.FullName, "docs", "audit", "schema_sik.sql"));

        foreach (var form in FormKlinisKatalog.Semua)
        {
            var ddl = Regex.Match(skema, $@"CREATE TABLE `{form.Tabel}` \((.*?)\) ENGINE", RegexOptions.Singleline).Groups[1].Value;
            var kolom = Regex.Matches(ddl, @"^\s*`(\w+)` (\w+)(?:\((.*)\))?[ ,]", RegexOptions.Multiline)
                .ToDictionary(m => m.Groups[1].Value, m => (Tipe: m.Groups[2].Value, Rinci: m.Groups[3].Value));
            string[] kunci = ["no_rawat", form.KolomTanggal, form.KolomPemilik, .. form.KolomJam is { } jam ? new[] { jam } : []];
            Assert.Equal(kolom.Keys.Order(), kunci.Concat(form.Isian.Select(i => i.Kolom)).Order());
            Assert.Equal(form.KolomJam is null ? "datetime" : "date", kolom[form.KolomTanggal].Tipe);
            var utama = Regex.Match(ddl, @"PRIMARY KEY \(([^)]*)\)").Groups[1].Value.Replace("`", "").Split(',');
            Assert.Equal(form.SatuPerKunjungan, utama.Length == 1);

            foreach (var isian in form.Isian)
            {
                var (tipe, rinci) = kolom[isian.Kolom];
                if (tipe == "enum")
                    Assert.Equal(Regex.Matches(rinci, "'([^']*)'").Select(m => m.Groups[1].Value), isian.Pilihan!);
                else Assert.Null(isian.Pilihan);
                if (tipe == "varchar" && !isian.Otomatis) Assert.InRange(isian.Maks, 1, int.Parse(rinci));
            }
        }
    }

    [FixtureFact]
    public async Task SimpanUbahHapusMengikutiAturanPemilikDanWaktu()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", User = "root", Database = "simrs_formklinis_fixture",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_FORMKLINIS_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=0,sql_mode=''");
        await db.ExecAsync("INSERT INTO pasien(no_rkm_medis,nm_pasien,jk,tgl_lahir) VALUES ('RM','Pasien Uji','P','1990-01-02')");
        await db.ExecAsync("""
            INSERT INTO reg_periksa(no_rawat,no_rkm_medis,tgl_registrasi,jam_reg,umurdaftar,sttsumur)
            VALUES ('R1','RM',CURRENT_DATE - INTERVAL 5 DAY,'08:00:00',36,'Th')
            """);
        await db.ExecAsync("INSERT INTO petugas(nip,nama) VALUES ('P1','Petugas Satu'),('P2','Petugas Dua')");
        await db.ExecAsync("INSERT INTO dokter(kd_dokter,nm_dokter) VALUES ('D1','Dokter Satu')");

        var sekarang = (DateTime)(await db.ScalarAsync("SELECT NOW()"))!;
        sekarang = sekarang.AddTicks(-(sekarang.Ticks % TimeSpan.TicksPerSecond));
        var izin = FormKlinisKatalog.Semua.Select(f => f.Izin).ToHashSet();
        var petugas = new AuthenticatedUser { UserId = "P1", Permissions = izin };
        var lain = new AuthenticatedUser { UserId = "P2", Permissions = izin };
        var dokter = new AuthenticatedUser { UserId = "D1", Permissions = izin };
        var admin = new AuthenticatedUser { IsAdminUtama = true };
        var tanpaHak = new AuthenticatedUser { UserId = "P1" };
        var kop = new KopInstansi("RS UJI", "Jalan Uji", "000", "", null) { Kota = "Kota Uji" };

        foreach (var form in FormKlinisKatalog.Semua)
        {
            var akun = form.Pemilik == PemilikForm.Dokter ? dokter : petugas;
            var bukan = form.Pemilik == PemilikForm.Dokter ? petugas : dokter;
            FormKlinisService Layanan(AuthenticatedUser u) => new(settings, u, form, "R1");
            var service = Layanan(akun);
            var isi = form.Isian.Where(i => !i.Otomatis).ToDictionary(i => i.Kolom, i => i.Pilihan?[^1] ?? (i.Maks > 0 ? "1" : ""));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Layanan(tanpaHak).MuatAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Layanan(tanpaHak).SimpanAsync(sekarang, "P1", isi));
            // Akun yang bukan petugas/dokter sesuai formulir ditolak.
            await Assert.ThrowsAsync<InvalidOperationException>(() => Layanan(bukan).SimpanAsync(sekarang, akun.UserId, isi));
            if (form.Isian.FirstOrDefault(i => i.Wajib) is { } wajib)
                await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(sekarang, "", new Dictionary<string, string>(isi) { [wajib.Kolom] = "  " }));
            var panjang = form.Isian.First(i => i.Maks > 0);
            await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(sekarang, "",
                new Dictionary<string, string>(isi) { [panjang.Kolom] = new string('x', panjang.Maks + 1) }));
            if (form.Isian.FirstOrDefault(i => i.Pilihan is not null) is { } pilih)
                await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(sekarang, "",
                    new Dictionary<string, string>(isi) { [pilih.Kolom] = "Di luar daftar" }));
            var sebelumRegistrasi = sekarang.AddDays(-6);
            if (form.CekWaktuRegistrasi)
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(sebelumRegistrasi, "", isi));
            Assert.Empty((await service.MuatAsync()).Catatan);

            // Pemilik yang dikirim akun biasa diabaikan: selalu akun yang login.
            await service.SimpanAsync(sekarang, "P2", isi);
            var (kunjungan, daftar) = await service.MuatAsync();
            var catatan = Assert.Single(daftar);
            Assert.Equal(("Pasien Uji", akun.UserId, sekarang), (kunjungan.Pasien, catatan.Pemilik, catatan.Waktu));
            foreach (var (kolom, nilai) in isi) Assert.Equal(nilai, catatan.Isi[kolom]);
            if (form.Hitung is not null)
                foreach (var auto in form.Isian.Where(i => i.Otomatis)) Assert.NotEqual("", catatan.Isi[auto.Kolom]);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(sekarang, "", isi));

            // Bukan pemilik ditolak, termasuk bila ia berhak atas menunya.
            var kolomUbah = form.Isian.First(i => i is { Otomatis: false, Pilihan: null });
            var baru = new Dictionary<string, string>(isi) { [kolomUbah.Kolom] = "2" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => Layanan(lain).UbahAsync(catatan, sekarang, "", baru));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Layanan(lain).HapusAsync(catatan));
            await service.UbahAsync(catatan, sekarang.AddMinutes(-1), "", baru);
            var diubah = Assert.Single((await service.MuatAsync()).Catatan);
            Assert.Equal(("2", form.SatuPerKunjungan ? diubah.Waktu : sekarang.AddMinutes(-1)), (diubah.Isi[kolomUbah.Kolom], diubah.Waktu));
            // Data lama dari layar yang belum dimuat ulang tidak menimpa perubahan terbaru.
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(catatan, sekarang, "", isi));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(catatan));

            var cetak = await service.CetakAsync(diubah);
            Assert.Equal(akun.UserId, cetak.IdPemilik);
            var pdf = FormKlinisReport.GeneratePdf(form, cetak, kop);
            Assert.True(new FileInfo(pdf).Length > 1000);
            File.Delete(pdf);
            await service.HapusAsync(diubah);
            Assert.Empty((await service.MuatAsync()).Catatan);

            // Catatan lebih dari 2 x 24 jam: pemilik ditolak (kecuali formulir tanpa batas), Admin Utama boleh.
            var lama = sekarang.AddDays(-3);
            await service.SimpanAsync(lama, "", isi);
            var usang = Assert.Single((await service.MuatAsync()).Catatan);
            if (form.Batas48Jam)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.UbahAsync(usang, lama, "", baru));
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.HapusAsync(usang));
                await Layanan(admin).UbahAsync(usang, sebelumRegistrasi, akun.UserId, baru);
                await Layanan(admin).HapusAsync(Assert.Single((await service.MuatAsync()).Catatan));
            }
            else await service.HapusAsync(usang);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Layanan(admin).SimpanAsync(sekarang, "TIDAKADA", isi));
            Assert.Empty((await service.MuatAsync()).Catatan);
        }

        // Dua perubahan bersamaan atas catatan yang sama: hanya satu yang menang.
        var keperawatan = new FormKlinisService(settings, petugas, FormKlinisKatalog.CatatanKeperawatanRalan, "R1");
        await keperawatan.SimpanAsync(sekarang, "", new Dictionary<string, string> { ["uraian"] = "awal" });
        var asal = Assert.Single((await keperawatan.MuatAsync()).Catatan);
        var hasil = await Task.WhenAll(new[] { "A", "B" }.Select(async teks =>
        {
            try
            {
                await keperawatan.UbahAsync(asal, sekarang, "", new Dictionary<string, string> { ["uraian"] = teks });
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }));
        Assert.Single(hasil, ok => ok);
    }
}
