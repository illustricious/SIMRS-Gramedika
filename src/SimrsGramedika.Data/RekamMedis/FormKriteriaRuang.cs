namespace SimrsGramedika.Data.RekamMedis;

/// <summary>
/// Check list kriteria masuk dan keluar HCU, ICU, NICU, PICU dan ruang isolasi, dari RMChecklistKriteria*.java.
/// Label, urutan, pilihan dan isian awal diambil apa adanya dari layar Java.
/// </summary>
internal static class FormKriteriaRuang
{
    private static readonly string[] YaTidak = ["Ya", "Tidak"];

    private static IsianKlinis Cek(string kolom, string label, string awal, string? bagian = null) =>
        new(kolom, label, Pilihan: YaTidak, Bagian: bagian, Awal: awal);

    private static FormKlinis Buat(string tile, string judul, string tabel, string izin, IsianKlinis[] isian) =>
        new(tile, judul, tabel, izin, PemilikForm.Pegawai, isian);

    private static readonly FormKlinis MasukHCU = Buat(
        "btnChecklistKriteriaMasukHCU", "Check List Kriteria Masuk HCU", "checklist_kriteria_masuk_hcu", "checklist_kriteria_masuk_hcu",
        [
            Cek("kardiologi1", "Gangguan Sirkulasi Atau Pre Dan Pasca Operasi (Syok Hypovolemic)", "Tidak", "I. SISTEM KARDIOLOGI"),
            Cek("kardiologi2", "Hypertensi Emergency", "Tidak"),
            Cek("kardiologi3", "HR 60x/menit (Tiidak Stabil Hasil EKG Gambaran Mengancam Nyawa)", "Tidak"),
            Cek("kardiologi4", "Gagal Jantung Acute", "Tidak"),
            Cek("kardiologi5", "Menggunakan Inotropik / Vasoaktif Gent", "Tidak"),
            Cek("kardiologi6", "MAP < 60 mmHg", "Tidak"),
            Cek("pernapasan1", "R <8 x/ menit > 25 x/menit (Adanya Gangguan Pada Ventilasi : Hypoxia / Hypercapnia / Sumbatan Jalan Nafas / Oedema Paru Acute)", "Tidak", "II. SISTEM PERNAFASAN"),
            Cek("pernapasan2", "Trauma Thorax / Peumothorax", "Tidak"),
            Cek("pernapasan3", "Oxigenisasi Tidak Cukup Dari Hasil AGD", "Tidak"),
            Cek("syaraf1", "Kesadaran Dengan GCS >= 7", "Tidak", "III. SISTEM SYARAF"),
            Cek("syaraf2", "Temperatur <35 C / >38 C", "Tidak"),
            Cek("syaraf3", "Trauma Kepala Sedang - Berat", "Tidak"),
            Cek("syaraf4", "Kejang Yang Tidak Memerlukan Ventilator / Cerebro Vasculer / Neoromusculer / Infeksi Syaraf", "Tidak"),
            Cek("pencernaan1", "Gangguan Elektrolit (Na, Ca,CI, Mg, Cal) & Asam Basa", "Tidak", "IV. SISTEM PENCERNAAN DAN ENDOKRIN"),
            Cek("pencernaan2", "Hypeglikemia & Hypoglikemia, Ketoasidosis Metabolic", "Tidak"),
            Cek("pencernaan3", "Pendarahan Saluran Pencernaan Tanpa Hypotensi & Repon Dengan Cairan", "Tidak"),
            Cek("pencernaan4", "Pengobatan Keracunan", "Tidak"),
            Cek("pembedahan1", "Penyulit Pasca Pembedahan : Digestif / Orthopedi / Urologi / Vasculer / Plastik / Kebidanan (Eklamsia Pre Operasi & Pasca Bedah) Dll", "Tidak", "V. PEMBEDAHAN"),
            Cek("pembedahan2", "Pasca Pembedahan Hemodinamik Stabil Tetapi Masih Perlu Resusitasi Cairan", "Tidak"),
            Cek("hematologi1", "Gangguan Imunologi (Reaksi Alergi, Steven Jhonson) dll", "Tidak", "VI. GANGGUAN HEMATOLOGI"),
            Cek("hematologi2", "DIC, Anemia Berat, Reaksi Penolakan Transfusi Darah", "Tidak"),
            Cek("infeksi", "Semua Infeksi Yang Menyebabkan Penurunan Kesadaran & Tidak Memerlukan Ventilator : DBD Thrombositopenia, Sepsis, Tetanus, Dll", "Tidak", "VII. PENYAKIT INFEKSI"),
        ]);

    private static readonly FormKlinis KeluarHCU = Buat(
        "btnChecklistKriteriaKeluarHCU", "Check List Kriteria Keluar HCU", "checklist_kriteria_keluar_hcu", "checklist_kriteria_keluar_hcu",
        [
            Cek("kriteria1", "Pasien Yang Sudah Tidak Memerlukan Perawatan Di Ruang HCU", "Ya"),
            Cek("kriteria2", "Hypertensi Emergency Teratasi / Normal", "Ya"),
            Cek("kriteria3", "HR 60 - 100 x/menit (Hasil EKG Menunjukkan Perbaikan / Sinus Rithme)", "Ya"),
            Cek("kriteria4", "Kesadaran Dengan GCS >= 7", "Ya"),
            Cek("kriteria5", "Gangguan Inotropic / Vasoaktif Gent Teratasi / Normal", "Ya"),
            Cek("kriteria6", "MAP >= 60 mmHg", "Ya"),
            Cek("kriteria7", "RR 12 - 20x/menit", "Ya"),
            Cek("kriteria8", "Gangguan Elektrolit (Na, Ca, CI, Mg, Cal) & Asam Basa Normal", "Ya"),
            Cek("kriteria9", "Perdarahan Saluran Pencernaan Teratasi", "Ya"),
            Cek("kriteria10", "Temperature 35 - 38°C", "Ya"),
            Cek("kriteria11", "Gagal Jantung Akut Teratasi", "Ya"),
            Cek("kriteria12", "Gangguan Sirkulasi Teratasi", "Ya"),
        ]);

    private static readonly FormKlinis MasukICU = Buat(
        "btnChecklistKriteriaMasukICU", "Check List Kriteria Masuk ICU", "checklist_kriteria_masuk_icu", "checklist_kriteria_masuk_icu",
        [
            Cek("prioritas1_1", "Pasca Operasi Dengan Gangguan Nafas Atau Hipotensi", "Tidak", "I. PRIORITAS 1"),
            Cek("prioritas1_2", "Gagal Nafas", "Tidak"),
            Cek("prioritas1_3", "Gagal Jantung Dengan Tanda Bendungan Paru", "Tidak"),
            Cek("prioritas1_4", "Gangguan Asam Basa / Elektrolit", "Tidak"),
            Cek("prioritas1_5", "Gagal Ginjal Dengan Tanda Bendungan Paru", "Tidak"),
            Cek("prioritas1_6", "Syok Karena Perdarahan Anafilaksis", "Tidak"),
            Cek("prioritas2_1", "Pasca Operasi Besar", "Tidak", "II. PRIORITAS 2"),
            Cek("prioritas2_2", "Kejang Berulang", "Tidak"),
            Cek("prioritas2_3", "Gangguan Kesadaran", "Tidak"),
            Cek("prioritas2_4", "Dehidrasi Berat", "Tidak"),
            Cek("prioritas2_5", "Gangguan Jalan Nafas", "Tidak"),
            Cek("prioritas2_6", "Arimia Jantung", "Tidak"),
            Cek("prioritas2_7", "Asma Akut Berat", "Tidak"),
            Cek("prioritas2_8", "Diabetes Yang Memerlukan Terapi Insulin Kontinyu", "Tidak"),
            Cek("prioritas3_1", "Penyakit Keganasan Dengan Metastasis", "Tidak", "III. PRIORITAS 3"),
            Cek("prioritas3_2", "Pasien Geriatrik Dengan Fungsi Hidup Sebelumnya Minimal", "Tidak"),
            Cek("prioritas3_3", "Pasien Dengan GCS 3", "Tidak"),
            Cek("prioritas3_4", "Pasien Jantung, Penyakit Paru Terminal Disertai Komplikasi Penyakit Akut Berat", "Tidak"),
            Cek("kriteria_fisiologis_tanda_vital_1", "Nadi < 40 atau >150 (x/menit)", "Tidak", "IV. KRITERIA FISIOLOGIS TANDA-TANDA VITAL"),
            Cek("kriteria_fisiologis_tanda_vital_2", "SBP < 80 mmHg Atau 20 mmHg Di Bawah SBP Pasien", "Tidak"),
            Cek("kriteria_fisiologis_tanda_vital_3", "MAP < 60 mmHg", "Tidak"),
            Cek("kriteria_fisiologis_tanda_vital_4", "DBP > 120 mmHg", "Tidak"),
            Cek("kriteria_fisiologis_tanda_vital_5", "R > 35 x/menit", "Tidak"),
            Cek("kriteria_fisiologis_laborat_1", "Na < 110 meq/L Atau > 170 meq/L", "Tidak", "V. KRITERIA FISIOLOGIS LABORATORIUM"),
            Cek("kriteria_fisiologis_laborat_2", "Ca > 15 mg/dl", "Tidak"),
            Cek("kriteria_fisiologis_laborat_3", "GDS > 800 mg/dl", "Tidak"),
            Cek("kriteria_fisiologis_laborat_4", "K < 2 meq/L Atau 7meq/L", "Tidak"),
            Cek("kriteria_fisiologis_laborat_5", "PaO2 < 50 mmHg", "Tidak"),
            Cek("kriteria_fisiologis_laborat_6", "PH < 7,1 Atau 7,7", "Tidak"),
            Cek("kriteria_fisiologis_radiologi_1", "Perbedaan Cerebrovaskuler, SAH, Atau Contusion Dengan Gangguan Kesadaran Atau Neorologi", "Tidak", "VI. KRITERIA FISIOLOGIS RADIOLOGI"),
            Cek("kriteria_fisiologis_radiologi_2", "Ruptor Organ Dalam, Kandung Kemih, Hati, Varices Esophagus Atau Uterus Dengan Gangguan Hemodinamik", "Tidak"),
            Cek("kriteria_fisiologis_klinis_1", "Pupil Anisokor", "Tidak", "VII. KRITERIA FISILOGIS KLINIS"),
            Cek("kriteria_fisiologis_klinis_2", "Obstruksi Jalan Nafas", "Tidak"),
            Cek("kriteria_fisiologis_klinis_3", "Anuria", "Tidak"),
            Cek("kriteria_fisiologis_klinis_4", "Kejang Berulang", "Tidak"),
            Cek("kriteria_fisiologis_klinis_5", "Tamponade Jantung", "Tidak"),
            Cek("kriteria_fisiologis_klinis_6", "Coma", "Tidak"),
            Cek("kriteria_fisiologis_klinis_7", "Sianosis", "Tidak"),
            Cek("kriteria_fisiologis_klinis_8", "Luka Bakar > 10 % BSA", "Tidak"),
        ]);

    private static readonly FormKlinis KeluarICU = Buat(
        "btnChecklistKriteriaKeluarICU", "Check List Kriteria Keluar ICU", "checklist_kriteria_keluar_icu", "checklist_kriteria_keluar_icu",
        [
            Cek("kriteria1", "Tidak Lagi Memerlukan Perawatan ICU", "Ya"),
            Cek("kriteria2", "Kondisi Pasien Yang Menyebabkan Pasien Dirawat Di ICU Telah Teratasi", "Ya"),
            Cek("kriteria3", "Pasien Dalam Keadaan Terminal Yang Dengan Terapi Tidak Memberikan Hasil Yang Bermakna", "Ya"),
            Cek("kriteria4", "Pasien Meninggal", "Ya"),
            Cek("kriteria5", "Pasien Membutuhkan Alat / Prosedur Yang Tidak Tersedia", "Ya"),
            Cek("kriteria6", "Keluarga Menghendaki Pasien Dipindahkan", "Ya"),
            Cek("kriteria7", "Nadi > 60 Atau < 100 x/menit", "Ya"),
            Cek("kriteria8", "SBP > 08 mmHg Atau 20 mmHg Di Atas SBP Pasien", "Ya"),
            Cek("kriteria9", "MAP > 60 mmHg", "Ya"),
            Cek("kriteria10", "DBP < 120 mmHg", "Ya"),
            Cek("kriteria11", "RR > 12 Sampai < 25 x/menit", "Ya"),
        ]);

    private static readonly FormKlinis MasukNICU = Buat(
        "btnChecklistKriteriaMasukNICU", "Check List Kriteria Masuk NICU", "checklist_kriteria_masuk_nicu", "kriteria_masuk_nicu",
        [
            Cek("respirasi1", "Distres Napas Berat (Grunting, Retraksi, Takipnea > 70x/menit)", "Tidak", "I. RESPIRASI"),
            Cek("respirasi2", "Apnea Berulang/Apnea Dengan Bradikardia", "Tidak"),
            Cek("respirasi3", "Kebutuhan CPAP Atau Ventilasi Mekanik", "Tidak"),
            Cek("respirasi4", "Saturasi O₂ < 90% Dengan Oksigen Suplementasi", "Tidak"),
            Cek("prematur1", "Berat Lahir < 2000 g (Khususnya < 1500 g : VLBW)", "Tidak", "II. PREMATURITAS & BERAT BADAN"),
            Cek("prematur2", "Usia Kehamilan < 35 minggu (Khususnya < 32 minggu)", "Tidak"),
            Cek("prematur3", "Hipotermia (< 36°C) Yang Tidak Membaik Dengan Penghangatan Biasa", "Tidak"),
            Cek("kardio1", "Syok Refrakter Resusitasi Awal", "Tidak", "III. KONDISI KARDIOVASKULAR"),
            Cek("kardio2", "Bradikardia Berat (< 80x/menit)", "Tidak"),
            Cek("kardio3", "Sianosis Sentral Menetap", "Tidak"),
            Cek("neuro1", "Kejang", "Tidak", "IV. NEUROLOGI"),
            Cek("neuro2", "GCS/Kesadaran Bayi Sangat Rendah", "Tidak"),
            Cek("neuro3", "Ensefalopati Hipoksik Iskemik Sedang–Berat", "Tidak"),
            Cek("metabolik1", "Hipoglikemia (< 40 mg/dL) Atau Hiperglikemia Berat", "Tidak", "V. METABOLIK & INFEKSI"),
            Cek("metabolik2", "Dugaan Atau Terkonfirmasi Sepsis Neonatal", "Tidak"),
            Cek("metabolik3", "Asidosis Metabolik Berat (pH < 7,2)", "Tidak"),
            Cek("kondisilain1", "Kelainan Kongenital Berat Memerlukan Stabilisasi Intensif", "Tidak", "VI. KONDISI LAIN"),
            Cek("kondisilain2", "Dari Ibu Dengan Riwayat Komplikasi Perinatal Berat", "Tidak"),
            Cek("kondisilain3", "Pasca Operasi Besar Neonatus", "Tidak"),
            Cek("kondisilain4", "Ikterus Berat Memerlukan Fototerapi Intensif Atau Transfusi Tukar", "Tidak"),
            new("keputusan", "Keputusan", Pilihan: ["Diterima Di NICU", "Tidak Diterima - Dirawat Di Ruang Perawatan Bayi Lain"], Bagian: "VII. KEPUTUSAN & KETERANGAN"),
            new("keterangan", "Keterangan/Catatan", 50),
        ]);

    private static readonly FormKlinis KeluarNICU = Buat(
        "btnChecklistKriteriaKeluarNICU", "Check List Kriteria Keluar NICU", "checklist_kriteria_keluar_nicu", "kriteria_keluar_nicu",
        [
            Cek("respirasi1", "Tidak Memerlukan Ventilasi Mekanik Atau CPAP", "Tidak", "I. RESPIRASI"),
            Cek("respirasi2", "Saturasi O₂ ≥ 90% Tanpa Oksigen Tambahan", "Tidak"),
            Cek("respirasi3", "Tidak Ada Apnea Atau Bradikardia Dalam 5–7 Hari Terakhir", "Tidak"),
            Cek("kardio1", "Tekanan Darah Stabil Sesuai Usia", "Tidak", "II. KARDIOVASKULAR"),
            Cek("kardio2", "Tidak Ada Episode Syok Dalam 5 Hari Terakhir", "Tidak"),
            Cek("nutrisi1", "Asupan Oral Penuh (ASI/PMK) Tanpa Distress", "Tidak", "III. NUTRISI"),
            Cek("nutrisi2", "Tidak Memerlukan Nutrisi Parenteral", "Tidak"),
            Cek("nutrisi3", "Berat Badan Stabil/Meningkat ≥ 15 g/Kg/Hari", "Tidak"),
            Cek("suhutubuh1", "Suhu Stabil (36,5–37,5°C) Di Ruang/Incubator Terbuka", "Tidak", "VI. SUHU TUBUH"),
            Cek("suhutubuh2", "Tidak Ada Episode Hipotermia/Hipertermia 3 Hari Terakhir", "Tidak"),
            Cek("infeksi1", "Tidak Memerlukan Monitoring Invasif", "Tidak", "V. INFEKSI & MONITORING"),
            Cek("infeksi2", "Semua Terapi/Tindakan Invasif Telah Selesai", "Tidak"),
            Cek("infeksi3", "Tidak Ada Tanda Infeksi Aktif", "Tidak"),
            new("keputusan", "Keputusan", Pilihan: ["Layak Dipindahkan Ke Ruang Rawat Bayi/Rawat Gabung", "Layak Pulang", "Tetap Di NICU"], Bagian: "VI. KEPUTUSAN & KETERANGAN"),
            new("keterangan", "Keterangan/Catatan", 50),
        ]);

    private static readonly FormKlinis MasukPICU = Buat(
        "btnChecklistKriteriaMasukPICU", "Check List Kriteria Masuk PICU", "checklist_kriteria_masuk_picu", "kriteria_masuk_picu",
        [
            Cek("kriteriaumum1", "Membutuhkan Monitoring & Terapi Intensif Secara Berkelanjutan", "Tidak", "I. KRITERIA UMUM"),
            Cek("kriteriaumum2", "Membutuhkan Dukungan ≥ 1 Organ Vital", "Tidak"),
            Cek("kriteriaumum3", "Pasien Dengan Kondisi Mengancam Jiwa Yang Masih Berpotensi Reversibel", "Tidak"),
            Cek("respirasi1", "Gagal Napas Akut (Misal: ARDS, Status Asmatik)", "Tidak", "II. KRITERIA KHUSUS · 1. Respirasi"),
            Cek("respirasi2", "Hipoksemia Berat (PaO₂ < 60 mmHg Dengan FiO₂ > 0,6)", "Tidak"),
            Cek("respirasi3", "Butuh Ventilasi Mekanik Invasif/Non-invasif", "Tidak"),
            Cek("respirasi4", "Hiperkarbia Berat (PaCO₂ > 60 mmHg Dengan pH < 7,25)", "Tidak"),
            Cek("kardio1", "Syok Refrakter (Septik, Kardiogenik, Hipovolemik, Anafilaksis)", "Tidak", "II. KRITERIA KHUSUS · 2. Kardiovaskular"),
            Cek("kardio2", "Gagal Jantung Berat", "Tidak"),
            Cek("kardio3", "Gangguan Irama Jantung Yang Mengancam Nyawa", "Tidak"),
            Cek("kardio4", "Pasca Resusitasi Jantung Paru", "Tidak"),
            Cek("neuro1", "Trauma Kepala Berat Dengan Gangguan Hemodinamik/Napas", "Tidak", "II. KRITERIA KHUSUS · 3. Neurologis"),
            Cek("neuro2", "Kejang Berulang / Status Epileptikus", "Tidak"),
            Cek("neuro3", "Penurunan Kesadaran (GCS ≤ 8 Atau Koma)", "Tidak"),
            Cek("neuro4", "Edema Serebri, Perdarahan Intrakranial", "Tidak"),
            Cek("bedah1", "Pasca Operasi Mayor Dengan Risiko Komplikasi Tinggi", "Tidak", "II. KRITERIA KHUSUS · 4. Bedah / Pasca Operasi"),
            Cek("bedah2", "Pasca Transplantasi Organ", "Tidak"),
            Cek("bedah3", "Pasca Operasi Jantung / Thoraks Kompleks", "Tidak"),
            Cek("kondisilain1", "Gangguan Metabolik / Elektrolit Yang Mengancam Jiwa", "Tidak", "II. KRITERIA KHUSUS · 5. Lain-lain"),
            Cek("kondisilain2", "Intoksikasi Berat", "Tidak"),
            Cek("kondisilain3", "Sepsis Berat Dengan Disfungsi Organ Multipel", "Tidak"),
            new("keputusan", "Keputusan", Pilihan: ["Diterima Di PICU", "Tidak Diterima - Dirawat Di Ruang Lain"], Bagian: "III. KEPUTUSAN & KETERANGAN"),
            new("keterangan", "Keterangan/Catatan", 50),
        ]);

    private static readonly FormKlinis KeluarPICU = Buat(
        "btnChecklistKriteriaKeluarPICU", "Check List Kriteria Keluar PICU", "checklist_kriteria_keluar_picu", "kriteria_keluar_picu",
        [
            Cek("kondisiklinis1", "Tidak Ada Tanda Gagal Napas Akut", "Tidak", "I. KONDISI KLINIS"),
            Cek("kondisiklinis2", "Saturasi O₂ Stabil Tanpa Ventilator Mekanik Atau O₂ Nasal < 2 L/menit", "Tidak"),
            Cek("kondisiklinis3", "Status Kesadaran Stabil (Sesuai Baseline, GCS ≥ 13/Tidak Ada Penurunan Akut)", "Tidak"),
            Cek("kondisiklinis4", "Tidak Ada Perdarahan Aktif/Syok", "Tidak"),
            Cek("kondisiklinis5", "Tidak Membutuhkan Vasopressor / Inotropik", "Tidak"),
            Cek("kondisiklinis6", "Tanda Vital Stabil (HR, RR, Nadi, TD, Suhu)", "Tidak"),
            Cek("kebutuhanperawatan1", "Tidak Lagi Membutuhkan Monitoring Invasif", "Tidak", "II. KEBUTUHAN PERAWATAN"),
            Cek("kebutuhanperawatan2", "Tidak Membutuhkan Terapi Intensif Berkelanjutan", "Tidak"),
            Cek("kebutuhanperawatan3", "Nyeri Terkontrol", "Tidak"),
            Cek("kebutuhanperawatan4", "Kebutuhan Cairan & Nutrisi Dapat Dipenuhi Secara Oral / Enteral / IV Standar", "Tidak"),
            Cek("tindaklanjut1", "Rencana Kontrol/Tindakan Lanjutan Tercatat", "Tidak", "III. RENCANA TINDAK LANJUT"),
            Cek("tindaklanjut2", "Orang Tua/Wali Mendapat Edukasi Kondisi & Rencana Perawatan", "Tidak"),
            Cek("tindaklanjut3", "Konsultasi Dengan Tim Terkait Sudah Dilakukan", "Tidak"),
            Cek("tindaklanjut4", "Rencana Terapi Jelas Untuk Rawat Ruang Biasa", "Tidak"),
            new("keputusan", "Keputusan", Pilihan: ["Layak Keluar Dari PICU/Pindah Ke Ruang Rawat Biasa", "Tidak Layak Keluar/Tetap Dirawat Di PICU"], Bagian: "IV. KEPUTUSAN & KETERANGAN"),
            new("keterangan", "Keterangan/Catatan", 50),
        ]);

    private static readonly FormKlinis MasukIsolasi = Buat(
        "btnChecklistKriteriaMasukIsolasi", "Check List Kriteria Masuk Isolasi", "checklist_kriteria_masuk_isolasi", "checklist_kriteria_masuk_isolasi",
        [
            Cek("airborne_tb", "TB Paru Aktif/Suspek", "Tidak", "I. AIRBONE DISEASE"),
            Cek("airborne_campak", "Campak", "Tidak"),
            Cek("airborne_zoster_diseminata", "Herpes Zoster Diseminata", "Tidak"),
            Cek("airborne_varisela", "Varisela", "Tidak"),
            Cek("airborne_lainnya", "Airbone Lainnya", "Tidak"),
            Cek("droplet_covid19", "COVID-19", "Tidak", "II. DROPLET DISEASE"),
            Cek("droplet_pertusis", "Pertusis", "Tidak"),
            Cek("droplet_meningitis", "Meningitis", "Tidak"),
            Cek("droplet_difteri", "Difteri", "Tidak"),
            Cek("droplet_influenza", "Influenza", "Tidak"),
            Cek("droplet_lainnya", "Droplet Lainnya", "Tidak"),
            Cek("kontak_mdro", "MDRO (MRSA/VRE/CRE/ESBL)", "Tidak", "III. KONTAK"),
            Cek("kontak_luka_drainase", "Luka Dengan Drainase", "Tidak"),
            Cek("kontak_scabies", "Skabies", "Tidak"),
            Cek("kontak_clostridium", "Clostridioides Difficile", "Tidak"),
            Cek("kontak_diare_infeksi", "Diare Infeksius", "Tidak"),
            Cek("kontak_lainnya", "Kontak Lainnya", "Tidak"),
            Cek("kontak_erat", "Kontak Erat", "Tidak", "IV. FAKTOR RISIKO"),
            Cek("riwayat_mdro", "Riwayat MDRO", "Tidak"),
            Cek("riwayat_perjalanan_wabah", "Riwayat Daerah Wabah", "Tidak"),
            Cek("hasil_lab_radiologi_positif", "Hasil Pemeriksaan Lab & Rontgen Positif Penyakit Infeksi Menular", "Tidak"),
            Cek("gejala_klinis_menular", "Gejala Klinis Infeksi Menular", "Tidak"),
            Cek("pasien_imunokompromis", "Pasien Imunokompromis Berat Butuh Isolasi Protektif", "Tidak"),
            Cek("instruksi_dpjp", "Intruksi Rawat Isolasi Dari DPJP", "Tidak", "V. KRITERIA ADMINISTRASI & PERSETUJUAN"),
            Cek("persetujuan_isolasi", "Informed Consent (Persetujuan Isolasi Oleh Keluarga/Pasien)", "Tidak"),
            Cek("kelengkapan_jaminan", "Kelengkapan Jaminan (BPJS/Asuransi/Umum)", "Tidak"),
            Cek("ketersediaan_apd", "Ketersediaan APD Lengkap Di Anteroom", "Tidak", "VI. KESIAPAN FASILITAS KAMAR ISOLASI/PARAMETER KAMAR ISOLASI"),
            Cek("fasilitas_cuci_tangan", "Fasilitas Cuci Tangan (Wastafel, Sabun, Handrub) Siap Pakai", "Tidak"),
            Cek("tekanan_negatif_berfungsi", "Sistem Tekanan Udara Negatif Berfungsi Baik", "Tidak"),
            Cek("pintu_otomatis_berfungsi", "Pintu Kamar Penutup Otomatis Bekerja Dengan Baik", "Tidak"),
            new("diagnosa_isolasi", "Diagnosa Isolasi", 100, Bagian: "VII. KEPUTUSAN & KETERANGAN"),
            new("indikasi_isolasi", "Keputusan", Pilihan: ["Masuk Di Ruangan Isolasi", "Tetap Di Ruangan Biasa"], Awal: "Tetap Di Ruangan Biasa"),
            new("jenis_isolasi", "Jenis Isolasi", Pilihan: ["Standar", "Kontak", "Droplet", "Airborne", "Kontak+Droplet", "Airborne+Kontak", "Protective"], Awal: "Kontak"),
            new("keterangan", "Keterangan/Catatan", 100),
        ]);

    private static readonly FormKlinis KeluarIsolasi = Buat(
        "btnChecklistKriteriaKeluarIsolasi", "Check List Kriteria Keluar Isolasi", "checklist_kriteria_keluar_isolasi", "checklist_kriteria_keluar_isolasi",
        [
            Cek("gejala_membaik", "Gejala Klinis Infeksi Telah Membaik / Hilang", "Ya", "I. CHECK LIST KRITERIA PASIEN KELUAR ISOLASI"),
            Cek("tidak_ada_indikasi_transmisi", "Tidak Ada Lagi Indikasi Kewaspadaan Transmisi", "Ya"),
            Cek("persetujuan_dpjp", "DPJP Menyetujui Penghentian Isolasi", "Ya"),
            Cek("kriteria_pedoman_terpenuhi", "Kriteria Penghentian Isolasi Sesuai Pedoman Penyakit Telah Terpenuhi", "Ya"),
            Cek("hasil_penunjang_memenuhi", "Hasil Pemeriksaan Penunjang Memenuhi Kriteria Penghentian Isolasi (Bila Diperlukan)", "Ya"),
            new("keputusan", "Keputusan", Pilihan: ["Keluar Isolasi", "Lanjut Isolasi"], Bagian: "II. KESIMPULAN/KEPUTUSAN"),
            new("alasan", "Alasan", 500),
        ]);

    public static IReadOnlyList<FormKlinis> Semua => [MasukHCU, KeluarHCU, MasukICU, KeluarICU, MasukNICU, KeluarNICU, MasukPICU, KeluarPICU, MasukIsolasi, KeluarIsolasi];
}
