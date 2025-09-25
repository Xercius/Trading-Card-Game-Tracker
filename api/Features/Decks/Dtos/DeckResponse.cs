namespace api.Features.Decks.Dtos;

public sealed record DeckResponse(
    int Id,
    int UserId,
    string Game,
    string Name,
    string? Description,
    DateTime CreatedUtc,
    DateTime? UpdatedUtc);
