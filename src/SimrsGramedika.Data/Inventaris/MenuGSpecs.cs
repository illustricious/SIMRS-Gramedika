using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

public static class MenuGSpecs
{
    private static MasterSpec Master(string title, string table, string key, int keyLength, string name, int nameLength,
        params MasterField[] contacts) => new(
        title, table,
        [new(key, "Kode", MaxLen: keyLength, Key: true), new(name, "Nama", MaxLen: nameLength, Required: true), .. contacts],
        $"SELECT * FROM {table} WHERE CONCAT_WS(' ',{string.Join(",", new[] { key, name }.Concat(contacts.Select(c => c.Column)))}) LIKE @p0 ORDER BY {name},{key}",
        [(key, "Kode"), (name, "Nama"), .. contacts.Select(c => (c.Column, c.Label))]);

    public static readonly IReadOnlyDictionary<string, MasterSpec> Masters = new Dictionary<string, MasterSpec>(MenuGFormSpecs.Masters)
    {
        ["btnJenisInventaris"] = Master("Jenis Inventaris", "inventaris_jenis", "id_jenis", 10, "nama_jenis", 40)
            with { CanDelete = false, Hint = "Jenis inventaris dapat terkait dengan akun aset." },
        ["btnKategoriInventaris"] = Master("Kategori Inventaris", "inventaris_kategori", "id_kategori", 10, "nama_kategori", 40),
        ["btnMerkInventaris"] = Master("Merk Inventaris", "inventaris_merk", "id_merk", 10, "nama_merk", 40),
        ["btnRuangInventaris"] = Master("Ruang Inventaris", "inventaris_ruang", "id_ruang", 5, "nama_ruang", 40)
            with { CanDelete = false, Hint = "Ruang inventaris dapat terkait dengan riwayat peminjaman berkas." },
        ["btnProdusenInventaris"] = Master("Produsen Inventaris", "inventaris_produsen", "kode_produsen", 10, "nama_produsen", 40,
            new("alamat_produsen", "Alamat", MaxLen: 70), new("no_telp", "Telepon", MaxLen: 13),
            new("email", "Email", MaxLen: 25), new("website_produsen", "Situs Web", MaxLen: 30)),
        ["btnSuplierInventaris"] = Master("Suplier Aset/Inventaris", "inventaris_suplier", "kode_suplier", 5, "nama_suplier", 50,
            new("alamat", "Alamat", MaxLen: 50), new("kota", "Kota", MaxLen: 20), new("no_telp", "Telepon", MaxLen: 13),
            new("nama_bank", "Bank", MaxLen: 30), new("rekening", "No. Rekening", MaxLen: 20)),
    };

    public static readonly IReadOnlyDictionary<string, ReportSpec> Reports = new Dictionary<string, ReportSpec>(MenuGReportSpecs.Reports)
    {
        ["btnBarangInventaris"] = new("Koleksi Inventaris", """
            SELECT b.kode_barang,b.nama_barang,b.jml_barang,b.thn_produksi,b.isbn,
                   p.nama_produsen,m.nama_merk,k.nama_kategori,j.nama_jenis
            FROM inventaris_barang b
            LEFT JOIN inventaris_produsen p ON p.kode_produsen=b.kode_produsen
            LEFT JOIN inventaris_merk m ON m.id_merk=b.id_merk
            LEFT JOIN inventaris_kategori k ON k.id_kategori=b.id_kategori
            LEFT JOIN inventaris_jenis j ON j.id_jenis=b.id_jenis
            WHERE CONCAT_WS(' ',b.kode_barang,b.nama_barang,p.nama_produsen,m.nama_merk,k.nama_kategori,j.nama_jenis,b.isbn) LIKE @p0
            ORDER BY b.nama_barang,b.kode_barang
            """,
            [("kode_barang", "Kode Barang"), ("nama_barang", "Nama Barang"), ("jml_barang", "Jumlah"),
             ("nama_produsen", "Produsen"), ("nama_merk", "Merk"), ("thn_produksi", "Tahun Produksi"),
             ("isbn", "ISBN"), ("nama_kategori", "Kategori"), ("nama_jenis", "Jenis")],
            ReportPeriod.None)
        { SearchWatermark = "Kode, nama, merk, kategori, atau jenis" },

        ["btnInventaris"] = new("Data Inventaris", """
            SELECT i.no_inventaris,i.kode_barang,b.nama_barang,i.asal_barang,i.tgl_pengadaan,i.harga,
                   i.status_barang,i.id_ruang,r.nama_ruang,i.no_rak,i.no_box
            FROM inventaris i
            LEFT JOIN inventaris_barang b ON b.kode_barang=i.kode_barang
            LEFT JOIN inventaris_ruang r ON r.id_ruang=i.id_ruang
            WHERE CONCAT_WS(' ',i.no_inventaris,i.kode_barang,b.nama_barang,i.asal_barang,
                            i.status_barang,i.id_ruang,r.nama_ruang,i.no_rak,i.no_box) LIKE @p0
            ORDER BY i.no_inventaris
            """,
            [("no_inventaris", "No. Inventaris"), ("kode_barang", "Kode Barang"), ("nama_barang", "Nama Barang"),
             ("asal_barang", "Asal"), ("tgl_pengadaan", "Tgl. Pengadaan"), ("harga", "Harga"), ("status_barang", "Status"),
             ("id_ruang", "Kode Ruang"), ("nama_ruang", "Ruang"), ("no_rak", "Rak"), ("no_box", "Box")],
            ReportPeriod.None)
        { SearchWatermark = "No. inventaris, barang, ruang, atau status" },

        ["btnSirkulasiInventaris"] = new("Sirkulasi Inventaris", """
            SELECT s.no_inventaris,b.nama_barang,s.peminjam,s.tlp,s.tgl_pinjam,s.tgl_kembali,s.status_pinjam,s.nip,p.nama AS petugas
            FROM inventaris_peminjaman s
            LEFT JOIN inventaris i ON i.no_inventaris=s.no_inventaris
            LEFT JOIN inventaris_barang b ON b.kode_barang=i.kode_barang
            LEFT JOIN petugas p ON p.nip=s.nip
            WHERE s.tgl_pinjam BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',s.no_inventaris,b.nama_barang,s.peminjam,s.nip,p.nama,s.status_pinjam) LIKE @p0
            ORDER BY s.tgl_pinjam DESC,s.no_inventaris,s.peminjam,s.nip
            """,
            [("no_inventaris", "No. Inventaris"), ("nama_barang", "Nama Barang"), ("peminjam", "Peminjam"),
             ("tlp", "Telepon"), ("tgl_pinjam", "Tgl. Pinjam"), ("tgl_kembali", "Tgl. Kembali"),
             ("status_pinjam", "Status"), ("nip", "NIP Petugas"), ("petugas", "Petugas")],
            ReportPeriod.DateRange, "Periode memakai tanggal pinjam.")
        { SearchWatermark = "Barang, peminjam, petugas, atau status" },

        ["btnPermintaanPerbaikanInventaris"] = new("Permintaan Perbaikan Inventaris", """
            SELECT q.no_permintaan,q.tanggal,q.no_inventaris,b.nama_barang,r.nama_ruang,q.nik,p.nama AS pemohon,q.deskripsi_kerusakan
            FROM permintaan_perbaikan_inventaris q
            LEFT JOIN inventaris i ON i.no_inventaris=q.no_inventaris
            LEFT JOIN inventaris_barang b ON b.kode_barang=i.kode_barang
            LEFT JOIN inventaris_ruang r ON r.id_ruang=i.id_ruang
            LEFT JOIN pegawai p ON p.nik=q.nik
            WHERE q.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',q.no_permintaan,q.no_inventaris,b.nama_barang,r.nama_ruang,q.nik,p.nama,q.deskripsi_kerusakan) LIKE @p0
            ORDER BY q.tanggal DESC,q.no_permintaan
            """,
            [("no_permintaan", "No. Permintaan"), ("tanggal", "Waktu Permintaan"), ("no_inventaris", "No. Inventaris"),
             ("nama_barang", "Nama Barang"), ("nama_ruang", "Ruang"), ("nik", "NIK Pemohon"), ("pemohon", "Pemohon"),
             ("deskripsi_kerusakan", "Deskripsi Kerusakan")],
            ReportPeriod.DateRange)
        { SearchWatermark = "No. permintaan, barang, ruang, atau pemohon" },

        ["btnPerbaikanInventaris"] = new("Perbaikan Inventaris", """
            SELECT x.no_permintaan,x.tanggal,q.no_inventaris,b.nama_barang,r.nama_ruang,x.uraian_kegiatan,
                   x.nip,p.nama AS petugas,x.pelaksana,x.biaya,x.keterangan,x.status
            FROM perbaikan_inventaris x
            LEFT JOIN permintaan_perbaikan_inventaris q ON q.no_permintaan=x.no_permintaan
            LEFT JOIN inventaris i ON i.no_inventaris=q.no_inventaris
            LEFT JOIN inventaris_barang b ON b.kode_barang=i.kode_barang
            LEFT JOIN inventaris_ruang r ON r.id_ruang=i.id_ruang
            LEFT JOIN petugas p ON p.nip=x.nip
            WHERE x.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',x.no_permintaan,q.no_inventaris,b.nama_barang,r.nama_ruang,x.nip,p.nama,
                            x.pelaksana,x.uraian_kegiatan,x.keterangan,x.status) LIKE @p0
            ORDER BY x.tanggal DESC,x.no_permintaan
            """,
            [("no_permintaan", "No. Permintaan"), ("tanggal", "Tgl. Perbaikan"), ("no_inventaris", "No. Inventaris"),
             ("nama_barang", "Nama Barang"), ("nama_ruang", "Ruang"), ("uraian_kegiatan", "Uraian Kegiatan"), ("nip", "NIP Petugas"),
             ("petugas", "Petugas"), ("pelaksana", "Pelaksana"), ("biaya", "Biaya"), ("keterangan", "Keterangan"), ("status", "Status")],
            ReportPeriod.DateRange)
        { SearchWatermark = "No. permintaan, barang, petugas, atau status" },

        ["btnPemeliharaanInventaris"] = new("Pemeliharaan Inventaris", """
            SELECT x.no_inventaris,x.tanggal,b.nama_barang,r.nama_ruang,x.uraian_kegiatan,
                   x.nip,p.nama AS petugas,x.pelaksana,x.biaya,x.jenis_pemeliharaan
            FROM pemeliharaan_inventaris x
            LEFT JOIN inventaris i ON i.no_inventaris=x.no_inventaris
            LEFT JOIN inventaris_barang b ON b.kode_barang=i.kode_barang
            LEFT JOIN inventaris_ruang r ON r.id_ruang=i.id_ruang
            LEFT JOIN petugas p ON p.nip=x.nip
            WHERE x.tanggal BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',x.no_inventaris,b.nama_barang,r.nama_ruang,x.nip,p.nama,
                            x.pelaksana,x.uraian_kegiatan,x.jenis_pemeliharaan) LIKE @p0
            ORDER BY x.tanggal DESC,x.no_inventaris
            """,
            [("no_inventaris", "No. Inventaris"), ("tanggal", "Tgl. Pemeliharaan"), ("nama_barang", "Nama Barang"),
             ("nama_ruang", "Ruang"), ("uraian_kegiatan", "Uraian Kegiatan"), ("nip", "NIP Petugas"), ("petugas", "Petugas"),
             ("pelaksana", "Pelaksana"), ("biaya", "Biaya"), ("jenis_pemeliharaan", "Jenis Pemeliharaan")],
            ReportPeriod.DateRange)
        { SearchWatermark = "Barang, ruang, petugas, atau jenis pemeliharaan" },
    };
}
