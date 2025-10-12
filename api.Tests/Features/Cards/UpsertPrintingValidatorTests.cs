using api.Features.Cards.Dtos;
using api.Features.Cards.Validation;
using Xunit;

namespace api.Tests.Features.Cards;

public sealed class UpsertPrintingValidatorTests
{
    private static readonly UpsertPrintingValidator Validator = new();

    [Fact]
    public void Validate_Allows_Set_Length_Up_To_64()
    {
        var request = CreateRequest(set: new string('a', 64));

        var result = Validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_When_Set_Exceeds_64()
    {
        var request = CreateRequest(set: new string('a', 65));

        var result = Validator.Validate(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal("Set", error.PropertyName);
    }

    [Fact]
    public void Validate_Allows_Style_Length_Up_To_64()
    {
        var request = CreateRequest(style: new string('a', 64));

        var result = Validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_When_Style_Exceeds_64()
    {
        var request = CreateRequest(style: new string('a', 65));

        var result = Validator.Validate(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal("Style", error.PropertyName);
    }

    [Fact]
    public void Validate_Allows_Number_Length_Up_To_32()
    {
        var request = CreateRequest(number: new string('a', 32));

        var result = Validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_When_Number_Exceeds_32()
    {
        var request = CreateRequest(number: new string('a', 33));

        var result = Validator.Validate(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal("Number", error.PropertyName);
    }

    [Fact]
    public void Validate_Allows_Rarity_Length_Up_To_32()
    {
        var request = CreateRequest(rarity: new string('a', 32));

        var result = Validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_When_Rarity_Exceeds_32()
    {
        var request = CreateRequest(rarity: new string('a', 33));

        var result = Validator.Validate(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal("Rarity", error.PropertyName);
    }

    private static UpsertPrintingRequest CreateRequest(
        string? set = null,
        string? number = null,
        string? rarity = null,
        string? style = null)
    {
        return new UpsertPrintingRequest(
            Id: null,
            CardId: 1,
            Set: set,
            Number: number,
            Rarity: rarity,
            Style: style,
            ImageUrl: null);
    }
}
