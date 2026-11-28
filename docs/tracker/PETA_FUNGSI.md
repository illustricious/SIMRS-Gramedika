# Peta fungsi Khanza → Gramedika

Dihasilkan `node tools/peta-fungsi.mjs --java ../simrs-khanza/src`. Jangan diubah tangan. Rincian per jendela ada di
[peta-fungsi.tsv](peta-fungsi.tsv), per tile di [peta-tile.tsv](peta-tile.tsv). Cara membaca dan hasil cek sampel ada di
[AUDIT_PETA_FUNGSI.md](AUDIT_PETA_FUNGSI.md).

**Ini analisis statis pada tingkat tabel.** "Belum ada" cukup dapat dipercaya. "Ada" adalah batas atas: tabelnya ditulis
Gramedika, tetapi validasi, jurnal, cetak dan cabang layarnya belum tentu setara.

## Ringkasan

| Ukuran | Ada | Total | Persen |
|---|---:|---:|---:|
| Pasangan tabel dan aksi tulis (tambah/ubah/hapus) yang dilakukan jendela Java | 968 | 2202 | 44% |
| Tabel yang ditulis jendela Java dan juga ditulis Gramedika | 454 | 980 | 46% |
| Jendela Java yang menulis data, seluruh tulisannya ada | 324 | 743 | 44% |
| Jendela Java yang menulis data, minimal sebagian ada | 471 | 743 | 63% |

Angka di atas tidak memasukkan bridging, tabel sementara, catatan sistem, jurnal dan riwayat stok.

Dari 324 jendela yang seluruh tulisannya ada, 179 tercakup oleh layar master generik (tambah/ubah/hapus satu tabel).

## Jendela alur inti

Aksi tulis adalah pasangan tabel dan tambah/ubah/hapus. "Ada" di sini juga batas atas.

| Jendela | Status | Aksi tulis ada | Contoh tabel yang aksinya belum ada |
|---|---:|---:|---:|
| DlgReg | tulis sebagian | 46/187 | asuhan_gizi, aturan_pakai, bayar_detail_periksa_lab_perujuk, bayar_detail_periksa_lab, bayar_operasi_dokter_anak, bayar_operasi_dokter_anestesi |
| DlgIGD | tulis sebagian | 44/165 | asuhan_gizi, aturan_pakai, bayar_detail_periksa_lab_perujuk, bayar_detail_periksa_lab, bayar_operasi_dokter_anak, bayar_operasi_dokter_anestesi |
| DlgRawatJalan | tulis ada | 20/20 | - |
| DlgRawatInap | tulis ada | 18/18 | - |
| DlgKamarInap | tulis ada | 11/11 | - |
| DlgKasirRalan | tulis sebagian | 87/236 | antripoli, asuhan_gizi, aturan_pakai, bayar_detail_periksa_lab_perujuk, bayar_detail_periksa_lab, bayar_operasi_dokter_anak |
| DlgBilingRalan | tulis sebagian | 20/27 | tagihan_bpd_jabar, tagihan_bpd_jateng, tagihan_bpd_papua, tagihan_briva, tagihan_mandiri, tagihan_obat_langsung |
| DlgBilingRanap | tulis sebagian | 23/30 | tagihan_bpd_jabar, tagihan_bpd_jateng, tagihan_bpd_papua, tagihan_briva, tagihan_mandiri, tagihan_obat_langsung |
| DlgPeriksaLaboratorium | tulis ada | 6/6 | - |
| DlgPeriksaRadiologi | tulis sebagian | 6/8 | gambar_radiologi, order_out |
| DlgDaftarPermintaanResep | tulis sebagian | 2/8 | antriapotek2, antriapotek3, bukti_penyerahan_resep_obat, permintaan_stok_obat_pasien |
| DlgPasien | tulis sebagian | 13/45 | bayar_piutang, booking_periksa_diterima, booking_registrasi, bridging_dukcapil, diagnosa_corona, pasien_corona |

## Sisa pekerjaan menurut jenis

Jendela yang menulis data dan belum seluruhnya ada: 419. Setiap jendela dihitung sekali, pada baris pertama yang cocok.

| Jenis | Jendela | Di antaranya sebagian | Dibuka dari alur inti |
|---|---:|---:|---:|
| Transaksi stok (riwayat stok, sebagian besar juga jurnal) | 60 | 36 | 6 |
| Pembayaran, tagihan dan jurnal tanpa stok | 43 | 14 | 9 |
| Formulir rekam medis dengan satu tabel isi | 83 | 5 | 78 |
| Formulir rekam medis dengan tabel tambahan | 36 | 2 | 30 |
| Surat dan persetujuan | 39 | 11 | 30 |
| Restore data terhapus | 16 | 14 | 0 |
| Lainnya (master, permintaan, pengaturan, pelayanan) | 142 | 65 | 24 |

## Semua jendela Java

| Status | Jendela |
|---|---:|
| tulis ada | 324 |
| tulis sebagian | 147 |
| hanya baca | 266 |
| baca ada | 502 |
| baca sebagian | 14 |
| belum ada | 8 |
| tanpa data | 29 |
| bridging | 241 |
| **Total** | 1531 |

## Per folder source Java

| Folder | Jendela | Menulis data | Tulis ada | Sebagian | Hanya baca | Belum ada | Aksi tulis tercakup |
|---|---:|---:|---:|---:|---:|---:|---:|
| bridging | 237 | 0 | 0 | 0 | 0 | 0 | - |
| dapur | 35 | 19 | 2 | 0 | 17 | 0 | 9% |
| grafikanalisa | 148 | 0 | 0 | 0 | 0 | 0 | - |
| informasi | 11 | 0 | 0 | 0 | 0 | 0 | - |
| inventaris | 35 | 30 | 16 | 0 | 14 | 0 | 54% |
| inventory | 117 | 63 | 27 | 26 | 9 | 1 | 54% |
| ipsrs | 43 | 22 | 3 | 0 | 19 | 0 | 13% |
| kepegawaian | 73 | 44 | 41 | 3 | 0 | 0 | 88% |
| keuangan | 168 | 71 | 23 | 9 | 37 | 2 | 44% |
| laporan | 107 | 11 | 3 | 1 | 7 | 0 | 26% |
| parkir | 3 | 2 | 2 | 0 | 0 | 0 | 100% |
| pcraicra | 12 | 10 | 0 | 9 | 1 | 0 | 51% |
| permintaan | 17 | 17 | 7 | 7 | 3 | 0 | 59% |
| perpustakaan | 16 | 12 | 9 | 2 | 1 | 0 | 70% |
| rekammedis | 252 | 228 | 109 | 7 | 109 | 3 | 42% |
| restore | 18 | 18 | 2 | 14 | 2 | 0 | 51% |
| setting | 34 | 30 | 16 | 13 | 1 | 0 | 77% |
| simrskhanza | 71 | 54 | 20 | 27 | 7 | 0 | 41% |
| smsui | 3 | 0 | 0 | 0 | 0 | 0 | - |
| surat | 47 | 43 | 4 | 11 | 28 | 0 | 17% |
| toko | 34 | 24 | 3 | 16 | 5 | 0 | 18% |
| tranfusidarah | 10 | 10 | 2 | 2 | 6 | 0 | 14% |
| viabarcode | 24 | 20 | 20 | 0 | 0 | 0 | 100% |
| widget | 1 | 0 | 0 | 0 | 0 | 0 | - |
| ziscsr | 15 | 15 | 15 | 0 | 0 | 0 | 100% |

## Per menu dashboard

Hanya menghitung tile yang jendela Java-nya menulis data. "Khusus" berarti tile dibuka dengan layar buatan sendiri; kemampuan
tulisnya lihat status jendelanya. "?" berarti jendela Java tidak terpetakan dari `frmUtama`.

| Menu | Tile | Java menulis | Bisa isi (master/formulir) | Layar khusus | Hanya tampil | Ditunda/lain | ? |
|---|---:|---:|---:|---:|---:|---:|---:|
| A | 34 | 32 | 8 | 23 | 1 | 0 | 1 |
| B | 16 | 14 | 0 | 14 | 0 | 0 | 0 |
| C | 61 | 41 | 40 | 1 | 0 | 0 | 2 |
| D | 75 | 33 | 11 | 10 | 12 | 0 | 0 |
| E | 33 | 15 | 5 | 0 | 10 | 0 | 0 |
| F | 27 | 13 | 4 | 0 | 9 | 0 | 0 |
| G | 28 | 27 | 16 | 0 | 11 | 0 | 0 |
| H | 3 | 2 | 1 | 1 | 0 | 0 | 0 |
| I | 60 | 2 | 1 | 0 | 1 | 0 | 1 |
| J | 96 | 6 | 4 | 0 | 2 | 0 | 0 |
| K | 87 | 54 | 22 | 0 | 32 | 0 | 0 |
| L | 183 | 97 | 0 | 0 | 0 | 97 | 3 |
| M | 255 | 242 | 120 | 2 | 120 | 0 | 1 |
| N | 11 | 11 | 2 | 0 | 9 | 0 | 0 |
| O | 113 | 0 | 0 | 0 | 0 | 0 | 0 |
| P | 45 | 43 | 12 | 0 | 31 | 0 | 0 |
| Q | 16 | 12 | 10 | 0 | 2 | 0 | 0 |
| R | 10 | 10 | 9 | 0 | 1 | 0 | 0 |
| S | 25 | 18 | 5 | 0 | 13 | 0 | 0 |
| T | 16 | 16 | 16 | 0 | 0 | 0 | 0 |
| U | 30 | 29 | 21 | 8 | 0 | 0 | 0 |

Tile di luar menu L: 1041. Yang di Java menulis data tetapi tile Gramedika hanya menampilkan:
**254**.

## Celah terpenting

Diurutkan menurut berapa alur inti (registrasi, IGD, rawat jalan, rawat inap, kamar, kasir, penunjang, farmasi, pasien) yang
membuka jendela itu, lalu jumlah pembuka. Urutan ini ukuran keterhubungan, bukan frekuensi pemakaian di rumah sakit.

| Jendela | Modul | Alur inti | Pembuka | Tulis belum ada (tabel|aksi) |
|---|---:|---:|---:|---:|
| DlgPeresepanDokter | inventory | 7 | 9 | antrianiterasi tambah |
| DlgPermintaanPelayananInformasiObat | permintaan | 7 | 8 | jawaban_pio_apoteker tambah, pelayanan_informasi_obat hapus, pelayanan_informasi_obat tambah, pelayanan_informasi_obat ubah |
| RMKonselingFarmasi | rekammedis | 7 | 8 | konseling_farmasi hapus, konseling_farmasi tambah, konseling_farmasi ubah |
| RMCatatanAnastesiSedasi | rekammedis | 6 | 8 | catatan_anestesi_sedasi hapus, catatan_anestesi_sedasi tambah, catatan_anestesi_sedasi ubah |
| RMCatatanPengkajianPaskaOperasi | rekammedis | 6 | 8 | catatan_pengkajian_paska_operasi hapus, catatan_pengkajian_paska_operasi tambah, catatan_pengkajian_paska_operasi ubah |
| RMChecklistPreOperasi | rekammedis | 6 | 8 | checklist_pre_operasi hapus, checklist_pre_operasi tambah, checklist_pre_operasi ubah |
| RMMonitoringAldrettePascaAnestesi | rekammedis | 6 | 8 | skor_aldrette_pasca_anestesi hapus, skor_aldrette_pasca_anestesi tambah, skor_aldrette_pasca_anestesi ubah |
| RMMonitoringBromagePascaAnestesi | rekammedis | 6 | 8 | skor_bromage_pasca_anestesi hapus, skor_bromage_pasca_anestesi tambah, skor_bromage_pasca_anestesi ubah |
| RMMonitoringStewardPascaAnestesi | rekammedis | 6 | 8 | skor_steward_pasca_anestesi hapus, skor_steward_pasca_anestesi tambah, skor_steward_pasca_anestesi ubah |
| RMPenilaianFisioterapi | rekammedis | 6 | 8 | penilaian_fisioterapi hapus, penilaian_fisioterapi tambah, penilaian_fisioterapi ubah |
| RMPenilaianPreAnastesi | rekammedis | 6 | 8 | penilaian_pre_anestesi hapus, penilaian_pre_anestesi tambah, penilaian_pre_anestesi ubah |
| RMPenilaianPreInduksi | rekammedis | 6 | 8 | penilaian_pre_induksi hapus, penilaian_pre_induksi tambah, penilaian_pre_induksi ubah |
| RMPenilaianPreOperasi | rekammedis | 6 | 8 | penilaian_pre_operasi hapus, penilaian_pre_operasi tambah, penilaian_pre_operasi ubah |
| RMSignInSebelumAnastesi | rekammedis | 6 | 8 | signin_sebelum_anestesi hapus, signin_sebelum_anestesi tambah, signin_sebelum_anestesi ubah |
| RMSignOutSebelumMenutupLuka | rekammedis | 6 | 8 | signout_sebelum_menutup_luka hapus, signout_sebelum_menutup_luka tambah, signout_sebelum_menutup_luka ubah |
| RMTimeOutSebelumInsisi | rekammedis | 6 | 8 | timeout_sebelum_insisi hapus, timeout_sebelum_insisi tambah, timeout_sebelum_insisi ubah |
| RMTransferPasienAntarRuang | rekammedis | 6 | 8 | antripersetujuantransferantarruang hapus, antripersetujuantransferantarruang tambah, bukti_persetujuan_transfer_pasien_antar_ruang hapus, transfer_pasien_antar_ruang hapus, transfer_pasien_antar_ruang tambah (+1) |
| DlgPeriksaRadiologi | simrskhanza | 6 | 7 | gambar_radiologi tambah, order_out ubah |
| DlgPermintaanKonsultasiMedik | permintaan | 6 | 7 | jawaban_konsultasi_medik tambah, konsultasi_medik hapus, konsultasi_medik tambah, konsultasi_medik ubah |
| DlgPermintaanKonsultasiPerawat | permintaan | 6 | 7 | jawaban_konsultasi_perawat tambah, konsultasi_perawat hapus, konsultasi_perawat tambah, konsultasi_perawat ubah |
| RMAdmisiSkoringTOLAC | rekammedis | 6 | 7 | admisi_skoring_tolac hapus, admisi_skoring_tolac tambah, admisi_skoring_tolac ubah |
| RMCatatanADIMEGizi | rekammedis | 6 | 7 | catatan_adime_gizi hapus, catatan_adime_gizi tambah, catatan_adime_gizi ubah |
| RMCatatanPersalinan | rekammedis | 6 | 7 | catatan_persalinan hapus, catatan_persalinan tambah, catatan_persalinan ubah |
| RMChecklistKesiapanAnestesi | rekammedis | 6 | 7 | checklist_kesiapan_anestesi hapus, checklist_kesiapan_anestesi tambah, checklist_kesiapan_anestesi ubah |
| RMChecklistPemberianFibrinolitik | rekammedis | 6 | 7 | checklist_pemberian_fibrinolitik hapus, checklist_pemberian_fibrinolitik tambah, checklist_pemberian_fibrinolitik ubah |
| RMDataAsuhanGizi | rekammedis | 6 | 7 | asuhan_gizi hapus, asuhan_gizi tambah, asuhan_gizi ubah |
| RMHasilPemeriksaanEcho | rekammedis | 6 | 7 | hasil_pemeriksaan_echo_gambar hapus, hasil_pemeriksaan_echo hapus, hasil_pemeriksaan_echo tambah, hasil_pemeriksaan_echo ubah |
| RMHasilPemeriksaanEchoPediatrik | rekammedis | 6 | 7 | hasil_pemeriksaan_echo_pediatrik_gambar hapus, hasil_pemeriksaan_echo_pediatrik hapus, hasil_pemeriksaan_echo_pediatrik tambah, hasil_pemeriksaan_echo_pediatrik ubah |
| RMHasilPemeriksaanEKG | rekammedis | 6 | 7 | hasil_pemeriksaan_ekg_gambar hapus, hasil_pemeriksaan_ekg hapus, hasil_pemeriksaan_ekg tambah, hasil_pemeriksaan_ekg ubah |
| RMHasilPemeriksaanOCT | rekammedis | 6 | 7 | hasil_pemeriksaan_oct_gambar hapus, hasil_pemeriksaan_oct hapus, hasil_pemeriksaan_oct tambah, hasil_pemeriksaan_oct ubah |
| RMHasilPemeriksaanSlitLamp | rekammedis | 6 | 7 | hasil_pemeriksaan_slit_lamp_gambar hapus, hasil_pemeriksaan_slit_lamp hapus, hasil_pemeriksaan_slit_lamp tambah, hasil_pemeriksaan_slit_lamp ubah |
| RMHasilPemeriksaanTreadmill | rekammedis | 6 | 7 | hasil_pemeriksaan_treadmill_gambar hapus, hasil_pemeriksaan_treadmill hapus, hasil_pemeriksaan_treadmill tambah, hasil_pemeriksaan_treadmill ubah |
| RMHasilPersalinanTOLAC | rekammedis | 6 | 7 | hasil_persalinan_tolac hapus, hasil_persalinan_tolac tambah, hasil_persalinan_tolac ubah |
| RMLaporanTindakan | rekammedis | 6 | 7 | laporan_tindakan hapus, laporan_tindakan tambah, laporan_tindakan ubah |
| RMPelaksanaanInformasiEdukasi | rekammedis | 6 | 7 | antripelaksanaanedukasi hapus, antripelaksanaanedukasi tambah, bukti_pelaksanaan_informasi_edukasi hapus, pelaksanaan_informasi_edukasi hapus, pelaksanaan_informasi_edukasi tambah (+1) |
| RMPengkajianRestrain | rekammedis | 6 | 7 | antripengkajianrestrain hapus, antripengkajianrestrain tambah, pengkajian_restrain_yang_menyetujui hapus, pengkajian_restrain hapus, pengkajian_restrain tambah (+1) |
| RMPenilaianAwalMedisHemodialisa | rekammedis | 6 | 7 | penilaian_medis_hemodialisa hapus, penilaian_medis_hemodialisa tambah, penilaian_medis_hemodialisa ubah |
| RMPenilaianBayiBaruLahir | rekammedis | 6 | 7 | penilaian_bayi_baru_lahir hapus, penilaian_bayi_baru_lahir tambah, penilaian_bayi_baru_lahir ubah, riwayat_persalinan_pasien hapus, riwayat_persalinan_pasien tambah |
| RMPenilaianBishop | rekammedis | 6 | 7 | penilaian_bishop_scor hapus, penilaian_bishop_scor tambah, penilaian_bishop_scor ubah |
| RMPenilaianDerajatDehidrasi | rekammedis | 6 | 7 | penilaian_dehidrasi hapus, penilaian_dehidrasi tambah, penilaian_dehidrasi ubah |
| RMPenilaianKorbanKekerasan | rekammedis | 6 | 7 | penilaian_korban_kekerasan hapus, penilaian_korban_kekerasan tambah, penilaian_korban_kekerasan ubah |
| RMPenilaianPasienImunitasRendah | rekammedis | 6 | 7 | penilaian_pasien_imunitas_rendah hapus, penilaian_pasien_imunitas_rendah tambah, penilaian_pasien_imunitas_rendah ubah |
| RMPenilaianPasienPenyakitMenular | rekammedis | 6 | 7 | penilaian_pasien_penyakit_menular hapus, penilaian_pasien_penyakit_menular tambah, penilaian_pasien_penyakit_menular ubah |
| RMPenilaianPasienTerminal | rekammedis | 6 | 7 | penilaian_pasien_terminal hapus, penilaian_pasien_terminal tambah, penilaian_pasien_terminal ubah |
| RMPenilaianPsikologi | rekammedis | 6 | 7 | penilaian_psikologi hapus, penilaian_psikologi tambah, penilaian_psikologi ubah |
| RMPenilaianPsikologiKlinis | rekammedis | 6 | 7 | penilaian_psikologi_klinis hapus, penilaian_psikologi_klinis tambah, penilaian_psikologi_klinis ubah |
| RMPenilaianTambahanBunuhDiri | rekammedis | 6 | 7 | penilaian_tambahan_bunuh_diri hapus, penilaian_tambahan_bunuh_diri tambah, penilaian_tambahan_bunuh_diri ubah |
| RMPenilaianTambahanGeriatri | rekammedis | 6 | 7 | penilaian_tambahan_geriatri hapus, penilaian_tambahan_geriatri tambah, penilaian_tambahan_geriatri ubah |
| RMPenilaianTambahanMelarikanDiri | rekammedis | 6 | 7 | penilaian_tambahan_beresiko_melarikan_diri hapus, penilaian_tambahan_beresiko_melarikan_diri tambah, penilaian_tambahan_beresiko_melarikan_diri ubah |
| RMPenilaianTambahanPerilakuKekerasan | rekammedis | 6 | 7 | penilaian_tambahan_perilaku_kekerasan hapus, penilaian_tambahan_perilaku_kekerasan tambah, penilaian_tambahan_perilaku_kekerasan ubah |
| RMPenilaianUlangNyeri | rekammedis | 6 | 7 | penilaian_ulang_nyeri hapus, penilaian_ulang_nyeri tambah, penilaian_ulang_nyeri ubah |
| RMReaksiTransfusi | rekammedis | 6 | 7 | laporan_reaksi_transfusi hapus, laporan_reaksi_transfusi tambah, laporan_reaksi_transfusi ubah |
| SuratKontrol | surat | 6 | 7 | booking_registrasi hapus, booking_registrasi tambah, booking_registrasi ubah, skdp_bpjs hapus, skdp_bpjs tambah (+1) |
| RMRekonsiliasiObat | rekammedis | 6 | 6 | rekonsiliasi_obat_detail_obat tambah, rekonsiliasi_obat tambah |
| DlgKabupaten | simrskhanza | 3 | 30 | kabupaten hapus |
| DlgKelurahan | simrskhanza | 3 | 30 | kelurahan hapus |
| DlgKecamatan | simrskhanza | 3 | 29 | kecamatan hapus |
| DlgResepObat | inventory | 5 | 9 | obat_racikan ubah |
| RMChecklistPostOperasi | rekammedis | 5 | 7 | checklist_post_operasi hapus, checklist_post_operasi tambah, checklist_post_operasi ubah |
| RMHasilEndoskopiFaringLaring | rekammedis | 5 | 6 | hasil_endoskopi_faring_laring_gambar hapus, hasil_endoskopi_faring_laring hapus, hasil_endoskopi_faring_laring tambah, hasil_endoskopi_faring_laring ubah |

## Jurnal atau riwayat stok belum terlihat

Jendela yang sebagian tulisannya ada, memposting jurnal atau riwayat stok di Java, tetapi berkas Gramedika penulisnya tidak
melakukannya. Perlu diperiksa satu per satu; 15 jendela.

| Jendela | Modul | Tanda | Alur inti |
|---|---:|---:|---:|
| TokoPemesanan | toko | jurnal, riwayat stok | 0 |
| TokoCariPembelian | toko | jurnal, riwayat stok | 0 |
| TokoCariPemesanan | toko | jurnal, riwayat stok | 0 |
| TokoCariPenjualan | toko | jurnal, riwayat stok | 0 |
| TokoCariPiutang | toko | jurnal, riwayat stok | 0 |
| TokoCariReturBeli | toko | jurnal, riwayat stok | 0 |
| TokoCariReturJual | toko | jurnal, riwayat stok | 0 |
| TokoCariReturPiutang | toko | jurnal, riwayat stok | 0 |
| TokoInputStok | toko | riwayat stok | 0 |
| TokoPembelian | toko | jurnal, riwayat stok | 0 |
| TokoPenjualan | toko | jurnal, riwayat stok | 0 |
| TokoPiutang | toko | jurnal, riwayat stok | 0 |
| TokoReturBeli | toko | jurnal, riwayat stok | 0 |
| TokoReturJual | toko | jurnal, riwayat stok | 0 |
| TokoReturPiutang | toko | jurnal, riwayat stok | 0 |

## Tabel yang sama sekali belum pernah ditulis

526 tabel. Tiga puluh yang paling banyak dipakai jendela Java: `pembayaran_pihak_ke3_bankmandiri` (15), `utd_stok_medis` (7), `utd_stok_penunjang` (7), `dapurpemesanan` (6), `inventaris_pemesanan` (6), `ipsrspemesanan` (6), `riwayat_persalinan_pasien` (6), `inventaris` (5), `pcare_obat_diberikan` (5), `penilaian_awal_keperawatan_kebidanan_ranap` (5), `penilaian_medis_ralan_anak` (5), `resep_luar` (5), `returpasien` (5), `tagihan_obat_langsung` (5), `asuhan_gizi` (4), `bayar_detail_periksa_lab` (4), `bayar_detail_periksa_lab_perujuk` (4), `bayar_operasi_dokter_anak` (4), `bayar_operasi_dokter_anestesi` (4), `bayar_operasi_dokter_pjanak` (4), `bayar_operasi_dokter_umum` (4), `bayar_operasi_operator1` (4), `bayar_operasi_operator2` (4), `bayar_operasi_operator3` (4), `bayar_periksa_lab` (4), `bayar_periksa_lab_perujuk` (4), `bayar_periksa_radiologi` (4), `bayar_periksa_radiologi_perujuk` (4), `bayar_rawat_inap_dr` (4), `bayar_rawat_inap_drpr` (4).

## Batas analisis

- Kode Data yang tidak dirujuk antarmuka tidak dihitung (3 berkas: `DokterRepository.cs`, `PenunjangRepository.cs`, `RawatJalanRepository.cs`).
- 4 panggilan simpan/ubah/hapus di jendela Java memakai nama tabel dari variabel dan tidak terbaca.
- Layar Java yang memposting jurnal atau riwayat stok dan hanya tersentuh editor master generik dinilai "hanya baca".
  Angka pasangan dan tabel tidak dikoreksi dengan aturan ini.
- Penulisan Gramedika yang nama tabelnya dirakit saat jalan atau lewat EF Core didaftar tangan di skrip.
- Perintah "kosongkan tabel" Menu U bisa menghapus tabel apa pun dan sengaja tidak dihitung.
- Logika di kelas Java yang bukan jendela (misalnya pembantu jurnal dan stok) tidak dihitung ke jendela pemanggilnya.
- Cetakan Jasper, validasi, hak akses, bridging dan pengaturan tidak dibandingkan.
