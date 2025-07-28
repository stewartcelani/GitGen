using System.Text.Json;
using GitGen.Models;

namespace GitGen.Services;

/// <summary>
///     Implementation of usage tracking that writes to JSONL files in ~/.gitgen/usage/.
/// </summary>
public class UsageTrackingService : IUsageTrackingService
{
    private readonly string _usageDirectory;
    private readonly string _sessionId;
    private readonly IConsoleLogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public UsageTrackingService(IConsoleLogger logger)
    {
        _logger = logger;
        _sessionId = GenerateSessionId();
        
        // Set up the usage directory
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _usageDirectory = Path.Combine(homeDir, ".gitgen", "usage");
        _logger.Debug($"Usage directory: {_usageDirectory}");
        
        // Ensure directory exists
        Directory.CreateDirectory(_usageDirectory);
        _logger.Debug($"Usage directory created/verified: {Directory.Exists(_usageDirectory)}");
    }

    public async Task RecordUsageAsync(UsageEntry entry)
    {
        _logger.Debug($"UsageTrackingService.RecordUsageAsync called for model: {entry.Model?.Name}");
        try
        {
            // Set the session ID if not already set
            if (string.IsNullOrEmpty(entry.SessionId))
            {
                entry.SessionId = _sessionId;
            }

            // Determine the file name based on the timestamp (monthly files)
            var fileName = $"usage-{entry.Timestamp:yyyy-MM}.jsonl";
            var filePath = Path.Combine(_usageDirectory, fileName);
            _logger.Debug($"Usage file path: {filePath}");

            // Serialize the entry to JSON
            var json = JsonSerializer.Serialize(entry, UsageJsonContext.Default.UsageEntry);
            _logger.Debug($"Serialized JSON length: {json.Length} characters");

            // Write to file with lock to prevent concurrent access issues
            await _writeLock.WaitAsync();
            try
            {
                // Append to file (create if doesn't exist)
                _logger.Debug($"Writing to file: {filePath}");
                await File.AppendAllTextAsync(filePath, json + Environment.NewLine);
                _logger.Debug($"Successfully wrote to file");
            }
            finally
            {
                _writeLock.Release();
            }

            _logger.Debug($"Usage recorded to {fileName}");
        }
        catch (Exception ex)
        {
            // Log error but don't throw - usage tracking should not break the main flow
            _logger.Debug($"Failed to record usage: {ex.GetType().Name} - {ex.Message}");
        }
    }

    public string GetSessionId()
    {
        return _sessionId;
    }

    private static string GenerateSessionId()
    {
        // Generate a unique session ID using timestamp and random component
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var random = Guid.NewGuid().ToString("N").Substring(0, 8);
        return $"{timestamp}-{random}";
    }
}