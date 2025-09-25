namespace api.Features.Decks.Dtos;

public sealed record CreateDeckRequest(
    string Game,
    string Name,
    string? Description);
