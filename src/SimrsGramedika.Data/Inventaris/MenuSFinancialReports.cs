using SimrsGramedika.Data.Master;

namespace SimrsGramedika.Data.Inventaris;

internal static class MenuSFinancialReports
{
    internal static readonly IReadOnlyDictionary<string, ReportSpec> Specs = new Dictionary<string, ReportSpec>
    {
        ["btnHutangToko"] = new("Hutang Toko", """
            SELECT h.no_faktur,h.no_order,h.tgl_faktur,h.tgl_pesan,h.tgl_tempo,s.nama_suplier,p.nama AS petugas,
                   h.status,h.tagihan,COALESCE(x.bayar,0) AS bayar,h.tagihan-COALESCE(x.bayar,0) AS sisa
            FROM tokopemesanan h
            LEFT JOIN tokosuplier s ON s.kode_suplier=h.kode_suplier
            LEFT JOIN petugas p ON p.nip=h.nip
            LEFT JOIN (SELECT no_faktur,SUM(besar_bayar) AS bayar FROM toko_bayar_pemesanan GROUP BY no_faktur) x
              ON x.no_faktur=h.no_faktur
            WHERE h.status IN ('Belum Dibayar','Belum Lunas')
              AND CONCAT_WS(' ',h.no_faktur,h.no_order,h.tgl_tempo,s.nama_suplier,p.nama,h.status) LIKE @p0
            ORDER BY h.tgl_tempo,h.no_faktur
            """,
            [("no_faktur", "No. Faktur"), ("no_order", "No. Pesanan"), ("nama_suplier", "Suplier"), ("petugas", "Petugas"),
             ("tgl_faktur", "Tanggal Faktur"), ("tgl_pesan", "Tanggal Terima"), ("tgl_tempo", "Jatuh Tempo"),
             ("status", "Status"), ("tagihan", "Tagihan"), ("bayar", "Sudah Dibayar"), ("sisa", "Sisa Hutang")],
            ReportPeriod.None, "Faktur berstatus Belum Dibayar atau Belum Lunas. Sisa dihitung dari tagihan dikurangi pembayaran tercatat.")
        { TotalColumns = ["tagihan", "bayar", "sisa"] },

        ["btnBayarPesanToko"] = new("Bayar Pesan Toko", """
            SELECT x.tgl_bayar,x.no_bukti,x.no_faktur,h.no_order,s.nama_suplier,p.nama AS petugas,
                   x.nama_bayar,x.besar_bayar,x.keterangan
            FROM toko_bayar_pemesanan x
            LEFT JOIN tokopemesanan h ON h.no_faktur=x.no_faktur
            LEFT JOIN tokosuplier s ON s.kode_suplier=h.kode_suplier
            LEFT JOIN petugas p ON p.nip=x.nip
            WHERE x.tgl_bayar BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',x.no_bukti,x.no_faktur,h.no_order,s.nama_suplier,x.nip,p.nama,x.nama_bayar,x.keterangan) LIKE @p0
            ORDER BY x.tgl_bayar DESC,x.no_bukti,x.no_faktur
            """,
            [("tgl_bayar", "Tanggal Bayar"), ("no_bukti", "No. Bukti"), ("no_faktur", "No. Faktur"),
             ("no_order", "No. Pesanan"), ("nama_suplier", "Suplier"), ("petugas", "Petugas"),
             ("nama_bayar", "Akun Bayar"), ("besar_bayar", "Pembayaran"), ("keterangan", "Keterangan")], ReportPeriod.DateRange)
        { TotalColumns = ["besar_bayar"] },

        ["btnBayarPiutangToko"] = new("Bayar Piutang Toko", """
            SELECT x.tgl_bayar,x.nota_piutang,x.no_member,m.nama,x.besar_cicilan,x.kd_rek,x.kd_rek_kontra,x.catatan
            FROM toko_bayar_piutang x
            LEFT JOIN tokomember m ON m.no_member=x.no_member
            WHERE x.tgl_bayar BETWEEN @p1 AND @p2
              AND CONCAT_WS(' ',x.nota_piutang,x.no_member,m.nama,x.kd_rek,x.kd_rek_kontra,x.catatan) LIKE @p0
            ORDER BY x.tgl_bayar DESC,x.nota_piutang,x.no_member,x.kd_rek,x.kd_rek_kontra
            """,
            [("tgl_bayar", "Tanggal Bayar"), ("nota_piutang", "Nota Piutang"), ("no_member", "No. Member"),
             ("nama", "Member"), ("besar_cicilan", "Cicilan"), ("kd_rek", "Rekening"),
             ("kd_rek_kontra", "Rekening Kontra"), ("catatan", "Catatan")], ReportPeriod.DateRange)
        { TotalColumns = ["besar_cicilan"] },

        ["btnPenjualanHarianToko"] = new("Penjualan Harian Toko", """
            SELECT h.nota_jual,h.tgl_jual,h.nip,p.nama AS petugas,h.no_member,h.nm_member,h.jns_jual,
                   h.nama_bayar,h.keterangan,h.ongkir,h.ppn,h.total
            FROM tokopenjualan h LEFT JOIN petugas p ON p.nip=h.nip
            WHERE h.tgl_jual BETWEEN @p1 AND @p2 AND
                  CONCAT_WS(' ',h.nota_jual,h.nip,p.nama,h.no_member,h.nm_member,h.jns_jual,h.nama_bayar,h.keterangan) LIKE @p0
            ORDER BY h.tgl_jual DESC,h.nota_jual
            """,
            [("tgl_jual", "Tanggal"), ("nota_jual", "Nota Jual"), ("nip", "NIP"), ("petugas", "Petugas"),
             ("no_member", "No. Member"), ("nm_member", "Member"), ("jns_jual", "Jenis Jual"), ("nama_bayar", "Cara Bayar"),
             ("ongkir", "Ongkir"), ("ppn", "PPN"), ("total", "Total Tercatat"), ("keterangan", "Keterangan")], ReportPeriod.DateRange)
        { TotalColumns = ["ongkir", "ppn", "total"] },

        ["btnPiutangHarianToko"] = new("Piutang Harian Toko", $"""
            SELECT h.nota_piutang,h.tgl_piutang,h.tgltempo,h.nip,p.nama AS petugas,h.no_member,h.nm_member,
                   h.jns_jual,h.ongkir,h.uangmuka,h.sisapiutang,{ReceivableTotal} AS total,h.catatan
            FROM tokopiutang h LEFT JOIN petugas p ON p.nip=h.nip
            WHERE h.tgl_piutang BETWEEN @p1 AND @p2 AND
                  CONCAT_WS(' ',h.nota_piutang,h.nip,p.nama,h.no_member,h.nm_member,h.jns_jual,h.catatan) LIKE @p0
            ORDER BY h.tgl_piutang DESC,h.nota_piutang
            """,
            [("tgl_piutang", "Tanggal"), ("nota_piutang", "Nota Piutang"), ("tgltempo", "Jatuh Tempo"),
             ("nip", "NIP"), ("petugas", "Petugas"), ("no_member", "No. Member"), ("nm_member", "Member"),
             ("jns_jual", "Jenis Jual"), ("ongkir", "Ongkir"), ("uangmuka", "Uang Muka"),
             ("sisapiutang", "Piutang pada Nota"), ("total", "Nilai Nota"), ("catatan", "Catatan")], ReportPeriod.DateRange,
            "Nilai nota mengikuti rumus Khanza: ongkir + uang muka + piutang pada nota. Cicilan tersedia di Bayar Piutang Toko.")
        { TotalColumns = ["ongkir", "uangmuka", "sisapiutang", "total"] },

        ["btnPendapatanHarianToko"] = new("Pendapatan Harian Toko", $"""
            SELECT tanggal,jenis,nip,petugas,COUNT(*) AS transaksi,SUM(ongkir) AS ongkir,SUM(ppn) AS ppn,
                   SUM(uangmuka) AS uangmuka,SUM(piutang) AS piutang,SUM(total) AS total
            FROM (
                SELECT h.tgl_jual AS tanggal,'Penjualan' AS jenis,h.nip,p.nama AS petugas,
                       h.ongkir,h.ppn,NULL AS uangmuka,NULL AS piutang,h.total
                FROM tokopenjualan h LEFT JOIN petugas p ON p.nip=h.nip
                WHERE h.tgl_jual BETWEEN @p1 AND @p2
                UNION ALL
                SELECT h.tgl_piutang,'Piutang',h.nip,p.nama,h.ongkir,NULL,h.uangmuka,h.sisapiutang,{ReceivableTotal}
                FROM tokopiutang h LEFT JOIN petugas p ON p.nip=h.nip
                WHERE h.tgl_piutang BETWEEN @p1 AND @p2
            ) d
            WHERE CONCAT_WS(' ',jenis,nip,petugas) LIKE @p0
            GROUP BY tanggal,jenis,nip,petugas
            ORDER BY tanggal DESC,jenis,nip
            """,
            [("tanggal", "Tanggal"), ("jenis", "Sumber"), ("nip", "NIP"), ("petugas", "Petugas"), ("transaksi", "Jumlah Nota"),
             ("ongkir", "Ongkir"), ("ppn", "PPN"), ("uangmuka", "Uang Muka"), ("piutang", "Piutang"), ("total", "Nilai Nota")],
            ReportPeriod.DateRange, "Rekap nilai nota per hari dan petugas. Nilai piutang mencakup tagihan kredit. Penerimaan cicilan tampil terpisah.")
        { TotalColumns = ["transaksi"] },

        ["btnKeuntunganBarangToko"] = new("Keuntungan Barang Toko", """
            SELECT d.tanggal,d.sumber,d.nota,d.kode_brng,b.nama_brng,s.satuan,d.jumlah,d.h_jual,d.h_beli,
                   d.total,d.h_beli*d.jumlah AS modal,d.total-d.h_beli*d.jumlah AS keuntungan
            FROM (
                SELECT h.tgl_jual AS tanggal,'Penjualan' AS sumber,h.nota_jual AS nota,
                       x.kode_brng,x.kode_sat,x.jumlah,x.h_jual,x.h_beli,x.total
                FROM tokopenjualan h JOIN toko_detail_jual x ON x.nota_jual=h.nota_jual
                WHERE h.tgl_jual BETWEEN @p1 AND @p2
                UNION ALL
                SELECT h.tgl_piutang,'Piutang',h.nota_piutang,x.kode_brng,x.kode_sat,x.jumlah,x.h_jual,x.h_beli,x.total
                FROM tokopiutang h JOIN toko_detail_piutang x ON x.nota_piutang=h.nota_piutang
                WHERE h.tgl_piutang BETWEEN @p1 AND @p2
            ) d
            LEFT JOIN tokobarang b ON b.kode_brng=d.kode_brng
            LEFT JOIN kodesatuan s ON s.kode_sat=d.kode_sat
            WHERE CONCAT_WS(' ',d.sumber,d.nota,d.kode_brng,b.nama_brng) LIKE @p0
            ORDER BY d.tanggal DESC,d.sumber,d.nota,d.kode_brng
            """,
            [("tanggal", "Tanggal"), ("sumber", "Sumber"), ("nota", "No. Nota"), ("kode_brng", "Kode Barang"),
             ("nama_brng", "Nama Barang"), ("satuan", "Satuan"), ("jumlah", "Jumlah"), ("h_jual", "Harga Jual"),
             ("h_beli", "Harga Beli"), ("total", "Nilai Baris"), ("modal", "Modal"), ("keuntungan", "Keuntungan")],
            ReportPeriod.DateRange, "Keuntungan kotor per baris memakai harga beli yang tersimpan pada nota. Retur dan biaya operasional dicatat terpisah.")
        { TotalColumns = ["total", "modal", "keuntungan"] },
    };

    private const string ReceivableTotal = "COALESCE(h.ongkir,0)+COALESCE(h.uangmuka,0)+h.sisapiutang";
}
