using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace api.Tests.Fixtures;

internal sealed record TestLogEntry(
    string Category,
    LogLevel Level,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    string Message,
    Exception? Exception);

internal sealed class NullScopeProvider : IExternalScopeProvider
{
    public static NullScopeProvider Instance { get; } = new();

    private NullScopeProvider() { }

    public void ForEachScope<TState>(Action<object?, TState> callback, TState state) { }

    public IDisposable Push(object? state) => NullScope.Instance;

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        private NullScope() { }
        public void Dispose() { }
    }
}

internal sealed class TestLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<TestLogEntry> _entries = new();
    private IExternalScopeProvider _scopeProvider = NullScopeProvider.Instance;

    public IReadOnlyCollection<TestLogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, _entries, () => _scopeProvider);

    public void Dispose() { }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider ?? NullScopeProvider.Instance;
    }

    private sealed class TestLogger(
        string categoryName,
        ConcurrentQueue<TestLogEntry> sink,
        Func<IExternalScopeProvider> scopeProviderAccessor) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => scopeProviderAccessor().Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var stateValues = state as IReadOnlyList<KeyValuePair<string, object?>> ?? Array.Empty<KeyValuePair<string, object?>>();
            sink.Enqueue(new TestLogEntry(categoryName, logLevel, stateValues, formatter(state, exception), exception));
        }
    }
}
