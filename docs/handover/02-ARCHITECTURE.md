# Arsitektur dan pola menambah layar

## Solusi `SimrsKhanza.sln`

| Proyek | Isi |
|---|---|
| `SimrsKhanza.Config` | `DatabaseSettings` dan penyimpanan pengaturan koneksi per pengguna |
| `SimrsKhanza.Core` | Entitas, autentikasi (`IAuthService`), hak akses, `LegacyTilePermissions.g.cs` (peta tile ke permission Khanza, dihasilkan) |
| `SimrsKhanza.Data` | Akses data. Repository, SQL mentah lewat `Db`, dan katalog spesifikasi layar per menu |
| `SimrsKhanza.UI` | Avalonia: `MainWindow` (satu jendela, `PageHost`), `Modules/<Fitur>/`, `Modules/Shared` (komponen bersama) |
| `SimrsKhanza.Tests` | xUnit. Tes spesifikasi, tes layar headless, tes integrasi DB (opsional) |

Navigasi: satu `MainWindow`, halaman diganti lewat `PageHost`. Setiap halaman adalah `PageView` dengan event `BackRequested`. Dashboard menu A–U dibangun dari `LegacyMenuCatalog` (1.224 tile) dan disaring dengan hak akses.

## Dua cara membuat layar

### 1. Berbasis spesifikasi (dipakai untuk hampir semua menu)

Dua layar generik di `UI/Modules/Shared`:

- `MasterView(MasterSpec)`: daftar dengan pencarian, formulir tambah/ubah, hapus, aksi per baris, ekspor CSV.
- `ReportView(ReportSpec)`: daftar hanya-baca dengan pencarian, filter periode (rentang tanggal, bulan, tahun, atau tidak ada), total kolom, ekspor CSV.

Spesifikasi hidup di `Data/**/MenuXSpecs.cs` sebagai kamus `Masters` dan `Reports` dengan kunci nama tombol legacy (`btnKamar`, `btnBubes`, ...). Definisinya di `Data/Master/MasterSpec.cs` dan `ReportSpec.cs`.

Aturan SQL:
- `ListSql` dan `ReportSpec.Sql` selalu menerima `@p0` = `"%cari%"`. Periode `DateRange`/`Month` menambah `@p1` (awal) dan `@p2` (akhir hari). `Year` menambah `@p1` = tahun. Parameter boleh dipakai berulang.
- `{hpp}` di SQL diganti kolom HPP profil (`dasar` atau `h_beli`).
- Nama tabel dan kolom di SQL harus cocok dengan `docs/audit/schema_sik.sql`. Cek dengan skrip sederhana (regex `FROM|JOIN`) terhadap daftar `CREATE TABLE` sebelum commit.
- `MasterField` memberi jenis (`Text`, `Number`, `Lookup`, `Choice`, `Date`, ...), kunci, panjang maksimum, lookup SQL. `BeforeSave`/`BeforeDelete` untuk validasi dan kolom turunan (`ComputedColumns`). `Singleton` untuk tabel pengaturan satu baris. `Actions` untuk perintah per baris.
- Hanya-baca bila transaksinya memposting jurnal di Khanza. Master yang terkait riwayat memakai `CanDelete = false`.

Contoh terlengkap: `Data/Master/MenuKSpecs.cs` (tarif dengan total otomatis, akun, kategori) dan `Data/Inventaris/MenuSSpecs.cs`.

### 2. Layar khusus

Untuk alur yang bukan daftar/formulir sederhana: `PostingJurnalView` (jurnal manual), `ParkirMasukView`, `PenggajianView`, `StokTransaksiView`, `RawatInapView`, `KasirView`, dan sebagainya. Dibangun dalam kode (`Ui.Bar`, `Ui.Grid`, `LookupBox`) atau AXAML. Tulis logika data di repository, bukan di view.

## Menambah tile baru, langkah demi langkah

1. Pastikan kunci tile ada di `LegacyMenuCatalog` dan permission-nya di `LegacyTilePermissions.g.cs`.
2. Buat spesifikasi di `Data/.../MenuXSpecs.cs` (atau layar khusus). Verifikasi nama tabel dan kolom dengan `schema_sik.sql`.
3. Hubungkan di `MainWindow.axaml.cs`, di dispatcher tile (bagian bawah, sebelum `ShowNotImplementedAsync`): cek kamus `Masters` lalu `Reports`, buka dengan `OpenPageAsync(new MasterView(spec))`/`ReportView(spec)`. Urutan penting: pencocokan yang lebih awal menang.
4. Tambahkan layar ke `Tests/MenuCScreensTests.cs` agar dibangun di tes headless.
5. Tulis tes spesifikasi untuk logika (validasi, total, penomoran). Contoh: `ManualJournalTests`, `LogisticsSpecsTests`.
6. `dotnet build` dan `dotnet test`.
7. Perbarui `docs/audit/TILE_BACKLOG.md` (hapus tile dari daftar, ubah hitungan), README, dan `MENU_X_STATUS.md` menu itu.

## Data dan jurnal

- `Db` (`Data/Sql/Db.cs`): koneksi MySql, `QueryAsync`/`ExecAsync`/`CountAsync`/`ScalarAsync`, transaksi `BeginAsync`/`CommitAsync`, penomoran `AutoNomorAsync`. Parameter bernama `@p0..`.
- `JurnalPoster.PostAsync(conn, tx, noBukti, keterangan, baris)` menulis satu `jurnal` + `detailjurnal` seimbang, nomor `JR` + `yyyyMMdd` + 6 digit. Dipakai deposit, billing ranap, parkir, dan posting manual. Rekening diambil dari `set_akun*` (`JurnalPoster.ReadAkunAsync`).
- Entitas EF Core dipakai fitur klinis awal (Registrasi, Rawat Jalan, Pasien, dst). Fitur baru lebih sering memakai `Db` dengan SQL langsung. EF: deklarasikan relasi `HasOne()` eksplisit dan konversi enum string persis seperti MySQL.

## Komponen UI bersama (`UI/Modules/Shared`)

`Ui` (pembangun kontrol: `Bar`, `Grid`, `Btn`, `Text`, `Number`, `Money`), `PageView` (rangka halaman, `RunAsync`, `StatusText`), `LookupBox` (pencarian kode/nama), `PeriodPicker`, `DataGridHeaderFit` (header kolom tidak terpotong), `PatientContextMenu` (klik kanan baris pasien, dipakai Registrasi/IGD/Ranap/Ralan/Pasien/Lab/Radiologi), `DashboardIcons`, `MenuTileBuilder`.

Tema kompak global di `App.axaml` (Segoe UI 11, header tabel biru). Jendela utama `BorderOnly` dengan tombol caption buatan.

## Kasir dan obat pulang

`KasirRepository` membangun snapshot tagihan, mengunci baris kunjungan di `reg_periksa`, lalu menghitung ulang sebelum menulis. Simpan nota, rincian pembayaran, jurnal, billing, dan status kunjungan dalam transaksi yang sama. `MuatKuitansiAsync` membaca data tersimpan. Input tindakan, perubahan biaya penunjang dan validasi resep pulang memakai kunci kunjungan yang sama. `JurnalPoster` memposting pada transaksi pemanggil. Lihat [audit Ranap](../tracker/AUDIT_RAWAT_INAP.md) untuk rekonsiliasi suspen dan batas penggunaan bersama Java.
