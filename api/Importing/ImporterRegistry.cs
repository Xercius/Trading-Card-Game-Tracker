namespace api.Importing;

public sealed class ImporterRegistry
{
    private readonly Dictionary<string, ISourceImporter> _byKey;

    public ImporterRegistry(IEnumerable<ISourceImporter> importers)
    {
        _byKey = importers.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string key, out ISourceImporter importer) => _byKey.TryGetValue(key, out importer!);

    public IReadOnlyCollection<string> Keys => _byKey.Keys.ToList().AsReadOnly();

    public IReadOnlyCollection<ISourceImporter> All => _byKey.Values.ToList().AsReadOnly();
}
