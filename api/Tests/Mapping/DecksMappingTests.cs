using AutoMapper;
using api.Features.Decks.Mapping;
using api.Models;
using Xunit;

namespace api.Tests.Mapping;

public sealed class DecksMappingTests
{
    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<DecksMappingProfile>());
        cfg.AssertConfigurationIsValid();
        return cfg.CreateMapper();
    }

    [Fact]
    public void Deck_To_DeckResponse_Maps()
    {
        var mapper = CreateMapper();
        var deck = new Deck { Id = 1, UserId = 2, Game = "SWU", Name = "Test", Description = "D", CreatedUtc = DateTime.UtcNow };
        var dto = mapper.Map<api.Features.Decks.Dtos.DeckResponse>(deck);
        Assert.Equal(deck.Id, dto.Id);
        Assert.Equal(deck.UserId, dto.UserId);
        Assert.Equal(deck.Name, dto.Name);
    }
}
