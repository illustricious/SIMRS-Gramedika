using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

/// <param name="Sisa">Sisa menurut Khanza: piutang_pasien.sisapiutang dikurangi seluruh pembayaran, diskon dan tak terbayar.</param>
public sealed record PiutangPasien(string NoRawat, string NoRekamMedis, string Nama, DateTime? Tanggal,
    decimal Total, decimal UangMuka, decimal Sisa, string Status);

/// <summary>Satu baris detail_piutang_pasien: sisa piutang pada satu akun.</summary>
public sealed record AkunPiutangPasien(string Nama, string KodeRekening, decimal Total, decimal Sisa, DateTime? JatuhTempo);

/// <summary>Satu baris bayar_piutang.</summary>
public sealed record CicilanPiutang(DateTime Tanggal, string NoRekamMedis, string RekeningBayar, string NamaRekeningBayar,
    string RekeningPiutang, decimal Cicilan, decimal Diskon, string RekeningDiskon,
    decimal TidakTerbayar, string RekeningTidakTerbayar, string Catatan)
{
    public decimal Total => Cicilan + Diskon + TidakTerbayar;
}

/// <summary>Bayar piutang pasien beserta jurnalnya (DlgBayarPiutang).</summary>
public sealed class PiutangPasienService(DatabaseSettings settings, AuthenticatedUser user)
{
    public const string Izin = "bayar_piutang";

    private const string SudahDibayar =
        "COALESCE((SELECT SUM(b.besar_cicilan+b.diskon_piutang+b.tidak_terbayar) FROM bayar_piutang b WHERE b.no_rawat=p.no_rawat),0)";

    public async Task<IReadOnlyList<PiutangPasien>> DaftarAsync(string pencarian, bool belumLunas)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync($"""
            SELECT p.no_rawat,p.no_rkm_medis,COALESCE(s.nm_pasien,'') nm_pasien,p.tgl_piutang,p.totalpiutang,p.uangmuka,
                   p.sisapiutang-{SudahDibayar} sisa,p.status
            FROM piutang_pasien p LEFT JOIN pasien s ON s.no_rkm_medis=p.no_rkm_medis
            WHERE (p.no_rawat LIKE @p0 OR p.no_rkm_medis LIKE @p0 OR s.nm_pasien LIKE @p0)
                  {(belumLunas ? "AND p.status='Belum Lunas'" : "")}
            ORDER BY p.tgl_piutang DESC,p.no_rawat DESC LIMIT 500
            """, "%" + pencarian.Trim() + "%");
        return rows.Select(r => new PiutangPasien(r.S("no_rawat"), r.S("no_rkm_medis"), r.S("nm_pasien"), r.Dt("tgl_piutang"),
            Uang(r, "totalpiutang"), Uang(r, "uangmuka"), Uang(r, "sisa"), r.S("status"))).ToList();
    }

    public async Task<IReadOnlyList<AkunPiutangPasien>> AkunAsync(string noRawat)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT d.nama_bayar,COALESCE(a.kd_rek,'') kd_rek,d.totalpiutang,d.sisapiutang,d.tgltempo
            FROM detail_piutang_pasien d LEFT JOIN akun_piutang a ON a.nama_bayar=d.nama_bayar
            WHERE d.no_rawat=@p0 ORDER BY d.nama_bayar
            """, noRawat);
        return rows.Select(Akun).ToList();
    }

    public async Task<IReadOnlyList<CicilanPiutang>> RiwayatAsync(string noRawat)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("""
            SELECT b.*,COALESCE(r.nm_rek,b.kd_rek) nama_rekening FROM bayar_piutang b
            LEFT JOIN rekening r ON r.kd_rek=b.kd_rek
            WHERE b.no_rawat=@p0 ORDER BY b.tgl_bayar,b.kd_rek,b.kd_rek_kontra
            """, noRawat);
        return rows.Select(r => new CicilanPiutang(r.Dt("tgl_bayar")!.Value, r.S("no_rkm_medis"), r.S("kd_rek"), r.S("nama_rekening"),
            r.S("kd_rek_kontra"), Uang(r, "besar_cicilan"), Uang(r, "diskon_piutang"), r.S("kd_rek_diskon_piutang"),
            Uang(r, "tidak_terbayar"), r.S("kd_rek_tidak_terbayar"), r.S("catatan"))).ToList();
    }

    public async Task<IReadOnlyList<string>> AkunBayarAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var rows = await db.QueryAsync("SELECT nama_bayar FROM akun_bayar WHERE COALESCE(kd_rek,'')<>'' ORDER BY nama_bayar");
        return rows.Select(r => r.S("nama_bayar")).ToList();
    }

    /// <summary>Catat satu pembayaran bertanggal hari ini. <paramref name="dilihat"/> adalah baris akun yang tampil di layar.</summary>
    public async Task BayarAsync(string noRawat, AkunPiutangPasien dilihat, string akunBayar,
        decimal cicilan, decimal diskon, decimal tidakTerbayar, string catatan)
    {
        PastikanIzin();
        catatan = catatan.Trim();
        if (catatan.Length is 0 or > 100) throw new ArgumentException("Isi keterangan pembayaran, maksimum 100 karakter.");
        if (cicilan <= 0) throw new ArgumentException("Besar cicilan harus lebih dari 0.");
        if (diskon < 0 || tidakTerbayar < 0) throw new ArgumentException("Diskon dan tidak terbayar tidak boleh negatif.");
        if (new[] { cicilan, diskon, tidakTerbayar }.Any(n => n != decimal.Round(n, 4)))
            throw new ArgumentException("Angka pembayaran paling banyak empat desimal.");
        var total = cicilan + diskon + tidakTerbayar;

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        var piutang = await KunciAsync(db, noRawat);
        var akun = await KunciAkunAsync(db, noRawat, dilihat.Nama);
        if (akun != dilihat) throw new InvalidOperationException("Sisa piutang sudah berubah. Muat ulang sebelum membayar.");
        if (akun.KodeRekening.Length == 0) throw new InvalidOperationException("Akun piutang belum memiliki rekening.");
        if (total > akun.Sisa) throw new ArgumentException("Pembayaran melebihi sisa piutang pada akun ini.");
        var rekeningBayar = await db.StrAsync("SELECT kd_rek FROM akun_bayar WHERE nama_bayar=@p0", akunBayar);
        if (rekeningBayar.Length == 0) throw new ArgumentException("Pilih akun bayar yang memiliki rekening.");
        var umum = await JurnalPoster.ReadAkunAsync(db.Conn, db.Tx, "set_akun");
        var rekeningDiskon = umum.GetValueOrDefault("Diskon_Piutang", "");
        var rekeningHapus = umum.GetValueOrDefault("Piutang_Tidak_Terbayar", "");
        if (rekeningDiskon.Length == 0 || rekeningHapus.Length == 0)
            throw new InvalidOperationException("Rekening diskon piutang atau piutang tidak terbayar belum diatur di set_akun.");

        var sisaSemua = Uang(piutang, "sisa") - total;
        try
        {
            await db.ExecAsync("""
                INSERT INTO bayar_piutang(tgl_bayar,no_rkm_medis,besar_cicilan,catatan,no_rawat,kd_rek,kd_rek_kontra,
                    diskon_piutang,kd_rek_diskon_piutang,tidak_terbayar,kd_rek_tidak_terbayar)
                VALUES (CURRENT_DATE,@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)
                """, piutang.S("no_rkm_medis"), cicilan, catatan, noRawat, rekeningBayar, akun.KodeRekening,
                diskon, rekeningDiskon, tidakTerbayar, rekeningHapus);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new InvalidOperationException(
                "Sudah ada pembayaran hari ini dengan akun bayar dan akun piutang yang sama. Batalkan dulu, lalu masukkan jumlah gabungannya.", ex);
        }
        // Seperti Khanza, sisa sampai 1 rupiah dianggap lunas.
        if (sisaSemua <= 1) await db.ExecAsync("UPDATE piutang_pasien SET status='Lunas' WHERE no_rawat=@p0", noRawat);
        await db.ExecAsync("UPDATE detail_piutang_pasien SET sisapiutang=sisapiutang-@p0 WHERE no_rawat=@p1 AND nama_bayar=@p2",
            total, noRawat, akun.Nama);
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noRawat, $"BAYAR PIUTANG, OLEH {user.KodeLegacy}",
        [
            new(akun.KodeRekening, "BAYAR PIUTANG", 0, (double)total),
            new(rekeningBayar, akunBayar, (double)cicilan, 0),
            new(rekeningDiskon, "Diskon Piutang", (double)diskon, 0),
            new(rekeningHapus, "Piutang Tidak Terbayar", (double)tidakTerbayar, 0),
        ]);
        await db.CommitAsync();
    }

    /// <summary>Hapus satu pembayaran, kembalikan sisa piutang dan posting jurnal pembalik.</summary>
    public async Task BatalkanAsync(string noRawat, CicilanPiutang bayar)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunciAsync(db, noRawat);
        const string kunci = "tgl_bayar=@p0 AND no_rkm_medis=@p1 AND no_rawat=@p2 AND kd_rek=@p3 AND kd_rek_kontra=@p4";
        object?[] args = [bayar.Tanggal, bayar.NoRekamMedis, noRawat, bayar.RekeningBayar, bayar.RekeningPiutang];
        var tersimpan = await db.FirstAsync($"SELECT * FROM bayar_piutang WHERE {kunci} FOR UPDATE", args);
        if (tersimpan is null || Uang(tersimpan, "besar_cicilan") != bayar.Cicilan || Uang(tersimpan, "diskon_piutang") != bayar.Diskon
            || Uang(tersimpan, "tidak_terbayar") != bayar.TidakTerbayar)
            throw new InvalidOperationException("Pembayaran sudah berubah atau dihapus. Muat ulang riwayat.");
        var nama = await db.QueryAsync("""
            SELECT d.nama_bayar FROM detail_piutang_pasien d JOIN akun_piutang a ON a.nama_bayar=d.nama_bayar
            WHERE d.no_rawat=@p0 AND a.kd_rek=@p1
            """, noRawat, bayar.RekeningPiutang);
        if (nama.Count != 1)
            throw new InvalidOperationException("Akun piutang pembayaran ini tidak dapat ditentukan. Batalkan lewat Khanza.");
        var akun = await KunciAkunAsync(db, noRawat, nama[0].S("nama_bayar"));
        if (akun.Sisa + bayar.Total > akun.Total)
            throw new InvalidOperationException("Sisa akun piutang tidak cocok dengan riwayat pembayaran. Periksa di Keuangan.");

        await db.ExecAsync($"DELETE FROM bayar_piutang WHERE {kunci}", args);
        await db.ExecAsync("UPDATE piutang_pasien SET status='Belum Lunas' WHERE no_rawat=@p0", noRawat);
        await db.ExecAsync("UPDATE detail_piutang_pasien SET sisapiutang=sisapiutang+@p0 WHERE no_rawat=@p1 AND nama_bayar=@p2",
            bayar.Total, noRawat, akun.Nama);
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noRawat, $"PEMBATALAN BAYAR PIUTANG, OLEH {user.KodeLegacy}",
        [
            new(bayar.RekeningPiutang, "BAYAR PIUTANG", (double)bayar.Total, 0),
            new(bayar.RekeningBayar, bayar.NamaRekeningBayar, 0, (double)bayar.Cicilan),
            new(bayar.RekeningDiskon, "Diskon Piutang", 0, (double)bayar.Diskon),
            new(bayar.RekeningTidakTerbayar, "Piutang Tidak Terbayar", 0, (double)bayar.TidakTerbayar),
        ]);
        await db.CommitAsync();
    }

    /// <summary>Kunci kunjungan lalu piutangnya, urutan yang sama dengan pelunasan di Kasir.</summary>
    private static async Task<Row> KunciAsync(Db db, string noRawat)
    {
        await db.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", noRawat);
        if (await db.FirstAsync("SELECT no_rawat FROM piutang_pasien WHERE no_rawat=@p0 FOR UPDATE", noRawat) is null)
            throw new InvalidOperationException("Piutang pasien tidak ditemukan.");
        return (await db.FirstAsync(
            $"SELECT p.no_rkm_medis,p.sisapiutang-{SudahDibayar} sisa FROM piutang_pasien p WHERE p.no_rawat=@p0", noRawat))!;
    }

    private static async Task<AkunPiutangPasien> KunciAkunAsync(Db db, string noRawat, string nama)
    {
        var row = await db.FirstAsync("""
            SELECT d.nama_bayar,COALESCE(a.kd_rek,'') kd_rek,d.totalpiutang,d.sisapiutang,d.tgltempo
            FROM detail_piutang_pasien d LEFT JOIN akun_piutang a ON a.nama_bayar=d.nama_bayar
            WHERE d.no_rawat=@p0 AND d.nama_bayar=@p1 FOR UPDATE
            """, noRawat, nama);
        return row is null ? throw new InvalidOperationException("Akun piutang pada kunjungan ini sudah tidak ada.") : Akun(row);
    }

    private static AkunPiutangPasien Akun(Row r) =>
        new(r.S("nama_bayar"), r.S("kd_rek"), Uang(r, "totalpiutang"), Uang(r, "sisapiutang"), r.Dt("tgltempo"));

    // Kolom uang bertipe double; dibulatkan empat desimal supaya sisa pecahan tetap bisa dilunasi persis.
    private static decimal Uang(Row r, string kolom) => decimal.Round((decimal)r.D(kolom), 4, MidpointRounding.AwayFromZero);

    private void PastikanIzin()
    {
        if (!user.HasPermission(Izin)) throw new InvalidOperationException("Akun tidak memiliki hak Bayar Piutang.");
    }
}
