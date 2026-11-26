using SimrsGramedika.Config;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Data.Keuangan;

/// <summary>Manual journal posting (legacy "Posting Jurnal"): one balanced entry written through <see cref="JurnalPoster"/>.</summary>
public class ManualJournalRepository(DatabaseSettings settings)
{
    public async Task<List<(string Kode, string Nama)>> AccountsAsync()
    {
        await using var db = await Db.OpenAsync(settings);
        return (await db.QueryAsync("SELECT kd_rek,nm_rek FROM rekening WHERE level='0' ORDER BY kd_rek"))
            .Select(r => (r.S("kd_rek"), r.S("nm_rek"))).ToList();
    }

    public static void Validate(string noBukti, string keterangan, IReadOnlyList<JurnalLine> lines)
    {
        if (string.IsNullOrWhiteSpace(noBukti)) throw new ArgumentException("No. bukti wajib diisi.");
        if (string.IsNullOrWhiteSpace(keterangan)) throw new ArgumentException("Keterangan wajib diisi.");
        if (lines.Count < 2) throw new ArgumentException("Jurnal minimal terdiri dari dua baris rekening.");
        if (lines.Any(l => !double.IsFinite(l.Debet) || !double.IsFinite(l.Kredit) || l.Debet < 0 || l.Kredit < 0
                           || (l.Debet > 0 && l.Kredit > 0) || (l.Debet == 0 && l.Kredit == 0)))
            throw new ArgumentException("Setiap baris harus berisi debet atau kredit saja, bernilai lebih dari nol.");
        var debet = lines.Sum(l => l.Debet);
        var kredit = lines.Sum(l => l.Kredit);
        if (Math.Abs(debet - kredit) >= 0.5)
            throw new ArgumentException($"Jurnal tidak seimbang (debet {debet:N0}, kredit {kredit:N0}).");
    }

    public async Task PostAsync(string noBukti, string keterangan, IReadOnlyList<JurnalLine> lines)
    {
        Validate(noBukti, keterangan, lines);
        await using var db = await Db.OpenAsync(settings);
        foreach (var line in lines)
            if (await db.CountAsync("SELECT COUNT(*) FROM rekening WHERE kd_rek=@p0", line.KdRek) == 0)
                throw new InvalidOperationException($"Rekening {line.KdRek} tidak ditemukan.");
        await db.BeginAsync();
        await JurnalPoster.PostAsync(db.Conn, db.Tx!, noBukti.Trim(), keterangan.Trim(), lines);
        await db.CommitAsync();
    }
}
