# Handover untuk agen AI lain (28 November 2026)

Ditulis saat pemilik berpindah dari Claude ke ChatGPT. Dokumen ini cukup untuk mulai bekerja tanpa riwayat percakapan. Aturan wajib ada di [AGENTS.md](../../AGENTS.md).

## Proyek

Menulis ulang SIMRS Khanza (Java Swing) menjadi aplikasi C#, .NET 10 dan Avalonia, memakai database Khanza yang sama sehingga bisa berdampingan dengan klien Java. Targetnya fungsi yang setara, bukan hanya layar. Bridging (BPJS, Satu Sehat, dan sejenisnya, Menu L) ditunda di repo ini; pemilik menyiapkannya sebagai aplikasi terpisah ([log 12](12-SESSION-2026-11-27.md#bridging-di-aplikasi-terpisah)).

## Posisi terakhir

| Hal | Keadaan |
|---|---|
| Commit fitur terakhir | `372617e` batal nota di Kasir, `e8a4ba7` data kelahiran bayi, `66759e7` dokumennya. Commit handover ini menyusul. |
| Push | Pemilik meminta push bersama handover ini. Cek `git status -sb` untuk memastikan. |
| EXE lokal terbaru | `dist/SIMRS-Gramedika-0.3.0-alpha.202611281617-win-x64-single.exe`, dari `66759e7`, 63.811.849 byte, SHA-256 `15C75CCD7B5234D14F76477284CDE2BD7E8D5E495AD3C961677F3A4D1BFF81EC` |
| Build lama di `dist/` | `202611280241` dan `202611280738`, belum diminta dihapus |
| Release GitHub | Tidak dibuat; pre-release lama tertinggal dari source |
| Tes | `uji-fixture.ps1` lulus: fixture 87, alur penunjang 1, skema penuh 36, tanpa database 234 lulus dan 50 dilewati |
| Peta fungsi | 963 dari 2.202 pasangan tabel-aksi tulis (44%), analisis statis dan batas atas |
| Formulir rekam medis | 78 |

### Catatan uji EXE `202611281617`

Dibuka 12 kali dengan folder konfigurasi kosong. Sepuluh kali jendela **Koneksi Database** muncul dan bertahan. Dua kali proses selesai sendiri; kode keluar hanya terekam pada salah satunya, yaitu 0. Log Application Windows tidak memuat catatan galat aplikasi pada rentang uji. Dengan folder konfigurasi kosong, kode awal keluar secara normal bila jendela koneksi ditutup, dan komputernya sedang dipakai, jadi dugaan terkuat jendelanya ditutup dari luar. **Belum terbukti.** Sembilan percobaan terakhir yang diukur (enam build ini, tiga build `202611280738`) semuanya bertahan. Bila pemilik melaporkan aplikasi menutup sendiri, mulai dari `App.axaml.cs`. Login, menu dan database belum diuji pada build ini.

## Lingkungan

Pengembangan dilakukan di Windows 11.

- .NET SDK 10, Node 22 atau lebih baru, Python 3 (untuk `tools/survei-formulir.py`), PowerShell 5.1.
- Set `$env:MSBuildEnableWorkloadResolver = 'false'` sebelum build. Opsi build yang dipakai: `-m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false`.
- Aktifkan hook sekali per clone: `git config core.hooksPath .githooks`. Hook memperbarui laporan tracker pada setiap commit.
- **Sumber Java acuan** tidak ada di repo ini. Letakkan di sebelah folder repo: `git clone https://github.com/mas-elkhanza/simrs-khanza ../simrs-khanza`, lalu `git -C ../simrs-khanza checkout 6da4e15deb`. Salinan lokal pemilik hanya berbeda karena berkas `.jasper` dan hasil build dihapus; tidak ada berkas `.java` di `src` yang berbeda menurut `git status`.
- **MariaDB portabel** untuk tes: `dist/mariadb-test-11.4.8.zip` (tidak di git). Di komputer lain, unduh paket ZIP MariaDB 11.4 untuk Windows dan beri `-MariaDb <folder>` pada skrip.
- Lingkungan Linux atau cloud: `dotnet test` tetap jalan dan tes database dilewati. Skrip fixture hanya untuk Windows; langkah manual ada di [runbook](01-RUNBOOK.md#menjalankan-fixture-satu-per-satu). Katakan terus terang bila tes database tidak bisa dijalankan.

## Cara kerja yang sudah dipakai

1. Baca handler dan aturan di Java (`..\simrs-khanza\src`) sebelum menulis: validasi, hak akses, tabel yang ditulis, jurnal, penomoran.
2. Cocokkan nama tabel dan kolom dengan `docs/audit/schema_sik.sql`.
3. Logika dan SQL di `SimrsGramedika.Data`, layar di `SimrsGramedika.UI`. Tulisan yang berkaitan harus satu transaksi.
4. Tes fixture baru: kelas tes dengan atribut yang melewati diri bila `SIMRS_<NAMA>_FIXTURE_PORT` kosong, database `simrs_<nama>_fixture`, lalu daftarkan di kamus `$lain` pada `tools/uji-fixture.ps1`. Contoh: `KasirBatalNotaTests`.
5. Layar baru ditambahkan ke `MenuCScreensTests`, lalu diklik dengan tes headless sementara (di bawah).
6. Perbarui dokumen audit terkait, `PRIORITAS.md`, log sesi dan handover, `docs/tracker/status.json`; jalankan `node tools/peta-fungsi.mjs --java ../simrs-khanza/src` lalu `node tools/tracker.mjs`. Samakan angka progres di handover.
7. Laporkan apa adanya: yang sudah diuji, yang belum, dan perbedaan dari Khanza. Database rumah sakit belum pernah diuji.

### Uji klik headless

Tulis `src/SimrsGramedika.Tests/ZzSementaraKlikTests.cs`, jalankan skrip, lihat gambarnya, lalu **hapus** tes itu dan folder `TestResults`.

```powershell
powershell -ExecutionPolicy Bypass -File tools\klik-headless.ps1
```

Contoh berikut sudah dijalankan dan lulus:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;
using SimrsGramedika.UI.Modules.RekamMedis;

namespace SimrsGramedika.Tests;

public class ZzSementaraKlikTests
{
    [Fact]
    public void Klik()
    {
        var shots = Environment.GetEnvironmentVariable("ZZ_SHOTS");
        if (shots is null) return;
        var galat = new List<string>();
        var thread = new Thread(() =>
        {
            try { Jalankan(shots); }
            catch (Exception ex) { galat.Add(ex.ToString()); }
        });
        thread.Start();
        thread.Join();
        Assert.True(galat.Count == 0, string.Join("\n", galat));
    }

    private static void Tunggu(Func<bool> syarat, string apa)
    {
        var batas = DateTime.Now.AddSeconds(20);
        while (!syarat())
        {
            if (DateTime.Now > batas) throw new TimeoutException(apa);
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
    }

    private static void Jalankan(string shots)
    {
        AppBuilder.Configure<SimrsGramedika.UI.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        var settings = DatabaseSettings.Load();
        // Panggilan database di thread UI harus lewat Task.Run; menunggu langsung membuat tes macet.
        var db = Task.Run(() => Db.OpenAsync(settings)).GetAwaiter().GetResult();
        void Sql(string q) => Task.Run(() => db.ExecAsync(q)).GetAwaiter().GetResult();
        // Data sintetis boleh hanya mengisi kolom yang dipakai.
        Sql("SET SESSION sql_mode='', FOREIGN_KEY_CHECKS=0");
        Sql("INSERT INTO pegawai(nik,nama,stts_aktif) VALUES ('BD01','Bidan Sintetis','AKTIF')");

        AppSession.CurrentUser = new() { UserId = "uji", Permissions = new HashSet<string> { "kelahiran_bayi" } };
        var view = new KelahiranBayiView();
        var window = new Window { Width = 1366, Height = 768, Content = view };
        window.Show();
        var muat = view.ReloadAsync();
        Tunggu(() => muat.IsCompleted, "muat layar");
        Directory.CreateDirectory(shots);
        window.CaptureRenderedFrame()!.Save(Path.Combine(shots, "contoh.png"));
        window.Close();
        Task.Run(() => db.DisposeAsync().AsTask()).GetAwaiter().GetResult();
    }
}
```

Untuk klik: `button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))`. Kontrol yang dibuat dari kode dicari dengan `view.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "...")`, bukan `FindControl`. Dialog ada di `window.OwnedWindows`.

## Jebakan yang pernah terjadi

- Berkas repo bercampur CRLF dan LF, sebagian ber-BOM. Pertahankan bila mengubah lewat skrip, dan pastikan teks yang diganti cocok tepat satu kali.
- Perintah panjang berkutip di Bash gagal; tulis skrip ke berkas dulu.
- EF Core mengembalikan entitas yang sudah dilacak dengan nilai lama. Kosongkan `ChangeTracker` atau pakai context baru sebelum membaca ulang baris yang diubah lewat SQL.
- Baris tabel lama bisa berisi `NULL` pada kolom yang di entitas dianggap tidak kosong. Jangan memuat entitas utuh hanya untuk mengunci baris.
- `AppSession` ada di namespace `SimrsGramedika.Core.Auth`.
- Koneksi memakai `GuidFormat=None`; tanpa itu kolom 36 karakter dibaca sebagai GUID.
- Urutan kunci transaksi: baris `reg_periksa` lebih dulu ([arsitektur](02-ARCHITECTURE.md#urutan-kunci-transaksi)). Cek "sudah ditagih" lewat `TagihanKunjungan`.
- ID di `status.json` harus ada di `fitur.tsv` (menu popup `popup|berkas|...|Nama`, tombol `aksi|berkas|Nama`). `peta-fungsi.mjs` berhenti bila berkas baru merakit nama tabel tetapi belum didaftarkan di skrip.
- Matikan aplikasi yang sedang berjalan sebelum build ulang. Folder induk memuat proyek lain; jangan sentuh proses atau berkasnya.

## Pekerjaan berikutnya

Belum ada yang disetujui. Terakhir diusulkan **pembelian dan retur farmasi**; pemilik berpindah alat sebelum menjawab.

| Calon | Acuan dan catatan |
|---|---|
| Pembelian, retur beli, retur jual, piutang obat | `inventory/DlgPembelian.java`, `DlgReturBeli.java`, `DlgReturJual.java`, `DlgPiutang.java`. Tabel transaksinya belum ditulis; mutasi stok dan jurnal harus ikut. Lihat kode penerimaan dan jual bebas yang sudah ada sebelum menulis yang baru. |
| Formulir rekam medis kelompok berikutnya | Penilaian awal medis per spesialis (21), lain-lain (19). Langkah di [log 13](13-SESSION-2026-11-28.md#pekerjaan-yang-sedang-berjalan-formulir-rekam-medis-per-kelompok). |
| Operasi rawat jalan | Operasi baru tersedia untuk rawat inap. |
| Sisa kelahiran bayi | Buka dari Rawat Inap atau Pasien, cetak daftar, label, templat surat rumah sakit. |
| Sisa Kasir | Retur obat, obat langsung, batal billing parsial. |
| Uji pada salinan database RS | Perlu persetujuan pemilik sistem. Daftar menu yang dipakai harian akan memperjelas prioritas. |

Prioritas lengkap: [PRIORITAS.md](../tracker/PRIORITAS.md) dan [peta fungsi](../tracker/AUDIT_PETA_FUNGSI.md).

## Keputusan dan pertanyaan pemilik

Sudah diputuskan:

- Batal nota sementara mengikuti Khanza: tanpa batas tanggal dan tanpa kolom alasan.
- Perbedaan dari Khanza diterima bila lebih akurat dan efisien.
- Skor Warna Kulit EWS Neonatus tidak dijumlahkan karena dasar WHO atau Kemenkes tidak ditemukan.

Belum dijawab: lihat [log 14](14-SESSION-2026-11-28.md#pertanyaan-yang-belum-dijawab-pemilik).

## Prompt pembuka

> Baca `AGENTS.md` dan `docs/handover/15-HANDOVER-AGEN-LAIN.md`. Cek `git status -sb` dan `git log -5`, lalu jalankan `tools\uji-fixture.ps1` bila lingkungannya mendukung. Laporkan hasilnya singkat dalam bahasa Indonesia santai, lalu tunggu saya memilih pekerjaan. Jangan commit atau push sebelum saya minta.
