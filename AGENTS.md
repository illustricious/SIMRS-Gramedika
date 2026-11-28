# Panduan untuk agen AI

SIMRS Gramedika: aplikasi desktop C#, .NET 10 dan Avalonia di atas skema MySQL/MariaDB SIMRS Khanza (versi Java). Repo `illustricious/SIMRS-Gramedika`, branch `master`.

**Baca dulu, berurutan:**

1. [Handover untuk agen lain](docs/handover/15-HANDOVER-AGEN-LAIN.md): posisi terakhir, aturan pemilik, lingkungan dan cara kerja.
2. [Handover](docs/handover/README.md) dan [log sesi 14](docs/handover/14-SESSION-2026-11-28.md).
3. [Arsitektur](docs/handover/02-ARCHITECTURE.md) dan [runbook](docs/handover/01-RUNBOOK.md).

## Aturan yang tidak boleh dilanggar

- Bicara dengan pemilik dalam bahasa Indonesia santai (dipanggil "bang"), ringkas dan jujur.
- Commit, push, build EXE dan Release GitHub **hanya bila diminta**. Persetujuan sekali tidak berlaku untuk berikutnya.
- EXE tidak pernah di-commit atau diunggah. Build lama dihapus hanya bila diminta.
- Pakai skema dan data sintetis. Jangan mengarahkan pengembangan ke database produksi; akses data rumah sakit perlu persetujuan pemilik sistem.
- Jangan menaruh data pribadi, kata sandi atau data pasien di repo. Repo ini publik.
- Author commit memakai identitas yang sudah ada di konfigurasi repo; jangan memakai email pribadi.
- Ikuti perilaku Khanza. Berbeda hanya bila lebih akurat dan efisien, dan tulis perbedaannya di dokumen audit. Untuk aturan klinis, berubah hanya bila ada dasar resmi (WHO atau Kemenkes).

## Perintah utama (PowerShell, dari folder repo)

```powershell
$env:MSBuildEnableWorkloadResolver = 'false'
powershell -ExecutionPolicy Bypass -File tools\uji-fixture.ps1
node tools/tracker.mjs
```

Sumber Java acuan harus ada di `..\simrs-khanza` (lihat handover 15).
