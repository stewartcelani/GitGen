using GitGen.Models;

namespace GitGen.Tests.Helpers;

/// <summary>
/// Builder classes for creating test data.
/// </summary>
public class UsageEntryBuilder
{
    private readonly UsageEntry _entry;

    public UsageEntryBuilder()
    {
        _entry = new UsageEntry
        {
            Timestamp = DateTime.UtcNow,
            SessionId = Guid.NewGuid().ToString(),
            Model = new ModelInfo
            {
                Name = "test-model",
                Provider = "test-provider",
                ModelId = "test-model-id"
            },
            Tokens = new TokenUsage
            {
                Input = 100,
                Output = 50,
                Total = 150
            },
            Duration = 1.5,
            Success = true,
            ProjectPath = "/test/project",
            GitBranch = "main"
        };
    }

    public UsageEntryBuilder WithTimestamp(DateTime timestamp)
    {
        _entry.Timestamp = timestamp;
        return this;
    }

    public UsageEntryBuilder WithModel(string name, string provider = "test-provider")
    {
        _entry.Model.Name = name;
        _entry.Model.Provider = provider;
        _entry.Model.ModelId = name.ToLower();
        return this;
    }

    public UsageEntryBuilder WithTokens(int input, int output)
    {
        _entry.Tokens.Input = input;
        _entry.Tokens.Output = output;
        _entry.Tokens.Total = input + output;
        return this;
    }

    public UsageEntryBuilder WithCost(decimal amount, string currency = "USD")
    {
        _entry.Cost = new CostInfo
        {
            Amount = amount,
            Currency = currency
        };
        return this;
    }

    public UsageEntryBuilder WithDuration(double seconds)
    {
        _entry.Duration = seconds;
        return this;
    }

    public UsageEntryBuilder WithSessionId(string sessionId)
    {
        _entry.SessionId = sessionId;
        return this;
    }

    public UsageEntryBuilder WithProject(string projectPath, string branch = "main")
    {
        _entry.ProjectPath = projectPath;
        _entry.GitBranch = branch;
        return this;
    }

    public UsageEntryBuilder AsFailure(string error)
    {
        _entry.Success = false;
        _entry.Error = error;
        return this;
    }

    public UsageEntry Build() => _entry;

    public static List<UsageEntry> CreateMultiple(int count, Action<UsageEntryBuilder, int>? customizer = null)
    {
        var entries = new List<UsageEntry>();
        for (int i = 0; i < count; i++)
        {
            var builder = new UsageEntryBuilder();
            customizer?.Invoke(builder, i);
            entries.Add(builder.Build());
        }
        return entries;
    }
}

public static class TestConstants
{
    public static readonly DateTime TestDate = new DateTime(2025, 7, 28, 12, 0, 0, DateTimeKind.Utc);
    public static readonly string TestSessionId = "test-session-123";
    public static readonly string TestProjectPath = "/Users/test/project";
    
    public static class Models
    {
        public const string Gpt4 = "gpt-4";
        public const string Gpt35 = "gpt-3.5-turbo";
        public const string Claude = "claude-3-opus";
        public const string Gemini = "gemini-pro";
    }
}