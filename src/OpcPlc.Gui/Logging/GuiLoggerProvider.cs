using Microsoft.Extensions.Logging;
using System;

namespace OpcPlc.Gui.Logging;

public sealed class GuiLoggerProvider : ILoggerProvider
{
    private readonly Action<string> _onLog;

    public GuiLoggerProvider(Action<string> onLog)
    {
        _onLog = onLog ?? throw new ArgumentNullException(nameof(onLog));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new GuiLogger(_onLog);
    }

    public void Dispose()
    {
        // No-op: this provider is stateless and holds no unmanaged resources.
    }

    private sealed class GuiLogger : ILogger
    {
        private readonly Action<string> _onLog;

        public GuiLogger(Action<string> onLog)
        {
            _onLog = onLog;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null)
            {
                return;
            }

            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (exception != null)
            {
                message += $" {exception}";
            }

            _onLog($"[{logLevel}] {message}");
        }
    }
}
