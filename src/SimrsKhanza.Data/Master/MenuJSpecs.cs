namespace SimrsKhanza.Data.Master;

public static partial class MenuJSpecs
{
    public static IReadOnlyDictionary<string, MasterSpec> Masters { get; } = new Dictionary<string, MasterSpec>
    {
        ["btnICD9"] = new("ICD 9", "icd9",
            [new("kode", "Kode", MaxLen: 8, Key: true, Required: true),
             new("deskripsi_pendek", "Deskripsi Pendek", MaxLen: 40),
             new("deskripsi_panjang", "Deskripsi Panjang", FieldKind.Memo, MaxLen: 350),
             Flag("validcode", "Kode Valid", "0", "1"), Flag("accpdx", "Diagnosis Utama", "N", "Y"),
             Flag("im", "IM", "0", "1")],
            """
            SELECT * FROM icd9
            WHERE kode LIKE @p0 OR deskripsi_pendek LIKE @p0 OR deskripsi_panjang LIKE @p0
            ORDER BY kode
            """,
            [("kode", "Kode"), ("deskripsi_pendek", "Deskripsi Pendek"), ("deskripsi_panjang", "Deskripsi Panjang")]),

        ["btnICD"] = new("ICD 10", "penyakit",
            [new("kd_penyakit", "Kode", MaxLen: 15, Key: true, Required: true),
             new("nm_penyakit", "Penyakit", MaxLen: 250, Required: true),
             new("ciri_ciri", "Ciri-ciri", FieldKind.Memo),
             new("keterangan", "Keterangan", MaxLen: 60),
             new("kd_ktg", "Kategori", FieldKind.Lookup, LookupSql:
                 "SELECT kd_ktg AS kode, nm_kategori AS nama FROM kategori_penyakit ORDER BY nm_kategori"),
             Flag("status", "Status", "Tidak Menular", "Menular"), Flag("validcode", "Kode Valid", "0", "1"),
             Flag("accpdx", "Diagnosis Utama", "N", "Y"), Flag("asterisk", "Asterisk", "0", "1"),
             Flag("im", "IM", "0", "1")],
            """
            SELECT p.*, k.nm_kategori FROM penyakit p
            LEFT JOIN kategori_penyakit k ON k.kd_ktg = p.kd_ktg
            WHERE p.kd_penyakit LIKE @p0 OR p.nm_penyakit LIKE @p0
            ORDER BY p.kd_penyakit
            """,
            [("kd_penyakit", "Kode"), ("nm_penyakit", "Penyakit"), ("nm_kategori", "Kategori"), ("status", "Status")])
        {
            BeforeSave = (_, values, _) =>
            {
                if (values["kd_ktg"] is "") values["kd_ktg"] = null;
                return Task.CompletedTask;
            }
        },
    };

    public static IReadOnlyDictionary<string, ReportSpec> Reports { get; } = new Dictionary<string, ReportSpec>
    {
        ["btnRegistrasiPoliPerTanggal"] = ClinicRegistrations(),
        ["btnRekapMutasiBerkas"] = MedicalRecordMovement(),
        ["btnLamaPenyiapanRM"] = MedicalRecordPreparation(),
        ["btnDosisRadiologi"] = RadiologyDose(),
        ["btnJumlahPorsiDiet"] = DietCounts("Rekap Bulanan Porsi Diet", "b.nm_bangsal", "Bangsal", ReportPeriod.Month),
        ["btnJumlahMacamDiet"] = DietCounts("Rekap Bulanan Macam Diet", "d.nama_diet", "Diet", ReportPeriod.Month),
        ["btnRekapPermintaanDiet"] = DietCounts("Rekap Permintaan Diet", "k.kelas", "Kelas", ReportPeriod.DateRange),
        ["btnSisaDietPasien"] = DietLeftovers(),
        ["btnPerujukLabPerTahun"] = AnnualActivity("Perujuk Lab Per Tahun", "periksa_lab", "tgl_periksa",
            "dokter", "dokter_perujuk", "kd_dokter", "nm_dokter", "Dokter Perujuk"),
        ["btnPerujukRadiologiPerTahun"] = AnnualActivity("Perujuk Radiologi Per Tahun", "periksa_radiologi", "tgl_periksa",
            "dokter", "dokter_perujuk", "kd_dokter", "nm_dokter", "Dokter Perujuk"),
        ["btnRekapRadiologiPerTahun"] = AnnualActivity("Rekap Radiologi Per Tahun", "periksa_radiologi", "tgl_periksa",
            "jns_perawatan_radiologi", "kd_jenis_prw", "kd_jenis_prw", "nm_perawatan", "Tindakan"),
        ["btnOperasiPerBulan"] = AnnualActivity("Operasi Per Bulan", "operasi", "tgl_operasi",
            "paket_operasi", "kode_paket", "kode_paket", "nm_perawatan", "Paket Operasi"),
        ["btnLamaPelayananLab"] = ServiceStages("Lama Pelayanan Lab PK", "permintaan_lab",
            "noorder", "tgl_permintaan", "jam_permintaan", "tgl_sampel", "jam_sampel", "tgl_hasil", "jam_hasil", "Sampel", "Hasil"),
        ["btnLamaPelayananLabPA"] = ServiceStages("Lama Pelayanan Lab PA", "permintaan_labpa",
            "noorder", "tgl_permintaan", "jam_permintaan", "tgl_sampel", "jam_sampel", "tgl_hasil", "jam_hasil", "Sampel", "Hasil"),
        ["btnLamaPelayananLabMB"] = ServiceStages("Lama Pelayanan Lab MB", "permintaan_labmb",
            "noorder", "tgl_permintaan", "jam_permintaan", "tgl_sampel", "jam_sampel", "tgl_hasil", "jam_hasil", "Sampel", "Hasil"),
        ["btnLamaPelayananRadiologi"] = ServiceStages("Lama Pelayanan Radiologi", "permintaan_radiologi",
            "noorder", "tgl_permintaan", "jam_permintaan", "tgl_sampel", "jam_sampel", "tgl_hasil", "jam_hasil", "Sampel", "Hasil"),
        ["btnLamaPelayananApotek"] = ServiceStages("Lama Pelayanan Apotek", "resep_obat",
            "no_resep", "tgl_peresepan", "jam_peresepan", "tgl_perawatan", "jam", "tgl_penyerahan", "jam_penyerahan", "Validasi", "Penyerahan"),
        ["btnLamaPelayananRalan"] = RegistrationToService("Lama Pelayanan Ralan", "Nota Terbit",
            "nota_jalan", "tanggal", "jam"),
        ["btnLamaPelayananPoli"] = RegistrationToService("Lama Pelayanan Poli", "Pemeriksaan Pertama",
            "pemeriksaan_ralan", "tgl_perawatan", "jam_rawat"),
        ["btnLamaOperasi"] = OperationDuration(),
        ["btnLamaPelayananCSSD"] = CssdDuration(),
        ["btnRekapLabPerTahun"] = AnnualLabActivity(),
        ["btnKunjunganLabRalan"] = ExamVisits("Lab", "periksa_lab", "Ralan"),
        ["btnKunjunganLabRanap"] = ExamVisits("Lab", "periksa_lab", "Ranap"),
        ["btnKunjunganRadRalan"] = ExamVisits("Radiologi", "periksa_radiologi", "Ralan"),
        ["btnKunjunganRadRanap"] = ExamVisits("Radiologi", "periksa_radiologi", "Ranap"),
        ["btnFrekuensiRalan"] = DiseaseFrequency("Frekuensi Penyakit Ralan", "Ralan"),
        ["btnFrekuensiRanap"] = DiseaseFrequency("Frekuensi Penyakit Ranap", "Ranap"),
        ["btnPnyMenularRalan"] = DiseaseFrequency("Pny Menular Ralan", "Ralan", "Menular"),
        ["btnPnyMenularRanap"] = DiseaseFrequency("Pny Menular Ranap", "Ranap", "Menular"),
        ["btnPnyTakMenularRalan"] = DiseaseFrequency("Pny Tdk Menular Ralan", "Ralan", "Tidak Menular"),
        ["btnPnyTakMenularRanap"] = DiseaseFrequency("Pny Tdk Menular Ranap", "Ranap", "Tidak Menular"),
        ["btnPoliAsalPasienRanap"] = InpatientOrigin("Poli Asal Pasien Ranap", "poliklinik", "kd_poli", "nm_poli", "Poli Asal"),
        ["btnDokterAsalPasienRanap"] = InpatientOrigin("Dokter Asal Pasien Ranap", "dokter", "kd_dokter", "nm_dokter", "Dokter Asal"),
        ["btnPembatalanPeriksaDokter"] = new("Pembatalan Periksa Per Dokter",
            """
            SELECT d.kd_dokter, d.nm_dokter,
                   COUNT(CASE WHEN r.stts = 'Batal' THEN 1 END) AS batal,
                   COUNT(CASE WHEN r.stts <> 'Batal' THEN 1 END) AS tidak_batal,
                   COUNT(*) AS jumlah
            FROM reg_periksa r
            JOIN dokter d ON d.kd_dokter = r.kd_dokter
            WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
              AND (d.kd_dokter LIKE @p0 OR d.nm_dokter LIKE @p0)
            GROUP BY d.kd_dokter, d.nm_dokter
            ORDER BY d.nm_dokter, d.kd_dokter
            """,
            [("kd_dokter", "Kode Dokter"), ("nm_dokter", "Dokter"), ("batal", "Batal"),
             ("tidak_batal", "Tidak Batal"), ("jumlah", "Total Registrasi")],
            ReportPeriod.DateRange, "Registrasi berdasarkan tanggal dan dokter pada saat pendaftaran.")
            { SearchWatermark = "Kode atau nama dokter" },
    };

    private static MasterField Flag(string column, string label, string first, string second) =>
        new(column, label, FieldKind.Choice, Choices: [first, second], Required: true) { Default = first };

    private static ReportSpec ExamVisits(string service, string table, string care) => new(
        $"Kunjungan {service} {care}",
        $"""
        SELECT e.tgl_periksa, e.jam, e.no_rawat, r.no_rkm_medis, p.nm_pasien,
               COUNT(*) AS tindakan
        FROM {table} e
        JOIN reg_periksa r ON r.no_rawat = e.no_rawat
        JOIN pasien p ON p.no_rkm_medis = r.no_rkm_medis
        WHERE e.tgl_periksa BETWEEN @p1 AND @p2 AND e.status = '{care}'
          AND (r.no_rkm_medis LIKE @p0 OR e.no_rawat LIKE @p0 OR p.nm_pasien LIKE @p0)
        GROUP BY e.tgl_periksa, e.jam, e.no_rawat, r.no_rkm_medis, p.nm_pasien
        ORDER BY e.tgl_periksa, e.jam, e.no_rawat
        """,
        [("tgl_periksa", "Tanggal Periksa"), ("jam", "Jam"), ("no_rawat", "No. Rawat"),
         ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("tindakan", "Jumlah Tindakan")],
        ReportPeriod.DateRange, "Satu kunjungan per nomor rawat, tanggal, dan jam pemeriksaan.")
        { SearchWatermark = "Pasien, No. RM, atau No. Rawat" };

    private static ReportSpec DiseaseFrequency(string title, string care, string? classification = null)
    {
        var classificationFilter = classification is null ? "" : $"AND p.status = '{classification}'";
        var dateFilter = care == "Ralan"
            ? "AND r.tgl_registrasi BETWEEN @p1 AND @p2"
            : """
              AND EXISTS (
                  SELECT 1 FROM kamar_inap k
                  WHERE k.no_rawat = r.no_rawat AND k.tgl_keluar BETWEEN @p1 AND @p2
                    AND k.stts_pulang NOT IN ('Pindah Kamar', '-')
              )
              """;
        var period = care == "Ralan" ? "tanggal registrasi" : "tanggal keluar rawat inap";
        return new(title,
            $"""
            SELECT p.kd_penyakit, p.nm_penyakit,
                   COUNT(CASE WHEN dp.status_penyakit = 'Baru' THEN 1 END) AS baru,
                   COUNT(CASE WHEN dp.status_penyakit = 'Lama' THEN 1 END) AS lama,
                   COUNT(*) AS jumlah
            FROM diagnosa_pasien dp
            JOIN penyakit p ON p.kd_penyakit = dp.kd_penyakit
            JOIN reg_periksa r ON r.no_rawat = dp.no_rawat
            WHERE dp.status = '{care}' AND r.stts <> 'Batal'
              {dateFilter}
              {classificationFilter}
              AND (p.kd_penyakit LIKE @p0 OR p.nm_penyakit LIKE @p0)
            GROUP BY p.kd_penyakit, p.nm_penyakit
            ORDER BY jumlah DESC, p.kd_penyakit
            """,
            [("kd_penyakit", "Kode ICD 10"), ("nm_penyakit", "Penyakit"),
             ("baru", "Kasus Baru"), ("lama", "Kasus Lama"), ("jumlah", "Jumlah Diagnosis")],
            ReportPeriod.DateRange, $"Rekap diagnosis menurut {period}. Satu nomor rawat dapat memiliki beberapa diagnosis.")
            { SearchWatermark = "Kode atau nama penyakit" };
    }

    private static ReportSpec InpatientOrigin(string title, string table, string code, string name, string label) => new(
        title,
        $"""
        SELECT a.{code} AS kode, a.{name} AS nama, COUNT(*) AS jumlah
        FROM reg_periksa r
        JOIN {table} a ON a.{code} = r.{code}
        WHERE r.tgl_registrasi BETWEEN @p1 AND @p2
          AND r.status_lanjut = 'Ranap' AND r.stts <> 'Batal'
          AND (a.{code} LIKE @p0 OR a.{name} LIKE @p0)
        GROUP BY a.{code}, a.{name}
        ORDER BY jumlah DESC, a.{name}, a.{code}
        """,
        [("kode", "Kode"), ("nama", label), ("jumlah", "Jumlah Registrasi")],
        ReportPeriod.DateRange, "Registrasi yang berlanjut ke rawat inap, dikelompokkan menurut data asal pada registrasi.")
        { SearchWatermark = "Kode atau nama" };
}
