namespace api.Features.Decks.Dtos;

public sealed record UpdateDeckRequest(
    string Game,
    string Name,
    string? Description);
