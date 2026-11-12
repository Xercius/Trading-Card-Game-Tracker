namespace api.Features.Decks.Dtos;

public sealed record DeckValidationResponse(
    string Format,
    bool IsValid,
    long CardCount,
    long BasicPokemonCount,
    IReadOnlyList<string> Violations);
