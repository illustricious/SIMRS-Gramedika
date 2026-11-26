using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

internal static class MenuGReportSpecs
{
    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>
    {
        ["btnPengajuanAsetInventaris"] = new("Pengajuan Aset/Inventaris", """
            SELECT q.no_pengajuan,q.tanggal,q.nik,p.nama AS pemohon,d.nama AS departemen,q.urgensi,q.latar_belakang,
                   q.nama_barang,q.spesifikasi,q.jumlah,q.harga,q.total,q.keterangan,q.nik_pj,pj.nama AS penanggung_jawab,q.status
            FROM pengajuan_inventaris q
            LEFT JOIN pegawai p ON p.nik=q.nik
            LEFT JOIN departemen d ON d.dep_id=p.departemen
            LEFT JOIN pegawai pj ON pj.nik=q.nik_pj
            WHERE q.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',q.no_pengajuan,q.nik,p.nama,d.nama,q.nama_barang,q.spesifikasi,q.urgensi,q.status,pj.nama) LIKE @p0
            ORDER BY q.tanggal DESC,q.no_pengajuan
            """,
            [("no_pengajuan", "No. Pengajuan"), ("tanggal", "Tanggal"), ("nama_barang", "Nama Barang"),
             ("spesifikasi", "Spesifikasi"), ("urgensi", "Urgensi"), ("status", "Status"), ("jumlah", "Jumlah"),
             ("harga", "Harga"), ("total", "Total"), ("nik", "NIK Pemohon"), ("pemohon", "Pemohon"),
             ("departemen", "Departemen"), ("latar_belakang", "Latar Belakang"), ("keterangan", "Keterangan"),
             ("nik_pj", "NIK PJ"), ("penanggung_jawab", "Penanggung Jawab")],
            ReportPeriod.DateRange, "Pengajuan dan persetujuan aset dicatat di Khanza.")
        { SearchWatermark = "No. pengajuan, barang, departemen, atau status" },

        ["btnRekapPengajuanAsetDepartemen"] = new("Rekap Pengajuan Aset Departemen", """
            SELECT p.departemen AS kode_departemen,d.nama AS departemen,DATE_FORMAT(q.tanggal,'%Y-%m') AS bulan,
                   q.status,COUNT(*) AS jumlah_pengajuan,SUM(q.total) AS nilai
            FROM pengajuan_inventaris q
            LEFT JOIN pegawai p ON p.nik=q.nik
            LEFT JOIN departemen d ON d.dep_id=p.departemen
            WHERE q.tanggal >= MAKEDATE(@p1,1) AND q.tanggal < MAKEDATE(@p1+1,1)
              AND CONCAT_WS(' ',p.departemen,d.nama,q.status) LIKE @p0
            GROUP BY p.departemen,d.nama,DATE_FORMAT(q.tanggal,'%Y-%m'),q.status
            ORDER BY d.nama,p.departemen,bulan,q.status
            """,
            [("kode_departemen", "Kode Departemen"), ("departemen", "Departemen"), ("bulan", "Bulan"),
             ("status", "Status"), ("jumlah_pengajuan", "Jumlah Pengajuan"), ("nilai", "Nilai Pengajuan")],
            ReportPeriod.Year, "Jumlah dan nilai pengajuan per departemen, bulan, dan status.")
        { SearchWatermark = "Departemen atau status pengajuan" },

        ["btnPengadaanAset"] = new("Pengadaan Aset", """
            SELECT h.no_faktur,h.tgl_beli,h.kode_suplier,s.nama_suplier,p.nama AS petugas,h.kd_rek_aset,
                   x.kode_barang,b.nama_barang,x.jumlah,x.harga,x.besardis,x.total AS total_item
            FROM inventaris_pembelian h
            LEFT JOIN inventaris_detail_beli x ON x.no_faktur=h.no_faktur
            LEFT JOIN inventaris_barang b ON b.kode_barang=x.kode_barang
            LEFT JOIN inventaris_suplier s ON s.kode_suplier=h.kode_suplier
            LEFT JOIN petugas p ON p.nip=h.nip
            WHERE h.tgl_beli BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_faktur,h.kode_suplier,s.nama_suplier,h.nip,p.nama,x.kode_barang,b.nama_barang,h.kd_rek_aset) LIKE @p0
            ORDER BY h.tgl_beli DESC,h.no_faktur,x.kode_barang
            """,
            [("no_faktur", "No. Faktur"), ("tgl_beli", "Tgl. Beli"), ("nama_suplier", "Suplier"),
             ("kode_barang", "Kode Barang"), ("nama_barang", "Nama Barang"), ("jumlah", "Jumlah"),
             ("harga", "Harga Satuan"), ("besardis", "Diskon Item"), ("total_item", "Total Item"),
             ("kode_suplier", "Kode Suplier"), ("petugas", "Petugas"), ("kd_rek_aset", "Kode Akun Aset")],
            ReportPeriod.DateRange, "Satu baris per item faktur. Catat pembelian dan jurnal di Khanza.")
        { SearchWatermark = "Faktur, barang, suplier, atau petugas" },

        ["btnPenerimaanAset"] = new("Penerimaan Aset", """
            SELECT h.no_faktur,h.no_order,h.tgl_pesan,h.tgl_faktur,h.tgl_tempo,h.status,
                   h.kode_suplier,s.nama_suplier,p.nama AS petugas,h.kd_rek_aset,
                   x.kode_barang,b.nama_barang,x.jumlah,x.harga,x.besardis,x.total AS total_item
            FROM inventaris_pemesanan h
            LEFT JOIN inventaris_detail_pesan x ON x.no_faktur=h.no_faktur
            LEFT JOIN inventaris_barang b ON b.kode_barang=x.kode_barang
            LEFT JOIN inventaris_suplier s ON s.kode_suplier=h.kode_suplier
            LEFT JOIN petugas p ON p.nip=h.nip
            WHERE h.tgl_pesan BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_faktur,h.no_order,h.kode_suplier,s.nama_suplier,h.nip,p.nama,
                            h.status,x.kode_barang,b.nama_barang,h.kd_rek_aset) LIKE @p0
            ORDER BY h.tgl_pesan DESC,h.no_faktur,x.kode_barang
            """,
            [("no_faktur", "No. Faktur"), ("no_order", "No. Order"), ("tgl_pesan", "Tgl. Pesan"), ("tgl_faktur", "Tgl. Faktur"),
             ("tgl_tempo", "Jatuh Tempo"), ("status", "Status Bayar"), ("nama_suplier", "Suplier"), ("kode_barang", "Kode Barang"),
             ("nama_barang", "Nama Barang"), ("jumlah", "Jumlah"), ("harga", "Harga Satuan"), ("besardis", "Diskon Item"),
             ("total_item", "Total Item"), ("kode_suplier", "Kode Suplier"), ("petugas", "Petugas"), ("kd_rek_aset", "Kode Akun Aset")],
            ReportPeriod.DateRange, "Periode memakai tanggal pesan. Satu baris per item faktur.")
        { SearchWatermark = "Faktur, order, barang, suplier, atau status bayar" },

        ["btnHibahAsetInventaris"] = new("Hibah Aset/Inventaris", """
            SELECT h.no_hibah,h.tgl_hibah,h.kode_pemberi,s.nama_pemberi,p.nama AS petugas,h.kd_rek_aset,
                   x.kode_barang,b.nama_barang,x.jumlah,x.h_hibah,x.subtotalhibah
            FROM inventaris_hibah h
            LEFT JOIN inventaris_detail_hibah x ON x.no_hibah=h.no_hibah
            LEFT JOIN inventaris_barang b ON b.kode_barang=x.kode_barang
            LEFT JOIN pemberihibah s ON s.kode_pemberi=h.kode_pemberi
            LEFT JOIN petugas p ON p.nip=h.nip
            WHERE h.tgl_hibah BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',h.no_hibah,h.kode_pemberi,s.nama_pemberi,h.nip,p.nama,x.kode_barang,b.nama_barang,h.kd_rek_aset) LIKE @p0
            ORDER BY h.tgl_hibah DESC,h.no_hibah,x.kode_barang
            """,
            [("no_hibah", "No. Hibah"), ("tgl_hibah", "Tgl. Hibah"), ("nama_pemberi", "Pemberi Hibah"),
             ("kode_barang", "Kode Barang"), ("nama_barang", "Nama Barang"), ("jumlah", "Jumlah"),
             ("h_hibah", "Nilai Satuan"), ("subtotalhibah", "Nilai Item"),
             ("kode_pemberi", "Kode Pemberi"), ("petugas", "Petugas"), ("kd_rek_aset", "Kode Akun Aset")],
            ReportPeriod.DateRange, "Satu baris per item hibah. Catat penerimaan hibah dan jurnal di Khanza.")
        { SearchWatermark = "No. hibah, barang, pemberi, atau petugas" },

        ["btnSirkulasiInventarisCSSD"] = new("Sirkulasi CSSD", """
            SELECT s.no_sirkulasi,s.no_inventaris,i.kode_barang,b.nama_barang,c.jenis_barang,
                   s.tgl_ambil,p1.nama AS pengambil,s.keterangan_ambil,
                   t.tgl_sterilisasi,p2.nama AS petugas_sterilisasi,t.keterangan_strerilisasi AS keterangan_sterilisasi,
                   k.tgl_kembali,p3.nama AS petugas_kembali,k.keterangan_kembali,
                   CASE WHEN k.no_sirkulasi IS NOT NULL THEN 'Dikembalikan'
                        WHEN t.no_sirkulasi IS NOT NULL THEN 'Sterilisasi Tercatat'
                        ELSE 'Diambil' END AS status_sirkulasi
            FROM inventaris_ambil_cssd s
            LEFT JOIN cssd_barang c ON c.no_inventaris=s.no_inventaris
            LEFT JOIN inventaris i ON i.no_inventaris=s.no_inventaris
            LEFT JOIN inventaris_barang b ON b.kode_barang=i.kode_barang
            LEFT JOIN inventaris_sterilisasi_cssd t ON t.no_sirkulasi=s.no_sirkulasi
            LEFT JOIN inventaris_kembali_cssd k ON k.no_sirkulasi=s.no_sirkulasi
            LEFT JOIN petugas p1 ON p1.nip=s.nip
            LEFT JOIN petugas p2 ON p2.nip=t.nip
            LEFT JOIN petugas p3 ON p3.nip=k.nip
            WHERE s.tgl_ambil BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',s.no_sirkulasi,s.no_inventaris,i.kode_barang,b.nama_barang,c.jenis_barang,p1.nama,p2.nama,p3.nama) LIKE @p0
            ORDER BY s.tgl_ambil DESC,s.no_sirkulasi
            """,
            [("no_sirkulasi", "No. Sirkulasi"), ("no_inventaris", "No. Inventaris"), ("kode_barang", "Kode Barang"),
             ("nama_barang", "Nama Barang"), ("jenis_barang", "Jenis Barang CSSD"), ("status_sirkulasi", "Tahap Tercatat"),
             ("tgl_ambil", "Waktu Ambil"), ("pengambil", "Pengambil"), ("keterangan_ambil", "Keterangan Ambil"),
             ("tgl_sterilisasi", "Waktu Sterilisasi"), ("petugas_sterilisasi", "Petugas Sterilisasi"),
             ("keterangan_sterilisasi", "Keterangan Sterilisasi"), ("tgl_kembali", "Waktu Kembali"),
             ("petugas_kembali", "Petugas Kembali"), ("keterangan_kembali", "Keterangan Kembali")],
            ReportPeriod.DateRange, "Periode memakai waktu pengambilan. Tahap ditampilkan dari catatan sirkulasi Khanza.")
        { SearchWatermark = "No. sirkulasi, inventaris, barang, atau petugas" },
    };
}
