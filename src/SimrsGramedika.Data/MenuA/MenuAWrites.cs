using System.Globalization;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Farmasi;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.MenuA;

internal static class MenuAWrites
{
    internal const string NomorDepositSql = """
        SELECT GREATEST(
            COALESCE((SELECT MAX(CAST(RIGHT(no_deposit,4) AS UNSIGNED)) FROM deposit
                WHERE tgl_deposit>=@p0 AND tgl_deposit<DATE_ADD(@p0, INTERVAL 1 DAY)),0),
            COALESCE((SELECT MAX(CAST(RIGHT(no_bukti,4) AS UNSIGNED)) FROM jurnal
                WHERE no_bukti LIKE CONCAT('DP',DATE_FORMAT(@p0,'%Y%m%d'),'____')
                AND keterangan LIKE 'DEPOSIT PASIEN %'),0)
        )
        """;

    internal static async Task<Row> KunciKunjunganAsync(Db db, object? noRawat, bool cekBilling = false)
    {
        var row = await db.FirstAsync("""
            SELECT r.no_rkm_medis,r.stts,r.status_bayar,p.nm_pasien
            FROM reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis
            WHERE r.no_rawat=@p0 FOR UPDATE
            """, noRawat) ?? throw new InvalidOperationException("No. Rawat tidak ditemukan.");
        if (row.S("stts") == "Batal")
            throw new InvalidOperationException("Registrasi pasien sudah dibatalkan.");
        if (cekBilling &&
            await TagihanKunjungan.SudahDitagihAsync(db, noRawat))
            throw new InvalidOperationException("Billing pasien sudah diverifikasi. Hubungi bagian kasir/keuangan.");
        return row;
    }

    internal static async Task PesanKamarAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        var noRawat = values["no_rawat"];
        await KunciKunjunganAsync(db, noRawat);
        var lama = keys is null ? null : await AmbilPesananAsync(db, keys["no_rawat"]);
        if (lama is not null && !Equals(noRawat, keys!["no_rawat"]))
            throw new InvalidOperationException("No. Rawat pada pesanan kamar tidak bisa diganti.");
        if (await db.CountAsync("SELECT COUNT(*) FROM kamar_inap WHERE no_rawat=@p0", noRawat) > 0)
            throw new InvalidOperationException("Pasien sudah tercatat masuk rawat inap.");
        var baru = (string)values["kd_kamar"]!;
        var kamar = await db.FirstAsync("SELECT status,statusdata FROM kamar WHERE kd_kamar=@p0 FOR UPDATE", baru);
        if (kamar is null || kamar.S("statusdata") != "1" ||
            !(kamar.S("status") == "KOSONG" || (lama == baru && kamar.S("status") == "DIBOOKING")))
            throw new InvalidOperationException("Pilih kamar aktif yang masih tersedia.");
        await db.ExecAsync("UPDATE kamar SET status='DIBOOKING' WHERE kd_kamar=@p0", baru);
        if (lama is not null && lama != baru) await LepasKamarAsync(db, lama, noRawat);
    }

    internal static async Task BatalPesanKamarAsync(Db db, Dictionary<string, object?> keys)
    {
        var noRawat = keys["no_rawat"];
        await db.FirstAsync("SELECT no_rawat FROM reg_periksa WHERE no_rawat=@p0 FOR UPDATE", noRawat);
        var kamar = await AmbilPesananAsync(db, noRawat);
        if (await db.CountAsync("SELECT COUNT(*) FROM kamar_inap WHERE no_rawat=@p0", noRawat) > 0)
            throw new InvalidOperationException("Permintaan sudah diproses ke rawat inap.");
        await LepasKamarAsync(db, kamar, noRawat);
    }

    private static async Task<string> AmbilPesananAsync(Db db, object? noRawat) =>
        (await db.FirstAsync("SELECT kd_kamar FROM permintaan_ranap WHERE no_rawat=@p0 FOR UPDATE", noRawat)
            ?? throw new InvalidOperationException("Permintaan tidak ditemukan.")).S("kd_kamar");

    private static Task LepasKamarAsync(Db db, string kamar, object? noRawat) => db.ExecAsync("""
        UPDATE kamar SET status='KOSONG' WHERE kd_kamar=@p0 AND status='DIBOOKING'
        AND NOT EXISTS (
            SELECT 1 FROM permintaan_ranap p
            WHERE p.kd_kamar=@p0 AND p.no_rawat<>@p1
            AND NOT EXISTS(SELECT 1 FROM kamar_inap i WHERE i.no_rawat=p.no_rawat))
        """, kamar, noRawat);

    internal static async Task CekJadwalOperasiAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        var noRawat = values["no_rawat"];
        await KunciKunjunganAsync(db, noRawat, cekBilling: true);
        if (keys is not null && !Equals(noRawat, keys["no_rawat"]))
            await KunciKunjunganAsync(db, keys["no_rawat"], cekBilling: true);
        var ruang = values["kd_ruang_ok"];
        await db.FirstAsync("SELECT kd_ruang_ok FROM ruang_ok WHERE kd_ruang_ok=@p0 FOR UPDATE", ruang);
        var mulai = (TimeSpan)values["jam_mulai"]!;
        var selesai = (TimeSpan)values["jam_selesai"]!;
        if (selesai != TimeSpan.Zero && selesai < mulai)
            throw new ArgumentException("Jam selesai harus sama dengan atau sesudah jam mulai.");
        if (mulai == TimeSpan.Zero) return;
        var konflik = await db.FirstAsync("""
            SELECT no_rawat FROM booking_operasi
            WHERE kd_ruang_ok=@p0 AND tanggal=@p1 AND no_rawat<>@p2 AND jam_mulai<>'00:00:00'
            AND jam_mulai<=@p4 AND GREATEST(jam_mulai,COALESCE(jam_selesai,jam_mulai))>=@p3
            LIMIT 1 FOR UPDATE
            """, ruang, values["tanggal"], noRawat, mulai, selesai == TimeSpan.Zero ? mulai : selesai);
        if (konflik is not null) throw new InvalidOperationException("Jadwal bentrok dengan operasi lain di ruang tersebut.");
    }

    internal static Task CekHapusDenganBillingAsync(Db db, Dictionary<string, object?> keys)
        => KunciKunjunganAsync(db, keys["no_rawat"], cekBilling: true);

    internal static async Task CekDietAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        await KunciKunjunganAsync(db, values["no_rawat"]);
        if (await db.CountAsync("""
            SELECT COUNT(*) FROM kamar_inap
            WHERE no_rawat=COALESCE((SELECT rg.no_rawat FROM ranap_gabung rg WHERE rg.no_rawat2=@p0 LIMIT 1),@p0) AND kd_kamar=@p1
            AND tgl_masuk<=@p2 AND (stts_pulang='-' OR tgl_keluar>=@p2)
            """, values["no_rawat"], values["kd_kamar"], values["tanggal"]) == 0)
            throw new InvalidOperationException("Pasien tidak tercatat menempati kamar tersebut pada tanggal diet.");
    }

    internal static async Task CekPerkiraanBiayaAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        await KunciKunjunganAsync(db, values["no_rawat"]);
        if (await db.CountAsync("SELECT COUNT(*) FROM kamar_inap WHERE no_rawat=" + RawatGabungService.PemilikKamar("@p0"), values["no_rawat"]) == 0)
            throw new InvalidOperationException("Pilih pasien yang sudah tercatat masuk rawat inap.");
        _ = Positif(values, "tarif", "Perkiraan biaya");
    }

    internal static async Task CekSesiKfrAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        var sesi = await KunciKunjunganAsync(db, values["no_rawat"]);
        var permintaan = await db.FirstAsync("""
            SELECT x.status_program,r.no_rkm_medis FROM layanan_kedokteran_fisik_rehabilitasi x
            JOIN reg_periksa r ON r.no_rawat=x.no_rawat WHERE x.no_rawat=@p0 FOR UPDATE
            """, values["no_rawat_layanan"]);
        if (permintaan is null || permintaan.S("no_rkm_medis") != sesi.S("no_rkm_medis"))
            throw new InvalidOperationException("Pilih No. Rawat dari pasien yang sama dengan permintaan KFR.");
        if (keys is null && permintaan.S("status_program") != "Belum Selesai")
            throw new InvalidOperationException("Program telah selesai. Buka kembali sebelum menambah sesi.");
    }

    internal static async Task PostDepositAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        var uangMuka = await db.StrAsync("SELECT Uang_Muka_Ranap FROM set_akun_ranap2 LIMIT 1 FOR UPDATE");
        var tanggal = ((DateTime)values["tgl_deposit"]!).Date;
        values["no_deposit"] = await db.AutoNomorAsync(NomorDepositSql, $"DP{tanggal:yyyyMMdd}", 4, tanggal);
        if (((string)values["no_deposit"]!).Length != 14) throw new InvalidOperationException("Nomor deposit harian sudah habis.");
        var row = await KunciKunjunganAsync(db, values["no_rawat"], cekBilling: true);
        var akun = await AkunBayarAsync(db, values["nama_bayar"]);
        var jumlah = Positif(values, "besar_deposit", "Deposit");
        var ppn = jumlah * akun.D("ppn") / 100;
        if (!double.IsFinite(ppn) || ppn < 0) throw new InvalidOperationException("PPN/admin akun bayar tidak valid.");
        values["besarppn"] = ppn;
        await PostJurnalDepositAsync(db, (string)values["no_deposit"]!, values["no_rawat"], row,
            [new(akun.S("kd_rek"), akun.S("nama_bayar"), jumlah, 0), new(uangMuka, "UANG MUKA RANAP", 0, jumlah)], batal: false);
        await db.ExecAsync("""
            INSERT INTO tagihan_sadewa
            (no_nota,no_rkm_medis,nama_pasien,alamat,tgl_bayar,jenis_bayar,jumlah_tagihan,jumlah_bayar,status,petugas)
            VALUES (@p0,@p1,@p2,'-',@p3,'Uang Muka',@p4,@p4,'Belum',@p5)
            """, values["no_deposit"], row.S("no_rkm_medis"), row.S("nm_pasien"), values["tgl_deposit"], jumlah, KodePetugas);
    }

    internal static async Task BalikDepositAsync(Db db, Dictionary<string, object?> keys)
    {
        var nomor = (string)keys["no_deposit"]!;
        var deposit = await db.FirstAsync("""
            SELECT no_rawat FROM deposit WHERE no_deposit=@p0 FOR UPDATE
            """, nomor) ?? throw new InvalidOperationException("Deposit tidak ditemukan.");
        var row = await KunciKunjunganAsync(db, deposit["no_rawat"], cekBilling: true);
        if (await db.CountAsync("SELECT COUNT(*) FROM pengembalian_deposit WHERE no_rawat=@p0", deposit["no_rawat"]) > 0)
            throw new InvalidOperationException("Pengembalian deposit sudah tercatat untuk kunjungan ini.");
        var detail = await db.QueryAsync("""
            SELECT d.kd_rek,d.debet,d.kredit FROM detailjurnal d
            JOIN jurnal j ON j.no_jurnal=d.no_jurnal WHERE j.no_jurnal=(
                SELECT no_jurnal FROM jurnal WHERE no_bukti=@p0 AND keterangan LIKE 'DEPOSIT PASIEN %'
                ORDER BY tgl_jurnal DESC,jam_jurnal DESC,no_jurnal DESC LIMIT 1)
            """, nomor);
        if (detail.Count == 0) throw new InvalidOperationException("Jurnal awal deposit tidak ditemukan.");
        await PostJurnalDepositAsync(db, nomor, deposit["no_rawat"], row,
            detail.Select(d => new JurnalLine(d.S("kd_rek"), "", d.D("kredit"), d.D("debet"))), batal: true);
        await db.ExecAsync("DELETE FROM tagihan_sadewa WHERE no_nota=@p0 AND jenis_bayar='Uang Muka'", nomor);
    }

    private static async Task<Row> AkunBayarAsync(Db db, object? akun) =>
        await db.FirstAsync("SELECT nama_bayar,kd_rek,ppn FROM akun_bayar WHERE nama_bayar=@p0", akun)
            ?? throw new InvalidOperationException("Akun bayar tidak ditemukan.");

    private static async Task PostJurnalDepositAsync(Db db, string nomor, object? noRawat, Row pasien,
        IEnumerable<JurnalLine> lines, bool batal)
    {
        var ket = $"{(batal ? "PEMBATALAN " : "")}DEPOSIT PASIEN {noRawat} {pasien.S("no_rkm_medis")} {pasien.S("nm_pasien")}, OLEH {KodePetugas}";
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, nomor, ket, lines);
    }

    internal static async Task KeluarkanResepAsync(Db db, Dictionary<string, object?> values, Dictionary<string, object?>? keys)
    {
        var pasien = await KunciKunjunganAsync(db, values["no_rawat"], cekBilling: true);
        if (await db.CountAsync("""
            SELECT COUNT(*) FROM permintaan_resep_pulang
            WHERE no_rawat=@p0 AND tgl_validasi=@p1 AND jam_validasi=@p2 AND status='Sudah'
            """, values["no_rawat"], values["tanggal"], values["jam"]) != 0)
            throw new InvalidOperationException("Waktu sudah digunakan oleh validasi permintaan resep. Gunakan waktu pengeluaran yang berbeda.");
        var jumlah = Positif(values, "jml_barang", "Jumlah obat");
        var harga = Convert.ToDouble(values["harga"], CultureInfo.InvariantCulture);
        if (!double.IsFinite(harga) || harga < 0 || !double.IsFinite(harga * jumlah)) throw new ArgumentException("Harga obat tidak valid.");
        var item = Item((string)values["kode_brng"]!, (string)values["no_batch"]!, (string)values["no_faktur"]!);
        var depo = (string)values["kd_bangsal"]!;
        if (string.IsNullOrEmpty(item.NoBatch) != string.IsNullOrEmpty(item.NoFaktur))
            throw new ArgumentException("Isi nomor batch dan faktur bersama-sama untuk obat berbatch.");
        await MutasiResepAsync(db, item, depo, jumlah, masuk: false, $"{values["no_rawat"]} {pasien.S("nm_pasien")}");
        values["total"] = harga * jumlah;
    }

    internal static async Task KembalikanResepAsync(Db db, Dictionary<string, object?> keys)
    {
        var pasien = await KunciKunjunganAsync(db, keys["no_rawat"], cekBilling: true);
        if (await db.CountAsync("""
            SELECT COUNT(*) FROM permintaan_resep_pulang
            WHERE no_rawat=@p0 AND tgl_validasi=@p1 AND jam_validasi=@p2 AND status='Sudah'
            """, keys["no_rawat"], keys["tanggal"], keys["jam"]) != 0)
            throw new InvalidOperationException("Obat ini berasal dari validasi permintaan resep. Gunakan Retur Validasi Resep untuk mengembalikan seluruh permintaan.");
        var resep = await db.FirstAsync("""
            SELECT jml_barang,kd_bangsal FROM resep_pulang
            WHERE no_rawat=@p0 AND kode_brng=@p1 AND tanggal=@p2 AND jam=@p3 AND no_batch=@p4 AND no_faktur=@p5 FOR UPDATE
            """, keys["no_rawat"], keys["kode_brng"], keys["tanggal"], keys["jam"], keys["no_batch"], keys["no_faktur"])
            ?? throw new InvalidOperationException("Baris resep tidak ditemukan.");
        var item = Item((string)keys["kode_brng"]!, (string)keys["no_batch"]!, (string)keys["no_faktur"]!);
        await MutasiResepAsync(db, item, resep.S("kd_bangsal"), resep.D("jml_barang"), masuk: true,
            $"{keys["no_rawat"]} {pasien.S("nm_pasien")}");
    }

    private static StokItem Item(string kode, string batch, string faktur) => new(kode, "", "", "", batch, faktur, 0, 0, null);

    internal static async Task MutasiResepAsync(Db db, StokItem item, string depo, double jumlah, bool masuk, string ket, string? petugas = null)
    {
        var stok = await db.FirstAsync("""
            SELECT stok FROM gudangbarang WHERE kode_brng=@p0 AND kd_bangsal=@p1 AND no_batch=@p2 AND no_faktur=@p3 FOR UPDATE
            """, item.Kode, depo, item.NoBatch, item.NoFaktur);
        if (!masuk && (stok is null || stok.D("stok") < jumlah))
            throw new InvalidOperationException("Stok di depo/batch tersebut tidak cukup.");
        var delta = masuk ? jumlah : -jumlah;
        if (item.NoBatch.Length > 0 && await db.ExecAsync("""
            UPDATE data_batch SET sisa=sisa+@p0 WHERE kode_brng=@p1 AND no_batch=@p2 AND no_faktur=@p3 AND sisa+@p0>=0
            """, delta, item.Kode, item.NoBatch, item.NoFaktur) != 1)
            throw new InvalidOperationException("Batch obat tidak ditemukan atau sisa batch tidak cukup.");
        await FarmasiStokRepository.CatatAsync(db, item, masuk ? jumlah : 0, masuk ? 0 : jumlah, "Resep Pulang",
            petugas ?? KodePetugas, depo, ket, masuk ? "Hapus" : "Simpan");
        await FarmasiStokRepository.TambahStokAsync(db, item, depo, delta);
    }

    private static string KodePetugas => AppSession.CurrentUser?.KodeLegacy ?? "";

    private static double Positif(Dictionary<string, object?> values, string column, string label)
    {
        var v = Convert.ToDouble(values[column], CultureInfo.InvariantCulture);
        return double.IsFinite(v) && v > 0 ? v : throw new ArgumentException($"{label} harus lebih dari nol.");
    }
}
