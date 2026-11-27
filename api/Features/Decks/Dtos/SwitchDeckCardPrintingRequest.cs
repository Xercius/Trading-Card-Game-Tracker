namespace api.Features.Decks.Dtos;

/// <summary>Request payload to switch a deck card entry to another printing of the same card.</summary>
public sealed record SwitchDeckCardPrintingRequest(int TargetCardPrintingId);
