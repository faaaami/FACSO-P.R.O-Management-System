using Microsoft.Extensions.Logging;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 7C.3.1: a logger that captures fully formatted messages (arguments
/// interpolated) and exception details so security tests can prove the
/// OpenRouter provider never logs the API key, the document text, or the raw
/// model response.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(formatter(state, exception));

        if (exception is not null)
        {
            _entries.Add(exception.ToString());
        }
    }
}