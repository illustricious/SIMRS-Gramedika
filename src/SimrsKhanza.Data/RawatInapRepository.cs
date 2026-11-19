using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

/// <summary>Row for the "kamar kosong" picker shown when admitting / moving a patient.</summary>
public record KamarTersediaItem(string KodeKamar, string? NamaBangsal, KelasKamar Kelas, decimal TarifKamar)
{
    public string Label => $"{KodeKamar} {NamaBangsal}  (Rp {TarifKamar:N0})";
}

/// <summary>Identifies one kamar_inap row — legacy keys edits on no_rawat+kd_kamar+tgl_masuk+jam_masuk.</summary>
public record RawatInapKey(string NoRawat, string KodeKamar, DateOnly TanggalMasuk, TimeOnly JamMasuk);

/// <summary>
/// One row of "Daftar Pasien Rawat Inap" — same columns/order as the legacy
/// DlgKamarInap grid. Dates/amounts are pre-formatted strings (legacy shows
/// empty for the '0000-00-00' "not discharged yet" date).
/// </summary>
public record RawatInapListItem(
    string NoRawat, string NoRekamMedis, string NamaPasien, string Alamat,
    string PenanggungJawab, string HubunganPj, string JenisBayar,
    string KodeKamar, string Kamar, string TarifKamar, string DiagnosaAwal, string DiagnosaAkhir,
    DateOnly TanggalMasuk, TimeOnly JamMasuk, string TglMasuk, string JamMasukText,
    string TglKeluar, string JamKeluar, string TotalBiaya, string StatusPulang, string Lama,
    string DokterPj, string StatusBayar, string Agama, string KodePenjab)
{
    public RawatInapKey Key => new(NoRawat, KodeKamar, TanggalMasuk, JamMasuk);
    public bool BelumPulang => StatusPulang == "-";
}

public enum RawatInapMode { BelumPulang, TanggalMasuk, Pulang }

public record RawatInapFilter(
    RawatInapMode Mode, DateOnly From, DateOnly To, StatusBayar? StatusBayar,
    string? NoRawat, string? NoRekamMedis, string? Nama, string? Kamar, string? Keyword);

public interface IRawatInapRepository
{
    Task<IReadOnlyList<KamarTersediaItem>> GetAvailableKamarAsync(CancellationToken ct = default);

    /// <summary>Legacy "Daftar Pasien Rawat Inap" list — see RawatInapFilter / RawatInapMode.</summary>
    Task<IReadOnlyList<RawatInapListItem>> GetDaftarAsync(RawatInapFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Admits a patient already registered (reg_periksa row with that NoRawat
    /// must exist) into a room: inserts kamar_inap, flips the room to "ISI",
    /// and flips reg_periksa.StatusLanjut to RawatInap. Refuses if the visit is
    /// already admitted or the room isn't KOSONG.
    /// </summary>
    Task AdmitAsync(string noRawat, string kodeKamar, string? diagnosaAwal, CancellationToken ct = default);

    /// <summary>
    /// Discharges one active stay: stamps tgl_keluar/jam_keluar, computes Lama
    /// (days, minimum 1) and TotalBiaya (Lama * TarifKamar), frees the room back
    /// to "KOSONG". Returns the computed room bill.
    /// </summary>
    Task<decimal> DischargeAsync(RawatInapKey key, StatusPulang statusPulang, string? diagnosaAkhir, CancellationToken ct = default);

    /// <summary>
    /// "Pindah" — closes the current stay with stts_pulang "Pindah Kamar"
    /// (billed for the days spent so far), frees its room, and opens a NEW
    /// kamar_inap row for the same visit in the target room (same as legacy).
    /// Returns the room bill of the closed stay.
    /// </summary>
    Task<decimal> PindahKamarAsync(RawatInapKey key, string kodeKamarBaru, CancellationToken ct = default);

    /// <summary>Legacy right-click "Ganti → Jenis Bayar" — updates reg_periksa.kd_pj for the visit.</summary>
    Task GantiJenisBayarAsync(string noRawat, string kodePenjab, CancellationToken ct = default);

    /// <summary>Legacy "Ganti → Diagnosa Masuk / Diagnosa Akhir" — edits the stay's diagnosa text.</summary>
    Task GantiDiagnosaAsync(RawatInapKey key, string? diagnosaAwal, string? diagnosaAkhir, CancellationToken ct = default);
}

public class RawatInapRepository(SimrsKhanzaDbContext db) : IRawatInapRepository
{
    private const int MaxRows = 2000;

    public async Task<IReadOnlyList<KamarTersediaItem>> GetAvailableKamarAsync(CancellationToken ct = default)
    {
        var query =
            from k in db.KamarSet
            join b in db.BangsalSet on k.KodeBangsal equals b.KodeBangsal into bJoin
            from b in bJoin.DefaultIfEmpty()
            where k.Status == StatusKamar.Kosong
            orderby b.NamaBangsal, k.KodeKamar
            select new KamarTersediaItem(k.KodeKamar, b != null ? b.NamaBangsal : null, k.Kelas, k.TarifKamar);

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RawatInapListItem>> GetDaftarAsync(RawatInapFilter f, CancellationToken ct = default)
    {
        var q =
            from ki in db.KamarInapSet
            join r in db.RegPeriksaSet on ki.NoRawat equals r.NoRawat
            join p in db.PasienSet on r.NoRekamMedis equals p.NoRekamMedis
            join k in db.KamarSet on ki.KodeKamar equals k.KodeKamar
            join b in db.BangsalSet on k.KodeBangsal equals b.KodeBangsal
            join d in db.DokterSet on r.KodeDokter equals d.KodeDokter into dj
            from d in dj.DefaultIfEmpty()
            join pj in db.PenjabSet on r.KodePenjab equals pj.KodePenjab into pjj
            from pj in pjj.DefaultIfEmpty()
            select new { ki, r, p, b, d, pj };

        q = f.Mode switch
        {
            RawatInapMode.BelumPulang => q.Where(x => x.ki.StatusPulang == StatusPulang.BelumPulang),
            RawatInapMode.TanggalMasuk => q.Where(x => x.ki.TanggalMasuk >= f.From && x.ki.TanggalMasuk <= f.To),
            _ => q.Where(x => x.ki.TanggalKeluar != null && x.ki.TanggalKeluar >= f.From && x.ki.TanggalKeluar <= f.To),
        };

        if (f.StatusBayar is { } sb) q = q.Where(x => x.r.StatusBayar == sb);
        if (!string.IsNullOrWhiteSpace(f.NoRawat)) q = q.Where(x => x.ki.NoRawat.Contains(f.NoRawat));
        if (!string.IsNullOrWhiteSpace(f.NoRekamMedis)) q = q.Where(x => x.r.NoRekamMedis.Contains(f.NoRekamMedis));
        if (!string.IsNullOrWhiteSpace(f.Nama)) q = q.Where(x => x.p.NamaPasien.Contains(f.Nama));
        if (!string.IsNullOrWhiteSpace(f.Kamar))
            q = q.Where(x => x.ki.KodeKamar.Contains(f.Kamar) || x.b.NamaBangsal.Contains(f.Kamar));
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var kw = f.Keyword;
            q = q.Where(x =>
                x.ki.NoRawat.Contains(kw) || x.r.NoRekamMedis.Contains(kw) || x.p.NamaPasien.Contains(kw) ||
                (x.p.Alamat != null && x.p.Alamat.Contains(kw)) || x.ki.KodeKamar.Contains(kw) ||
                x.b.NamaBangsal.Contains(kw) ||
                (x.ki.DiagnosaAwal != null && x.ki.DiagnosaAwal.Contains(kw)) ||
                (x.ki.DiagnosaAkhir != null && x.ki.DiagnosaAkhir.Contains(kw)) ||
                (x.d != null && x.d.NamaDokter.Contains(kw)) ||
                (x.pj != null && x.pj.NamaPenanggungJawab.Contains(kw)) ||
                (x.p.Agama != null && x.p.Agama.Contains(kw)));
        }

        var rows = await q
            .OrderBy(x => x.b.NamaBangsal).ThenBy(x => x.ki.TanggalMasuk).ThenBy(x => x.ki.JamMasuk)
            .Take(MaxRows)
            .Select(x => new
            {
                x.ki.NoRawat, x.r.NoRekamMedis, x.p.NamaPasien, x.p.Alamat, x.p.TanggalLahir, x.r.TanggalRegistrasi,
                x.r.PenanggungJawab, x.r.HubunganPj, JenisBayar = x.pj != null ? x.pj.NamaPenanggungJawab : null,
                x.r.KodePenjab, x.ki.KodeKamar, x.b.NamaBangsal, x.ki.TarifKamar, x.ki.DiagnosaAwal, x.ki.DiagnosaAkhir,
                x.ki.TanggalMasuk, x.ki.JamMasuk, x.ki.TanggalKeluar, x.ki.JamKeluar, x.ki.TotalBiaya,
                x.ki.StatusPulang, x.ki.Lama, DokterPj = x.d != null ? x.d.NamaDokter : null,
                x.r.StatusBayar, x.p.Agama,
            })
            .ToListAsync(ct);

        return rows.Select(x => new RawatInapListItem(
            x.NoRawat, x.NoRekamMedis,
            $"{x.NamaPasien} ({UmurText(x.TanggalLahir, x.TanggalRegistrasi)})",
            x.Alamat ?? "", x.PenanggungJawab ?? "", x.HubunganPj ?? "", x.JenisBayar ?? "",
            x.KodeKamar, $"{x.KodeKamar} {x.NamaBangsal}", (x.TarifKamar ?? 0).ToString("N0"),
            x.DiagnosaAwal ?? "", x.DiagnosaAkhir ?? "",
            x.TanggalMasuk, x.JamMasuk, x.TanggalMasuk.ToString("yyyy-MM-dd"), x.JamMasuk.ToString("HH:mm:ss"),
            DateText(x.TanggalKeluar), TimeText(x.JamKeluar), (x.TotalBiaya ?? 0).ToString("N0"),
            (x.StatusPulang ?? StatusPulang.BelumPulang).ToLabel(), (x.Lama ?? 0).ToString("0.##"),
            x.DokterPj ?? "",
            x.StatusBayar switch { StatusBayar.SudahBayar => "Sudah Bayar", StatusBayar.BelumBayar => "Belum Bayar", _ => "" },
            x.Agama ?? "", x.KodePenjab)).ToList();
    }

    // Legacy stores "not yet discharged" as '0000-00-00' / '00:00:00'; with
    // ConvertZeroDateTime=True that arrives as MinValue. NULL (rows written by
    // this app's AdmitAsync) is treated the same way: shown as empty.
    private static string DateText(DateOnly? d) => d is null || d.Value.Year < 1900 ? "" : d.Value.ToString("yyyy-MM-dd");
    private static string TimeText(TimeOnly? t) => t is null || t.Value == TimeOnly.MinValue ? "" : t.Value.ToString("HH:mm:ss");

    private static string UmurText(DateOnly? lahir, DateOnly? daftar)
    {
        if (lahir is null || lahir.Value.Year < 1900) return "-";
        var today = daftar ?? DateOnly.FromDateTime(DateTime.Today);
        var years = today.Year - lahir.Value.Year - (today < lahir.Value.AddYears(today.Year - lahir.Value.Year) ? 1 : 0);
        if (years >= 1) return $"{years} Th";
        var months = (today.Year - lahir.Value.Year) * 12 + today.Month - lahir.Value.Month - (today.Day < lahir.Value.Day ? 1 : 0);
        return months >= 1 ? $"{months} Bl" : $"{Math.Max(0, today.DayNumber - lahir.Value.DayNumber)} Hr";
    }

    public async Task AdmitAsync(string noRawat, string kodeKamar, string? diagnosaAwal, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var registrations = await db.RegPeriksaSet
            .FromSqlInterpolated($"SELECT * FROM reg_periksa WHERE no_rawat={noRawat} FOR UPDATE").ToListAsync(ct);
        var reg = registrations.SingleOrDefault()
            ?? throw new InvalidOperationException($"No.Rawat '{noRawat}' tidak ditemukan — pasien harus registrasi dulu.");
        if (reg.Status == StatusRegistrasi.Batal)
            throw new InvalidOperationException("Registrasi pasien sudah dibatalkan.");
        var rooms = await db.KamarSet
            .FromSqlInterpolated($"SELECT * FROM kamar WHERE kd_kamar={kodeKamar} FOR UPDATE").ToListAsync(ct);
        var kamar = rooms.SingleOrDefault()
            ?? throw new InvalidOperationException($"Kamar '{kodeKamar}' tidak ditemukan.");
        var dipesanPasien = kamar.Status == StatusKamar.Dibooking && await db.Database.SqlQuery<long>(
            $"SELECT COUNT(*) AS Value FROM permintaan_ranap WHERE no_rawat={noRawat} AND kd_kamar={kodeKamar}").SingleAsync(ct) == 1;
        if (kamar.Status != StatusKamar.Kosong && !dipesanPasien)
            throw new InvalidOperationException($"Kamar '{kodeKamar}' sedang tidak tersedia untuk pasien ini.");
        if (await db.KamarInapSet.AnyAsync(k => k.NoRawat == noRawat && k.StatusPulang == StatusPulang.BelumPulang, ct))
            throw new InvalidOperationException($"No.Rawat '{noRawat}' masih tercatat dirawat inap (belum pulang).");

        db.KamarInapSet.Add(new KamarInap
        {
            NoRawat = noRawat,
            KodeKamar = kodeKamar,
            TarifKamar = kamar.TarifKamar,
            TanggalMasuk = DateOnly.FromDateTime(DateTime.Now),
            JamMasuk = TimeOnly.FromDateTime(DateTime.Now),
            DiagnosaAwal = diagnosaAwal,
            StatusPulang = StatusPulang.BelumPulang,
        });

        kamar.Status = StatusKamar.Isi;
        reg.StatusLanjut = StatusLanjut.RawatInap;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<KamarInap> FindActiveStayAsync(RawatInapKey key, CancellationToken ct)
        => await db.KamarInapSet.FirstOrDefaultAsync(k =>
                k.NoRawat == key.NoRawat && k.KodeKamar == key.KodeKamar &&
                k.TanggalMasuk == key.TanggalMasuk && k.JamMasuk == key.JamMasuk, ct)
           ?? throw new InvalidOperationException($"Data kamar inap untuk No.Rawat '{key.NoRawat}' tidak ditemukan.");

    public async Task<decimal> DischargeAsync(RawatInapKey key, StatusPulang statusPulang, string? diagnosaAkhir, CancellationToken ct = default)
    {
        var stay = await FindActiveStayAsync(key, ct);
        if (stay.StatusPulang != StatusPulang.BelumPulang)
            throw new InvalidOperationException("Pasien ini sudah tercatat pulang/pindah — pilih baris yang masih dirawat.");
        if (statusPulang is StatusPulang.BelumPulang or StatusPulang.PindahKamar)
            throw new InvalidOperationException("Pilih status pulang yang valid.");

        var total = CloseStay(stay, statusPulang, diagnosaAkhir ?? stay.DiagnosaAkhir);
        await FreeKamarAsync(stay.KodeKamar, ct);
        await db.SaveChangesAsync(ct);
        return total;
    }

    public async Task<decimal> PindahKamarAsync(RawatInapKey key, string kodeKamarBaru, CancellationToken ct = default)
    {
        var stay = await FindActiveStayAsync(key, ct);
        if (stay.StatusPulang != StatusPulang.BelumPulang)
            throw new InvalidOperationException("Pasien ini sudah tercatat pulang/pindah — pilih baris yang masih dirawat.");
        if (kodeKamarBaru == stay.KodeKamar)
            throw new InvalidOperationException("Kamar tujuan sama dengan kamar saat ini.");

        var baru = await db.KamarSet.FirstOrDefaultAsync(k => k.KodeKamar == kodeKamarBaru, ct)
            ?? throw new InvalidOperationException($"Kamar '{kodeKamarBaru}' tidak ditemukan.");
        if (baru.Status != StatusKamar.Kosong)
            throw new InvalidOperationException($"Kamar '{kodeKamarBaru}' sedang tidak kosong.");

        var total = CloseStay(stay, StatusPulang.PindahKamar, stay.DiagnosaAkhir);
        await FreeKamarAsync(stay.KodeKamar, ct);

        var now = DateTime.Now;
        db.KamarInapSet.Add(new KamarInap
        {
            NoRawat = stay.NoRawat,
            KodeKamar = kodeKamarBaru,
            TarifKamar = baru.TarifKamar,
            TanggalMasuk = DateOnly.FromDateTime(now),
            JamMasuk = TimeOnly.FromDateTime(now),
            DiagnosaAwal = stay.DiagnosaAwal,
            StatusPulang = StatusPulang.BelumPulang,
        });
        baru.Status = StatusKamar.Isi;

        await db.SaveChangesAsync(ct);
        return total;
    }

    private static decimal CloseStay(KamarInap stay, StatusPulang status, string? diagnosaAkhir)
    {
        var now = DateTime.Now;
        var keluar = DateOnly.FromDateTime(now);
        var lama = Math.Max(1, keluar.DayNumber - stay.TanggalMasuk.DayNumber);
        var total = lama * (stay.TarifKamar ?? 0m);

        stay.TanggalKeluar = keluar;
        stay.JamKeluar = TimeOnly.FromDateTime(now);
        stay.Lama = lama;
        stay.TotalBiaya = total;
        stay.StatusPulang = status;
        stay.DiagnosaAkhir = diagnosaAkhir;
        return total;
    }

    private async Task FreeKamarAsync(string kodeKamar, CancellationToken ct)
    {
        var kamar = await db.KamarSet.FirstOrDefaultAsync(k => k.KodeKamar == kodeKamar, ct);
        if (kamar is not null) kamar.Status = StatusKamar.Kosong;
    }

    public async Task GantiJenisBayarAsync(string noRawat, string kodePenjab, CancellationToken ct = default)
    {
        var reg = await db.RegPeriksaSet.FirstOrDefaultAsync(r => r.NoRawat == noRawat, ct)
            ?? throw new InvalidOperationException($"No.Rawat '{noRawat}' tidak ditemukan.");
        reg.KodePenjab = kodePenjab;
        await db.SaveChangesAsync(ct);
    }

    public async Task GantiDiagnosaAsync(RawatInapKey key, string? diagnosaAwal, string? diagnosaAkhir, CancellationToken ct = default)
    {
        var stay = await FindActiveStayAsync(key, ct);
        stay.DiagnosaAwal = diagnosaAwal;
        stay.DiagnosaAkhir = diagnosaAkhir;
        await db.SaveChangesAsync(ct);
    }
}
