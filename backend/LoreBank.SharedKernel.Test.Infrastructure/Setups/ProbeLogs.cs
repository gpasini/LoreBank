using Microsoft.Extensions.Logging;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// La sonde de logs du harnais : un provider qui garde chaque entrée avec ses
// scopes aplatis — c'est là que le hosting pose la Corrélation (TraceId) et
// que les tests la relisent. Singleton statique comme les autres sondes,
// remis à zéro par ResetFakes.
public sealed class ProbeLogs : ILoggerProvider, ISupportExternalScope
{
    public sealed record Entry(
        string Category,
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> Scopes
    );

    public static ProbeLogs Provider { get; } = new();

    public static List<Entry> Entries { get; } = [];

    private IExternalScopeProvider? _scopes;

    public static void Reset() => Entries.Clear();

    public ILogger CreateLogger(string categoryName) => new Logger(
        provider: this,
        category: categoryName
    );

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class Logger(
        ProbeLogs provider,
        string category
    ) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var scopes = new Dictionary<string, object?>();

            provider._scopes?.ForEachScope(
                callback: (
                    scope,
                    accumulator
                ) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> pairs) {
                        foreach (var (key, value) in pairs) {
                            accumulator[key] = value;
                        }
                    }
                },
                state: scopes
            );

            Entries.Add(new Entry(
                Category: category,
                Level: logLevel,
                Message: formatter(
                    arg1: state,
                    arg2: exception
                ),
                Scopes: scopes
            ));
        }
    }
}
