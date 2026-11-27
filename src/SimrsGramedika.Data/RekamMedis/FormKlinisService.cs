using System.Globalization;
using MySqlConnector;
using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.RekamMedis;

public sealed record KunjunganKlinis(string NoRawat, string NoRekamMedis, string Pasien, string JenisKelamin,
    DateTime? TanggalLahir, string Umur, DateTime Registrasi);

/// <summary>Satu catatan tersimpan; dipakai juga sebagai pembanding agar perubahan orang lain tidak tertimpa.</summary>
public sealed record CatatanKlinis(DateTime Waktu, string Pemilik, string NamaPemilik,
    IReadOnlyDictionary<string, string> Isi)
{
    internal bool SamaDengan(CatatanKlinis lain) => Waktu == lain.Waktu && Pemilik == lain.Pemilik &&
        Isi.Count == lain.Isi.Count && Isi.All(x => lain.Isi.TryGetValue(x.Key, out var v) && v == x.Value);
}

public sealed record CetakKlinis(KunjunganKlinis Kunjungan, CatatanKlinis Catatan, string IdPemilik);

/// <summary>
/// Aturan bersama formulir rekam medis Khanza: hak menu untuk semua aksi; selain Admin Utama pemilik catatan adalah
/// akun yang login, hanya pemilik yang boleh mengubah atau menghapus, dengan batas waktu bila formulirnya memintanya.
/// </summary>
public sealed class FormKlinisService(DatabaseSettings settings, AuthenticatedUser user, FormKlinis form, string noRawat)
{
    private static readonly TimeSpan Batas = TimeSpan.FromHours(48);

    public async Task<(KunjunganKlinis Kunjungan, IReadOnlyList<CatatanKlinis> Catatan)> MuatAsync()
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var kunjungan = await KunjunganAsync(db);
        var urut = form.KolomJam is null ? $"s.{form.KolomTanggal} DESC" : $"s.{form.KolomTanggal} DESC,s.{form.KolomJam} DESC";
        var rows = await db.QueryAsync($"{Pilih} WHERE s.no_rawat=@p0 ORDER BY {urut}", noRawat);
        return (kunjungan, rows.Select(Baca).ToList());
    }

    /// <summary>Pemilik catatan untuk akun yang login; null untuk Admin Utama, yang memilih sendiri.</summary>
    public async Task<(string Kode, string Nama)?> PemilikAkunAsync()
    {
        PastikanIzin();
        if (user.IsAdminUtama) return null;
        await using var db = await Db.OpenAsync(settings);
        return (user.UserId, await NamaPemilikAsync(db, user.UserId));
    }

    public async Task<IReadOnlyList<Row>> CariPemilikAsync(string kata)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var (tabel, kode, nama) = TabelPemilik;
        return await db.QueryAsync(
            $"SELECT {kode} AS kode,{nama} AS nama FROM {tabel} WHERE CONCAT_WS(' ',{kode},{nama}) LIKE @p0 ORDER BY {nama} LIMIT 100",
            $"%{kata.Trim()}%");
    }

    /// <summary>Akun ini tidak boleh memilih tanggal dan jam catatan.</summary>
    public bool TanggalTerkunci => form.IkutKunciTanggal && KunciTanggal.Aktif(settings, user.IsAdminUtama);

    public Task SimpanAsync(DateTime waktu, string pemilik, IReadOnlyDictionary<string, string> isi) =>
        TulisAsync(null, waktu, pemilik, isi);

    public Task UbahAsync(CatatanKlinis lama, DateTime waktu, string pemilik, IReadOnlyDictionary<string, string> isi) =>
        TulisAsync(lama, waktu, pemilik, isi);

    private async Task TulisAsync(CatatanKlinis? lama, DateTime waktu, string pemilik, IReadOnlyDictionary<string, string> isi)
    {
        PastikanIzin();
        waktu = waktu.AddTicks(-(waktu.Ticks % TimeSpan.TicksPerSecond));
        pemilik = (user.IsAdminUtama ? pemilik : user.UserId).Trim();
        var nilai = Periksa(isi, lama);

        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        if (lama is not null) await KunciAsync(db, lama);
        waktu = await KunciTanggal.WaktuAsync(db, TanggalTerkunci, waktu, lama?.Waktu);
        var kunjungan = await KunjunganAsync(db);
        if (pemilik.Length == 0) throw new InvalidOperationException($"Pilih {form.SebutanPemilik} terlebih dahulu.");
        await NamaPemilikAsync(db, pemilik);
        if (!user.IsAdminUtama && form.CekWaktuRegistrasi && waktu < kunjungan.Registrasi)
            throw new InvalidOperationException($"Waktu catatan minimal {kunjungan.Registrasi:dd-MM-yyyy HH:mm:ss}, sesuai waktu registrasi.");

        var kolom = new List<string> { "no_rawat", form.KolomTanggal };
        var args = new List<object?> { noRawat, form.KolomJam is null ? waktu : waktu.Date };
        if (form.KolomJam is { } jam)
        {
            kolom.Add(jam);
            args.Add(waktu.TimeOfDay);
        }
        kolom.Add(form.KolomPemilik);
        args.Add(pemilik);
        foreach (var isian in form.Isian)
        {
            kolom.Add(isian.Kolom);
            var teks = nilai[isian.Kolom];
            // Pilihan dan angka hitungan yang kosong disimpan NULL; teks kosong tetap string kosong seperti Khanza.
            args.Add(teks.Length == 0 && (isian.Pilihan is not null || isian.Otomatis) ? null : teks);
        }

        try
        {
            if (lama is null)
                await db.ExecAsync($"INSERT INTO {form.Tabel} ({string.Join(',', kolom)}) VALUES ({string.Join(',', kolom.Select((_, i) => $"@p{i}"))})",
                    args.ToArray());
            else
            {
                var set = string.Join(',', kolom.Select((k, i) => $"{k}=@p{i}"));
                var (syarat, kunci) = Kunci(lama, args.Count);
                await db.ExecAsync($"UPDATE {form.Tabel} SET {set} WHERE {syarat}", [.. args, .. kunci]);
            }
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new InvalidOperationException(form.SatuPerKunjungan
                ? "Kunjungan ini sudah punya penilaian. Pilih data yang ada untuk mengubahnya."
                : "Sudah ada catatan pada waktu tersebut. Ganti jamnya.", ex);
        }
        await db.CommitAsync();
    }

    public async Task HapusAsync(CatatanKlinis lama)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunciAsync(db, lama);
        var (syarat, kunci) = Kunci(lama, 0);
        await db.ExecAsync($"DELETE FROM {form.Tabel} WHERE {syarat}", kunci);
        await db.CommitAsync();
    }

    public async Task<CetakKlinis> CetakAsync(CatatanKlinis catatan)
    {
        PastikanIzin();
        await using var db = await Db.OpenAsync(settings);
        var (syarat, kunci) = Kunci(catatan, 0, "s.");
        var row = await db.FirstAsync($"{Pilih} WHERE {syarat}", kunci)
            ?? throw new InvalidOperationException("Catatan sudah tidak ada. Muat ulang daftar.");
        var terbaru = Baca(row);
        return new(await KunjunganAsync(db), terbaru, await TandaTanganSurat.IdAsync(db, terbaru.Pemilik));
    }

    /// <summary>Mengunci baris tersimpan, memastikan belum berubah, lalu memeriksa hak ubah/hapus pada data itu.</summary>
    private async Task KunciAsync(Db db, CatatanKlinis lama)
    {
        var (syarat, kunci) = Kunci(lama, 0, "s.");
        var row = await db.FirstAsync($"{Pilih} WHERE {syarat} FOR UPDATE", kunci);
        if (row is null || !Baca(row).SamaDengan(lama))
            throw new InvalidOperationException("Catatan sudah diubah atau dihapus. Muat ulang daftar.");
        if (user.IsAdminUtama) return;
        var tersimpan = Baca(row);
        if (!tersimpan.Pemilik.Equals(user.UserId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Hanya bisa diubah atau dihapus oleh {form.SebutanPemilik} yang bersangkutan.");
        var sekarang = (DateTime)(await db.ScalarAsync("SELECT NOW()"))!;
        if (form.Batas48Jam && sekarang - tersimpan.Waktu > Batas)
            throw new InvalidOperationException("Perubahan atau penghapusan data tidak boleh lebih dari 2 x 24 jam.");
    }

    private Dictionary<string, string> Periksa(IReadOnlyDictionary<string, string> isi, CatatanKlinis? lama)
    {
        var nilai = form.Isian.ToDictionary(i => i.Kolom, i => (isi.GetValueOrDefault(i.Kolom) ?? "").Trim());
        form.Hitung?.Invoke(nilai);
        foreach (var isian in form.Isian.Where(i => !i.Otomatis))
        {
            var teks = nilai[isian.Kolom];
            if (isian.Wajib && teks.Length == 0) throw new ArgumentException($"{isian.Label} wajib diisi.");
            if (isian.Maks > 0 && teks.Length > isian.Maks)
                throw new ArgumentException($"{isian.Label} maksimum {isian.Maks} karakter.");
            // Nilai lama di luar daftar (misalnya dari versi Khanza lain) boleh dipertahankan, tidak boleh dibuat baru.
            if (isian.Pilihan is { } pilihan && !pilihan.Contains(teks) && lama?.Isi.GetValueOrDefault(isian.Kolom) != teks)
                throw new ArgumentException($"Pilih {isian.Label} dari daftar.");
        }
        if (form.Periksa?.Invoke(nilai) is { } pesan) throw new ArgumentException(pesan);
        return nilai;
    }

    private string Pilih
    {
        get
        {
            var (tabel, kode, nama) = TabelPemilik;
            return $"SELECT s.*,m.{nama} AS nama_pemilik FROM {form.Tabel} s LEFT JOIN {tabel} m ON m.{kode}=s.{form.KolomPemilik}";
        }
    }

    private (string Tabel, string Kode, string Nama) TabelPemilik => form.Pemilik switch
    {
        PemilikForm.Dokter => ("dokter", "kd_dokter", "nm_dokter"),
        PemilikForm.Pegawai => ("pegawai", "nik", "nama"),
        _ => ("petugas", "nip", "nama"),
    };

    private (string Syarat, object?[] Args) Kunci(CatatanKlinis catatan, int mulai, string awalan = "")
    {
        var syarat = $"{awalan}no_rawat=@p{mulai}";
        if (form.SatuPerKunjungan) return (syarat, [noRawat]);
        syarat += $" AND {awalan}{form.KolomTanggal}=@p{mulai + 1}";
        if (form.KolomJam is not { } jam) return (syarat, [noRawat, catatan.Waktu]);
        return ($"{syarat} AND {awalan}{jam}=@p{mulai + 2}", [noRawat, catatan.Waktu.Date, catatan.Waktu.TimeOfDay]);
    }

    private CatatanKlinis Baca(Row row)
    {
        var waktu = row.Dt(form.KolomTanggal) ?? throw new InvalidOperationException("Tanggal catatan tidak terbaca.");
        if (form.KolomJam is { } jam && row[jam] is TimeSpan t) waktu = waktu.Date + t;
        return new(waktu, row.S(form.KolomPemilik), row.S("nama_pemilik"),
            form.Isian.ToDictionary(i => i.Kolom, i => Convert.ToString(row[i.Kolom], CultureInfo.InvariantCulture) ?? ""));
    }

    private async Task<KunjunganKlinis> KunjunganAsync(Db db)
    {
        var r = await db.FirstAsync("""
            SELECT r.no_rkm_medis,p.nm_pasien,p.jk,p.tgl_lahir,r.umurdaftar,r.sttsumur,r.tgl_registrasi,r.jam_reg
            FROM reg_periksa r JOIN pasien p ON p.no_rkm_medis=r.no_rkm_medis WHERE r.no_rawat=@p0
            """, noRawat) ?? throw new InvalidOperationException("Kunjungan tidak ditemukan.");
        var registrasi = (r.Dt("tgl_registrasi") ?? DateTime.MinValue).Date + (r["jam_reg"] is TimeSpan jam ? jam : TimeSpan.Zero);
        return new(noRawat, r.S("no_rkm_medis"), r.S("nm_pasien"), r.S("jk") == "P" ? "Perempuan" : "Laki-Laki",
            r.Dt("tgl_lahir"), $"{r.S("umurdaftar")} {r.S("sttsumur")}".Trim(), registrasi);
    }

    private async Task<string> NamaPemilikAsync(Db db, string kode)
    {
        var (tabel, kolom, nama) = TabelPemilik;
        var row = await db.FirstAsync($"SELECT {nama} AS nama FROM {tabel} WHERE {kolom}=@p0", kode)
            ?? throw new InvalidOperationException(user.IsAdminUtama
                ? $"{kode} tidak terdaftar sebagai {form.SebutanPemilik}."
                : $"Akun yang login bukan {form.SebutanPemilik}.");
        return row.S("nama");
    }

    private void PastikanIzin()
    {
        if (!user.HasPermission(form.Izin)) throw new UnauthorizedAccessException($"Akun tidak memiliki hak {form.Judul}.");
        if (string.IsNullOrWhiteSpace(noRawat)) throw new ArgumentException("Pilih kunjungan terlebih dahulu.");
    }
}
