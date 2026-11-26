using SimrsGramedika.Data;

namespace SimrsGramedika.Tests;

public class RegistrasiServiceTests
{
    [Fact]
    public void EditWindow_Is48Hours()
    {
        var registered = new DateTime(2026, 11, 20, 8, 0, 0);
        Assert.False(RegistrasiService.MelewatiBatasEdit(registered, registered.AddHours(48)));
        Assert.True(RegistrasiService.MelewatiBatasEdit(registered, registered.AddHours(48).AddSeconds(1)));
    }

    [Theory]
    [InlineData("2020-11-22", "2026-11-22", 6, "Th")]
    [InlineData("2026-05-25", "2026-11-22", 5, "Bl")]
    [InlineData("2026-11-10", "2026-11-22", 12, "Hr")]
    [InlineData("2026-11-22", "2026-11-22", 0, "Hr")]
    [InlineData("2020-11-23", "2026-11-22", 5, "Th")]
    public void Age_UsesYearsThenMonthsThenDays(string born, string today, int value, string unit)
    {
        var (age, suffix) = RegistrasiService.HitungUmur(DateOnly.Parse(born), DateOnly.Parse(today));
        Assert.Equal((value, unit), (age, suffix));
    }

    [Theory]
    [InlineData("Baru", "registrasi")]
    [InlineData("Lama", "registrasilama")]
    [InlineData("-", "registrasi")]
    public void Fee_NewPatientUsesRegistrasi_ReturningUsesRegistrasiLama(string status, string column) =>
        Assert.Equal(column, RegistrasiService.KolomBiaya(status));

    [Theory]
    [InlineData(2026, 11, 22, "AKHAD")]
    [InlineData(2026, 11, 23, "SENIN")]
    [InlineData(2026, 11, 28, "SABTU")]
    public void ScheduleDay_UsesLegacyNames(int y, int m, int d, string expected) =>
        Assert.Equal(expected, RegistrasiService.HariJadwal(new DateTime(y, m, d)));
}
