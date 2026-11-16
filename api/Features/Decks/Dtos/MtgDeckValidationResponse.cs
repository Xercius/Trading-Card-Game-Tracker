namespace api.Features.Decks.Dtos;

public sealed record MtgDeckValidationResponse(
    string Format,
    bool IsValid,
    long MainDeckCount,
    long SideboardCount,
    long CommanderCount,
    IReadOnlyList<string> CommanderColorIdentity,
    IReadOnlyList<string> Violations);
