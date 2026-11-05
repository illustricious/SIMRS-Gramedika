using Microsoft.EntityFrameworkCore;
using SimrsKhanza.Core.Entities;

namespace SimrsKhanza.Data;

public interface IPasienRepository
{
    Task<IReadOnlyList<Pasien>> SearchAsync(string keyword, CancellationToken ct = default);

    /// <summary>Generates the next no_rkm_medis in the same "D" + 5-digit pattern as the dev seed rows.</summary>
    Task<string> NextNoRekamMedisAsync(CancellationToken ct = default);

    Task SaveNewAsync(Pasien pasien, CancellationToken ct = default);
}

public class PasienRepository(SimrsKhanzaDbContext db) : IPasienRepository
{
    public async Task<IReadOnlyList<Pasien>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        var query = db.PasienSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => p.NamaPasien.Contains(keyword) || p.NoRekamMedis.Contains(keyword));
        }

        return await query.OrderByDescending(p => p.TanggalDaftar).Take(100).ToListAsync(ct);
    }

    public async Task<string> NextNoRekamMedisAsync(CancellationToken ct = default)
    {
        var count = await db.PasienSet.CountAsync(ct);
        return $"D{count + 1:00000}";
    }

    public async Task SaveNewAsync(Pasien pasien, CancellationToken ct = default)
    {
        db.PasienSet.Add(pasien);
        await db.SaveChangesAsync(ct);
    }
}
