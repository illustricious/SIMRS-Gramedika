using System.Data;
using Microsoft.EntityFrameworkCore;

namespace SimrsKhanza.Data;

public partial class KasirRepository
{
    public async Task<KasirKuitansi?> MuatKuitansiAsync(string noRawat, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var bill = await GetBillAsync(noRawat, ct);
        if (bill is not { StatusBayar: "Sudah Bayar", RincianTersimpan: true, NoNota.Length: > 0 }
            || bill.Rincian.Count == 0)
            return null;

        var waktu = await db.Database.SqlQuery<DateTime?>(bill.Ranap ? (FormattableString)$"""
            SELECT TIMESTAMP(tanggal,jam) AS Value FROM nota_inap WHERE no_rawat={noRawat} AND no_nota={bill.NoNota}
            """ : $"""
            SELECT TIMESTAMP(tanggal,jam) AS Value FROM nota_jalan WHERE no_rawat={noRawat} AND no_nota={bill.NoNota}
            """).SingleOrDefaultAsync(ct);
        if (waktu is null) return null;

        var pembayaran = await db.Database.SqlQuery<KasirPembayaran>(bill.Ranap ? (FormattableString)$"""
            SELECT nama_bayar AS AkunBayar,CAST(COALESCE(besarppn,0) AS DECIMAL(20,4)) AS BiayaAdmin,
                   CAST(COALESCE(besar_bayar,0) AS DECIMAL(20,4)) AS Jumlah
            FROM detail_nota_inap WHERE no_rawat={noRawat} ORDER BY nama_bayar
            """ : $"""
            SELECT nama_bayar AS AkunBayar,CAST(COALESCE(besarppn,0) AS DECIMAL(20,4)) AS BiayaAdmin,
                   CAST(COALESCE(besar_bayar,0) AS DECIMAL(20,4)) AS Jumlah
            FROM detail_nota_jalan WHERE no_rawat={noRawat} ORDER BY nama_bayar
            """).ToListAsync(ct);
        if (bill.Total > 0 && pembayaran.Count == 0) return null;

        await tx.CommitAsync(ct);
        return new(bill, waktu.Value, pembayaran);
    }
}
