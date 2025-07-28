using FluentAssertions;
using Xunit;

namespace GitGen.Tests;

/// <summary>
/// Comprehensive tests for command line argument parsing, particularly the @model syntax.
/// These tests ensure that the preprocessing logic correctly handles all variations of
/// model alias specification in combination with options and custom prompts.
/// </summary>
public class CommandLineParsingTests
{
    #region Test Helpers

    /// <summary>
    /// Helper method to assert processed args using the actual Program.PreprocessArguments method.
    /// Also extracts the model name from the processed args for verification.
    /// </summary>
    private static void AssertArgumentProcessing(
        string[] input, 
        string[] expectedArgs, 
        string? expectedModel = null,
        string? testDescription = null)
    {
        // Use the actual preprocessing logic from Program.cs
        var processedArgs = Program.PreprocessArguments(input);
        
        // Extract model name from processed args if --model option exists
        string? modelName = null;
        for (int i = 0; i < processedArgs.Length - 1; i++)
        {
            if (processedArgs[i] == "--model")
            {
                modelName = processedArgs[i + 1];
                break;
            }
        }
        
        processedArgs.Should().Equal(expectedArgs, 
            because: testDescription ?? $"processing {string.Join(" ", input)} should produce expected args");
        modelName.Should().Be(expectedModel, 
            because: testDescription ?? $"processing {string.Join(" ", input)} should extract model '{expectedModel ?? "null"}'");
    }

    #endregion

    #region Basic @model Parsing Tests

    [Fact]
    public void ParsesSimpleModelAlias()
    {
        // Test: gitgen @fast
        // Expected: ["--model", "fast"]
        AssertArgumentProcessing(
            new[] { "@fast" },
            new[] { "--model", "fast" },
            "fast",
            "simple @model should be parsed correctly");
    }

    [Fact]
    public void ParsesModelAliasWithSingleOption()
    {
        // Test: gitgen -d @free
        // Expected: ["-d", "--model", "free"]
        AssertArgumentProcessing(
            new[] { "-d", "@free" },
            new[] { "-d", "--model", "free" },
            "free",
            "@model with debug flag should parse correctly");
    }

    [Fact]
    public void ParsesModelAliasWithMultipleOptions()
    {
        // Test: gitgen -d -p @smart
        // Expected: ["-d", "-p", "--model", "smart"]
        AssertArgumentProcessing(
            new[] { "-d", "-p", "@smart" },
            new[] { "-d", "-p", "--model", "smart" },
            "smart",
            "@model with multiple options should parse correctly");
    }

    [Fact]
    public void ParsesModelAliasBeforeOptions()
    {
        // Test: gitgen @fast -d
        // Expected: ["-d", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "@fast", "-d" },
            new[] { "-d", "--model", "fast" },
            "fast",
            "@model before options should parse correctly");
    }

    [Fact]
    public void ParsesModelAliasInMiddleOfOptions()
    {
        // Test: gitgen -d @fast -p
        // Expected: ["-d", "-p", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "-d", "@fast", "-p" },
            new[] { "-d", "-p", "--model", "fast" },
            "fast",
            "@model between options should parse correctly");
    }

    [Fact]
    public void ParsesModelWithLongOptions()
    {
        // Test: gitgen --debug @ultrathink --preview
        // Expected: ["--debug", "--preview", "--model", "ultrathink"]
        AssertArgumentProcessing(
            new[] { "--debug", "@ultrathink", "--preview" },
            new[] { "--debug", "--preview", "--model", "ultrathink" },
            "ultrathink",
            "@model with long option names should parse correctly");
    }

    #endregion

    #region Custom Prompt Tests

    [Fact]
    public void ParsesModelWithSimplePrompt()
    {
        // Test: gitgen fix bug @fast
        // Expected: ["fix", "bug", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "fix", "bug", "@fast" },
            new[] { "fix", "bug", "--model", "fast" },
            "fast",
            "@model with unquoted prompt should parse correctly");
    }

    [Fact]
    public void ParsesModelWithQuotedPrompt()
    {
        // Test: gitgen "my custom prompt" @fast
        // Expected: ["my custom prompt", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "my custom prompt", "@fast" },
            new[] { "my custom prompt", "--model", "fast" },
            "fast",
            "@model with quoted prompt should parse correctly");
    }

    [Fact]
    public void ParsesPromptContainingAtSymbol()
    {
        // Test: gitgen "email@example.com" @fast
        // Expected: ["email@example.com", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "email@example.com", "@fast" },
            new[] { "email@example.com", "--model", "fast" },
            "fast",
            "@ symbol within prompt should be ignored");
    }

    [Fact]
    public void ParsesAtSymbolInQuotedString()
    {
        // Test: gitgen "@symbol in quotes"
        // Expected: ["--model", "symbol in quotes"] 
        // Note: The preprocessing logic treats ANY argument starting with @ as a model
        AssertArgumentProcessing(
            new[] { "@symbol in quotes" },
            new[] { "--model", "symbol in quotes" },
            "symbol in quotes",
            "@ at start of argument is always treated as model, even with spaces");
    }

    [Fact]
    public void ParsesModelBeforePrompt()
    {
        // Test: gitgen @claude "explain the changes"
        // Expected: ["explain the changes", "--model", "claude"]
        AssertArgumentProcessing(
            new[] { "@claude", "explain the changes" },
            new[] { "explain the changes", "--model", "claude" },
            "claude",
            "@model before prompt should parse correctly");
    }

    [Fact]
    public void ParsesComplexPromptWithModel()
    {
        // Test: gitgen "feat: add @mention support" @fast
        // Expected: ["feat: add @mention support", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "feat: add @mention support", "@fast" },
            new[] { "feat: add @mention support", "--model", "fast" },
            "fast",
            "prompt containing @ should not interfere with @model parsing");
    }

    #endregion

    #region Multiple @model Tests

    [Fact]
    public void LastModelWins()
    {
        // Test: gitgen @fast @smart @free
        // Expected: ["--model", "free"] (last one wins)
        AssertArgumentProcessing(
            new[] { "@fast", "@smart", "@free" },
            new[] { "--model", "free" },
            "free",
            "when multiple @models specified, last one should win");
    }

    [Fact]
    public void LastModelWinsWithOptions()
    {
        // Test: gitgen -d @fast -p @smart
        // Expected: ["-d", "-p", "--model", "smart"]
        AssertArgumentProcessing(
            new[] { "-d", "@fast", "-p", "@smart" },
            new[] { "-d", "-p", "--model", "smart" },
            "smart",
            "when multiple @models with options, last one should win");
    }

    [Fact]
    public void LastModelWinsWithPrompts()
    {
        // Test: gitgen "prompt1" @fast "prompt2" @smart
        // Expected: ["prompt1", "prompt2", "--model", "smart"]
        AssertArgumentProcessing(
            new[] { "prompt1", "@fast", "prompt2", "@smart" },
            new[] { "prompt1", "prompt2", "--model", "smart" },
            "smart",
            "when multiple @models with prompts, last one should win");
    }

    [Fact]
    public void LastModelWinsInComplexScenario()
    {
        // Test: gitgen -d @gpt4 "fix bug" @claude -p @free
        // Expected: ["-d", "fix bug", "-p", "--model", "free"]
        AssertArgumentProcessing(
            new[] { "-d", "@gpt4", "fix bug", "@claude", "-p", "@free" },
            new[] { "-d", "fix bug", "-p", "--model", "free" },
            "free",
            "in complex scenario with multiple @models, last one should win");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void HandlesEmptyModelName()
    {
        // Test: gitgen @
        // Expected: ["@"] (no model extracted because length must be > 1)
        AssertArgumentProcessing(
            new[] { "@" },
            new[] { "@" },
            null,
            "single @ character should not be treated as model");
    }

    [Fact]
    public void HandlesModelWithNumbers()
    {
        // Test: gitgen @gpt4
        // Expected: ["--model", "gpt4"]
        AssertArgumentProcessing(
            new[] { "@gpt4" },
            new[] { "--model", "gpt4" },
            "gpt4",
            "model names with numbers should parse correctly");
    }

    [Fact]
    public void HandlesModelWithHyphens()
    {
        // Test: gitgen @claude-fast
        // Expected: ["--model", "claude-fast"]
        AssertArgumentProcessing(
            new[] { "@claude-fast" },
            new[] { "--model", "claude-fast" },
            "claude-fast",
            "model names with hyphens should parse correctly");
    }

    [Fact]
    public void HandlesModelWithUnderscores()
    {
        // Test: gitgen @llama_local
        // Expected: ["--model", "llama_local"]
        AssertArgumentProcessing(
            new[] { "@llama_local" },
            new[] { "--model", "llama_local" },
            "llama_local",
            "model names with underscores should parse correctly");
    }

    [Fact]
    public void HandlesModelWithDots()
    {
        // Test: gitgen @claude.3.5
        // Expected: ["--model", "claude.3.5"]
        AssertArgumentProcessing(
            new[] { "@claude.3.5" },
            new[] { "--model", "claude.3.5" },
            "claude.3.5",
            "model names with dots should parse correctly");
    }

    [Fact]
    public void HandlesModelWithMixedCase()
    {
        // Test: gitgen @GPT4Turbo
        // Expected: ["--model", "GPT4Turbo"]
        AssertArgumentProcessing(
            new[] { "@GPT4Turbo" },
            new[] { "--model", "GPT4Turbo" },
            "GPT4Turbo",
            "model names with mixed case should preserve case");
    }

    #endregion

    #region Complex Real-World Scenarios

    [Fact]
    public void ParsesRealWorldCommand_DebugWithPromptAndPreview()
    {
        // Test: gitgen -d "fix security bug" @fast -p
        // Expected: ["-d", "fix security bug", "-p", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "-d", "fix security bug", "@fast", "-p" },
            new[] { "-d", "fix security bug", "-p", "--model", "fast" },
            "fast",
            "real-world command with debug, prompt, model, and preview");
    }

    [Fact]
    public void ParsesRealWorldCommand_ModelFirstWithDetailedPrompt()
    {
        // Test: gitgen @ultrathink "explain the refactoring in detail"
        // Expected: ["explain the refactoring in detail", "--model", "ultrathink"]
        AssertArgumentProcessing(
            new[] { "@ultrathink", "explain the refactoring in detail" },
            new[] { "explain the refactoring in detail", "--model", "ultrathink" },
            "ultrathink",
            "real-world command with model first and detailed prompt");
    }

    [Fact]
    public void ParsesRealWorldCommand_AllOptionTypes()
    {
        // Test: gitgen --debug @free "make it a haiku" --preview
        // Expected: ["--debug", "make it a haiku", "--preview", "--model", "free"]
        AssertArgumentProcessing(
            new[] { "--debug", "@free", "make it a haiku", "--preview" },
            new[] { "--debug", "make it a haiku", "--preview", "--model", "free" },
            "free",
            "real-world command with long options, model, and prompt");
    }

    [Fact]
    public void ParsesRealWorldCommand_MultiWordPromptNoQuotes()
    {
        // Test: gitgen fix critical security vulnerability @smart -p
        // Expected: ["fix", "critical", "security", "vulnerability", "-p", "--model", "smart"]
        AssertArgumentProcessing(
            new[] { "fix", "critical", "security", "vulnerability", "@smart", "-p" },
            new[] { "fix", "critical", "security", "vulnerability", "-p", "--model", "smart" },
            "smart",
            "real-world command with multi-word unquoted prompt");
    }

    #endregion

    #region Special Characters and Unicode

    [Fact]
    public void HandlesSpecialCharactersInModelName()
    {
        // Test various special characters that might appear in model names
        var specialCharModels = new[]
        {
            ("@model+plus", "model+plus"),
            ("@model=equals", "model=equals"),
            ("@model$dollar", "model$dollar"),
            ("@model!exclaim", "model!exclaim"),
            ("@model~tilde", "model~tilde"),
            ("@model#hash", "model#hash"),
            ("@model%percent", "model%percent")
        };

        foreach (var (input, expected) in specialCharModels)
        {
            AssertArgumentProcessing(
                new[] { input },
                new[] { "--model", expected },
                expected,
                $"model name with special character '{input}' should parse correctly");
        }
    }

    [Fact]
    public void HandlesUnicodeInPrompt()
    {
        // Test: gitgen "🚀 feat: add emoji support" @fast
        // Expected: ["🚀 feat: add emoji support", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "🚀 feat: add emoji support", "@fast" },
            new[] { "🚀 feat: add emoji support", "--model", "fast" },
            "fast",
            "prompt with emoji should not affect model parsing");
    }

    [Fact]
    public void HandlesPromptWithNewlines()
    {
        // Test: gitgen "line1\nline2" @smart
        // Expected: ["line1\nline2", "--model", "smart"]
        AssertArgumentProcessing(
            new[] { "line1\nline2", "@smart" },
            new[] { "line1\nline2", "--model", "smart" },
            "smart",
            "prompt with newlines should parse correctly");
    }

    #endregion

    #region Empty and Null Tests

    [Fact]
    public void HandlesEmptyArgumentArray()
    {
        // Test: gitgen (no arguments)
        // Expected: [] (empty array)
        AssertArgumentProcessing(
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            "empty argument array should return empty processed array");
    }

    [Fact]
    public void HandlesSingleAtCharacter()
    {
        // Test: gitgen @
        // Expected: ["@"]
        AssertArgumentProcessing(
            new[] { "@" },
            new[] { "@" },
            null,
            "single @ should be passed through as regular argument");
    }

    [Fact]
    public void HandlesMultipleAtCharacters()
    {
        // Test: gitgen @@@@
        // Expected: ["--model", "@@@"]
        // Note: @@@@ is parsed as @ followed by @@@ (the model name)
        AssertArgumentProcessing(
            new[] { "@@@@" },
            new[] { "--model", "@@@" },
            "@@@",
            "multiple @ characters: first @ indicates model, rest are the model name");
    }

    [Fact]
    public void HandlesAtFollowedBySpace()
    {
        // Test: gitgen "@ space"
        // Expected: ["--model", " space"]
        // Note: The preprocessing sees "@ space" as @ followed by " space"
        AssertArgumentProcessing(
            new[] { "@ space" },
            new[] { "--model", " space" },
            " space",
            "@ followed by space extracts space as part of model name");
    }

    #endregion

    #region Windows Path Tests

    [Fact]
    public void HandlesWindowsPathsCorrectly()
    {
        // Test: gitgen "C:\Users\@test\file.txt" @model
        // Expected: ["C:\Users\@test\file.txt", "--model", "model"]
        AssertArgumentProcessing(
            new[] { @"C:\Users\@test\file.txt", "@model" },
            new[] { @"C:\Users\@test\file.txt", "--model", "model" },
            "model",
            "Windows paths containing @ should not interfere with model parsing");
    }

    #endregion

    #region Version and Help Tests

    [Fact]
    public void ParsesModelWithVersionFlag()
    {
        // Test: gitgen -v @fast
        // Expected: ["-v", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "-v", "@fast" },
            new[] { "-v", "--model", "fast" },
            "fast",
            "model with version flag should parse correctly");
    }

    [Fact]
    public void ParsesModelWithHelpFlag()
    {
        // Test: gitgen --help @fast
        // Expected: ["--help", "--model", "fast"]
        AssertArgumentProcessing(
            new[] { "--help", "@fast" },
            new[] { "--help", "--model", "fast" },
            "fast",
            "model with help flag should parse correctly");
    }

    #endregion

    #region Order Preservation Tests

    [Fact]
    public void PreservesArgumentOrder()
    {
        // Test: gitgen arg1 -d arg2 @model arg3 -p arg4
        // Expected: ["arg1", "-d", "arg2", "arg3", "-p", "arg4", "--model", "model"]
        AssertArgumentProcessing(
            new[] { "arg1", "-d", "arg2", "@model", "arg3", "-p", "arg4" },
            new[] { "arg1", "-d", "arg2", "arg3", "-p", "arg4", "--model", "model" },
            "model",
            "non-model arguments should preserve their relative order");
    }

    #endregion

    #region Stress Tests

    [Fact]
    public void HandlesVeryLongModelName()
    {
        var longModelName = new string('a', 100);
        var input = $"@{longModelName}";
        
        AssertArgumentProcessing(
            new[] { input },
            new[] { "--model", longModelName },
            longModelName,
            "very long model names should parse correctly");
    }

    [Fact]
    public void HandlesManyArguments()
    {
        // Create a scenario with many arguments
        var args = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            args.Add($"arg{i}");
        }
        args.Add("@model");
        for (int i = 50; i < 100; i++)
        {
            args.Add($"arg{i}");
        }

        var expectedArgs = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            expectedArgs.Add($"arg{i}");
        }
        expectedArgs.Add("--model");
        expectedArgs.Add("model");

        AssertArgumentProcessing(
            args.ToArray(),
            expectedArgs.ToArray(),
            "model",
            "large number of arguments should not affect model parsing");
    }

    #endregion

    #region Data-Driven Tests

    [Theory]
    [InlineData(new[] { "@fast" }, new[] { "--model", "fast" }, "fast")]
    [InlineData(new[] { "-d", "@free" }, new[] { "-d", "--model", "free" }, "free")]
    [InlineData(new[] { "@model1", "@model2" }, new[] { "--model", "model2" }, "model2")]
    [InlineData(new[] { "prompt", "@ai" }, new[] { "prompt", "--model", "ai" }, "ai")]
    [InlineData(new[] { "@", "text" }, new[] { "@", "text" }, null)]
    [InlineData(new[] { "" }, new[] { "" }, null)]
    public void DataDrivenModelParsingTests(string[] input, string[] expectedArgs, string? expectedModel)
    {
        AssertArgumentProcessing(input, expectedArgs, expectedModel);
    }

    #endregion

    #region Config Command Tests

    [Fact]
    public void ConfigCommandIgnoresModel()
    {
        // Test: gitgen config @model
        // Expected: ["config", "--model", "model"]
        // Note: The config command should still parse @model even if it doesn't use it
        AssertArgumentProcessing(
            new[] { "config", "@model" },
            new[] { "config", "--model", "model" },
            "model",
            "config command should still parse @model syntax");
    }

    #endregion
}