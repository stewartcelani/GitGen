using FluentAssertions;
using GitGen.Configuration;
using GitGen.Providers;
using GitGen.Services;
using NSubstitute;
using Xunit;

namespace GitGen.Tests.Services;

public class ConfigurationWizardServiceTests : TestBase
{
    private readonly ConfigurationWizardService _wizardService;
    private readonly ISecureConfigurationService _secureConfig;
    private readonly ProviderFactory _providerFactory;
    private readonly ICommitMessageProvider _provider;

    public ConfigurationWizardServiceTests()
    {
        _secureConfig = Substitute.For<ISecureConfigurationService>();
        _providerFactory = Substitute.For<ProviderFactory>(CreateServiceProvider(), Logger);
        _provider = Substitute.For<ICommitMessageProvider>();
        
        _providerFactory.CreateProvider(Arg.Any<ModelConfiguration>()).Returns(_provider);
        
        var configService = new ConfigurationService(Logger, _secureConfig);
        _wizardService = new ConfigurationWizardService(Logger, _providerFactory, configService, _secureConfig);
    }

    [Fact]
    public async Task QuickChangeMaxTokens_WithSingleModel_UpdatesTokens()
    {
        // Arrange
        var model = CreateTestModel("test-model");
        model.Id = "1";
        model.MaxOutputTokens = 1000;
        
        _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            Models = new List<ModelConfiguration> { model }
        });

        // Simulate user input
        var input = new StringReader("2000\n");
        Console.SetIn(input);

        // Act
        var result = await _wizardService.QuickChangeMaxTokens();

        // Assert
        result.Should().BeTrue();
        await _secureConfig.Received().UpdateModelAsync(
            Arg.Is<ModelConfiguration>(m => m.MaxOutputTokens == 2000));
    }

    [Fact]
    public async Task QuickChangeModel_TestsNewModelBeforeApplying()
    {
        // Arrange
        var model = CreateTestModel("test-model");
        model.Id = "1";
        model.ModelId = "gpt-4";
        
        _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            Models = new List<ModelConfiguration> { model }
        });

        _provider.TestConnectionAndDetectParametersAsync()
            .Returns((true, false, 0.2));

        // Simulate user input for new model ID
        var input = new StringReader("gpt-4-turbo\n");
        Console.SetIn(input);

        // Act
        var result = await _wizardService.QuickChangeModel();

        // Assert
        result.Should().BeTrue();
        await _provider.Received().TestConnectionAndDetectParametersAsync();
        await _secureConfig.Received().UpdateModelAsync(
            Arg.Is<ModelConfiguration>(m => m.ModelId == "gpt-4-turbo"));
    }

    [Fact]
    public async Task QuickChangeModel_WithFailedTest_RevertsChange()
    {
        // Arrange
        var model = CreateTestModel("test-model");
        model.Id = "1";
        model.ModelId = "gpt-4";
        
        _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            Models = new List<ModelConfiguration> { model }
        });

        _provider.TestConnectionAndDetectParametersAsync()
            .Returns((false, false, 0.2));

        // Simulate user input
        var input = new StringReader("invalid-model\n");
        Console.SetIn(input);

        // Act
        var result = await _wizardService.QuickChangeModel();

        // Assert
        result.Should().BeFalse();
        await _secureConfig.DidNotReceive().UpdateModelAsync(Arg.Any<ModelConfiguration>());
    }

    [Fact]
    public async Task RunWizardAsync_WithNoSecureConfig_ReturnsNull()
    {
        // Arrange
        var configService = new ConfigurationService(Logger, null);
        var wizard = new ConfigurationWizardService(Logger, _providerFactory, configService, null);

        // Act
        var result = await wizard.RunWizardAsync();

        // Assert
        result.Should().BeNull();
        Logger.Received().Error("Secure configuration service not available");
    }
}