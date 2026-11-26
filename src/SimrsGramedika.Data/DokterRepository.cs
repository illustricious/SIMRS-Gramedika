using Microsoft.EntityFrameworkCore;
using SimrsGramedika.Core.Entities;

namespace SimrsGramedika.Data;

public interface IDokterRepository
{
    Task<IReadOnlyList<Dokter>> SearchAsync(string query, CancellationToken ct = default);
}

/// <summary>
/// Real MySQL-backed implementation, querying the legacy `dokter` table through
/// SimrsGramedikaDbContext. Requires a reachable `sik` MySQL server — see
/// docs/handover/04-HISTORY.md for why that could not be verified end-to-end in this
/// environment (no local MySQL/Docker available).
/// </summary>
public class DokterRepository(SimrsGramedikaDbContext db) : IDokterRepository
{
    public async Task<IReadOnlyList<Dokter>> SearchAsync(string query, CancellationToken ct = default)
    {
        return await db.DokterSet
            .Where(d => string.IsNullOrWhiteSpace(query) || d.NamaDokter.Contains(query))
            .OrderBy(d => d.NamaDokter)
            .Take(20)
            .ToListAsync(ct);
    }
}
