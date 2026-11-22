namespace SimrsKhanza.Data.Master;

/// <summary>
/// First seventy-five remaining Menu M tiles: reference masters plus read-only patient and
/// clinical records that are entered through Khanza's visit workflows.
/// </summary>
public static class MenuMSpecs
{
    private static MasterField Key(string column, string label, int length) =>
        new(column, label, MaxLen: length, Key: true, Required: true);

    private static MasterField AutoId() =>
        new("id", "ID", FieldKind.Integer, Key: true, AutoKey: true);

    private static MasterSpec AutoReference(string title, string table, string nameColumn, string nameLabel) => new(
        title, table,
        [AutoId(), new(nameColumn, nameLabel, MaxLen: 30, Required: true)],
        $"SELECT id,`{nameColumn}` FROM `{table}` WHERE CONCAT_WS(' ',id,`{nameColumn}`) LIKE @p0 ORDER BY `{nameColumn}`,id",
        [("id", "ID"), (nameColumn, nameLabel)],
        $"SELECT COALESCE(MAX(id),0) FROM `{table}`", 0)
    {
        CanDelete = false,
        Hint = "Kode referensi yang sudah dipakai data pasien tidak dihapus dari layar ini.",
    };

    private static ReportSpec ReadOnly(string title, string sql, (string Column, string Header)[] columns,
        ReportPeriod period = ReportPeriod.None, string? hint = null) => new(title, sql, columns, period,
            hint ?? "Daftar baca-saja. Pencatatan dan perubahan dilakukan dari alur pelayanan Khanza.")
    { SearchWatermark = "Cari pasien atau data..." };

    private static MasterSpec TriageReference(string title, string table, string codeColumn, string codeLabel,
        string nameColumn, string nameLabel) => new(title, table,
        [Key(codeColumn, codeLabel, 3), new(nameColumn, nameLabel, MaxLen: 150, Required: true)],
        $"SELECT `{codeColumn}`,`{nameColumn}` FROM `{table}` WHERE CONCAT_WS(' ',`{codeColumn}`,`{nameColumn}`) LIKE @p0 ORDER BY `{nameColumn}`,`{codeColumn}`",
        [(codeColumn, codeLabel), (nameColumn, nameLabel)])
    { CanDelete = false, Hint = "Master triase yang sudah dipakai pada penilaian IGD tidak dihapus dari layar ini." };

    private static MasterSpec TriageScale(string title, string table, string codeColumn, string nameColumn) => new(title, table,
        [new("kode_pemeriksaan", "Pemeriksaan", FieldKind.Lookup, Required: true,
                LookupSql: "SELECT kode_pemeriksaan AS kode,nama_pemeriksaan AS nama FROM master_triase_pemeriksaan ORDER BY nama_pemeriksaan"),
         Key(codeColumn, "Kode Skala", 3), new(nameColumn, "Pengkajian", MaxLen: 150, Required: true)],
        $"SELECT s.{codeColumn},s.kode_pemeriksaan,p.nama_pemeriksaan,s.{nameColumn} FROM `{table}` s LEFT JOIN master_triase_pemeriksaan p ON p.kode_pemeriksaan=s.kode_pemeriksaan WHERE CONCAT_WS(' ',s.{codeColumn},s.kode_pemeriksaan,p.nama_pemeriksaan,s.{nameColumn}) LIKE @p0 ORDER BY p.nama_pemeriksaan,s.{nameColumn},s.{codeColumn}",
        [(codeColumn, "Kode"), ("kode_pemeriksaan", "Kode Pemeriksaan"), ("nama_pemeriksaan", "Pemeriksaan"), (nameColumn, "Pengkajian")])
    { CanDelete = false, Hint = "Pilihan skala yang sudah dipakai pada triase IGD tidak dihapus dari layar ini." };

    private static ReportSpec Soap(string title, string table, string memberTable) => ReadOnly(title,
        $"SELECT x.tgl_perawatan,x.jam_rawat,x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.keluhan,x.pemeriksaan,x.penilaian,x.rtl,x.instruksi,x.evaluasi,x.tensi,x.suhu_tubuh,x.nadi,x.respirasi,x.spo2,g.nama AS petugas FROM `{table}` x JOIN reg_periksa r ON r.no_rawat=x.no_rawat JOIN `{memberTable}` m ON m.no_rkm_medis=r.no_rkm_medis LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN pegawai g ON g.nik=x.nip WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.keluhan,x.pemeriksaan,x.penilaian,x.rtl,x.instruksi,x.evaluasi,g.nama) LIKE @p0 ORDER BY x.tgl_perawatan DESC,x.jam_rawat DESC",
        [("tgl_perawatan", "Tanggal"), ("jam_rawat", "Jam"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"),
         ("nm_pasien", "Pasien"), ("keluhan", "Keluhan"), ("pemeriksaan", "Pemeriksaan"), ("penilaian", "Penilaian"),
         ("rtl", "RTL"), ("instruksi", "Instruksi"), ("evaluasi", "Evaluasi"), ("tensi", "Tensi"), ("suhu_tubuh", "Suhu"),
         ("nadi", "Nadi"), ("respirasi", "Respirasi"), ("spo2", "SpO₂"), ("petugas", "Petugas")], ReportPeriod.DateRange,
        "Daftar SOAP baca-saja berdasarkan pasien TNI/POLRI. Pencatatan tetap melalui alur pelayanan Khanza.");

    private static ReportSpec NutritionRecord(string title, string table, params (string Column, string Header)[] fields)
    {
        var selected = string.Join(",", fields.Select(f => $"s.`{f.Column}` AS `{f.Column}`"));
        var searched = string.Join(",", fields.Select(f => $"s.`{f.Column}`"));
        var sql = $"SELECT s.tanggal,s.no_rawat,r.no_rkm_medis,p.nm_pasien,g.nama AS petugas,{selected} FROM `{table}` s LEFT JOIN reg_periksa r ON r.no_rawat=s.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN petugas g ON g.nip=s.nip WHERE s.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',s.no_rawat,r.no_rkm_medis,p.nm_pasien,g.nama,{searched}) LIKE @p0 ORDER BY s.tanggal DESC,s.no_rawat";
        return ReadOnly(title, sql,
            [("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("petugas", "Petugas"), .. fields.Select(f => (f.Column, f.Header))], ReportPeriod.DateRange,
            "Daftar skrining/asuhan gizi baca-saja. Pengkajian dan perubahan dilakukan pada alur pelayanan Khanza.");
    }

    private static MasterSpec NursingProblem(string title, string table) => new(title, table,
        [Key("kode_masalah", "Kode Masalah", 3), new("nama_masalah", "Nama Masalah", MaxLen: 100)],
        $"SELECT kode_masalah,nama_masalah FROM `{table}` WHERE CONCAT_WS(' ',kode_masalah,nama_masalah) LIKE @p0 ORDER BY nama_masalah,kode_masalah",
        [("kode_masalah", "Kode"), ("nama_masalah", "Masalah Keperawatan")])
    { CanDelete = false, Hint = "Master masalah yang digunakan pada pengkajian tidak dihapus dari layar ini." };

    private static MasterSpec NursingPlan(string title, string table, string problemTable) => new(title, table,
        [new("kode_masalah", "Masalah Keperawatan", FieldKind.Lookup, Required: true,
                LookupSql: $"SELECT kode_masalah AS kode,nama_masalah AS nama FROM `{problemTable}` ORDER BY nama_masalah"),
         Key("kode_rencana", "Kode Rencana", 3), new("rencana_keperawatan", "Rencana Keperawatan", FieldKind.Memo, MaxLen: 1000, Required: true)],
        $"SELECT r.kode_rencana,r.kode_masalah,m.nama_masalah,r.rencana_keperawatan FROM `{table}` r LEFT JOIN `{problemTable}` m ON m.kode_masalah=r.kode_masalah WHERE CONCAT_WS(' ',r.kode_rencana,r.kode_masalah,m.nama_masalah,r.rencana_keperawatan) LIKE @p0 ORDER BY m.nama_masalah,r.kode_rencana",
        [("kode_rencana", "Kode Rencana"), ("kode_masalah", "Kode Masalah"), ("nama_masalah", "Masalah Keperawatan"), ("rencana_keperawatan", "Rencana")])
    { CanDelete = false, Hint = "Rencana yang sudah dipakai pada asuhan tidak dihapus dari layar ini." };

    private static ReportSpec NursingAssessment(string title, string table, params (string Column, string Header)[] fields)
    {
        var selected = string.Join(",", fields.Select(f => $"s.`{f.Column}` AS `{f.Column}`"));
        var searched = string.Join(",", fields.Select(f => $"s.`{f.Column}`"));
        var sql = $"SELECT s.tanggal,s.no_rawat,r.no_rkm_medis,p.nm_pasien,{selected} FROM `{table}` s JOIN reg_periksa r ON r.no_rawat=s.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis WHERE s.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',s.no_rawat,r.no_rkm_medis,p.nm_pasien,{searched}) LIKE @p0 ORDER BY s.tanggal DESC,s.no_rawat";
        return ReadOnly(title, sql,
            [("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             .. fields.Select(f => (f.Column, f.Header))], ReportPeriod.DateRange,
            "Pengkajian klinis baca-saja. Catatan dibuat atau diubah dari alur pelayanan Khanza.");
    }

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnPerusahaan"] = new("Instansi/Perusahaan Pasien", "perusahaan_pasien",
            [Key("kode_perusahaan", "Kode Perusahaan", 8), new("nama_perusahaan", "Nama Perusahaan", MaxLen: 70, Required: true),
             new("alamat", "Alamat", MaxLen: 100), new("kota", "Kota", MaxLen: 40), new("no_telp", "Telepon", MaxLen: 27)],
            "SELECT kode_perusahaan,nama_perusahaan,alamat,kota,no_telp FROM perusahaan_pasien WHERE CONCAT_WS(' ',kode_perusahaan,nama_perusahaan,alamat,kota,no_telp) LIKE @p0 ORDER BY nama_perusahaan,kode_perusahaan",
            [("kode_perusahaan", "Kode"), ("nama_perusahaan", "Perusahaan"), ("alamat", "Alamat"), ("kota", "Kota"), ("no_telp", "Telepon")])
        { CanDelete = false, Hint = "Perusahaan yang terhubung dengan pasien tidak dihapus dari layar ini." },

        ["btnSuku"] = AutoReference("Suku/Bangsa Pasien", "suku_bangsa", "nama_suku_bangsa", "Suku/Bangsa"),
        ["btnBahasa"] = AutoReference("Bahasa Pasien", "bahasa_pasien", "nama_bahasa", "Bahasa"),
        ["btnGolonganTNI"] = AutoReference("Golongan TNI", "golongan_tni", "nama_golongan", "Golongan"),
        ["btnSatuanTNI"] = AutoReference("Satuan TNI", "satuan_tni", "nama_satuan", "Satuan"),
        ["btnJabatanTNI"] = AutoReference("Jabatan TNI", "jabatan_tni", "nama_jabatan", "Jabatan"),
        ["btnPangkatTNI"] = AutoReference("Pangkat TNI", "pangkat_tni", "nama_pangkat", "Pangkat"),
        ["btnGolonganPolri"] = AutoReference("Golongan POLRI", "golongan_polri", "nama_golongan", "Golongan"),
        ["btnSatuanPolri"] = AutoReference("Satuan POLRI", "satuan_polri", "nama_satuan", "Satuan"),
        ["btnJabatanPolri"] = AutoReference("Jabatan POLRI", "jabatan_polri", "nama_jabatan", "Jabatan"),
        ["btnPangkatPolri"] = AutoReference("Pangkat POLRI", "pangkat_polri", "nama_pangkat", "Pangkat"),
        ["btnCacatFisik"] = AutoReference("Cacat Fisik", "cacat_fisik", "nama_cacat", "Cacat Fisik"),

        ["btnInsidenKeselamatan"] = new("Master Insiden Keselamatan", "insiden_keselamatan",
            [Key("kode_insiden", "Kode Insiden", 5), new("nama_insiden", "Nama Insiden", MaxLen: 100, Required: true),
             new("jenis_insiden", "Jenis", FieldKind.Choice, Choices: ["KTC", "KNC", "KTD", "Sentinel"], Required: true),
             new("dampak", "Dampak", FieldKind.Choice, Choices: ["1. Tidak Signifikan", "2. Minor", "3. Moderat", "4. Mayor", "5. Katastropik"], Required: true)],
            "SELECT kode_insiden,nama_insiden,jenis_insiden,dampak FROM insiden_keselamatan WHERE CONCAT_WS(' ',kode_insiden,nama_insiden,jenis_insiden,dampak) LIKE @p0 ORDER BY nama_insiden,kode_insiden",
            [("kode_insiden", "Kode"), ("nama_insiden", "Nama Insiden"), ("jenis_insiden", "Jenis"), ("dampak", "Dampak")])
        { CanDelete = false, Hint = "Kode insiden yang sudah dipakai pada laporan tidak dihapus dari layar ini." },

        ["btnMasterTemplateHasilRadiologi"] = new("Template Hasil Radiologi", "template_hasil_radiologi",
            [Key("no_template", "No. Template", 5), new("nama_pemeriksaan", "Nama Pemeriksaan", MaxLen: 80) { NullWhenEmpty = true },
             new("template_hasil_radiologi", "Isi Template", FieldKind.Memo, MaxLen: 10000) { NullWhenEmpty = true }],
            "SELECT no_template,nama_pemeriksaan,template_hasil_radiologi FROM template_hasil_radiologi WHERE CONCAT_WS(' ',no_template,nama_pemeriksaan,template_hasil_radiologi) LIKE @p0 ORDER BY nama_pemeriksaan,no_template",
            [("no_template", "No. Template"), ("nama_pemeriksaan", "Pemeriksaan"), ("template_hasil_radiologi", "Isi Template")])
        { CanDelete = false, Hint = "Template dapat ditambah atau diubah. Penghapusan dinonaktifkan untuk menjaga template yang dipakai pemeriksaan." },

        ["btnMasterTemplateLaporanOperasi"] = new("Template Laporan Operasi", "template_laporan_operasi",
            [Key("no_template", "No. Template", 5), new("nama_operasi", "Nama Operasi", MaxLen: 100, Required: true),
             new("diagnosa_preop", "Diagnosa Preoperasi", MaxLen: 100, Required: true), new("diagnosa_postop", "Diagnosa Pasc operasi", MaxLen: 100, Required: true),
             new("jaringan_dieksisi", "Jaringan Dieksisi", MaxLen: 100, Required: true),
             new("permintaan_pa", "Permintaan PA", FieldKind.Choice, Choices: ["Ya", "Tidak"], Required: true),
             new("laporan_operasi", "Isi Laporan", FieldKind.Memo, MaxLen: 10000, Required: true)],
            "SELECT no_template,nama_operasi,diagnosa_preop,diagnosa_postop,jaringan_dieksisi,permintaan_pa,laporan_operasi FROM template_laporan_operasi WHERE CONCAT_WS(' ',no_template,nama_operasi,diagnosa_preop,diagnosa_postop,jaringan_dieksisi,permintaan_pa,laporan_operasi) LIKE @p0 ORDER BY nama_operasi,no_template",
            [("no_template", "No. Template"), ("nama_operasi", "Operasi"), ("diagnosa_preop", "Diagnosa Preop"),
             ("diagnosa_postop", "Diagnosa Postop"), ("jaringan_dieksisi", "Jaringan"), ("permintaan_pa", "PA"), ("laporan_operasi", "Isi Laporan")])
        { CanDelete = false, Hint = "Template dapat ditambah atau diubah. Penghapusan dinonaktifkan untuk menjaga template yang dipakai di laporan operasi." },

        ["btnMasterTemplatePemeriksaanDokter"] = new("Template Pemeriksaan Dokter", "template_pemeriksaan_dokter",
            [Key("no_template", "No. Template", 20), new("kd_dokter", "Dokter", FieldKind.Lookup,
                LookupSql: "SELECT kd_dokter AS kode,nm_dokter AS nama FROM dokter ORDER BY nm_dokter"),
             new("keluhan", "Keluhan", FieldKind.Memo, MaxLen: 2000) { NullWhenEmpty = true },
             new("pemeriksaan", "Pemeriksaan", FieldKind.Memo, MaxLen: 2000) { NullWhenEmpty = true },
             new("penilaian", "Penilaian", FieldKind.Memo, MaxLen: 2000) { NullWhenEmpty = true },
             new("rencana", "Rencana", FieldKind.Memo, MaxLen: 2000, Required: true),
             new("instruksi", "Instruksi", FieldKind.Memo, MaxLen: 2000, Required: true),
             new("evaluasi", "Evaluasi", FieldKind.Memo, MaxLen: 2000) { NullWhenEmpty = true }],
            "SELECT t.no_template,t.kd_dokter,d.nm_dokter,t.keluhan,t.pemeriksaan,t.penilaian,t.rencana,t.instruksi,t.evaluasi FROM template_pemeriksaan_dokter t LEFT JOIN dokter d ON d.kd_dokter=t.kd_dokter WHERE CONCAT_WS(' ',t.no_template,t.kd_dokter,d.nm_dokter,t.keluhan,t.pemeriksaan,t.penilaian,t.rencana,t.instruksi,t.evaluasi) LIKE @p0 ORDER BY d.nm_dokter,t.no_template",
            [("no_template", "No. Template"), ("nm_dokter", "Dokter"), ("keluhan", "Keluhan"), ("pemeriksaan", "Pemeriksaan"),
             ("penilaian", "Penilaian"), ("rencana", "Rencana"), ("instruksi", "Instruksi"), ("evaluasi", "Evaluasi")])
        { CanDelete = false, Hint = "Template terkait diagnosa, tindakan, resep, dan permintaan penunjang. Penghapusan dinonaktifkan; rincian template dikelola pada alur klinis Khanza." },

        ["btnMasterTemplateInformasiEdukasi"] = new("Template Informasi & Edukasi", "template_pelaksanaan_informasi_edukasi",
            [Key("no_template", "No. Template", 5), new("materi_edukasi", "Materi Edukasi", MaxLen: 1000) { NullWhenEmpty = true },
             new("lama_edukasi", "Lama Edukasi", MaxLen: 10) { NullWhenEmpty = true },
             new("metode_edukasi", "Metode", FieldKind.Choice, Choices: ["Ceramah", "Diskusi", "Demonstrasi"]) { NullWhenEmpty = true }],
            "SELECT no_template,materi_edukasi,lama_edukasi,metode_edukasi FROM template_pelaksanaan_informasi_edukasi WHERE CONCAT_WS(' ',no_template,materi_edukasi,lama_edukasi,metode_edukasi) LIKE @p0 ORDER BY no_template",
            [("no_template", "No. Template"), ("materi_edukasi", "Materi Edukasi"), ("lama_edukasi", "Lama"), ("metode_edukasi", "Metode")])
        { CanDelete = false, Hint = "Template dapat ditambah atau diubah; penghapusan dinonaktifkan untuk menjaga materi edukasi yang dipakai." },

        ["btnMasterMasalahKeperawatan"] = NursingProblem("Master Masalah Keperawatan", "master_masalah_keperawatan"),
        ["btnMasterMasalahKeperawatanGigi"] = NursingProblem("Master Masalah Keperawatan Gigi", "master_masalah_keperawatan_gigi"),
        ["btnMasterMasalahKeperawatanAnak"] = NursingProblem("Master Masalah Keperawatan Bayi/Anak", "master_masalah_keperawatan_anak"),
        ["btnMasterMasalahKeperawatanMata"] = NursingProblem("Master Masalah Keperawatan Mata", "master_masalah_keperawatan_mata"),
        ["btnMasterMasalahKeperawatanIGD"] = NursingProblem("Master Masalah Keperawatan IGD", "master_masalah_keperawatan_igd"),
        ["btnMasterMasalahKeperawatanPsikiatri"] = NursingProblem("Master Masalah Keperawatan Psikiatri", "master_masalah_keperawatan_psikiatri"),
        ["btnMasterMasalahKeperawatanGeriatri"] = NursingProblem("Master Masalah Keperawatan Geriatri", "master_masalah_keperawatan_geriatri"),
        ["btnMasterMasalahKeperawatanNeonatus"] = NursingProblem("Master Masalah Keperawatan Neonatus", "master_masalah_keperawatan_neonatus"),

        ["btnMasterRencanaKeperawatan"] = NursingPlan("Master Rencana Keperawatan", "master_rencana_keperawatan", "master_masalah_keperawatan"),
        ["btnMasterRencanaKeperawatanGigi"] = NursingPlan("Master Rencana Keperawatan Gigi", "master_rencana_keperawatan_gigi", "master_masalah_keperawatan_gigi"),
        ["btnMasterRencanaKeperawatanAnak"] = NursingPlan("Master Rencana Keperawatan Bayi/Anak", "master_rencana_keperawatan_anak", "master_masalah_keperawatan_anak"),
        ["btnMasterRencanaKeperawatanMata"] = NursingPlan("Master Rencana Keperawatan Mata", "master_rencana_keperawatan_mata", "master_masalah_keperawatan_mata"),
        ["btnMasterRencanaKeperawatanIGD"] = NursingPlan("Master Rencana Keperawatan IGD", "master_rencana_keperawatan_igd", "master_masalah_keperawatan_igd"),
        ["btnMasterRencanaKeperawatanPsikiatri"] = NursingPlan("Master Rencana Keperawatan Psikiatri", "master_rencana_keperawatan_psikiatri", "master_masalah_keperawatan_psikiatri"),
        ["btnMasterRencanaKeperawatanGeriatri"] = NursingPlan("Master Rencana Keperawatan Geriatri", "master_rencana_keperawatan_geriatri", "master_masalah_keperawatan_geriatri"),
        ["btnMasterRencanaKeperawatanNeonatus"] = NursingPlan("Master Rencana Keperawatan Neonatus", "master_rencana_keperawatan_neonatus", "master_masalah_keperawatan_neonatus"),
        ["btnMasterImunisasi"] = new("Master Imunisasi", "master_imunisasi",
            [Key("kode_imunisasi", "Kode Imunisasi", 3), new("nama_imunisasi", "Nama Imunisasi", MaxLen: 100)],
            "SELECT kode_imunisasi,nama_imunisasi FROM master_imunisasi WHERE CONCAT_WS(' ',kode_imunisasi,nama_imunisasi) LIKE @p0 ORDER BY nama_imunisasi,kode_imunisasi",
            [("kode_imunisasi", "Kode"), ("nama_imunisasi", "Imunisasi")])
        { CanDelete = false, Hint = "Master imunisasi yang sudah terhubung ke riwayat pasien tidak dihapus dari layar ini." },

        ["btnMasterTriasePemeriksaan"] = TriageReference("Master Triase Pemeriksaan", "master_triase_pemeriksaan", "kode_pemeriksaan", "Kode", "nama_pemeriksaan", "Pemeriksaan"),
        ["btnMasterTriaseMacamKasus"] = TriageReference("Master Triase Macam Kasus", "master_triase_macam_kasus", "kode_kasus", "Kode", "macam_kasus", "Macam Kasus"),
        ["btnMasterTriaseSkala1"] = TriageScale("Master Triase Skala 1", "master_triase_skala1", "kode_skala1", "pengkajian_skala1"),
        ["btnMasterTriaseSkala2"] = TriageScale("Master Triase Skala 2", "master_triase_skala2", "kode_skala2", "pengkajian_skala2"),
        ["btnMasterTriaseSkala3"] = TriageScale("Master Triase Skala 3", "master_triase_skala3", "kode_skala3", "pengkajian_skala3"),
        ["btnMasterTriaseSkala4"] = TriageScale("Master Triase Skala 4", "master_triase_skala4", "kode_skala4", "pengkajian_skala4"),
        ["btnMasterTriaseSkala5"] = TriageScale("Master Triase Skala 5", "master_triase_skala5", "kode_skala5", "pengkajian_skala5"),
    };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnLahir"] = ReadOnly("Kelahiran Bayi",
            """
            SELECT b.no_rkm_medis,p.nm_pasien,b.nama_ayah,b.umur_ayah,b.umur_ibu,b.berat_badan,b.panjang_badan,
                   b.lingkar_kepala,b.lingkar_dada,b.lingkar_perut,b.proses_lahir,b.anakke,b.jam_lahir,b.penolong,b.diagnosa,b.keterangan
            FROM pasien_bayi b LEFT JOIN pasien p ON p.no_rkm_medis=b.no_rkm_medis
            WHERE CONCAT_WS(' ',b.no_rkm_medis,p.nm_pasien,b.nama_ayah,b.proses_lahir,b.penolong,b.diagnosa,b.keterangan) LIKE @p0
            ORDER BY p.nm_pasien,b.no_rkm_medis
            """,
            [("no_rkm_medis", "No. RM"), ("nm_pasien", "Bayi"), ("nama_ayah", "Ayah"), ("umur_ayah", "Umur Ayah"),
             ("umur_ibu", "Umur Ibu"), ("berat_badan", "Berat"), ("panjang_badan", "Panjang"), ("lingkar_kepala", "Lingkar Kepala"),
             ("lingkar_dada", "Lingkar Dada"), ("lingkar_perut", "Lingkar Perut"), ("proses_lahir", "Proses Lahir"),
             ("anakke", "Anak Ke"), ("jam_lahir", "Jam Lahir"), ("penolong", "Penolong"), ("diagnosa", "Diagnosa"), ("keterangan", "Keterangan")]),

        ["btnCatatanPasien"] = ReadOnly("Catatan Pasien",
            "SELECT c.no_rkm_medis,p.nm_pasien,c.catatan FROM catatan_pasien c LEFT JOIN pasien p ON p.no_rkm_medis=c.no_rkm_medis WHERE CONCAT_WS(' ',c.no_rkm_medis,p.nm_pasien,c.catatan) LIKE @p0 ORDER BY p.nm_pasien,c.no_rkm_medis",
            [("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("catatan", "Catatan")]),

        ["btnPasienMati"] = ReadOnly("Pasien Meninggal",
            "SELECT m.tanggal,m.jam,m.no_rkm_medis,p.nm_pasien,m.temp_meninggal,m.keterangan,m.kd_dokter,d.nm_dokter FROM pasien_mati m LEFT JOIN pasien p ON p.no_rkm_medis=m.no_rkm_medis LEFT JOIN dokter d ON d.kd_dokter=m.kd_dokter WHERE m.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',m.no_rkm_medis,p.nm_pasien,m.temp_meninggal,m.keterangan,m.kd_dokter,d.nm_dokter) LIKE @p0 ORDER BY m.tanggal DESC,m.jam DESC",
            [("tanggal", "Tanggal"), ("jam", "Jam"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("temp_meninggal", "Tempat Meninggal"), ("keterangan", "Keterangan"), ("kd_dokter", "Kode Dokter"), ("nm_dokter", "Dokter")], ReportPeriod.DateRange),

        ["btnDiagnosa"] = ReadOnly("Diagnosa Pasien",
            "SELECT dp.no_rawat,r.no_rkm_medis,p.nm_pasien,r.tgl_registrasi,dp.status,dp.prioritas,dp.status_penyakit,dp.kd_penyakit,py.nm_penyakit FROM diagnosa_pasien dp JOIN reg_periksa r ON r.no_rawat=dp.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN penyakit py ON py.kd_penyakit=dp.kd_penyakit WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',dp.no_rawat,r.no_rkm_medis,p.nm_pasien,dp.status,dp.kd_penyakit,py.nm_penyakit) LIKE @p0 ORDER BY r.tgl_registrasi DESC,dp.no_rawat,dp.prioritas",
            [("tgl_registrasi", "Tgl. Registrasi"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("status", "Jenis Rawat"), ("prioritas", "Prioritas"), ("status_penyakit", "Status Diagnosa"), ("kd_penyakit", "Kode ICD"), ("nm_penyakit", "Diagnosa")], ReportPeriod.DateRange),

        ["btnDataHAIs"] = ReadOnly("Data HAIs",
            "SELECT h.tanggal,h.no_rawat,r.no_rkm_medis,p.nm_pasien,h.kd_kamar,k.kd_bangsal,h.ETT,h.CVL,h.IVL,h.UC,h.VAP,h.IAD,h.PLEB,h.ISK,h.ILO,h.HAP,h.Tinea,h.Scabies,h.DEKU,h.SPUTUM,h.DARAH,h.URINE,h.ANTIBIOTIK FROM data_HAIs h LEFT JOIN reg_periksa r ON r.no_rawat=h.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN kamar k ON k.kd_kamar=h.kd_kamar WHERE h.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',h.no_rawat,r.no_rkm_medis,p.nm_pasien,h.kd_kamar,h.SPUTUM,h.DARAH,h.URINE,h.ANTIBIOTIK) LIKE @p0 ORDER BY h.tanggal DESC,h.no_rawat",
            [("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("kd_kamar", "Kamar"),
             ("ETT", "ETT"), ("CVL", "CVL"), ("IVL", "IVL"), ("UC", "UC"), ("VAP", "VAP"), ("IAD", "IAD"), ("PLEB", "PLEB"),
             ("ISK", "ISK"), ("ILO", "ILO"), ("HAP", "HAP"), ("Tinea", "Tinea"), ("Scabies", "Scabies"), ("DEKU", "Dekubitus"),
             ("SPUTUM", "Sputum"), ("DARAH", "Darah"), ("URINE", "Urine"), ("ANTIBIOTIK", "Antibiotik")], ReportPeriod.DateRange),

        ["btnKlasifikasiPasienRanap"] = ReadOnly("Klasifikasi Pasien Ranap",
            "SELECT x.tanggal,x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.kd_kamar,b.nm_bangsal,x.Minimal,x.Partial,x.Total FROM data_klasifikasi_pasien_ranap x LEFT JOIN reg_periksa r ON r.no_rawat=x.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN kamar k ON k.kd_kamar=x.kd_kamar LEFT JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.kd_kamar,b.nm_bangsal) LIKE @p0 ORDER BY x.tanggal DESC,x.no_rawat",
            [("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("kd_kamar", "Kamar"),
             ("nm_bangsal", "Bangsal"), ("Minimal", "Minimal"), ("Partial", "Partial"), ("Total", "Total")], ReportPeriod.DateRange),

        ["btnSOAPPerawatan"] = ReadOnly("SOAP Perawatan (Rawat Inap)",
            "SELECT x.tgl_perawatan,x.jam_rawat,x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.keluhan,x.pemeriksaan,x.penilaian,x.rtl,x.instruksi,x.evaluasi,x.tensi,x.suhu_tubuh,x.nadi,x.respirasi,x.spo2,g.nama AS petugas FROM pemeriksaan_ranap x LEFT JOIN reg_periksa r ON r.no_rawat=x.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN pegawai g ON g.nik=x.nip WHERE x.tgl_perawatan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.keluhan,x.pemeriksaan,x.penilaian,x.rtl,x.instruksi,x.evaluasi,g.nama) LIKE @p0 ORDER BY x.tgl_perawatan DESC,x.jam_rawat DESC",
            [("tgl_perawatan", "Tanggal"), ("jam_rawat", "Jam"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("keluhan", "Keluhan"), ("pemeriksaan", "Pemeriksaan"), ("penilaian", "Penilaian"), ("rtl", "RTL"), ("instruksi", "Instruksi"),
             ("evaluasi", "Evaluasi"), ("tensi", "Tensi"), ("suhu_tubuh", "Suhu"), ("nadi", "Nadi"), ("respirasi", "Respirasi"),
             ("spo2", "SpO₂"), ("petugas", "Petugas")], ReportPeriod.DateRange),

        ["btnSOAPRalanAnggotaPolri"] = Soap("SOAP Rawat Jalan POLRI", "pemeriksaan_ralan", "pasien_polri"),
        ["btnSOAPRanapAnggotaPolri"] = Soap("SOAP Rawat Inap POLRI", "pemeriksaan_ranap", "pasien_polri"),
        ["btnSOAPRalanAnggotaTNI"] = Soap("SOAP Rawat Jalan TNI", "pemeriksaan_ralan", "pasien_tni"),
        ["btnSOAPRanapAnggotaTNI"] = Soap("SOAP Rawat Inap TNI", "pemeriksaan_ranap", "pasien_tni"),

        ["btnRiwayatKamarPasien"] = ReadOnly("Riwayat Kamar Pasien",
            "SELECT ki.tgl_masuk,ki.jam_masuk,ki.tgl_keluar,ki.jam_keluar,ki.no_rawat,r.no_rkm_medis,p.nm_pasien,ki.kd_kamar,b.nm_bangsal,ki.diagnosa_awal,ki.diagnosa_akhir,ki.stts_pulang,ki.lama FROM kamar_inap ki JOIN reg_periksa r ON r.no_rawat=ki.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN kamar k ON k.kd_kamar=ki.kd_kamar LEFT JOIN bangsal b ON b.kd_bangsal=k.kd_bangsal WHERE ki.tgl_masuk BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',ki.no_rawat,r.no_rkm_medis,p.nm_pasien,ki.kd_kamar,b.nm_bangsal,ki.diagnosa_awal,ki.diagnosa_akhir,ki.stts_pulang) LIKE @p0 ORDER BY ki.tgl_masuk DESC,ki.jam_masuk DESC",
            [("tgl_masuk", "Masuk"), ("jam_masuk", "Jam Masuk"), ("tgl_keluar", "Keluar"), ("jam_keluar", "Jam Keluar"),
             ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("kd_kamar", "Kode Kamar"),
             ("nm_bangsal", "Bangsal"), ("diagnosa_awal", "Diagnosa Awal"), ("diagnosa_akhir", "Diagnosa Akhir"),
             ("stts_pulang", "Status Pulang"), ("lama", "Hari")], ReportPeriod.DateRange),

        ["btnInsidenKeselamatanPasien"] = ReadOnly("Data Insiden Keselamatan Pasien",
            "SELECT i.tgl_kejadian,i.jam_kejadian,i.tgl_lapor,i.jam_lapor,i.no_rawat,r.no_rkm_medis,p.nm_pasien,i.kode_insiden,m.nama_insiden,m.jenis_insiden,m.dampak,i.lokasi,i.unit_terkait,i.kronologis,i.akibat,i.tindakan_insiden,i.identifikasi_masalah,i.rtl,i.nip,pt.nama AS petugas FROM insiden_keselamatan_pasien i LEFT JOIN reg_periksa r ON r.no_rawat=i.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN insiden_keselamatan m ON m.kode_insiden=i.kode_insiden LEFT JOIN petugas pt ON pt.nip=i.nip WHERE i.tgl_kejadian BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',i.no_rawat,r.no_rkm_medis,p.nm_pasien,i.kode_insiden,m.nama_insiden,m.jenis_insiden,i.lokasi,i.unit_terkait,i.kronologis,i.akibat,i.tindakan_insiden,i.identifikasi_masalah,i.rtl,pt.nama) LIKE @p0 ORDER BY i.tgl_kejadian DESC,i.jam_kejadian DESC",
            [("tgl_kejadian", "Tanggal Kejadian"), ("jam_kejadian", "Jam"), ("tgl_lapor", "Tanggal Lapor"), ("jam_lapor", "Jam Lapor"),
             ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("kode_insiden", "Kode"),
             ("nama_insiden", "Insiden"), ("jenis_insiden", "Jenis"), ("dampak", "Dampak"), ("lokasi", "Lokasi"),
             ("unit_terkait", "Unit Terkait"), ("kronologis", "Kronologis"), ("akibat", "Akibat"), ("tindakan_insiden", "Tindakan"),
             ("identifikasi_masalah", "Identifikasi"), ("rtl", "RTL"), ("petugas", "Pelapor")], ReportPeriod.DateRange),

        ["btnSirkulasiBerkas"] = ReadOnly("Peminjaman Berkas Rekam Medis",
            "SELECT x.tgl_pinjam,x.tgl_kembali,x.no_rkm_medis,p.nm_pasien,x.peminjam,x.id_ruang,u.nama_ruang,x.nip,pt.nama AS petugas,x.status_pinjam FROM peminjaman_berkas x LEFT JOIN pasien p ON p.no_rkm_medis=x.no_rkm_medis LEFT JOIN inventaris_ruang u ON u.id_ruang=x.id_ruang LEFT JOIN petugas pt ON pt.nip=x.nip WHERE x.tgl_pinjam BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rkm_medis,p.nm_pasien,x.peminjam,x.id_ruang,u.nama_ruang,x.nip,pt.nama,x.status_pinjam) LIKE @p0 ORDER BY x.tgl_pinjam DESC,x.peminjam",
            [("tgl_pinjam", "Tgl. Pinjam"), ("tgl_kembali", "Batas Kembali"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("peminjam", "Peminjam"), ("id_ruang", "Kode Ruang"), ("nama_ruang", "Ruang"), ("nip", "NIP"),
             ("petugas", "Petugas"), ("status_pinjam", "Status")], ReportPeriod.DateRange),

        ["btnResume"] = ReadOnly("Riwayat Perawatan Pasien",
            "SELECT r.tgl_registrasi,x.no_rawat,r.no_rkm_medis,p.nm_pasien,d.nm_dokter,x.keluhan_utama,x.jalannya_penyakit,x.diagnosa_utama,x.kondisi_pulang FROM resume_pasien x JOIN reg_periksa r ON r.no_rawat=x.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN dokter d ON d.kd_dokter=x.kd_dokter WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien,d.nm_dokter,x.keluhan_utama,x.jalannya_penyakit,x.diagnosa_utama) LIKE @p0 ORDER BY r.tgl_registrasi DESC,x.no_rawat",
            [("tgl_registrasi", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("nm_dokter", "Dokter"), ("keluhan_utama", "Keluhan Utama"), ("jalannya_penyakit", "Riwayat Perawatan"),
             ("diagnosa_utama", "Diagnosa Utama"), ("kondisi_pulang", "Kondisi Pulang")], ReportPeriod.DateRange),

        ["btnRetensiRM"] = ReadOnly("Retensi Berkas Rekam Medis",
            "SELECT x.no_rkm_medis,p.nm_pasien,x.terakhir_daftar,x.tgl_retensi,x.lokasi_pdf FROM retensi_pasien x LEFT JOIN pasien p ON p.no_rkm_medis=x.no_rkm_medis WHERE x.tgl_retensi BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rkm_medis,p.nm_pasien,x.lokasi_pdf) LIKE @p0 ORDER BY x.tgl_retensi DESC,p.nm_pasien",
            [("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("terakhir_daftar", "Terakhir Daftar"),
             ("tgl_retensi", "Tanggal Retensi"), ("lokasi_pdf", "Lokasi PDF")], ReportPeriod.DateRange,
            "Daftar baca-saja. Pemindahan atau pemusnahan berkas mengikuti kebijakan retensi rumah sakit."),

        ["btnMutasiBerkas"] = ReadOnly("Mutasi Berkas Rekam Medis",
            "SELECT x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.status,x.dikirim,x.diterima,x.kembali,x.tidakada,x.ranap FROM mutasi_berkas x LEFT JOIN reg_periksa r ON r.no_rawat=x.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis WHERE COALESCE(x.dikirim,x.ranap) BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.status) LIKE @p0 ORDER BY COALESCE(x.dikirim,x.ranap) DESC,x.no_rawat",
            [("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("status", "Status"),
             ("dikirim", "Dikirim"), ("diterima", "Diterima"), ("kembali", "Kembali"), ("tidakada", "Tidak Ada"), ("ranap", "Masuk Ranap")],
            ReportPeriod.DateRange),

        ["btnBerkasDigitalPerawatan"] = ReadOnly("Berkas Digital Perawatan",
            "SELECT x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.kode,m.nama AS jenis_berkas,x.lokasi_file FROM berkas_digital_perawatan x LEFT JOIN reg_periksa r ON r.no_rawat=x.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN master_berkas_digital m ON m.kode=x.kode WHERE CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien,x.kode,m.nama,x.lokasi_file) LIKE @p0 ORDER BY x.no_rawat,m.nama,x.kode",
            [("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("kode", "Kode Berkas"),
             ("jenis_berkas", "Jenis Berkas"), ("lokasi_file", "Lokasi File")]),

        ["btnPengaduan"] = ReadOnly("Pengaduan Pasien",
            "SELECT x.tanggal,x.id,x.no_rkm_medis,p.nm_pasien,x.pesan,b.pesan_balasan FROM pengaduan x LEFT JOIN pasien p ON p.no_rkm_medis=x.no_rkm_medis LEFT JOIN balasan_pengaduan b ON b.id_pengaduan=x.id WHERE x.tanggal BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.id,x.no_rkm_medis,p.nm_pasien,x.pesan,b.pesan_balasan) LIKE @p0 ORDER BY x.tanggal DESC,x.id",
            [("tanggal", "Tanggal"), ("id", "ID Pengaduan"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("pesan", "Pesan"), ("pesan_balasan", "Balasan")], ReportPeriod.DateRange,
            "Daftar pengaduan baca-saja; komunikasi dan balasan dicatat melalui alur e-Pasien Khanza."),

        ["btnDataTriaseIGD"] = ReadOnly("Data Triase IGD",
            "SELECT t.tgl_kunjungan,t.no_rawat,r.no_rkm_medis,p.nm_pasien,t.cara_masuk,t.alat_transportasi,t.alasan_kedatangan,t.keterangan_kedatangan,m.macam_kasus,t.tekanan_darah,t.nadi,t.pernapasan,t.suhu,t.saturasi_o2,t.nyeri,pr.keluhan_utama,pr.kebutuhan_khusus,pr.plan AS plan_primer,sk.anamnesa_singkat,sk.plan AS plan_sekunder,COALESCE(gp.nama,gs.nama) AS petugas,(SELECT GROUP_CONCAT(ms.pengkajian_skala1 SEPARATOR '; ') FROM data_triase_igddetail_skala1 d JOIN master_triase_skala1 ms ON ms.kode_skala1=d.kode_skala1 WHERE d.no_rawat=t.no_rawat) AS skala1,(SELECT GROUP_CONCAT(ms.pengkajian_skala2 SEPARATOR '; ') FROM data_triase_igddetail_skala2 d JOIN master_triase_skala2 ms ON ms.kode_skala2=d.kode_skala2 WHERE d.no_rawat=t.no_rawat) AS skala2,(SELECT GROUP_CONCAT(ms.pengkajian_skala3 SEPARATOR '; ') FROM data_triase_igddetail_skala3 d JOIN master_triase_skala3 ms ON ms.kode_skala3=d.kode_skala3 WHERE d.no_rawat=t.no_rawat) AS skala3,(SELECT GROUP_CONCAT(ms.pengkajian_skala4 SEPARATOR '; ') FROM data_triase_igddetail_skala4 d JOIN master_triase_skala4 ms ON ms.kode_skala4=d.kode_skala4 WHERE d.no_rawat=t.no_rawat) AS skala4,(SELECT GROUP_CONCAT(ms.pengkajian_skala5 SEPARATOR '; ') FROM data_triase_igddetail_skala5 d JOIN master_triase_skala5 ms ON ms.kode_skala5=d.kode_skala5 WHERE d.no_rawat=t.no_rawat) AS skala5 FROM data_triase_igd t JOIN reg_periksa r ON r.no_rawat=t.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN master_triase_macam_kasus m ON m.kode_kasus=t.kode_kasus LEFT JOIN data_triase_igdprimer pr ON pr.no_rawat=t.no_rawat LEFT JOIN data_triase_igdsekunder sk ON sk.no_rawat=t.no_rawat LEFT JOIN pegawai gp ON gp.nik=pr.nik LEFT JOIN pegawai gs ON gs.nik=sk.nik WHERE t.tgl_kunjungan BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',t.no_rawat,r.no_rkm_medis,p.nm_pasien,m.macam_kasus,t.keterangan_kedatangan,pr.keluhan_utama,sk.anamnesa_singkat) LIKE @p0 ORDER BY t.tgl_kunjungan DESC,t.no_rawat",
            [("tgl_kunjungan", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("cara_masuk", "Cara Masuk"), ("alat_transportasi", "Transportasi"), ("alasan_kedatangan", "Alasan Datang"),
             ("keterangan_kedatangan", "Keterangan"), ("macam_kasus", "Macam Kasus"), ("tekanan_darah", "Tensi"),
             ("nadi", "Nadi"), ("pernapasan", "Pernapasan"), ("suhu", "Suhu"), ("saturasi_o2", "SpO₂"), ("nyeri", "Nyeri"),
             ("keluhan_utama", "Keluhan Primer"), ("kebutuhan_khusus", "Kebutuhan Khusus"), ("plan_primer", "Plan Primer"),
             ("anamnesa_singkat", "Anamnesa Sekunder"), ("plan_sekunder", "Plan Sekunder"), ("petugas", "Petugas"),
             ("skala1", "Skala 1"), ("skala2", "Skala 2"), ("skala3", "Skala 3"), ("skala4", "Skala 4"), ("skala5", "Skala 5")], ReportPeriod.DateRange),

        ["btnResumePasien"] = ReadOnly("Resume Pasien Rawat Jalan",
            "SELECT r.tgl_registrasi,x.no_rawat,r.no_rkm_medis,p.nm_pasien,d.nm_dokter,x.keluhan_utama,x.jalannya_penyakit,x.pemeriksaan_penunjang,x.hasil_laborat,x.diagnosa_utama,x.diagnosa_sekunder,x.diagnosa_sekunder2,x.diagnosa_sekunder3,x.diagnosa_sekunder4,x.prosedur_utama,x.prosedur_sekunder,x.prosedur_sekunder2,x.prosedur_sekunder3,x.kondisi_pulang,x.obat_pulang FROM resume_pasien x JOIN reg_periksa r ON r.no_rawat=x.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN dokter d ON d.kd_dokter=x.kd_dokter WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien,d.nm_dokter,x.keluhan_utama,x.jalannya_penyakit,x.diagnosa_utama,x.diagnosa_sekunder,x.prosedur_utama) LIKE @p0 ORDER BY r.tgl_registrasi DESC,x.no_rawat",
            [("tgl_registrasi", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("nm_dokter", "Dokter"), ("keluhan_utama", "Keluhan Utama"), ("jalannya_penyakit", "Jalannya Penyakit"),
             ("pemeriksaan_penunjang", "Pemeriksaan Penunjang"), ("hasil_laborat", "Hasil Laboratorium"), ("diagnosa_utama", "Diagnosa Utama"),
             ("diagnosa_sekunder", "Diagnosa Sekunder 1"), ("diagnosa_sekunder2", "Diagnosa Sekunder 2"),
             ("diagnosa_sekunder3", "Diagnosa Sekunder 3"), ("diagnosa_sekunder4", "Diagnosa Sekunder 4"),
             ("prosedur_utama", "Prosedur Utama"), ("prosedur_sekunder", "Prosedur Sekunder 1"),
             ("prosedur_sekunder2", "Prosedur Sekunder 2"), ("prosedur_sekunder3", "Prosedur Sekunder 3"),
             ("kondisi_pulang", "Kondisi Pulang"), ("obat_pulang", "Obat Pulang")], ReportPeriod.DateRange),

        ["btnResumePasienRanap"] = ReadOnly("Resume Pasien Rawat Inap",
            "SELECT r.tgl_registrasi,x.no_rawat,r.no_rkm_medis,p.nm_pasien,d.nm_dokter,x.diagnosa_awal,x.alasan,x.keluhan_utama,x.pemeriksaan_fisik,x.jalannya_penyakit,x.pemeriksaan_penunjang,x.hasil_laborat,x.tindakan_dan_operasi,x.obat_di_rs,x.diagnosa_utama,x.diagnosa_sekunder,x.diagnosa_sekunder2,x.diagnosa_sekunder3,x.diagnosa_sekunder4,x.prosedur_utama,x.prosedur_sekunder,x.prosedur_sekunder2,x.prosedur_sekunder3,x.alergi,x.diet,x.lab_belum,x.edukasi,x.cara_keluar,x.keadaan,x.dilanjutkan,x.kontrol,x.obat_pulang FROM resume_pasien_ranap x JOIN reg_periksa r ON r.no_rawat=x.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis LEFT JOIN dokter d ON d.kd_dokter=x.kd_dokter WHERE r.tgl_registrasi BETWEEN @p1 AND @p2 AND CONCAT_WS(' ',x.no_rawat,r.no_rkm_medis,p.nm_pasien,d.nm_dokter,x.diagnosa_awal,x.keluhan_utama,x.jalannya_penyakit,x.diagnosa_utama,x.diagnosa_sekunder) LIKE @p0 ORDER BY r.tgl_registrasi DESC,x.no_rawat",
            [("tgl_registrasi", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
             ("nm_dokter", "Dokter"), ("diagnosa_awal", "Diagnosa Awal"), ("alasan", "Alasan Masuk"), ("keluhan_utama", "Keluhan Utama"),
             ("pemeriksaan_fisik", "Pemeriksaan Fisik"), ("jalannya_penyakit", "Jalannya Penyakit"),
             ("pemeriksaan_penunjang", "Pemeriksaan Penunjang"), ("hasil_laborat", "Hasil Laboratorium"),
             ("tindakan_dan_operasi", "Tindakan/Operasi"), ("obat_di_rs", "Obat di RS"), ("diagnosa_utama", "Diagnosa Utama"),
             ("diagnosa_sekunder", "Diagnosa Sekunder 1"), ("diagnosa_sekunder2", "Diagnosa Sekunder 2"),
             ("diagnosa_sekunder3", "Diagnosa Sekunder 3"), ("diagnosa_sekunder4", "Diagnosa Sekunder 4"),
             ("prosedur_utama", "Prosedur Utama"), ("prosedur_sekunder", "Prosedur Sekunder 1"),
             ("prosedur_sekunder2", "Prosedur Sekunder 2"), ("prosedur_sekunder3", "Prosedur Sekunder 3"),
             ("alergi", "Alergi"), ("diet", "Diet"), ("lab_belum", "Laboratorium Belum Selesai"), ("edukasi", "Edukasi"),
             ("cara_keluar", "Cara Keluar"), ("keadaan", "Keadaan"), ("dilanjutkan", "Dilanjutkan"),
             ("kontrol", "Kontrol"), ("obat_pulang", "Obat Pulang")], ReportPeriod.DateRange),

        ["btnSkriningGiziLanjut"] = NutritionRecord("Skrining Gizi Lanjut", "skrining_gizi",
            ("skrining_bb", "Berat Badan"), ("skrining_tb", "Tinggi Badan"), ("alergi", "Alergi"),
            ("parameter_imt", "Parameter IMT"), ("skor_imt", "Skor IMT"), ("parameter_bb", "Parameter BB"),
            ("skor_bb", "Skor BB"), ("parameter_penyakit", "Parameter Penyakit"), ("skor_penyakit", "Skor Penyakit"),
            ("skor_total", "Skor Total"), ("parameter_total", "Parameter Total")),
        ["btnAsuhanGizi"] = NutritionRecord("Asuhan Gizi", "asuhan_gizi",
            ("antropometri_bb", "BB"), ("antropometri_tb", "TB"), ("antropometri_imt", "IMT"), ("antropometri_lla", "LLA"),
            ("antropometri_tl", "Tinggi Lutut"), ("antropometri_bbideal", "BB Ideal"), ("biokimia", "Biokimia"),
            ("fisik_klinis", "Fisik/Klinis"), ("pola_makan", "Pola Makan"), ("riwayat_personal", "Riwayat Personal"),
            ("diagnosis", "Diagnosis Gizi"), ("intervensi_gizi", "Intervensi Gizi"), ("monitoring_evaluasi", "Monitoring/Evaluasi")),
        ["btnMonitoringAsuhanGizi"] = NutritionRecord("Monitoring Asuhan Gizi", "monitoring_asuhan_gizi",
            ("monitoring", "Monitoring"), ("evaluasi", "Evaluasi")),
        ["btnCatatanADIMEGizi"] = NutritionRecord("Catatan ADIME Gizi", "catatan_adime_gizi",
            ("asesmen", "Assessment"), ("diagnosis", "Diagnosis"), ("intervensi", "Intervensi"),
            ("monitoring", "Monitoring"), ("evaluasi", "Evaluasi"), ("instruksi", "Instruksi")),
        ["btnSkriningNutrisiDewasa"] = NutritionRecord("Skrining Nutrisi Pasien Dewasa", "skrining_nutrisi_dewasa",
            ("td", "Tensi"), ("hr", "Nadi"), ("rr", "Pernapasan"), ("suhu", "Suhu"), ("bb", "BB"), ("tbpb", "TB/PB"),
            ("spo2", "SpO₂"), ("alergi", "Alergi"), ("sg1", "Skrining 1"), ("nilai1", "Nilai 1"),
            ("sg2", "Skrining 2"), ("nilai2", "Nilai 2"), ("total_hasil", "Total")),
        ["btnSkriningNutrisiLansia"] = NutritionRecord("Skrining Nutrisi Pasien Lansia", "skrining_nutrisi_lansia",
            ("td", "Tensi"), ("hr", "Nadi"), ("rr", "Pernapasan"), ("suhu", "Suhu"), ("bb", "BB"), ("tbpb", "TB/PB"),
            ("spo2", "SpO₂"), ("alergi", "Alergi"), ("sg1", "Asupan Makan"), ("nilai1", "Nilai 1"),
            ("sg2", "Penurunan BB"), ("nilai2", "Nilai 2"), ("sg3", "Mobilitas"), ("nilai3", "Nilai 3"),
            ("sg4", "Stres/Penyakit"), ("nilai4", "Nilai 4"), ("sg5", "Kognitif"), ("nilai5", "Nilai 5"),
            ("sg6", "IMT/Lingkar Betis"), ("nilai6", "Nilai 6"), ("total_hasil", "Total"), ("skor_nutrisi", "Status Gizi")),
        ["btnSkriningNutrisiAnak"] = NutritionRecord("Skrining Nutrisi Pasien Anak", "skrining_nutrisi_anak",
            ("td", "Tensi"), ("hr", "Nadi"), ("rr", "Pernapasan"), ("suhu", "Suhu"), ("bb", "BB"), ("tbpb", "TB/PB"),
            ("spo2", "SpO₂"), ("alergi", "Alergi"), ("sg1", "Skrining 1"), ("nilai1", "Nilai 1"),
            ("sg2", "Skrining 2"), ("nilai2", "Nilai 2"), ("sg3", "Skrining 3"), ("nilai3", "Nilai 3"),
            ("sg4", "Skrining 4"), ("nilai4", "Nilai 4"), ("total_hasil", "Total"), ("skor_nutrisi", "Status Gizi"),
            ("diketahui_dietisien", "Diketahui Dietisien"), ("keterangan_diketahui_dietisien", "Keterangan Dietisien")),
        ["btnSkriningGiziKehamilan"] = NutritionRecord("Skrining Gizi Kehamilan", "skrining_gizi_kehamilan",
            ("parameter1", "Parameter 1"), ("skor1", "Skor 1"), ("parameter2", "Parameter 2"), ("skor2", "Skor 2"),
            ("parameter3", "Parameter 3"), ("skor3", "Skor 3"), ("parameter4", "Parameter 4"), ("skor4", "Skor 4"),
            ("nilai_skor", "Nilai Skor"), ("keterangan", "Keterangan")),

        ["btnPenilaianAwalKeperawatanRalan"] = NursingAssessment("Awal Keperawatan Rawat Jalan",
            "penilaian_awal_keperawatan_ralan", ("informasi", "Informasi"), ("td", "Tensi"), ("nadi", "Nadi"),
            ("rr", "Pernapasan"), ("suhu", "Suhu"), ("gcs", "GCS"), ("bb", "BB"), ("tb", "TB"), ("bmi", "BMI"),
            ("keluhan_utama", "Keluhan Utama"), ("rpd", "Riwayat Penyakit"), ("rpk", "Riwayat Keluarga"),
            ("rpo", "Riwayat Obat"), ("alergi", "Alergi")),
        ["btnPenilaianAwalKeperawatanRanap"] = NursingAssessment("Awal Keperawatan Rawat Inap",
            "penilaian_awal_keperawatan_ranap", ("informasi", "Informasi"), ("ket_informasi", "Keterangan Informasi"),
            ("tiba_diruang_rawat", "Tiba di Ruang Rawat"), ("kasus_trauma", "Kasus"), ("cara_masuk", "Cara Masuk"),
            ("rps", "Riwayat Penyakit Sekarang"), ("rpd", "Riwayat Penyakit Dahulu"), ("rpk", "Riwayat Keluarga"),
            ("rpo", "Riwayat Obat"), ("riwayat_pembedahan", "Riwayat Pembedahan"),
            ("riwayat_dirawat_dirs", "Riwayat Dirawat"), ("riwayat_alergi", "Riwayat Alergi")),
        ["btnPenilaianAwalKeperawatanIGD"] = NursingAssessment("Awal Keperawatan IGD",
            "penilaian_awal_keperawatan_igd", ("informasi", "Informasi"), ("keluhan_utama", "Keluhan Utama"),
            ("rpd", "Riwayat Penyakit"), ("rpo", "Riwayat Obat"), ("status_kehamilan", "Status Kehamilan"),
            ("gravida", "Gravida"), ("para", "Para"), ("abortus", "Abortus"), ("tekanan", "Keluhan Tekanan"),
            ("pupil", "Pupil"), ("neurosensorik", "Neurosensorik"), ("integumen", "Integumen"),
            ("turgor", "Turgor"), ("edema", "Edema"), ("mukosa", "Mukosa"), ("perdarahan", "Perdarahan"),
            ("intoksikasi", "Intoksikasi")),
        ["btnPenilaianAwalKeperawatanGigi"] = NursingAssessment("Awal Keperawatan Gigi",
            "penilaian_awal_keperawatan_gigi", ("informasi", "Informasi"), ("td", "Tensi"), ("nadi", "Nadi"),
            ("rr", "Pernapasan"), ("suhu", "Suhu"), ("bb", "BB"), ("tb", "TB"), ("bmi", "BMI"),
            ("keluhan_utama", "Keluhan Utama"), ("riwayat_penyakit", "Riwayat Penyakit"),
            ("ket_riwayat_penyakit", "Keterangan Riwayat"), ("alergi", "Alergi"),
            ("riwayat_perawatan_gigi", "Riwayat Perawatan Gigi"), ("kebiasaan_sikat_gigi", "Kebiasaan Sikat Gigi"),
            ("kebiasaan_lain", "Kebiasaan Lain"), ("obat_yang_diminum_saatini", "Obat Saat Ini")),
        ["btnPenilaianAwalKeperawatanKebidanan"] = NursingAssessment("Awal Keperawatan Kebidanan Rawat Jalan",
            "penilaian_awal_keperawatan_kebidanan", ("informasi", "Informasi"), ("td", "Tensi"), ("nadi", "Nadi"),
            ("rr", "Pernapasan"), ("suhu", "Suhu"), ("gcs", "GCS"), ("bb", "BB"), ("tb", "TB"), ("lila", "LILA"),
            ("bmi", "BMI"), ("tfu", "TFU"), ("tbj", "TBJ"), ("letak", "Letak"), ("presentasi", "Presentasi"),
            ("penurunan", "Penurunan"), ("his", "HIS"), ("kekuatan", "Kekuatan"), ("lamanya", "Lamanya"), ("bjj", "BJJ")),
        ["btnPenilaianAwalKeperawatanKebidananRanap"] = NursingAssessment("Awal Keperawatan Kebidanan Rawat Inap",
            "penilaian_awal_keperawatan_kebidanan_ranap", ("informasi", "Informasi"), ("tiba_diruang_rawat", "Tiba di Ruang Rawat"),
            ("cara_masuk", "Cara Masuk"), ("keluhan", "Keluhan"), ("rpk", "Riwayat Penyakit Keluarga"),
            ("psk", "Riwayat Psikososial"), ("rp", "Riwayat Penyakit"), ("alergi", "Alergi"),
            ("komplikasi_sebelumnya", "Komplikasi Sebelumnya"), ("keterangan_komplikasi_sebelumnya", "Keterangan Komplikasi"),
            ("riwayat_mens_umur", "Usia Menstruasi"), ("riwayat_mens_lamanya", "Lama Menstruasi"),
            ("riwayat_mens_banyaknya", "Jumlah Menstruasi"), ("riwayat_mens_siklus", "Siklus Menstruasi")),
        ["btnPenilaianAwalRalanBayi"] = NursingAssessment("Awal Keperawatan Rawat Jalan Bayi/Anak",
            "penilaian_awal_keperawatan_ralan_bayi", ("informasi", "Informasi"), ("td", "Tensi"), ("nadi", "Nadi"),
            ("rr", "Pernapasan"), ("suhu", "Suhu"), ("gcs", "GCS"), ("bb", "BB"), ("tb", "TB"),
            ("lp", "Lingkar Perut"), ("lk", "Lingkar Kepala"), ("ld", "Lingkar Dada"), ("keluhan_utama", "Keluhan Utama"),
            ("rpd", "Riwayat Penyakit"), ("rpk", "Riwayat Keluarga"), ("rpo", "Riwayat Obat"), ("alergi", "Alergi"),
            ("anakke", "Anak Ke"), ("darisaudara", "Dari Saudara"), ("caralahir", "Cara Lahir"),
            ("umurkelahiran", "Umur Kelahiran"), ("kelainanbawaan", "Kelainan Bawaan"), ("usiatengkurap", "Usia Tengkurap"),
            ("usiaduduk", "Usia Duduk"), ("usiaberdiri", "Usia Berdiri"), ("usiaberjalan", "Usia Berjalan"),
            ("usiabicara", "Usia Bicara"), ("gangguanemosi", "Gangguan Emosi")),
        ["btnPenilaianAwalKeperawatanRanapBayiAnak"] = NursingAssessment("Awal Keperawatan Rawat Inap Bayi/Anak",
            "penilaian_awal_keperawatan_ranap_bayi", ("informasi", "Informasi"), ("ket_informasi", "Keterangan Informasi"),
            ("tiba_diruang_rawat", "Tiba di Ruang Rawat"), ("rps", "Riwayat Penyakit Sekarang"), ("rpd", "Riwayat Penyakit Dahulu"),
            ("rpk", "Riwayat Keluarga"), ("rpo", "Riwayat Obat"), ("alergi", "Alergi"),
            ("tumbuh_kembang_tengkurap", "Tengkurap"), ("tumbuh_kembang_duduk", "Duduk"),
            ("tumbuh_kembang_berdiri", "Berdiri"), ("tumbuh_kembang_berjalan", "Berjalan"),
            ("tumbuh_kembang_bicara", "Bicara"), ("tumbuh_kembang_gangguan_emosi", "Gangguan Emosi"),
            ("persalinan_anakke", "Anak Ke"), ("persalinan_kelahiran", "Cara Kelahiran"),
            ("persalinan_umur_kelahiran", "Umur Kelahiran"), ("persalinan_bb_lahir", "BB Lahir"), ("persalinan_pb_lahir", "Panjang Lahir"),
            ("fisik_kesadaran", "Kesadaran"), ("fisik_gcs", "GCS"), ("fisik_td", "Tensi"), ("fisik_rr", "Pernapasan"),
            ("fisik_suhu", "Suhu"), ("fisik_nadi", "Nadi"), ("fisik_bb", "BB"), ("fisik_tb", "TB")),
        ["btnPenilaianAwalKeperawatanRalanPsikiatri"] = NursingAssessment("Awal Keperawatan Rawat Jalan Psikiatri",
            "penilaian_awal_keperawatan_ralan_psikiatri", ("informasi", "Informasi"), ("keluhan_utama", "Keluhan Utama"),
            ("rkd_sakit_sejak", "Sakit Sejak"), ("rkd_keluhan", "Riwayat Keluhan"), ("rkd_berobat", "Riwayat Berobat"),
            ("rkd_hasil_pengobatan", "Hasil Pengobatan"), ("fp_putus_obat", "Putus Obat"), ("fp_ekonomi", "Masalah Ekonomi"),
            ("fp_masalah_fisik", "Masalah Fisik"), ("fp_masalah_psikososial", "Masalah Psikososial"),
            ("rh_keluarga", "Riwayat Keluarga"), ("resiko_bunuh_diri", "Risiko Bunuh Diri"),
            ("rbd_ide", "Ide Bunuh Diri"), ("rbd_rencana", "Rencana Bunuh Diri"), ("rbd_percobaan", "Percobaan"),
            ("rbd_keinginan", "Keinginan"), ("rpo_penggunaan", "Penggunaan Obat"), ("rpo_napza", "Penggunaan NAPZA"),
            ("ket_lama_pemakaian", "Lama Pemakaian"), ("rpo_merokok", "Merokok")),
        ["btnPenilaianAwalKeperawatanRalanGeriatri"] = NursingAssessment("Awal Keperawatan Rawat Jalan Geriatri",
            "penilaian_awal_keperawatan_ralan_geriatri", ("informasi", "Informasi"), ("td", "Tensi"), ("nadi", "Nadi"),
            ("rr", "Pernapasan"), ("suhu", "Suhu"), ("gcs", "GCS"), ("bb", "BB"), ("tb", "TB"), ("bmi", "BMI"),
            ("keluhan_utama", "Keluhan Utama"), ("rpd", "Riwayat Penyakit"), ("rpk", "Riwayat Keluarga"),
            ("rpo", "Riwayat Obat"), ("alergi", "Alergi"), ("adl", "ADL"), ("status_psiko", "Status Psikologis"),
            ("hub_keluarga", "Hubungan Keluarga"), ("tinggal_dengan", "Tinggal Dengan"), ("ekonomi", "Ekonomi"),
            ("hasil", "Hasil Risiko Jatuh"), ("sg1", "Skrining Nutrisi 1"), ("sg2", "Skrining Nutrisi 2"),
            ("total_hasil", "Total Nutrisi"), ("nyeri", "Nyeri")),
        ["btnPenilaianAwalKeperawatanRanapNeonatus"] = NursingAssessment("Awal Keperawatan Rawat Inap Neonatus",
            "penilaian_awal_keperawatan_ranap_neonatus", ("asal_pasien", "Asal Pasien"), ("cara_masuk", "Cara Masuk"),
            ("diperoleh_dari", "Informasi dari"), ("hubungan_dengan_pasien", "Hubungan"), ("keluhan_utama", "Keluhan Utama"),
            ("prenatal_g", "Gravida"), ("prenatal_p", "Para"), ("prenatal_a", "Abortus"), ("prenatal_uk", "Usia Kehamilan"),
            ("prenatal_riwayat_penyakit_ibu", "Penyakit Ibu"), ("prenatal_pernah_dirawat", "Ibu Pernah Dirawat"),
            ("prenatal_status_gizi_ibu", "Status Gizi Ibu"), ("intranatal_kondisi_lahir", "Kondisi Lahir"),
            ("intranatal_cara_persalinan", "Cara Persalinan"), ("intranatal_apgar", "Apgar"), ("intranatal_letak", "Letak"),
            ("intranatal_ketuban", "Ketuban"), ("intranatal_bb", "BB Lahir"), ("intranatal_pb", "Panjang Lahir"),
            ("intranatal_lk", "Lingkar Kepala"), ("intranatal_ld", "Lingkar Dada"), ("intranatal_lp", "Lingkar Perut"),
            ("risiko_infeksi_mayor", "Risiko Infeksi Mayor"), ("risiko_infeksi_minor", "Risiko Infeksi Minor"),
            ("kebutuhan_biologis_nutrisi", "Nutrisi"), ("kebutuhan_biologis_nutrisi_frekuensi", "Frekuensi Nutrisi")),
        ["btnPenilaianAwalMedisIGD"] = NursingAssessment("Awal Medis IGD", "penilaian_medis_igd",
            ("anamnesis", "Anamnesis"), ("hubungan", "Hubungan Informan"), ("keluhan_utama", "Keluhan Utama"),
            ("rps", "Riwayat Penyakit Sekarang"), ("rpd", "Riwayat Penyakit Dahulu"), ("rpk", "Riwayat Keluarga"),
            ("rpo", "Riwayat Obat"), ("alergi", "Alergi"), ("keadaan", "Keadaan Umum"), ("gcs", "GCS"),
            ("kesadaran", "Kesadaran"), ("td", "Tensi"), ("nadi", "Nadi"), ("rr", "Pernapasan"),
            ("suhu", "Suhu"), ("spo", "SpO₂"), ("bb", "BB"), ("tb", "TB"), ("kepala", "Kepala"),
            ("mata", "Mata"), ("gigi", "Gigi"), ("leher", "Leher"), ("thoraks", "Thoraks"),
            ("abdomen", "Abdomen"), ("genital", "Genital"), ("ekstremitas", "Ekstremitas"),
            ("ket_fisik", "Keterangan Fisik"), ("ket_lokalis", "Keterangan Lokalis"), ("ekg", "EKG"), ("rad", "Radiologi")),
        ["btnPenilaianAwalMedisRalanIGDPsikiatri"] = NursingAssessment("Awal Medis IGD Psikiatri",
            "penilaian_medis_ralan_gawat_darurat_psikiatri", ("anamnesis", "Anamnesis"), ("hubungan", "Hubungan Informan"),
            ("keluhan_utama", "Keluhan Utama"), ("gejala_menyertai", "Gejala Menyertai"), ("faktor_pencetus", "Faktor Pencetus"),
            ("riwayat_penyakit_dahulu", "Riwayat Penyakit Dahulu"), ("keterangan_riwayat_penyakit_dahulu", "Keterangan Riwayat"),
            ("riwayat_kehamilan", "Riwayat Kehamilan"), ("riwayat_sosial", "Riwayat Sosial"),
            ("riwayat_pekerjaan", "Riwayat Pekerjaan"), ("riwayat_obat_diminum", "Riwayat Obat"),
            ("faktor_kepribadian_premorbid", "Kepribadian Premorbid"), ("faktor_keturunan", "Faktor Keturunan"),
            ("faktor_organik", "Faktor Organik"), ("riwayat_alergi", "Alergi"), ("fisik_kesadaran", "Kesadaran"),
            ("fisik_td", "Tensi"), ("fisik_rr", "Pernapasan"), ("fisik_suhu", "Suhu"), ("fisik_nyeri", "Nyeri"),
            ("fisik_nadi", "Nadi"), ("fisik_bb", "BB"), ("fisik_tb", "TB"), ("fisik_status_nutrisi", "Status Nutrisi")),
        ["btnPenilaianAwalMedisRalan"] = NursingAssessment("Awal Medis Rawat Jalan", "penilaian_medis_ralan",
            ("anamnesis", "Anamnesis"), ("hubungan", "Hubungan Informan"), ("keluhan_utama", "Keluhan Utama"),
            ("rps", "Riwayat Penyakit Sekarang"), ("rpd", "Riwayat Penyakit Dahulu"), ("rpk", "Riwayat Keluarga"),
            ("rpo", "Riwayat Obat"), ("alergi", "Alergi"), ("keadaan", "Keadaan Umum"), ("gcs", "GCS"),
            ("kesadaran", "Kesadaran"), ("td", "Tensi"), ("nadi", "Nadi"), ("rr", "Pernapasan"), ("suhu", "Suhu"),
            ("spo", "SpO₂"), ("bb", "BB"), ("tb", "TB"), ("kepala", "Kepala"), ("gigi", "Gigi"), ("tht", "THT"),
            ("thoraks", "Thoraks"), ("abdomen", "Abdomen"), ("genital", "Genital"), ("ekstremitas", "Ekstremitas"),
            ("kulit", "Kulit"), ("ket_fisik", "Keterangan Fisik"), ("ket_lokalis", "Keterangan Lokalis"),
            ("penunjang", "Penunjang"), ("diagnosis", "Diagnosis")),
        ["btnPenilaianAwalMedisRanap"] = NursingAssessment("Awal Medis Rawat Inap", "penilaian_medis_ranap",
            ("anamnesis", "Anamnesis"), ("hubungan", "Hubungan Informan"), ("keluhan_utama", "Keluhan Utama"),
            ("rps", "Riwayat Penyakit Sekarang"), ("rpd", "Riwayat Penyakit Dahulu"), ("rpk", "Riwayat Keluarga"),
            ("rpo", "Riwayat Obat"), ("alergi", "Alergi"), ("keadaan", "Keadaan Umum"), ("gcs", "GCS"),
            ("kesadaran", "Kesadaran"), ("td", "Tensi"), ("nadi", "Nadi"), ("rr", "Pernapasan"), ("suhu", "Suhu"),
            ("spo", "SpO₂"), ("bb", "BB"), ("tb", "TB"), ("kepala", "Kepala"), ("mata", "Mata"), ("gigi", "Gigi"),
            ("tht", "THT"), ("thoraks", "Thoraks"), ("jantung", "Jantung"), ("paru", "Paru"), ("abdomen", "Abdomen"),
            ("genital", "Genital"), ("ekstremitas", "Ekstremitas"), ("kulit", "Kulit"), ("ket_fisik", "Keterangan Fisik")),
        ["btnPenilaianAwalMedisRanapNeonatus"] = NursingAssessment("Awal Medis Rawat Inap Neonatus",
            "penilaian_medis_ranap_neonatus", ("no_rkm_medis_ibu", "No. RM Ibu"), ("g", "Gravida"), ("p", "Para"),
            ("a", "Abortus"), ("hidup", "Anak Hidup"), ("usiahamil", "Usia Kehamilan"), ("hbsag", "HBsAg"),
            ("hiv", "HIV"), ("syphilis", "Sifilis"), ("riwayat_obstetri_ibu", "Riwayat Obstetri Ibu"),
            ("faktor_risiko_neonatal", "Faktor Risiko Neonatal"), ("tanggal_persalinan", "Tanggal Persalinan"),
            ("bersalin_di", "Tempat Persalinan"), ("inisiasi_menyusui", "Inisiasi Menyusui"), ("jenis_persalinan", "Jenis Persalinan"),
            ("aterm", "Aterm"), ("bernafas", "Bernapas"), ("tanus_otot", "Tonus Otot"), ("cairan_amnion", "Cairan Amnion"),
            ("f1", "Apgar F1"), ("u1", "Apgar U1"), ("t1", "Apgar T1"), ("r1", "Apgar R1"), ("w1", "Apgar W1"), ("n1", "Apgar N1")),
        ["btnPenilaianBayiBaruLahir"] = NursingAssessment("Pengkajian Bayi Baru Lahir", "penilaian_bayi_baru_lahir",
            ("penyakit_diderita_ibu", "Penyakit Ibu"), ("keterangan_penyakit_diderita_ibu", "Keterangan Penyakit Ibu"),
            ("obat_dikonsumsi_selama_kehamilan", "Obat Selama Hamil"), ("perawatan_antenatal", "Perawatan Antenatal"),
            ("terdaftar_ekohort", "Terdaftar Ekohort"), ("penyulit_kehamilan", "Penyulit Kehamilan"),
            ("alergi", "Alergi"), ("umur_kehamilan", "Umur Kehamilan"), ("kehamilan", "Jenis Kehamilan"),
            ("urutan_kehamilan", "Urutan Kehamilan"), ("jumlah_air_ketuban", "Jumlah Air Ketuban"),
            ("warna_air_ketuban", "Warna Air Ketuban"), ("bau_air_ketuban", "Bau Air Ketuban"), ("letak_bayi", "Letak Bayi"),
            ("macam_persalinan", "Cara Persalinan"), ("indikasi_persalinan_operatif", "Indikasi Operatif"),
            ("lama_gawat_janin", "Lama Gawat Janin"), ("obat_selama_persalinan", "Obat Selama Persalinan"),
            ("berat_placenta", "Berat Plasenta"), ("kelainan_placenta", "Kelainan Plasenta")),
    };
}
