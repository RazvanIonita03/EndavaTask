using Microsoft.Extensions.Logging;

namespace CarInsurance.Api.Tests.Helpers;

public class TestLogger<T> : ILogger<T>
{
	private readonly List<(LogLevel level, string message, Exception? exception)> _logs = new();

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var message = formatter(state, exception);
		lock (_logs)
		{
			_logs.Add((logLevel, message, exception));
		}
	}

	public bool HasLogLevel(LogLevel level)
	{
		lock (_logs) return _logs.Any(l => l.level == level);
	}

	public bool HasLogWithMessage(LogLevel level, string contains)
	{
		lock (_logs) return _logs.Any(l => l.level == level && l.message.Contains(contains, StringComparison.OrdinalIgnoreCase));
	}

	public IEnumerable<string> GetLogs(LogLevel level)
	{
		lock (_logs) return _logs.Where(l => l.level == level).Select(l => l.message).ToList();
	}

	private class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();
		public void Dispose() { }
	}
}
