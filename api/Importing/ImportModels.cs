namespace api.Importing;

public record ImportOptions(
    bool DryRun = true,
    bool Upsert = true,
    int? Limit = null,
    int? UserId = null // optional: tag who ran it
);

public record ImportSummary(
    string Source,
    bool DryRun,
    int CardsCreated,
    int CardsUpdated,
    int PrintingsCreated,
    int PrintingsUpdated,
    int Errors
)
{
    public List<string> Messages { get; } = new();
}
