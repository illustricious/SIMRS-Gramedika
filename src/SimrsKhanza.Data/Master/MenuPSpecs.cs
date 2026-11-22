namespace SimrsKhanza.Data.Master;

/// <summary>
/// Menu P (letters, consent forms, e-patient announcements). Letter registers and reference tables are editable.
/// Patient letters and consent forms are read-only lists: Khanza fills and prints them from the visit record.
/// </summary>
public static class MenuPSpecs
{
    private static MasterField Key(string column, string label, int len) => new(column, label, MaxLen: len, Key: true);
    private static MasterField Req(string column, string label, int len) => new(column, label, MaxLen: len, Required: true);
    private static MasterField Opt(string column, string label, int len) => new(column, label, MaxLen: len);
    private static MasterField Look(string column, string label, string table, string key, string name) =>
        new(column, label, FieldKind.Lookup, Required: true,
            LookupSql: $"SELECT {key} AS kode,{name} AS nama FROM {table} ORDER BY {name},{key}");

    private static MasterSpec Reference(string title, string table, string name, int codeLen = 5) => new(title, table,
        [Key("kd", "Kode", codeLen), Req(name, "Nama", 50)],
        $"SELECT kd,`{name}` FROM `{table}` WHERE CONCAT_WS(' ',kd,`{name}`) LIKE @p0 ORDER BY `{name}`,kd",
        [("kd", "Kode"), (name, "Nama")])
    { CanDelete = false, Hint = "Kode yang dipakai surat tidak dihapus dari layar ini." };

    private static MasterField[] LetterFields(bool incoming) =>
    [
        Key("no_urut", "No. Urut", 15), Req("no_surat", "No. Surat", 50),
        .. incoming ? [Req("asal", "Asal Surat", 300)] : Array.Empty<MasterField>(),
        Req("tujuan", "Tujuan", 300), new("tgl_surat", "Tanggal Surat", FieldKind.Date), Req("perihal", "Perihal", 300),
        new(incoming ? "tgl_terima" : "tgl_kirim", incoming ? "Tanggal Terima" : "Tanggal Kirim", FieldKind.Date),
        Look("kd_lemari", "Almari", "surat_lemari", "kd", "lemari"), Look("kd_rak", "Rak", "surat_rak", "kd", "rak"),
        Look("kd_map", "Map", "surat_map", "kd", "map"), Look("kd_ruang", "Ruang", "surat_ruang", "kd", "ruang"),
        Look("kd_sifat", "Sifat", "surat_sifat", "kd", "sifat"), Opt("lampiran", "Lampiran", 300), Opt("tembusan", "Tembusan", 300),
        new("tgl_deadline_balas", "Batas Balas", FieldKind.Date), Look("kd_balas", "Status Balas", "surat_balas", "kd", "balas"),
        Opt("keterangan", "Keterangan", 300), Look("kd_status", "Status", "surat_status", "kd", "status"),
        Look("kd_klasifikasi", "Klasifikasi", "surat_klasifikasi", "kd", "klasifikasi"), Opt("file_url", "Berkas (lokasi/URL)", 500),
    ];

    private static MasterSpec Register(string title, string table, bool incoming)
    {
        var party = incoming ? "h.asal,h.tujuan" : "h.tujuan";
        var dateColumn = incoming ? "tgl_terima" : "tgl_kirim";
        return new(title, table, LetterFields(incoming),
            $"""
            SELECT h.*,l.lemari,r.rak,m.map,u.ruang,s.sifat,b.balas,st.status,k.klasifikasi FROM `{table}` h
            LEFT JOIN surat_lemari l ON l.kd=h.kd_lemari LEFT JOIN surat_rak r ON r.kd=h.kd_rak LEFT JOIN surat_map m ON m.kd=h.kd_map
            LEFT JOIN surat_ruang u ON u.kd=h.kd_ruang LEFT JOIN surat_sifat s ON s.kd=h.kd_sifat LEFT JOIN surat_balas b ON b.kd=h.kd_balas
            LEFT JOIN surat_status st ON st.kd=h.kd_status LEFT JOIN surat_klasifikasi k ON k.kd=h.kd_klasifikasi
            WHERE CONCAT_WS(' ',h.no_urut,h.no_surat,{party},h.perihal,h.keterangan,s.sifat,st.status,k.klasifikasi) LIKE @p0
            ORDER BY h.tgl_surat DESC,h.no_urut DESC
            """,
            [("no_urut", "No. Urut"), ("no_surat", "No. Surat"), .. incoming ? [("asal", "Asal")] : Array.Empty<(string, string)>(),
             ("tujuan", "Tujuan"), ("tgl_surat", "Tgl. Surat"), (dateColumn, incoming ? "Tgl. Terima" : "Tgl. Kirim"), ("perihal", "Perihal"),
             ("sifat", "Sifat"), ("klasifikasi", "Klasifikasi"), ("status", "Status"), ("balas", "Balas"),
             ("tgl_deadline_balas", "Batas Balas"), ("lemari", "Almari"), ("rak", "Rak"), ("map", "Map"), ("ruang", "Ruang"),
             ("lampiran", "Lampiran"), ("keterangan", "Keterangan")])
        {
            CanDelete = false,
            Hint = "No. urut diisi manual. Penomoran otomatis per klasifikasi, disposisi, dan kendali surat dilakukan di Khanza.",
        };
    }

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>
    {
        ["btnSuratIndeks"] = Reference("Indeks Surat", "surat_indeks", "indeks"),
        ["btnSuratMap"] = Reference("Map Surat", "surat_map", "map"),
        ["btnSuratAlmari"] = Reference("Almari Surat", "surat_lemari", "lemari"),
        ["btnSuratRak"] = Reference("Rak Surat", "surat_rak", "rak"),
        ["btnSuratRuang"] = Reference("Ruang Surat", "surat_ruang", "ruang"),
        ["btnSuratKlasifikasi"] = Reference("Klasifikasi Surat", "surat_klasifikasi", "klasifikasi"),
        ["btnSuratStatus"] = Reference("Status Surat", "surat_status", "status"),
        ["btnSuratSifat"] = Reference("Sifat Surat", "surat_sifat", "sifat"),
        ["btnSuratBalas"] = Reference("Stts Balas Surat", "surat_balas", "balas"),
        ["btnSuratMasuk"] = Register("Surat Masuk", "surat_masuk", true),
        ["btnSuratKeluar"] = Register("Surat Keluar", "surat_keluar", false),
        ["btnMasterMenolakAnjuranMedis"] = new("Master Menolak Anjuran Medis", "master_menolak_anjuran_medis",
            [Key("kode_penolakan", "Kode", 3), Req("nama_penolakan", "Alasan Penolakan", 40)],
            "SELECT kode_penolakan,nama_penolakan FROM master_menolak_anjuran_medis WHERE CONCAT_WS(' ',kode_penolakan,nama_penolakan) LIKE @p0 ORDER BY nama_penolakan",
            [("kode_penolakan", "Kode"), ("nama_penolakan", "Alasan Penolakan")])
        { CanDelete = false },
        ["btnTemplatePersetujuanPenolakanTindakan"] = new("Template Persetujuan Penolakan Tindakan", "template_persetujuan_penolakan_tindakan",
            [
                Key("kode_template", "Kode", 3), Req("diagnosa", "Diagnosa", 200), Req("tindakan", "Tindakan", 200),
                Req("indikasi_tindakan", "Indikasi Tindakan", 200), Req("tata_cara", "Tata Cara", 400), Req("tujuan", "Tujuan", 200),
                Req("risiko", "Risiko", 200), Req("komplikasi", "Komplikasi", 200), Req("prognosis", "Prognosis", 200),
                Req("alternatif_dan_risikonya", "Alternatif dan Risikonya", 200), Req("lain_lain", "Lain-lain", 200),
                MenuKSpecs.Money("biaya", "Biaya"),
            ],
            """
            SELECT * FROM template_persetujuan_penolakan_tindakan
            WHERE CONCAT_WS(' ',kode_template,diagnosa,tindakan,indikasi_tindakan,tujuan) LIKE @p0 ORDER BY tindakan,kode_template
            """,
            [("kode_template", "Kode"), ("diagnosa", "Diagnosa"), ("tindakan", "Tindakan"), ("indikasi_tindakan", "Indikasi"),
             ("tujuan", "Tujuan"), ("risiko", "Risiko"), ("komplikasi", "Komplikasi"), ("prognosis", "Prognosis"), ("biaya", "Biaya")])
        {
            CanDelete = false,
            BeforeSave = (_, v, _) =>
            {
                if (MenuKSpecs.D(v, "biaya") < 0) throw new ArgumentException("Biaya harus nol atau lebih.");
                return Task.CompletedTask;
            },
        },
        ["btnPengumumanEPasien"] = new("Pengumuman E-Pasien", "pengumuman_epasien",
            [
                new("nik", "Pegawai (NIK)", FieldKind.Lookup, Key: true, Required: true, LookupSql: MasterLookups.PegawaiNik),
                new("tanggal", "Tanggal", FieldKind.DateTime, Key: true) { DefaultFactory = () => DateTime.Now },
                Req("pengumuman", "Pengumuman", 150),
            ],
            """
            SELECT a.nik,p.nama,a.tanggal,a.pengumuman FROM pengumuman_epasien a LEFT JOIN pegawai p ON p.nik=a.nik
            WHERE CONCAT_WS(' ',a.nik,p.nama,a.pengumuman) LIKE @p0 ORDER BY a.tanggal DESC
            """,
            [("tanggal", "Tanggal"), ("nik", "NIK"), ("nama", "Pegawai"), ("pengumuman", "Pengumuman")])
        { Hint = "Pengumuman tampil di aplikasi e-pasien. Tanggal menjadi bagian kunci." },
    };

    private static (string Expr, string Alias, string Header) X(string column, string header) => ($"h.{column}", column, header);

    private static ReportSpec Letter(string title, string table, string doc, string date, bool doctor, params (string Expr, string Alias, string Header)[] extras)
    {
        var select = string.Join(",", extras.Select(e => $"{e.Expr} AS {e.Alias}"));
        var search = string.Join(",", extras.Select(e => e.Expr));
        var doctorSelect = doctor ? ",d.nm_dokter AS dokter" : "";
        var doctorJoin = doctor ? "LEFT JOIN dokter d ON d.kd_dokter=h.kd_dokter" : "";
        var doctorSearch = doctor ? ",d.nm_dokter" : "";
        var sql = $"""
            SELECT h.{doc} AS dokumen,h.{date} AS tanggal,h.no_rawat,r.no_rkm_medis,p.nm_pasien{(extras.Length > 0 ? "," + select : "")}{doctorSelect}
            FROM `{table}` h LEFT JOIN reg_periksa r ON r.no_rawat=h.no_rawat LEFT JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis {doctorJoin}
            WHERE h.{date} BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.{doc},h.no_rawat,r.no_rkm_medis,p.nm_pasien{(extras.Length > 0 ? "," + search : "")}{doctorSearch}) LIKE @p0
            ORDER BY h.{date} DESC,h.{doc}
            """;
        (string, string)[] columns =
        [
            ("dokumen", "No. Surat"), ("tanggal", "Tanggal"), ("no_rawat", "No. Rawat"), ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"),
            .. extras.Select(e => (e.Alias, e.Header)), .. doctor ? [("dokter", "Dokter")] : Array.Empty<(string, string)>(),
        ];
        return MenuKSpecs.Report(title, sql, columns, ReportPeriod.DateRange,
            "Daftar hanya-baca. Surat diisi dan dicetak di Khanza dari data kunjungan pasien.", []);
    }

    private static (string, string, string) Guardian() => X("nama_pj", "Penanggung Jawab");

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnSuratSakit"] = Letter("Surat Keterangan Sakit", "suratsakit", "no_surat", "tanggalawal", false,
            X("tanggalawal", "Mulai"), X("tanggalakhir", "Sampai"), X("lamasakit", "Lama Sakit")),
        ["btnSuratSakitPihak2"] = Letter("Surat Keterangan Sakit Pihak 2", "suratsakitpihak2", "no_surat", "tanggalawal", false,
            X("nama2", "Nama Pihak 2"), X("hubungan", "Hubungan"), X("pekerjaan", "Pekerjaan"), X("instansi", "Instansi"),
            X("tanggalawal", "Mulai"), X("tanggalakhir", "Sampai"), X("lamasakit", "Lama Sakit")),
        ["btnSuratHamil"] = Letter("Surat Hamil", "surat_hamil", "no_surat", "tanggalperiksa", false, X("hasilperiksa", "Hasil Pemeriksaan")),
        ["btnSuratCutiHamil"] = Letter("Surat Cuti Hamil", "surat_cuti_hamil", "no_surat", "terhitung_mulai", false,
            X("keterangan_hamil", "Keterangan"), X("terhitung_mulai", "Terhitung Mulai"), X("perkiraan_lahir", "Perkiraan Lahir")),
        ["btnSKDPBPJS"] = MenuKSpecs.Report("Surat Kontrol", """
            SELECT k.tahun,k.no_antrian,k.tanggal_rujukan,k.tanggal_datang,k.no_rkm_medis,p.nm_pasien,d.nm_dokter,k.diagnosa,k.terapi,
                   CONCAT_WS(' / ',NULLIF(k.alasan1,''),NULLIF(k.alasan2,'')) AS alasan,CONCAT_WS(' / ',NULLIF(k.rtl1,''),NULLIF(k.rtl2,'')) AS rtl,k.status
            FROM skdp_bpjs k LEFT JOIN pasien p ON p.no_rkm_medis=k.no_rkm_medis LEFT JOIN dokter d ON d.kd_dokter=k.kd_dokter
            WHERE k.tanggal_rujukan BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',k.no_antrian,k.no_rkm_medis,p.nm_pasien,d.nm_dokter,k.diagnosa,k.status) LIKE @p0
            ORDER BY k.tanggal_rujukan DESC,k.no_antrian
            """,
            [("tahun", "Tahun"), ("no_antrian", "No. Antrian"), ("tanggal_rujukan", "Tgl. Kontrol"), ("tanggal_datang", "Tgl. Datang"),
             ("no_rkm_medis", "No. RM"), ("nm_pasien", "Pasien"), ("nm_dokter", "Dokter"), ("diagnosa", "Diagnosa"), ("terapi", "Terapi"),
             ("alasan", "Alasan"), ("rtl", "Rencana Tindak Lanjut"), ("status", "Status")], ReportPeriod.DateRange,
            "Daftar hanya-baca. Surat kontrol dibuat di Khanza.", []),
        ["btnSuratBebasNarkoba"] = Letter("Surat Bebas Narkoba", "surat_skbn", "no_surat", "tanggalsurat", true,
            X("kategori", "Kategori"), X("keperluan", "Keperluan"), X("opiat", "Opiat"), X("ganja", "Ganja"), X("amphetamin", "Amphetamin"),
            X("methamphetamin", "Methamphetamin"), X("benzodiazepin", "Benzodiazepin"), X("cocain", "Cocain")),
        ["btnSuratKeteranganCovid"] = Letter("Surat Keterangan Covid", "surat_keterangan_covid", "no_surat", "berlakumulai", true,
            X("igm", "IgM"), X("igg", "IgG"), X("sehat", "Sehat"), X("tidaksehat", "Tidak Sehat"), X("berlakumulai", "Berlaku Mulai"),
            X("berlakuselsai", "Berlaku Sampai")),
        ["btnSuratKeteranganRawatInap"] = Letter("Surat Keterangan Rawat Inap", "surat_keterangan_rawat_inap", "no_surat", "tanggalawal", false,
            X("tanggalawal", "Mulai"), X("tanggalakhir", "Sampai")),
        ["btnSuratKeteranganSehat"] = Letter("Surat Keterangan Sehat", "surat_keterangan_sehat", "no_surat", "tanggalsurat", false,
            X("berat", "BB"), X("tinggi", "TB"), X("tensi", "Tensi"), X("suhu", "Suhu"), X("butawarna", "Buta Warna"),
            X("keperluan", "Keperluan"), X("kesimpulan", "Kesimpulan")),
        ["btnSuratBebasTBC"] = Letter("Surat Bebas TBC", "surat_bebas_tbc", "no_surat", "tanggalsurat", true, X("keperluan", "Keperluan")),
        ["btnSuratButaWarna"] = Letter("Surat Keterangan Buta Warna", "surat_buta_warna", "no_surat", "tanggalperiksa", false,
            X("hasilperiksa", "Hasil Pemeriksaan")),
        ["btnSuratBebasTato"] = Letter("Surat Bebas Tato", "surat_bebas_tato", "no_surat", "tanggalperiksa", false,
            X("hasilperiksa", "Hasil Pemeriksaan"), X("keperluan", "Keperluan")),
        ["btnSuratKeteranganLayakTerbang"] = Letter("Surat Keterangan Layak Terbang", "surat_keterangan_layak_terbang", "no_surat", "tanggal_periksa", false,
            X("kehamilan", "Kehamilan")),
        ["btnSuratKeteranganBerobat"] = Letter("Surat Keterangan Berobat", "surat_keterangan_berobat", "no_surat", "tanggal_surat", false),
        ["btnSuratKewaspadaanKesehatan"] = Letter("Surat Kewaspadaan Kesehatan", "surat_kewaspadaan_kesehatan", "no_surat", "tanggalperiksa", false,
            X("keluhan_saat_ini", "Keluhan"), X("keperluan", "Keperluan")),

        ["btnPersetujuanPenolakanTindakan"] = Letter("Persetujuan/Penolakan Tindakan", "persetujuan_penolakan_tindakan", "no_pernyataan", "tanggal", true,
            X("diagnosa", "Diagnosa"), X("tindakan", "Tindakan"), X("tujuan", "Tujuan"), X("risiko", "Risiko"),
            X("penerima_informasi", "Penerima Informasi"), X("hubungan_penerima_informasi", "Hubungan"), X("pernyataan", "Pernyataan"),
            X("saksi_keluarga", "Saksi Keluarga")),
        ["btnPersetujuanPulangAtasPermintanSendiri"] = Letter("Pulang Atas Permintaan Sendiri", "surat_pulang_atas_permintaan_sendiri", "no_surat", "tgl_pulang", false,
            X("rs_pilihan", "RS Pilihan"), Guardian(), X("hubungan", "Hubungan"), X("saksi_keluarga", "Saksi Keluarga")),
        ["btnPernyataanPasienUmum"] = Letter("Pernyataan Pasien Umum", "surat_pernyataan_pasien_umum", "no_surat", "tanggal", false,
            Guardian(), X("hubungan", "Hubungan"), X("no_telp", "Telepon")),
        ["btnPersetujuanUmum"] = Letter("Persetujuan Umum", "surat_persetujuan_umum", "no_surat", "tanggal", false,
            X("pengobatan_kepada", "Pengobatan Kepada"), X("nilai_kepercayaan", "Nilai Kepercayaan"), Guardian(),
            X("bertindak_atas", "Bertindak Atas"), X("no_telp", "Telepon")),
        ["btnPersetujuanRawatInap"] = Letter("Persetujuan Rawat Inap", "surat_persetujuan_rawat_inap", "no_surat", "tanggal", false,
            Guardian(), X("hubungan", "Hubungan"), X("ruang", "Ruang"), X("kelas", "Kelas"), X("hak_kelas", "Hak Kelas"), X("bayar_secara", "Bayar Secara")),
        ["btnPersetujuanPenundaanPelayanan"] = Letter("Persetujuan Penundaan Pelayanan", "persetujuan_penundaan_pelayanan", "no_surat", "tanggal", true,
            Guardian(), X("hubungan", "Hubungan"), X("ruang", "Ruang"), X("pelayanan_dilakukan", "Pelayanan"), X("ditunda_karena", "Ditunda Karena"),
            X("keterangan_ditunda", "Keterangan")),
        ["btnPenolakanAnjuranMedis"] = Letter("Penolakan Anjuran Medis", "surat_penolakan_anjuran_medis", "no_surat", "tanggal", false,
            Guardian(), X("hubungan", "Hubungan"), X("alasan_penolakan", "Alasan"), X("informasi_risiko_penolakan", "Informasi Risiko")),
        ["btnPersetujuanPemeriksaanHIV"] = Letter("Persetujuan Pemeriksaan HIV", "surat_persetujuan_pemeriksaan_hiv", "no_surat", "tanggal", false,
            X("nik", "NIK Petugas")),
        ["btnSuratPernyataanMemilihDPJP"] = Letter("Surat Pernyataan Memilih DPJP", "surat_pernyataan_memilih_dpjp", "no_pernyataan", "tanggal", true,
            X("pembuat_pernyataan", "Pembuat Pernyataan"), X("hubungan_pembuat_pernyataan", "Hubungan"), X("saksi_keluarga", "Saksi Keluarga")),
        ["btnSuratSerahTerimaBarangAnggotaTubuh"] = Letter("Serah Terima Anggota Tubuh/Barang", "surat_serah_terima_barang_anggota_tubuh", "no_pernyataan", "tanggal", false,
            X("jenis_barang", "Jenis"), X("uraian_barang", "Uraian"), X("jumlah_barang", "Jumlah"), X("kondisi_barang", "Kondisi"),
            Guardian(), X("hubungan", "Hubungan")),
        ["btnSuratPermintaanBinrohtal"] = Letter("Persetujuan Bimbingan Rohani & Mental", "permintaan_binrohtal", "no_surat", "tanggal", true,
            X("jns_permintaan", "Jenis Permintaan"), X("agama", "Agama"), X("jns_pelayanan", "Jenis Pelayanan"), X("respon", "Respon"),
            X("keterangan", "Keterangan")),
        ["btnSuratPermintaanPerlindunganDariKekerasan"] = Letter("Permintaan Perlindungan Diri Dari Kekerasan", "surat_perlindungan_dari_kekerasan", "no_surat", "tanggal", false,
            Guardian(), X("hubungan", "Hubungan"), X("no_telp", "Telepon")),
        ["btnSuratPermohonanPrivasi"] = Letter("Surat Permohonan Privasi", "surat_permohonan_privasi", "no_surat", "tanggal", false,
            X("kategori_privasi", "Kategori"), X("alasan", "Alasan"), Guardian(), X("bertindak_atas", "Bertindak Atas")),
        ["btnSuratPermintaanSecondOpinion"] = Letter("Surat Permintaan Second Opinion", "surat_permintaan_second_opinion", "no_pernyataan", "tanggal", true,
            X("pembuat_pernyataan", "Pembuat Pernyataan"), X("hubungan_pembuat_pernyataan", "Hubungan"), X("saksi_keluarga", "Saksi Keluarga")),
        ["btnSuratPenolakanResusitasi"] = Letter("Surat Penolakan Resusitasi", "surat_penolakan_resusitasi", "no_pernyataan", "tanggal", true,
            X("pembuat_pernyataan", "Pembuat Pernyataan"), X("hubungan_pembuat_pernyataan", "Hubungan"), X("saksi_keluarga", "Saksi Keluarga")),
        ["btnSuratPengajuanCutiPerawatan"] = Letter("Surat Pengajuan Cuti Perawatan", "surat_pengajuan_cuti_pasien", "no_surat", "tanggal", true,
            X("pembuat_pengajuan", "Pembuat Pengajuan"), X("hubungan_pembuat_pengajuan", "Hubungan"), X("alasan_cuti", "Alasan"),
            X("mulai_cuti", "Mulai Cuti"), X("kembali_dari_cuti", "Kembali")),
    };
}
