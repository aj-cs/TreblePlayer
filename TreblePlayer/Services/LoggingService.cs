using Microsoft.Extensions.Logging;

namespace TreblePlayer.Services;

public interface ILoggingService
{
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
    void LogDebug(string message);
}

public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message)
    {
        _logger.LogInformation(message);
    }

    public void LogWarning(string message)
    {
        _logger.LogWarning(message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        if (exception != null)
        {
            _logger.LogError(exception, message);
        }
        else
        {
            _logger.LogError(message);
        }
    }

    public void LogDebug(string message)
    {
        _logger.LogDebug(message);
    }
} 