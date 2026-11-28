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

    [Fact]
    public void KeseimbanganCairanDihitungDanDibatasiLebarKolom()
    {
        var form = FormKlinisKatalog.Cari("btnCatatanKeseimbanganCairan")!;
        var isi = new Dictionary<string, string>
        {
            ["infus"] = "500", ["tranfusi"] = "0", ["minum"] = "200", ["urine"] = "400", ["drain"] = "25",
            ["ngt"] = "0", ["iwl"] = "12.5",
        };
        form.Hitung!(isi);
        Assert.Equal("262.5", isi["keseimbangan"]);
        Assert.Contains("4 karakter", form.Periksa!(isi));
        isi["iwl"] = "";
        isi["minum"] = "x";
        form.Hitung!(isi);
        Assert.Equal("", isi["keseimbangan"]);
        Assert.Contains("Minum", form.Periksa!(isi));
        isi["minum"] = "0";
        isi["iwl"] = "125";
        form.Hitung!(isi);
        Assert.Equal("-50", isi["keseimbangan"]);
        Assert.Null(form.Periksa!(isi));
    }

    /// <summary>Skor tiap lembar peringatan dini pada catatan baru nol, lalu naik bersama tanda bahayanya.</summary>
    [Theory]
    [InlineData("btnPemantauanPEWSDewasa", "parameter_kesadaran", "Unrespon", "7", "Resusitasi")]
    [InlineData("btnPemantauanPEWSDewasa", "parameter_saturasi_oksigen", "<= 92", "3", "Assement segera oleh perawat senior")]
    [InlineData("btnPemantauanPEWSAnak", "parameter_perespirasi", "Stridor / O2 7-8 Lpm", "3", "Monitoring 1 sampai 2 jam")]
    [InlineData("btnPemantauanEWSNeonatus", "parameter1", "<= 29", "2", "Petugas : Perawat Jaga, Monitoring : Per 2 jam")]
    [InlineData("btnPemantauanMEOWS", "parameter_ketuban", "Busuk", "2", "Assessment segera oleh Bidan respon")]
    public void SkorPeringatanDiniMengikutiPilihan(string tile, string kolom, string pilihan, string total, string tindakLanjut)
    {
        var form = FormKlinisKatalog.Cari(tile)!;
        var isi = form.Isian.Where(i => i.Pilihan is not null).ToDictionary(i => i.Kolom, i => i.Awal ?? i.Pilihan![0]);
        form.Hitung!(isi);
        Assert.Equal("0", isi["skor_total"]);
        Assert.Contains("7 jam", isi["parameter_total"]);

        isi[kolom] = pilihan;
        form.Hitung!(isi);
        Assert.Equal(total, isi["skor_total"]);
        Assert.StartsWith(tindakLanjut, isi["parameter_total"]);
    }

    /// <summary>Seperti Khanza, skor warna kulit neonatus disimpan tetapi tidak dijumlahkan; layar mengingatkannya.</summary>
    [Fact]
    public void WarnaKulitNeonatusTidakIkutTotalTetapiDiingatkan()
    {
        var form = FormKlinisKatalog.Cari("btnPemantauanEWSNeonatus")!;
        var isi = form.Isian.Where(i => i.Pilihan is not null).ToDictionary(i => i.Kolom, i => i.Awal ?? i.Pilihan![0]);
        form.Hitung!(isi);
        Assert.DoesNotContain("Perhatian", form.Keterangan!(isi));
        isi["parameter8"] = "Pucat";
        form.Hitung!(isi);
        Assert.Equal(("2", "0"), (isi["skor8"], isi["skor_total"]));
        Assert.Contains("Warna Kulit bernilai 2 dan tidak ikut total", form.Keterangan!(isi));
    }

    /// <summary>Menu klinis Rawat Jalan yang menunjuk formulir harus memakai hak formulirnya, kalau tidak tombolnya mati.</summary>
    [Fact]
    public void MenuKlinisDanPencarianMembukaFormulir()
    {
        var terhubung = SimrsGramedika.UI.Modules.RawatJalan.MenuKlinisRalanCatalog.Items
            .Select(i => (Item: i, Form: FormKlinisKatalog.Cari(i.ReportKey))).Where(x => x.Form is not null).ToList();
        foreach (var (item, form) in terhubung) Assert.Equal((item.Id, form!.Izin), (item.Id, item.Permission));
        foreach (var tile in new[] { "btnCatatanObservasiCHBP", "btnCatatanCekGDS", "btnCatatanKeseimbanganCairan", "btnMonitoringTOLAC",
                     "btnMonitoringReaksiTranfusi", "btnSkriningCURB65", "btnPenilaianRisikoJatuhAnak", "btnPemantauanPEWSDewasa",
                     "btnPemantauanPEWSAnak", "btnPemantauanEWSNeonatus", "btnPemantauanMEOWS" })
            Assert.Contains(terhubung, x => x.Form!.Tile == tile);

        var admin = new AuthenticatedUser { IsAdminUtama = true };
        Assert.Equal(FormKlinisKatalog.Semua.Count, PilihFormulir.Cari(admin, "").Count);
        Assert.Equal("btnCatatanObservasiRanapKebidanan", Assert.Single(PilihFormulir.Cari(admin, "kebidanan observasi rawat")).Tile);
        var terbatas = new AuthenticatedUser { UserId = "u", Permissions = new HashSet<string> { "catatan_cek_gds" } };
        Assert.Equal("btnCatatanCekGDS", Assert.Single(PilihFormulir.Cari(terbatas, "")).Tile);
        Assert.True(PilihFormulir.Ada(terbatas));
        Assert.False(PilihFormulir.Ada(new AuthenticatedUser { UserId = "u" }));
    }

    /// <summary>Tile formulir harus punya daftar Menu M yang membawa No. Rawat dan permission tile yang sama.</summary>
    [Fact]
    public void SetiapFormulirTerhubungKeDaftarDanHakTile()
    {
        Assert.Equal(FormKlinisKatalog.Semua.Count, FormKlinisKatalog.Semua.Select(f => f.Tile).Distinct().Count());
        Assert.Equal(FormKlinisKatalog.Semua.Count, FormKlinisKatalog.Semua.Select(f => f.Tabel).Distinct().Count());
        foreach (var form in FormKlinisKatalog.Semua)
        {
            Assert.True(SimrsGramedika.Data.Master.MenuMSpecs.Reports.TryGetValue(form.Tile, out var daftar), form.Tile);
            Assert.Contains("no_rawat", daftar!.Sql);
            var akun = new AuthenticatedUser { UserId = "u", Permissions = new HashSet<string> { form.Izin } };
            Assert.True(LegacyTilePermissions.CanSeeTile(akun, form.Tile), form.Tile);
        }
    }

    /// <summary>Definisi formulir harus sama dengan DDL Khanza: kolom lengkap, pilihan sama dengan enum, panjang muat.</summary>
    [Fact]
    public void DefinisiCocokDenganSkemaKhanza()
    {
        var skema = Skema();

        foreach (var form in FormKlinisKatalog.Semua)
        {
            var ddl = Regex.Match(skema, $@"CREATE TABLE `{form.Tabel}` \((.*?)\) ENGINE", RegexOptions.Singleline).Groups[1].Value;
            var kolom = Kolom(skema, form.Tabel);
            string[] kunci = ["no_rawat", form.KolomTanggal, form.KolomPemilik, .. form.KolomJam is { } jam ? new[] { jam } : []];
            Assert.Equal(kolom.Keys.Order(), kunci.Concat(form.Isian.Select(i => i.Kolom)).Order());
            Assert.Equal(form.KolomJam is null && !form.TanggalSaja ? "datetime" : "date", kolom[form.KolomTanggal].Tipe);
            var utama = Regex.Match(ddl, @"PRIMARY KEY \(([^)]*)\)").Groups[1].Value.Replace("`", "").Split(',');
            Assert.Equal(form.SatuPerKunjungan, utama.Length == 1);

            foreach (var isian in form.Isian)
            {
                var (tipe, rinci) = kolom[isian.Kolom];
                if (tipe == "enum" && !isian.Otomatis)
                {
                    var enumDb = Enum(rinci);
                    // Layar Java kriteria keluar isolasi tidak menawarkan "Tidak Berlaku" walau kolomnya menerima.
                    if (form.Tabel == "checklist_kriteria_keluar_isolasi") Assert.Subset(enumDb.ToHashSet(), isian.Pilihan!.ToHashSet());
                    else Assert.Equal(enumDb, isian.Pilihan!);
                    if (isian.Awal is not null) Assert.Contains(isian.Awal, isian.Pilihan!);
                }
                else Assert.Null(isian.Pilihan);
                if (tipe == "varchar" && !isian.Otomatis) Assert.InRange(isian.Maks, 1, int.Parse(rinci));
            }
        }
    }

    /// <summary>
    /// Isian acak: hasil hitungan dan saran harus muat di kolomnya, yaitu salah satu pilihan enum atau teks selebar kolom.
    /// </summary>
    [Fact]
    public void HasilHitunganDanSaranMuatDiKolomKhanza()
    {
        var skema = Skema();
        var acak = new Random(1);
        string[] angka = ["", "1", "7.9", "8", "10.9", "11", "11.9", "12", "45", "80", "90", "120", "140", "160", "180", "250", "x"];
        foreach (var form in FormKlinisKatalog.Semua.Where(f => f.Hitung is not null || f.Usul is not null))
        {
            var kolom = Kolom(skema, form.Tabel);
            for (var kali = 0; kali < 400; kali++)
            {
                var isi = form.Isian.Where(i => !i.Otomatis).ToDictionary(i => i.Kolom,
                    i => i.Pilihan is { } p ? p[acak.Next(p.Length)] : angka[acak.Next(angka.Length)]);
                isi[FormKlinis.KunciJk] = kali % 2 == 0 ? "L" : "P";
                isi[FormKlinis.KunciUmur] = acak.Next(0, 99).ToString();
                isi[FormKlinis.KunciSatuanUmur] = kali % 5 == 0 ? "Bl" : "Th";
                form.Hitung?.Invoke(isi);
                // Isian yang ditolak aturan formulir tidak pernah sampai ke tabel.
                if (form.Periksa?.Invoke(isi) is not null) continue;
                var saran = form.Usul?.Invoke(isi) ?? [];
                var hasil = form.Isian.Where(i => i.Otomatis).Select(i => (i.Kolom, Nilai: isi[i.Kolom]))
                    .Concat(saran.Select(s => (Kolom: s.Key, Nilai: s.Value)));
                foreach (var (nama, nilai) in hasil)
                {
                    if (nilai.Length == 0) continue;
                    var (tipe, rinci) = kolom[nama];
                    if (tipe == "enum") Assert.True(Enum(rinci).Contains(nilai), $"{form.Tabel}.{nama} = {nilai}");
                    else if (tipe == "varchar") Assert.True(nilai.Length <= int.Parse(rinci), $"{form.Tabel}.{nama} = {nilai}");
                }
            }
        }
    }

    private static string Skema()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "docs", "audit", "schema_sik.sql"))) folder = folder.Parent;
        Assert.NotNull(folder);
        return File.ReadAllText(Path.Combine(folder.FullName, "docs", "audit", "schema_sik.sql"));
    }

    private static Dictionary<string, (string Tipe, string Rinci)> Kolom(string skema, string tabel)
    {
        var ddl = Regex.Match(skema, $@"CREATE TABLE `{tabel}` \((.*?)\) ENGINE", RegexOptions.Singleline).Groups[1].Value;
        return Regex.Matches(ddl, @"^\s*`(\w+)` (\w+)(?:\((.*)\))?[ ,]", RegexOptions.Multiline)
            .ToDictionary(m => m.Groups[1].Value, m => (m.Groups[2].Value, m.Groups[3].Value));
    }

    private static string[] Enum(string rinci) => [.. Regex.Matches(rinci, "'([^']*)'").Select(m => m.Groups[1].Value)];

    private static Dictionary<string, string> Hitungan(FormKlinis form, Dictionary<string, string> isi)
    {
        var salinan = new Dictionary<string, string>(isi) { [FormKlinis.KunciJk] = "P", [FormKlinis.KunciUmur] = "36", [FormKlinis.KunciSatuanUmur] = "Th" };
        form.Hitung?.Invoke(salinan);
        return salinan;
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
        await db.ExecAsync("INSERT INTO pegawai(nik,nama) VALUES ('P1','Pegawai Satu'),('P2','Pegawai Dua')");

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
            // Tinggi badan 1 cm membuat IMT tidak muat di kolomnya, jadi diberi nilai wajar.
            var isi = form.Isian.Where(i => !i.Otomatis).ToDictionary(i => i.Kolom,
                i => i.Pilihan?[^1] ?? (i.Kolom == "tinggi_badan" ? "100" : i.Maks > 0 ? "1" : ""));
            if (form.Periksa is not null)
                Assert.Null(form.Periksa(Hitungan(form, isi)));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Layanan(tanpaHak).MuatAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Layanan(tanpaHak).SimpanAsync(sekarang, "P1", isi));
            // Akun yang bukan petugas/dokter sesuai formulir ditolak.
            await Assert.ThrowsAsync<InvalidOperationException>(() => Layanan(bukan).SimpanAsync(sekarang, akun.UserId, isi));
            if (form.Isian.FirstOrDefault(i => i.Wajib) is { } wajib)
                await Assert.ThrowsAsync<ArgumentException>(() => service.SimpanAsync(sekarang, "", new Dictionary<string, string>(isi) { [wajib.Kolom] = "  " }));
            if (form.Isian.FirstOrDefault(i => i is { Maks: > 0, Otomatis: false }) is { } panjang)
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
            DateTime Tercatat(DateTime t) => form.TanggalSaja ? t.Date : t;
            Assert.Equal(("Pasien Uji", akun.UserId, Tercatat(sekarang)), (kunjungan.Pasien, catatan.Pemilik, catatan.Waktu));
            foreach (var (kolom, nilai) in isi) Assert.Equal(nilai, catatan.Isi[kolom]);
            if (form.Hitung is not null)
                foreach (var auto in form.Isian.Where(i => i.Otomatis)) Assert.NotEqual("", catatan.Isi[auto.Kolom]);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SimpanAsync(sekarang, "", isi));

            // Bukan pemilik ditolak, termasuk bila ia berhak atas menunya.
            // Formulir tanpa isian teks diubah lewat pilihan pertamanya.
            var kolomUbah = form.Isian.FirstOrDefault(i => i is { Otomatis: false, Pilihan: null }) ?? form.Isian.First(i => !i.Otomatis);
            var nilaiBaru = kolomUbah.Pilihan?[0] ?? "2";
            var baru = new Dictionary<string, string>(isi) { [kolomUbah.Kolom] = nilaiBaru };
            await Assert.ThrowsAsync<InvalidOperationException>(() => Layanan(lain).UbahAsync(catatan, sekarang, "", baru));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Layanan(lain).HapusAsync(catatan));
            await service.UbahAsync(catatan, sekarang.AddMinutes(-1), "", baru);
            var diubah = Assert.Single((await service.MuatAsync()).Catatan);
            Assert.Equal((nilaiBaru, form.SatuPerKunjungan ? diubah.Waktu : Tercatat(sekarang.AddMinutes(-1))), (diubah.Isi[kolomUbah.Kolom], diubah.Waktu));
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
        await keperawatan.HapusAsync(Assert.Single((await keperawatan.MuatAsync()).Catatan));

        // TANGGALMUNDUR=no: waktu pilihan akun biasa diganti waktu server, dan tidak bisa digeser saat diubah.
        var terkunci = new DatabaseSettings { Host = settings.Host, User = settings.User, Database = settings.Database,
            Port = settings.Port, TanggalMundur = false };
        var catatanForm = FormKlinisKatalog.CatatanKeperawatanRalan;
        var dikunci = new FormKlinisService(terkunci, petugas, catatanForm, "R1");
        Assert.True(dikunci.TanggalTerkunci);
        Assert.False(keperawatan.TanggalTerkunci);
        Assert.False(new FormKlinisService(terkunci, admin, catatanForm, "R1").TanggalTerkunci);
        Assert.False(new FormKlinisService(terkunci, petugas, FormKlinisKatalog.RisikoJatuhDewasa, "R1").TanggalTerkunci);
        await dikunci.SimpanAsync(sekarang.AddDays(-1), "", new Dictionary<string, string> { ["uraian"] = "kunci" });
        var tercatat = Assert.Single((await dikunci.MuatAsync()).Catatan);
        Assert.InRange(tercatat.Waktu, sekarang, sekarang.AddMinutes(5));
        await dikunci.UbahAsync(tercatat, sekarang.AddDays(-1), "", new Dictionary<string, string> { ["uraian"] = "ubah" });
        var sesudah = Assert.Single((await dikunci.MuatAsync()).Catatan);
        Assert.Equal((tercatat.Waktu, "ubah"), (sesudah.Waktu, sesudah.Isi["uraian"]));
        // Admin Utama tetap bisa memilih waktu.
        await new FormKlinisService(terkunci, admin, catatanForm, "R1").UbahAsync(sesudah, sekarang.AddDays(-1), "P1", sesudah.Isi);
        Assert.Equal(sekarang.AddDays(-1), Assert.Single((await dikunci.MuatAsync()).Catatan).Waktu);
    }
}
