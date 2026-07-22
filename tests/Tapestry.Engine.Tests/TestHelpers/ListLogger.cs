using Microsoft.Extensions.Logging;

namespace Tapestry.Engine.Tests.TestHelpers;

/// <summary>
/// Minimal capture logger for tests that need to assert on log output.
/// Only Warning-level messages are captured (the level PlayerSerializer's
/// unknown-key drop path uses); extend if a test needs other levels.
/// </summary>
public class ListLogger<T> : ILogger<T>
{
    public List<string> Warnings { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning)
        {
            Warnings.Add(formatter(state, exception));
        }
    }
}
