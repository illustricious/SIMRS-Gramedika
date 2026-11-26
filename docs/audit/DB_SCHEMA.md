# Skema Database `sik` — Ringkasan (Struktur Saja, Tanpa Data Pasien)

Diekstrak dari `sik.sql` + `sik_bridging_lab.sql` + `sik_bridging_radiologi.sql` dengan mengambil **hanya statement `CREATE TABLE`** (0 baris `INSERT INTO` ikut diekstrak — dikonfirmasi lewat `grep`). File hasil ekstraksi lengkap: `schema_sik.sql` (1.3MB, 1.199 `CREATE TABLE`), dikirim terpisah sebagai referensi.

## 1. Skala
- **1.199 tabel total** (1.190 dari `sik.sql` + 9 dari dua file bridging). Ini mengonfirmasi temuan audit sebelumnya: SIMRS Khanza berkembang dengan pola "satu fitur/formulir = satu tabel baru", bukan desain ternormalisasi dari awal.
- Distribusi grup tabel terbesar (berdasar awalan nama):
  | Grup | Jumlah tabel | Contoh |
  |---|---|---|
  | `penilaian*` | 87 | Formulir asesmen klinis (skor Bishop, GRACE, MEOWS, dst — satu tabel per jenis penilaian) |
  | `satu*` | 57 | Integrasi SatuSehat (Kemenkes) |
  | `surat*` | 55 | Surat/sertifikat/persetujuan |
  | `set*` (settings) | 53 | Konfigurasi/master kecil per fitur |
  | `skrining*` | 35 | Formulir skrining (TBC, obesitas, dll) |
  | `master*` | 34 | Data master umum |
  | `hasil*` | 31 | Hasil pemeriksaan (echo, USG, dll) |
  | `detail*` | 27 | Tabel detail/child dari transaksi lain |
  | `bayar*` | 26 | Transaksi pembayaran |
  | `inventaris*` | 22 | Inventaris aset |
  | `bridging*` | 16 | Integrasi sistem eksternal |

**Implikasi untuk rewrite**: tidak realistis (dan tidak perlu) mem-port 1.199 tabel sekaligus. Fokuskan domain model baru ke **tabel inti** dulu, modul penilaian/skrining/surat (yang jumlahnya ratusan tapi strukturnya sangat mirip satu sama lain) bisa digeneralisasi jadi SATU desain "formulir dinamis" di rewrite, bukan satu entity class per formulir seperti versi lama.

## 2. Tabel Inti & Relasi (fondasi domain model)

```
penjab (cara bayar/asuransi)
   ↑
pasien ──────────┐
   │              │
   ▼              ▼
reg_periksa ──► poliklinik
   │  │
   │  └──► dokter ──► pegawai ──► petugas
   ▼
kamar_inap ──► kamar ──► bangsal
```

| Tabel | Primary Key | Peran | FK keluar |
|---|---|---|---|
| `pasien` | `no_rkm_medis` | Identitas pasien (data master) | `penjab`, `kelurahan`, `kecamatan`, `kabupaten`, `propinsi`, `perusahaan_pasien`, `suku_bangsa`, `bahasa_pasien`, `cacat_fisik` |
| `reg_periksa` | `no_rawat` | Satu kunjungan/registrasi (rawat jalan/inap) | `pasien`, `poliklinik`, `dokter`, `penjab` |
| `dokter` | `kd_dokter` | Data dokter | `spesialis`, `pegawai` (dokter adalah subtype pegawai, FK ke `nik`) |
| `pegawai` | `id` (`nik` unique) | Data pegawai/karyawan (induk utk dokter & petugas) | `jnj_jabatan`, `departemen`, `bidang`, `bank`, dll (~11 FK) |
| `petugas` | `nip` | Petugas non-dokter (perawat, admin, dst) | `pegawai`, `jabatan` |
| `penjab` | `kd_pj` | Penanggung jawab biaya / cara bayar (umum, BPJS, dll) | - |
| `poliklinik` | `kd_poli` | Master poli | - |
| `bangsal` | `kd_bangsal` | Master bangsal rawat inap | - |
| `kamar` | `kd_kamar` | Master kamar | `bangsal` |
| `kamar_inap` | `no_rawat`+`tgl_masuk`+`jam_masuk` | Riwayat rawat inap per kunjungan | `kamar`, `reg_periksa` |

**Pengamatan desain lama (catatan migrasi):**
- Charset `latin1` di semua tabel inti — perlu keputusan migrasi ke `utf8mb4` di skema baru (karena data lama mungkin berisi karakter non-latin1 yang sudah "rusak" — perlu validasi saat migrasi data nanti, bukan sekarang).
- Banyak kolom bertipe `enum(...)` untuk status (contoh: `reg_periksa.stts`, `kamar.status`, `kamar_inap.stts_pulang`) — di rewrite lebih baik jadi lookup table atau C# enum yang tervalidasi, bukan string enum MySQL yang sulit di-extend.
- Beberapa FK pakai `varchar`/`char` sebagai key (`kd_dokter`, `kd_poli`, dst) bukan surrogate integer — pertimbangkan tetap pakai kode asli (untuk kompatibilitas data lama) tapi tambahkan surrogate `Guid`/`int` di internal model baru.
- `pegawai.photo` nullable varchar(500) — kemungkinan path file foto pegawai, bukan BLOB — konsisten dengan pola "path file di kolom varchar" yang juga dipakai di modul lain.

## 3. Dampak ke Desain `SimrsGramedika.Core` / `SimrsGramedika.Data`

Usulan entity awal (`SimrsGramedika.Core/Entities/`):
```
Pasien, RegPeriksa, Dokter, Pegawai, Petugas, Penjab, Poliklinik, Bangsal, Kamar, KamarInap
```
Setiap entity 1:1 secara konsep dengan tabel inti di atas (nama field di-PascalCase-kan, tipe enum MySQL → C# enum). Relasi dipetakan sebagai navigation property standar (mis. `RegPeriksa.Pasien`, `RegPeriksa.Dokter`).

**Pola "formulir dinamis"** diusulkan untuk menggantikan grup `penilaian*`/`skrining*`/`surat*` (87+35+55 = 177 tabel office) — satu entity generik `FormKlinis` + `FormKlinisJawaban` (key-value per field) + definisi skema formulir per jenis disimpan sebagai data (JSON/template), bukan 177 tabel & 177 dialog Avalonia terpisah. Ini keputusan arsitektur besar — **perlu persetujuan Anda sebelum dieksekusi**, karena mengubah model data secara fundamental dari pendekatan lama.

## 4. Yang Belum Dilakukan
- Tabel di luar 10 tabel inti (1.189 sisanya) belum didokumentasikan satu per satu — akan digali bertahap per modul sesuai urutan migrasi di `PLANNING_APLIKASI.md`.
- Belum membuat migration/EF Core model nyata — ini baru rencana struktur.
- **Data pasien tetap tidak disentuh** — ekstraksi ini 100% hanya `CREATE TABLE`, dikonfirmasi 0 baris data ikut.
