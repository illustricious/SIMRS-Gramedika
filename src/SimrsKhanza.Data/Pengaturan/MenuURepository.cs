using SimrsKhanza.Config;
using SimrsKhanza.Core.Auth;
using SimrsKhanza.Data.Farmasi;
using SimrsKhanza.Data.Sql;

namespace SimrsKhanza.Data.Pengaturan;

public record PengaturanDisplay(string Teks, bool Aktif, byte[]? Gambar);

public sealed class MenuURepository(DatabaseSettings settings)
{
    private static string DisplayTable(bool apotek) => apotek ? "runtextapotek" : "runtext";

    public async Task<PengaturanDisplay?> MuatDisplayAsync(bool apotek)
    {
        await using var db = await Db.OpenAsync(settings);
        var row = await db.FirstAsync($"SELECT teks,aktifkan,gambar FROM {DisplayTable(apotek)} LIMIT 1");
        return row is null ? null : new(row.S("teks"), row.S("aktifkan") == "Yes", row["gambar"] as byte[]);
    }

    public async Task SimpanDisplayAsync(bool apotek, string teks, bool aktif, byte[]? gambar)
    {
        Require(apotek ? "display_apotek" : "display");
        if (System.Text.Encoding.UTF8.GetByteCount(teks) > 65535) throw new ArgumentException("Teks display terlalu panjang.");
        await using var db = await Db.OpenAsync(settings);
        var table = DisplayTable(apotek);
        await db.WithSettingsLockAsync(table, async () =>
        {
            var count = await db.CountAsync($"SELECT COUNT(*) FROM {table}");
            if (count > 1) throw new InvalidOperationException("Ada lebih dari satu konfigurasi display di Khanza.");
            var status = aktif ? "Yes" : "No";
            if (count == 0)
                await db.ExecAsync($"INSERT INTO {table} (teks,aktifkan,gambar) VALUES (@p0,@p1,@p2)", teks, status, gambar ?? []);
            else
                await db.ExecAsync($"UPDATE {table} SET teks=@p0,aktifkan=@p1,gambar=COALESCE(@p2,gambar) LIMIT 1", teks, status, gambar);
        });
    }

    public async Task<IReadOnlyList<Row>> DaftarPenjaminAsync()
    {
        Require("password_asuransi");
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT p.kd_pj,p.png_jawab,IF(a.kd_pj IS NULL,'Belum diatur','Tersimpan') AS status
            FROM penjab p LEFT JOIN password_asuransi a ON a.kd_pj=p.kd_pj ORDER BY p.png_jawab
            """);
    }

    public async Task SimpanAkunAsuransiAsync(string penjamin, string user, string password)
    {
        Require("password_asuransi");
        if (string.IsNullOrWhiteSpace(penjamin)) throw new ArgumentException("Pilih cara bayar.");
        CekKredensial(user, password);
        await using var db = await Db.OpenAsync(settings);
        // Target the payer's primary key. A unique-key collision with another payer must never update that payer.
        await db.WithSettingsLockAsync("password_asuransi", async () =>
        {
            if (await db.CountAsync("SELECT COUNT(*) FROM password_asuransi WHERE kd_pj=@p0", penjamin) > 0)
                await db.ExecAsync("""
                    UPDATE password_asuransi SET usere=AES_ENCRYPT(@p1,'nur'),passworde=AES_ENCRYPT(@p2,'windi') WHERE kd_pj=@p0
                    """, penjamin, user, password);
            else
                await db.ExecAsync("""
                    INSERT INTO password_asuransi (kd_pj,usere,passworde) VALUES (@p0,AES_ENCRYPT(@p1,'nur'),AES_ENCRYPT(@p2,'windi'))
                    """, penjamin, user, password);
        });
    }

    public async Task HapusAkunAsuransiAsync(string penjamin)
    {
        Require("password_asuransi");
        await using var db = await Db.OpenAsync(settings);
        await db.WithSettingsLockAsync("password_asuransi", async () =>
        {
            await db.ExecAsync("DELETE FROM password_asuransi WHERE kd_pj=@p0", penjamin);
        });
    }

    public async Task<bool> AdaAkunEksekutifAsync()
    {
        Require(LegacyTilePermissions.AdminUtamaKey);
        await using var db = await Db.OpenAsync(settings);
        return await db.CountAsync("SELECT COUNT(*) FROM e_eksekutif") > 0;
    }

    public async Task SimpanAkunEksekutifAsync(string user, string password)
    {
        Require(LegacyTilePermissions.AdminUtamaKey);
        CekKredensial(user, password);
        await using var db = await Db.OpenAsync(settings);
        await db.WithSettingsLockAsync("e_eksekutif", async () =>
        {
            var count = await db.CountAsync("SELECT COUNT(*) FROM e_eksekutif");
            if (count > 1) throw new InvalidOperationException("Ada lebih dari satu akun E-Eksekutif di Khanza.");
            if (count == 0)
                await db.ExecAsync("""
                    INSERT INTO e_eksekutif (usere,passworde) VALUES (AES_ENCRYPT(@p0,'nur'),AES_ENCRYPT(@p1,'windi'))
                    """, user, password);
            else
                await db.ExecAsync("""
                    UPDATE e_eksekutif SET usere=AES_ENCRYPT(@p0,'nur'),passworde=AES_ENCRYPT(@p1,'windi') LIMIT 1
                    """, user, password);
        });
    }

    public async Task HapusAkunEksekutifAsync()
    {
        Require(LegacyTilePermissions.AdminUtamaKey);
        await using var db = await Db.OpenAsync(settings);
        await db.WithSettingsLockAsync("e_eksekutif", async () =>
        {
            if (await db.CountAsync("SELECT COUNT(*) FROM e_eksekutif") > 1)
                throw new InvalidOperationException("Ada lebih dari satu akun E-Eksekutif di Khanza.");
            await db.ExecAsync("DELETE FROM e_eksekutif LIMIT 1");
        });
    }

    private static void CekKredensial(string user, string password)
    {
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) ||
            System.Text.Encoding.UTF8.GetByteCount(user) > 512 || System.Text.Encoding.UTF8.GetByteCount(password) > 512)
            throw new ArgumentException("Isi ID dan kata sandi yang valid.");
    }

    public async Task<IReadOnlyList<Row>> DaftarTabelAsync(string? cari)
    {
        Require(LegacyTilePermissions.AdminUtamaKey);
        await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync("""
            SELECT table_name AS nama,engine AS mesin,table_rows AS perkiraan
            FROM information_schema.tables WHERE table_schema=DATABASE() AND table_type='BASE TABLE' AND table_name LIKE @p0
            ORDER BY table_name
            """, $"%{cari?.Trim()}%");
    }

    public async Task<int> KosongkanTabelAsync(string table, string konfirmasi)
    {
        Require(LegacyTilePermissions.AdminUtamaKey);
        await using var db = await Db.OpenAsync(settings);
        var expected = $"{db.Conn.Database}.{table}";
        if (!string.Equals(konfirmasi, expected, StringComparison.Ordinal))
            throw new ArgumentException($"Ketik {expected} untuk mengonfirmasi.");
        if (await db.CountAsync("""
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema=DATABASE() AND table_type='BASE TABLE' AND BINARY table_name=BINARY @p0
            """, table) != 1)
            throw new InvalidOperationException("Tabel tidak ditemukan dalam database aktif.");
        // Quote the verified metadata name. Keep all server-side foreign-key protections enabled.
        const string quote = "`";
        var identifier = quote + table.Replace(quote, quote + quote, StringComparison.Ordinal) + quote;
        return await db.ExecAsync("DELETE FROM " + identifier);
    }

    public async Task<int> TerapkanMarginAsync(string table, string? key)
    {
        Require("set_harga_obat");
        var column = table switch
        {
            "setpenjualanumum" => null,
            "setpenjualan" => "kdjns",
            "setpenjualanperbarang" => "kode_brng",
            _ => throw new ArgumentException("Sumber margin tidak valid.", nameof(table))
        };
        if (column is not null && string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Pilih satu baris margin.");

        await using var db = await Db.OpenAsync(settings);
        var filter = column is null ? "" : $"WHERE {column}=@p0";
        var rows = await db.QueryAsync($"SELECT * FROM {table} {filter} LIMIT 2", column is null ? [] : [key]);
        if (rows.Count != 1) throw new InvalidOperationException("Simpan satu baris pengaturan margin yang valid terlebih dahulu.");
        var margin = HargaObat.JualCols.Select(rows[0].D).ToArray();
        if (HargaObat.JualCols.Any(c => rows[0][c] is null) || margin.Any(v => !double.IsFinite(v) || v < -100))
            throw new InvalidOperationException("Isi semua margin dengan persentase yang valid.");
        var set = string.Join(",", HargaObat.JualCols.Select((c, i) => $"{c}=ROUND(h_beli*(1+@p{i}/100))"));
        var where = column is null ? "" : $"WHERE {column}=@p{margin.Length}";
        var args = margin.Cast<object?>().ToList();
        if (column is not null) args.Add(key);
        return await db.ExecAsync($"UPDATE databarang SET {set} {where}", args.ToArray());
    }

    private static void Require(string permission)
    {
        var user = AppSession.CurrentUser;
        if (user?.IsAdminUtama == true || (permission != LegacyTilePermissions.AdminUtamaKey && user?.HasPermission(permission) == true))
            return;
        throw new UnauthorizedAccessException("Akun ini tidak memiliki hak akses untuk pengaturan tersebut.");
    }
}
