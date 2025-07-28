using FluentAssertions;
using GitGen.Configuration;
using GitGen.Providers;
using GitGen.Services;
using NSubstitute;
using System.Text;
using Xunit;

namespace GitGen.Tests.Services;

public class ConfigurationMenuServiceTests : TestBase
{
    private readonly ISecureConfigurationService _secureConfig;
    private readonly ConfigurationWizardService _wizardService;
    private readonly ConfigurationService _configService;
    private readonly ProviderFactory _providerFactory;
    private readonly ConfigurationMenuService _menuService;

    public ConfigurationMenuServiceTests()
    {
        _secureConfig = Substitute.For<ISecureConfigurationService>();
        _providerFactory = Substitute.For<ProviderFactory>(CreateServiceProvider(), Logger);
        _configService = new ConfigurationService(Logger, _secureConfig);
        _wizardService = new ConfigurationWizardService(Logger, _providerFactory, _configService, _secureConfig);
        
        _menuService = new ConfigurationMenuService(
            Logger,
            _secureConfig,
            _wizardService,
            _configService,
            _providerFactory);
    }

    [Fact]
    public async Task DisplayAppSettingsMenu_ShowsRequirePromptConfirmationStatus()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Settings = new AppSettings
            {
                ShowTokenUsage = true,
                CopyToClipboard = true,
                EnablePartialAliasMatching = true,
                MinimumAliasMatchLength = 2,
                RequirePromptConfirmation = true
            }
        };
        _secureConfig.LoadSettingsAsync().Returns(settings);

        var output = new StringWriter();
        Console.SetOut(output);

        // Act
        // We'll simulate running the menu by calling the private method through the public interface
        // Since DisplayAppSettingsMenu is private, we'll test it through ConfigureAppSettings
        var input = new StringReader("0\n"); // Exit immediately
        Console.SetIn(input);
        
        await _menuService.RunAsync();
        input = new StringReader("4\n0\n"); // Go to app settings, then exit
        Console.SetIn(input);
        await _menuService.RunAsync();

        // Assert
        var outputText = output.ToString();
        outputText.Should().Contain("5. Require prompt confirmation: ON");
    }

    [Fact]
    public async Task TogglePromptConfirmation_ChangesSettingFromTrueToFalse()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Settings = new AppSettings
            {
                RequirePromptConfirmation = true
            }
        };
        _secureConfig.LoadSettingsAsync().Returns(settings);

        var output = new StringWriter();
        Console.SetOut(output);

        // Simulate selecting option 5 in app settings menu
        var input = new StringReader("4\n5\n0\n0\n"); // App settings -> Toggle confirmation -> Back -> Exit
        Console.SetIn(input);

        // Act
        await _menuService.RunAsync();

        // Assert
        await _secureConfig.Received(1).SaveSettingsAsync(
            Arg.Is<GitGenSettings>(s => s.Settings.RequirePromptConfirmation == false));
        
        var outputText = output.ToString();
        outputText.Should().Contain("Prompt confirmation disabled");
    }

    [Fact]
    public async Task TogglePromptConfirmation_ChangesSettingFromFalseToTrue()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Settings = new AppSettings
            {
                RequirePromptConfirmation = false
            }
        };
        _secureConfig.LoadSettingsAsync().Returns(settings);

        var output = new StringWriter();
        Console.SetOut(output);

        // Simulate selecting option 5 in app settings menu
        var input = new StringReader("4\n5\n0\n0\n"); // App settings -> Toggle confirmation -> Back -> Exit
        Console.SetIn(input);

        // Act
        await _menuService.RunAsync();

        // Assert
        await _secureConfig.Received(1).SaveSettingsAsync(
            Arg.Is<GitGenSettings>(s => s.Settings.RequirePromptConfirmation == true));
        
        var outputText = output.ToString();
        outputText.Should().Contain("Prompt confirmation enabled");
    }

    [Fact]
    public async Task AppSettingsMenu_ShowsCorrectStatusForAllSettings()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Settings = new AppSettings
            {
                ShowTokenUsage = false,
                CopyToClipboard = false,
                EnablePartialAliasMatching = false,
                MinimumAliasMatchLength = 3,
                RequirePromptConfirmation = false
            }
        };
        _secureConfig.LoadSettingsAsync().Returns(settings);

        var output = new StringWriter();
        Console.SetOut(output);

        // Navigate to app settings and exit
        var input = new StringReader("4\n0\n0\n");
        Console.SetIn(input);

        // Act
        await _menuService.RunAsync();

        // Assert
        var outputText = output.ToString();
        outputText.Should().Contain("1. Show token usage: OFF");
        outputText.Should().Contain("2. Copy to clipboard: OFF");
        outputText.Should().Contain("3. Enable partial alias matching: OFF");
        outputText.Should().Contain("4. Minimum alias match length: 3 chars");
        outputText.Should().Contain("5. Require prompt confirmation: OFF");
    }

    [Fact]
    public async Task ConfigureAppSettings_HandlesInvalidOption()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Settings = new AppSettings()
        };
        _secureConfig.LoadSettingsAsync().Returns(settings);

        var output = new StringWriter();
        Console.SetOut(output);

        // Try invalid option, then exit
        var input = new StringReader("4\n99\n0\n0\n");
        Console.SetIn(input);

        // Act
        await _menuService.RunAsync();

        // Assert
        var outputText = output.ToString();
        outputText.Should().Contain("Invalid choice. Please try again.");
    }
}