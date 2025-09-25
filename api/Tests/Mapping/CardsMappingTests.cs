using AutoMapper;
using api.Features.Cards.Dtos;
using api.Features.Cards.Mapping;
using api.Models;
using Xunit;

namespace api.Tests.Mapping;

public sealed class CardsMappingTests
{
    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<CardsMappingProfile>());
        cfg.AssertConfigurationIsValid();
        return cfg.CreateMapper();
    }

    [Fact]
    public void Card_To_CardListItemResponse_Maps()
    {
        var mapper = CreateMapper();
        var card = new Card { Id = 5, Game = "Test", Name = "Sample", CardType = "Unit" };
        var dto = mapper.Map<CardListItemResponse>(card);
        Assert.Equal(card.Id, dto.CardId);
        Assert.Equal(card.Name, dto.Name);
    }
}
