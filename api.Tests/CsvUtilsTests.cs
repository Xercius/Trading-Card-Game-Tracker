using api.Shared;
using Xunit;

namespace api.Tests;

public class CsvUtilsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_ReturnsEmpty_ForNullOrWhitespace(string? value)
    {
        var result = CsvUtils.Parse(value);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_TrimsEntries()
    {
        var result = CsvUtils.Parse(" Magic , Lorcana ");

        Assert.Equal(new[] { "Magic", "Lorcana" }, result);
    }

    [Fact]
    public void Parse_RemovesDuplicates_PreservingFirstOccurrence()
    {
        var result = CsvUtils.Parse("Magic,Magic,Lorcana,Magic");

        Assert.Equal(new[] { "Magic", "Lorcana" }, result);
    }

    [Fact]
    public void Parse_IgnoresEntriesThatBecomeEmptyAfterTrim()
    {
        var result = CsvUtils.Parse("Magic, , ,Lorcana");

        Assert.Equal(new[] { "Magic", "Lorcana" }, result);
    }
}
