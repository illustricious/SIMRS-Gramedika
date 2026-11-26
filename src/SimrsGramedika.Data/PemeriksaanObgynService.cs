using SimrsGramedika.Config;
using SimrsGramedika.Core.Auth;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data;

public sealed record ObgynField(string Name, string Label, int Max, string[]? Choices = null);

public sealed class PemeriksaanObgynService(DatabaseSettings settings, string noRawat, AuthenticatedUser user, bool obstetri)
{
    private static readonly string[] PlusMinus = ["-", "+"];
    public static readonly ObgynField[] Obstetri = [
        new("tinggi_uteri","Tinggi uteri",5), new("janin","Janin",0,["-","Tunggal","Gemelli"]), new("letak","Letak",50),
        new("panggul","Panggul",0,["-","5/5","4/5","3/5","2/5","1/5"]), new("denyut","Denyut",5), new("kontraksi","Kontraksi",0,PlusMinus),
        new("kualitas_mnt","Kualitas menit",5), new("kualitas_dtk","Kualitas detik",5), new("fluksus","Fluksus",0,PlusMinus), new("albus","Albus",0,PlusMinus),
        new("vulva","Vulva",50), new("portio","Portio",50), new("dalam","Dalam",0,["Kenyal","Lunak"]), new("tebal","Tebal",5),
        new("arah","Arah",0,["depan","axial","belakang"]), new("pembukaan","Pembukaan",50), new("penurunan","Penurunan",50), new("denominator","Denominator",50),
        new("ketuban","Ketuban",0,PlusMinus), new("feto","Feto",0,["Normal","Susp.CPD-FPD","CPD-FPD"])];
    public static readonly ObgynField[] Ginekologi = [
        new("inspeksi","Inspeksi",50), new("inspeksi_vulva","Inspeksi vulva",50), new("inspekulo_gine","Inspekulo",50),
        new("fluxus_gine","Fluksus",0,PlusMinus), new("fluor_gine","Fluor",0,PlusMinus), new("vulva_inspekulo","Vulva inspekulo",50),
        new("portio_inspekulo","Portio inspekulo",50), new("sondage","Sondage",50), new("portio_dalam","Portio dalam",50), new("bentuk","Bentuk",50),
        new("cavum_uteri","Cavum uteri",50), new("mobilitas","Mobilitas",0,PlusMinus), new("ukuran","Ukuran",50), new("nyeri_tekan","Nyeri tekan",0,PlusMinus),
        new("adnexa_kanan","Adnexa kanan",50), new("adnexa_kiri","Adnexa kiri",50), new("cavum_douglas","Cavum Douglas",50)];
    public ObgynField[] Fields => obstetri ? Obstetri : Ginekologi;
    private string Table => obstetri ? "pemeriksaan_obstetri_ralan" : "pemeriksaan_ginekologi_ralan";
    private void Izin() { if (!user.HasPermission("tindakan_ralan")) throw new InvalidOperationException("Akun tidak memiliki hak tindakan rawat jalan."); }

    public async Task<List<Row>> DaftarAsync()
    {
        Izin(); await using var db = await Db.OpenAsync(settings);
        return await db.QueryAsync($"SELECT * FROM {Table} WHERE no_rawat=@p0 ORDER BY tgl_perawatan DESC,jam_rawat DESC", noRawat);
    }

    public async Task SimpanAsync(DateTime waktu, IReadOnlyList<string> values)
    {
        Izin();
        Validasi(values);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunciKunjunganAsync(db, waktu);
        var cols = string.Join(",", Fields.Select(f => $"`{f.Name}`"));
        var parameters = string.Join(",", Fields.Select((_, i) => $"@p{i + 3}"));
        await db.ExecAsync($"INSERT INTO {Table}(no_rawat,tgl_perawatan,jam_rawat,{cols}) VALUES(@p0,@p1,@p2,{parameters})", [noRawat, waktu.Date, waktu.TimeOfDay, .. values.Cast<object?>()]);
        await db.CommitAsync();
    }

    private void Validasi(IReadOnlyList<string> values)
    {
        if (values.Count != Fields.Length) throw new ArgumentException("Jumlah isian tidak sesuai.");
        for (var i = 0; i < Fields.Length; i++)
        {
            var f = Fields[i];
            if (f.Choices is not null && !f.Choices.Contains(values[i])) throw new ArgumentException($"Pilihan {f.Label} tidak valid.");
            if (f.Choices is null && values[i].Length > f.Max) throw new ArgumentException($"Isian {f.Label} terlalu panjang.");
        }
        if (!Fields.Select((f, i) => f.Choices is null && !string.IsNullOrWhiteSpace(values[i])).Any(x => x)) throw new ArgumentException("Isi minimal satu hasil pemeriksaan.");
    }

    private async Task KunciKunjunganAsync(Db db, DateTime waktu)
    {
        var visit = await db.FirstAsync("SELECT tgl_registrasi,jam_reg FROM reg_periksa WHERE no_rawat=@p0 AND status_lanjut='Ralan' AND stts<>'Batal' FOR UPDATE", noRawat)
            ?? throw new InvalidOperationException("Kunjungan tidak ditemukan atau dibatalkan.");
        if (!user.IsAdminUtama)
        {
            if (visit.Dt("tgl_registrasi") is not { } date || visit["jam_reg"] is not TimeSpan time)
                throw new InvalidOperationException("Waktu registrasi tidak valid.");
            RawatJalanService.PastikanTidakSebelumRegistrasi(date.Date + time, waktu);
        }
    }

    private async Task KunciAsalAsync(Db db, Row asal)
    {
        var date = asal.Dt("tgl_perawatan") ?? throw new ArgumentException("Tanggal pemeriksaan tidak valid.");
        if (asal["jam_rawat"] is not TimeSpan time) throw new ArgumentException("Jam pemeriksaan tidak valid.");
        var current = await db.FirstAsync($"SELECT *,NOW() AS sekarang FROM {Table} WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam_rawat=@p2 FOR UPDATE", noRawat, date.Date, time)
            ?? throw new InvalidOperationException("Pemeriksaan tidak ditemukan lagi. Muat ulang daftar.");
        if (Fields.Any(f => current.S(f.Name) != asal.S(f.Name)))
            throw new InvalidOperationException("Pemeriksaan telah berubah. Muat ulang daftar.");
        if (!user.IsAdminUtama && RegistrasiService.MelewatiBatasEdit(date.Date + time, current.Dt("sekarang")!.Value))
            throw new InvalidOperationException("Perubahan atau penghapusan tidak boleh lebih dari 2 x 24 jam.");
    }

    public async Task UbahAsync(Row asal, DateTime waktu, IReadOnlyList<string> values)
    {
        Izin(); Validasi(values);
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunciKunjunganAsync(db, waktu);
        await KunciAsalAsync(db, asal);
        var assignments = string.Join(",", Fields.Select((f, i) => $"`{f.Name}`=@p{i + 5}"));
        await db.ExecAsync($"UPDATE {Table} SET tgl_perawatan=@p3,jam_rawat=@p4,{assignments} WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam_rawat=@p2",
            [noRawat, asal.Dt("tgl_perawatan")!.Value.Date, asal["jam_rawat"], waktu.Date, waktu.TimeOfDay, .. values.Cast<object?>()]);
        await db.CommitAsync();
    }

    public async Task HapusAsync(Row asal)
    {
        Izin();
        await using var db = await Db.OpenAsync(settings);
        await db.BeginAsync();
        await KunciKunjunganAsync(db, asal.Dt("tgl_perawatan")!.Value.Date + (TimeSpan)asal["jam_rawat"]!);
        await KunciAsalAsync(db, asal);
        await db.ExecAsync($"DELETE FROM {Table} WHERE no_rawat=@p0 AND tgl_perawatan=@p1 AND jam_rawat=@p2", noRawat, asal.Dt("tgl_perawatan")!.Value.Date, asal["jam_rawat"]);
        await db.CommitAsync();
    }
}
