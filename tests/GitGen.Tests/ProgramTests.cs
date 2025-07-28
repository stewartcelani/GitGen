using FluentAssertions;
using GitGen.Configuration;
using GitGen.Providers;
using GitGen.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Xunit;

namespace GitGen.Tests;

public class ProgramTests : TestBase
{
    [Theory]
    [InlineData(new[] { "@fast" }, new[] { "--model", "fast" })]
    [InlineData(new[] { "commit message", "@smart" }, new[] { "commit message", "--model", "smart" })]
    [InlineData(new[] { "@model1", "@model2" }, new[] { "--model", "model2" })]
    [InlineData(new[] { "-d", "@free", "-p" }, new[] { "-d", "-p", "--model", "free" })]
    public void PreprocessArguments_HandlesModelAliases(string[] input, string[] expected)
    {
        // Act
        var result = Program.PreprocessArguments(input);

        // Assert
        result.Should().Equal(expected);
    }

    [Fact]
    public async Task MainCommand_WithVersion_ShowsVersion()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        
        // Act
        var args = new[] { "-v" };
        var result = await InvokeMainAsync(args);

        // Assert
        result.Should().Be(0);
        output.ToString().Should().Contain("GitGen v");
    }

    [Fact]
    public async Task MainCommand_WithHelp_ShowsHelp()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        
        // Act
        var args = new[] { "--help" };
        var result = await InvokeMainAsync(args);

        // Assert
        result.Should().Be(0);
        var helpText = output.ToString();
        helpText.Should().Contain("GitGen");
        helpText.Should().Contain("Usage:");
        helpText.Should().Contain("Options:");
    }

    [Fact]
    public void PreprocessArguments_WithComplexScenarios_HandlesCorrectly()
    {
        // This test exercises more of the PreprocessArguments logic for better coverage
        
        // Test with empty args
        var result1 = Program.PreprocessArguments(new string[0]);
        result1.Should().BeEmpty();
        
        // Test with no alias
        var result2 = Program.PreprocessArguments(new[] { "some", "message" });
        result2.Should().Equal("some", "message");
        
        // Test with just @ symbol (should be ignored)
        var result3 = Program.PreprocessArguments(new[] { "@" });
        result3.Should().Equal("@");
        
        // Test with multiple arguments and alias at different positions
        var result4 = Program.PreprocessArguments(new[] { "commit", "@fast", "message" });
        result4.Should().Equal("commit", "message", "--model", "fast");
    }

    private async Task<int> InvokeMainAsync(string[] args)
    {
        // Use reflection to invoke the Main method
        var mainMethod = typeof(Program).GetMethod("Main", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        if (mainMethod != null)
        {
            var task = (Task<int>)mainMethod.Invoke(null, new object[] { args })!;
            return await task;
        }
        
        throw new InvalidOperationException("Could not find Main method");
    }

    private async Task<int> InvokeWithServicesAsync(string[] args, IServiceProvider serviceProvider)
    {
        // Build command line with test services
        var rootCommand = BuildTestRootCommand(serviceProvider);
        
        var parser = new CommandLineBuilder(rootCommand)
            .UseHelp()
            .Build();

        return await parser.InvokeAsync(args);
    }

    private RootCommand BuildTestRootCommand(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("Test GitGen");
        
        var configCommand = new Command("config", "Run configuration");
        configCommand.SetHandler(async () =>
        {
            var menuService = serviceProvider.GetRequiredService<ConfigurationMenuService>();
            await menuService.RunAsync();
        });
        
        rootCommand.AddCommand(configCommand);
        return rootCommand;
    }

    private void ConfigureTestServices(IServiceCollection services, ConfigurationMenuService menuService)
    {
        var factory = new ConsoleLoggerFactory();
        services.AddSingleton<ConsoleLoggerFactory>(factory);
        services.AddSingleton(factory.CreateLogger<Program>());
        services.AddSingleton<ConfigurationMenuService>(menuService);
    }
}