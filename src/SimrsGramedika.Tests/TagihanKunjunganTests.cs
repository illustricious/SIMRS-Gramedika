using SimrsGramedika.Config;
using SimrsGramedika.Data.Keuangan;
using SimrsGramedika.Data.Sql;

namespace SimrsGramedika.Tests;

public sealed class TagihanKunjunganTests
{
    public sealed class FixtureFactAttribute : FactAttribute
    {
        public FixtureFactAttribute()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("SIMRS_TAGIHAN_FIXTURE_PORT"), out _))
                Skip = "Memerlukan skema sintetis simrs_tagihan_fixture.";
        }
    }

    [FixtureFact]
    public async Task HanyaBarisBillingYangMenghalangi()
    {
        var settings = new DatabaseSettings { Host = "127.0.0.1", User = "root", Database = "simrs_tagihan_fixture",
            Port = int.Parse(Environment.GetEnvironmentVariable("SIMRS_TAGIHAN_FIXTURE_PORT")!) };
        await using var db = await Db.OpenAsync(settings);
        await db.ExecAsync("SET SESSION FOREIGN_KEY_CHECKS=0,sql_mode=''");
        await db.ExecAsync("INSERT INTO billing(noindex,no_rawat,`no`,nm_perawatan,pemisah,biaya,jumlah,tambahan,totalbiaya) VALUES (1,'BILL','','Uji','',0,1,0,0)");
        await db.ExecAsync("INSERT INTO nota_inap(no_rawat,no_nota,tanggal,jam,Uang_Muka) VALUES ('NOTA','N-UJI','2026-01-01','00:00:00',0)");

        await db.BeginAsync();
        foreach (var kunci in new[] { false, true })
        {
            Assert.False(await TagihanKunjungan.SudahDitagihAsync(db, "KOSONG", kunci));
            Assert.True(await TagihanKunjungan.SudahDitagihAsync(db, "BILL", kunci));
            // Seperti Khanza, nota tanpa baris billing tidak menghalangi.
            Assert.False(await TagihanKunjungan.SudahDitagihAsync(db, "NOTA", kunci));
        }
        await db.CommitAsync();
    }
}
