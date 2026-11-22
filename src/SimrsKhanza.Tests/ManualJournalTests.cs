using SimrsKhanza.Data.Keuangan;

namespace SimrsKhanza.Tests;

public class ManualJournalTests
{
    private static JurnalLine Line(string rek, double debet, double kredit) => new(rek, rek, debet, kredit);

    [Fact]
    public void Validate_AcceptsBalancedEntry() =>
        ManualJournalRepository.Validate("BKT1", "Koreksi", [Line("111", 500, 0), Line("411", 0, 500)]);

    [Theory]
    [InlineData("", "x")]
    [InlineData("BKT1", " ")]
    public void Validate_RequiresProofAndDescription(string bukti, string keterangan) =>
        Assert.Throws<ArgumentException>(() =>
            ManualJournalRepository.Validate(bukti, keterangan, [Line("111", 5, 0), Line("411", 0, 5)]));

    [Fact]
    public void Validate_RejectsUnbalancedEntry() =>
        Assert.Throws<ArgumentException>(() =>
            ManualJournalRepository.Validate("B", "K", [Line("111", 500, 0), Line("411", 0, 400)]));

    [Fact]
    public void Validate_RejectsSingleLineAndMixedLines()
    {
        Assert.Throws<ArgumentException>(() => ManualJournalRepository.Validate("B", "K", [Line("111", 5, 0)]));
        Assert.Throws<ArgumentException>(() =>
            ManualJournalRepository.Validate("B", "K", [Line("111", 5, 5), Line("411", 0, 0)]));
    }
}
