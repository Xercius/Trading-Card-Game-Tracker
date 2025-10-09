using api.Features._Common;
using Xunit;

namespace api.Tests.Features.Common;

public class QuantityGuardsTests
{
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(42, 42)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void Clamp_Int_ReturnsExpected(int input, int expected)
    {
        var result = QuantityGuards.Clamp(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(long.MinValue, 0)]
    [InlineData(-1L, 0)]
    [InlineData(0L, 0)]
    [InlineData(1234L, 1234)]
    [InlineData((long)int.MaxValue + 10, int.MaxValue)]
    public void Clamp_Long_ReturnsExpected(long input, int expected)
    {
        var result = QuantityGuards.Clamp(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 5, 15)]
    [InlineData(10, -3, 7)]
    [InlineData(int.MaxValue, 10, int.MaxValue)]
    [InlineData(1, -10, 0)]
    public void ClampDelta_PreventsOverflowAndUnderflow(int current, int delta, int expected)
    {
        var result = QuantityGuards.ClampDelta(current, delta);
        Assert.Equal(expected, result);
    }
}
