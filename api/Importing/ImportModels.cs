namespace api.Importing;

public record ImportOptions(
    bool DryRun = true,
    bool Upsert = true,
    int? Limit = null,
    int? UserId = null,
    string? SetCode = null // e.g. "khm"
);

public sealed class ImportSummary
{
    public string Source { get; set; } = "";
    public bool DryRun { get; set; }

    public int CardsCreated { get; set; }
    public int CardsUpdated { get; set; }
    public int PrintingsCreated { get; set; }
    public int PrintingsUpdated { get; set; }
    public int Errors { get; set; }

    public List<string> Messages { get; set; } = new();
}
