using GitGen.Configuration;
using GitGen.Helpers;
using GitGen.Providers;
using System.Text;

namespace GitGen.Services;

/// <summary>
///     Provides an interactive menu-driven configuration system for GitGen.
///     Consolidates various configuration tasks into a unified interface.
/// </summary>
public class ConfigurationMenuService
{
    private readonly IConsoleLogger _logger;
    private readonly ISecureConfigurationService _secureConfig;
    private readonly ConfigurationWizardService _wizardService;
    private readonly ConfigurationService _configService;
    private readonly ProviderFactory _providerFactory;
    
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationMenuService" /> class.
    /// </summary>
    public ConfigurationMenuService(
        IConsoleLogger logger,
        ISecureConfigurationService secureConfig,
        ConfigurationWizardService wizardService,
        ConfigurationService configService,
        ProviderFactory providerFactory)
    {
        _logger = logger;
        _secureConfig = secureConfig;
        _wizardService = wizardService;
        _configService = configService;
        _providerFactory = providerFactory;
    }
    
    /// <summary>
    ///     Runs the main configuration menu.
    /// </summary>
    public async Task RunAsync()
    {
        while (true)
        {
            Console.Clear();
            await DisplayMainMenu();
            
            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1":
                    await AddNewModel();
                    break;
                case "2":
                    await ManageModels();
                    break;
                case "3":
                    await TestModels();
                    break;
                case "4":
                    await ConfigureAppSettings();
                    break;
                case "5":
                    await ResetConfiguration();
                    break;
                case "0":
                case "":
                case null:
                    return;
                default:
                    _logger.Warning("Invalid choice. Please try again.");
                    await Task.Delay(1500);
                    break;
            }
        }
    }
    
    private async Task DisplayMainMenu()
    {
        var settings = await _secureConfig.LoadSettingsAsync();
        var modelCount = settings.Models.Count;
        
        _logger.Information("╔════════════════════════════════════════╗");
        _logger.Information("║         GitGen Configuration           ║");
        _logger.Information("╚════════════════════════════════════════╝");
        Console.WriteLine();
        
        _logger.Information("1. Add new model");
        _logger.Information($"2. Manage models ({modelCount} configured)");
        _logger.Information("3. Test models");
        _logger.Information("4. App settings");
        _logger.Information("5. Reset all configuration");
        _logger.Information("0. Exit");
        
        Console.WriteLine();
        Console.Write("Select option: ");
    }
    
    private async Task AddNewModel()
    {
        Console.Clear();
        _logger.Information("═══ Add New Model ═══");
        Console.WriteLine();
        
        var model = await _wizardService.RunMultiModelWizardAsync();
        if (model == null)
        {
            _logger.Warning("Model configuration cancelled.");
        }
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ManageModels()
    {
        while (true)
        {
            Console.Clear();
            await DisplayModelManagementMenu();
            
            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1":
                    await ListModels();
                    break;
                case "2":
                    await SetDefaultModel();
                    break;
                case "3":
                    await EditModel();
                    break;
                case "4":
                    await DeleteModel();
                    break;
                case "0":
                case "":
                case null:
                    return;
                default:
                    _logger.Warning("Invalid choice. Please try again.");
                    await Task.Delay(1500);
                    break;
            }
        }
    }
    
    private async Task DisplayModelManagementMenu()
    {
        var settings = await _secureConfig.LoadSettingsAsync();
        
        _logger.Information("═══ Model Management ═══");
        Console.WriteLine();
        
        _logger.Information("1. List models");
        _logger.Information("2. Set default model");
        _logger.Information("3. Edit model (aliases, tokens, etc.)");
        _logger.Information("4. Delete model");
        _logger.Information("0. Back to main menu");
        
        Console.WriteLine();
        Console.Write("Select option: ");
    }
    
    private async Task ListModels()
    {
        Console.Clear();
        _logger.Information("═══ Configured Models ═══");
        Console.WriteLine();
        
        var settings = await _secureConfig.LoadSettingsAsync();
        
        if (!settings.Models.Any())
        {
            _logger.Information("No models configured. Use 'Add new model' to configure one.");
        }
        else
        {
            foreach (var model in settings.Models.OrderByDescending(m => m.LastUsed))
            {
                var defaultMarker = model.Id == settings.DefaultModelId ? " ⭐ (default)" : "";
                var lastUsed = DateTimeHelper.ToLocalDateTimeString(model.LastUsed);
                
                _logger.Information($"  {model.Name}{defaultMarker}");
                _logger.Muted($"    Type: {model.Type} | Provider: {model.Provider} | Model: {model.ModelId}");
                _logger.Muted($"    URL: {model.Url}");
                _logger.Muted($"    Temperature: {model.Temperature} | Max Output Tokens: {model.MaxOutputTokens:N0}");
                
                if (!string.IsNullOrWhiteSpace(model.Note))
                    _logger.Muted($"    Note: {model.Note}");
                
                if (model.Aliases != null && model.Aliases.Count > 0)
                {
                    var aliasesStr = string.Join(", ", model.Aliases.OrderBy(a => a).Select(a => $"@{a}"));
                    _logger.Muted($"    Aliases: {aliasesStr}");
                }
                
                if (model.Pricing != null)
                {
                    var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
                    _logger.Muted($"    Pricing: {pricingInfo}");
                }
                
                _logger.Muted($"    Last used: {lastUsed}");
                Console.WriteLine();
            }
        }
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task SetDefaultModel()
    {
        Console.Clear();
        _logger.Information("═══ Set Default Model ═══");
        Console.WriteLine();
        
        var settings = await _secureConfig.LoadSettingsAsync();
        
        if (!settings.Models.Any())
        {
            _logger.Warning("No models configured.");
            await Task.Delay(1500);
            return;
        }
        
        // Display numbered list of models
        for (int i = 0; i < settings.Models.Count; i++)
        {
            var model = settings.Models[i];
            var defaultMarker = model.Id == settings.DefaultModelId ? " (current default)" : "";
            _logger.Information($"{i + 1}. {model.Name}{defaultMarker}");
        }
        
        Console.WriteLine();
        Console.Write("Select model number (0 to cancel): ");
        
        if (int.TryParse(Console.ReadLine(), out int selection) && 
            selection > 0 && selection <= settings.Models.Count)
        {
            var selectedModel = settings.Models[selection - 1];
            
            // Check if this appears to be a public/free model
            if (AppearsToBePublicModel(selectedModel))
            {
                Console.WriteLine();
                _logger.Warning($"{Constants.UI.WarningSymbol} Warning: This model appears to be configured for public/free use.");
                _logger.Warning("   Setting it as default means running 'gitgen' without specifying a model");
                _logger.Warning("   will send your code to this service.");
                Console.WriteLine();
                Console.Write("Are you sure you want to set this as the default model? (y/N): ");
                var confirm = Console.ReadLine()?.Trim().ToLower();
                
                if (confirm != "y" && confirm != "yes")
                {
                    _logger.Information("Default model change cancelled.");
                    await Task.Delay(1500);
                    return;
                }
            }
            
            try
            {
                await _secureConfig.SetDefaultModelAsync(selectedModel.Name);
                _logger.Success($"{Constants.UI.CheckMark} Default model changed to '{selectedModel.Name}'");
            }
            catch (Exception ex)
            {
                _logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
            }
        }
        
        await Task.Delay(1500);
    }
    
    private async Task EditModel()
    {
        Console.Clear();
        _logger.Information("═══ Edit Model ═══");
        Console.WriteLine();
        
        var settings = await _secureConfig.LoadSettingsAsync();
        
        if (!settings.Models.Any())
        {
            _logger.Warning("No models configured.");
            await Task.Delay(1500);
            return;
        }
        
        // Display numbered list of models
        for (int i = 0; i < settings.Models.Count; i++)
        {
            var model = settings.Models[i];
            _logger.Information($"{i + 1}. {model.Name}");
        }
        
        Console.WriteLine();
        Console.Write("Select model number to edit (0 to cancel): ");
        
        if (int.TryParse(Console.ReadLine(), out int selection) && 
            selection > 0 && selection <= settings.Models.Count)
        {
            var selectedModel = settings.Models[selection - 1];
            await EditModelMenu(selectedModel);
        }
    }
    
    private async Task EditModelMenu(ModelConfiguration model)
    {
        while (true)
        {
            Console.Clear();
            
            // Load settings to check if this is the default model
            var settings = await _secureConfig.LoadSettingsAsync();
            var defaultMarker = model.Id == settings.DefaultModelId ? " ⭐ (default)" : "";
            
            _logger.Information($"═══ Edit Model: {model.Name}{defaultMarker} ═══");
            Console.WriteLine();
            
            // Display model details (same as in ListModels)
            _logger.Muted($"    Type: {model.Type} | Provider: {model.Provider} | Model: {model.ModelId}");
            _logger.Muted($"    URL: {model.Url}");
            _logger.Muted($"    Temperature: {model.Temperature} | Max Output Tokens: {model.MaxOutputTokens:N0}");
            
            if (!string.IsNullOrWhiteSpace(model.Note))
                _logger.Muted($"    Note: {model.Note}");
            
            if (model.Aliases != null && model.Aliases.Count > 0)
            {
                var aliasesStr = string.Join(", ", model.Aliases.OrderBy(a => a).Select(a => $"@{a}"));
                _logger.Muted($"    Aliases: {aliasesStr}");
            }
            
            var lastUsed = DateTimeHelper.ToLocalDateTimeString(model.LastUsed);
            _logger.Muted($"    Last used: {lastUsed}");
            
            Console.WriteLine();
            
            _logger.Information("1. Manage aliases");
            _logger.Information("2. Change max output tokens");
            _logger.Information("3. Update note/description");
            _logger.Information("4. Test this model");
            _logger.Information("0. Back to model management");
            
            Console.WriteLine();
            Console.Write("Select option: ");
            
            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1":
                    await ManageAliases(model);
                    break;
                case "2":
                    await ChangeMaxTokens(model);
                    break;
                case "3":
                    await UpdateNote(model);
                    break;
                case "4":
                    await TestSingleModelFromMenu(model);
                    break;
                case "0":
                case "":
                case null:
                    return;
                default:
                    _logger.Warning("Invalid choice. Please try again.");
                    await Task.Delay(1500);
                    break;
            }
        }
    }
    
    private async Task ManageAliases(ModelConfiguration model)
    {
        Console.Clear();
        _logger.Information($"═══ Manage Aliases: {model.Name} ═══");
        Console.WriteLine();
        
        if (model.Aliases != null && model.Aliases.Count > 0)
        {
            _logger.Information("Current aliases:");
            foreach (var alias in model.Aliases.OrderBy(a => a))
            {
                _logger.Information($"  @{alias}");
            }
        }
        else
        {
            _logger.Information("No aliases configured.");
        }
        
        Console.WriteLine();
        _logger.Information("1. Add alias");
        _logger.Information("2. Remove alias");
        _logger.Information("0. Back");
        
        Console.WriteLine();
        Console.Write("Select option: ");
        
        var choice = Console.ReadLine()?.Trim();
        
        switch (choice)
        {
            case "1":
                Console.Write("Enter new alias (without @): ");
                var newAlias = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(newAlias))
                {
                    try
                    {
                        await _secureConfig.AddAliasAsync(model.Name, newAlias);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
                        await Task.Delay(1500);
                    }
                }
                break;
                
            case "2":
                if (model.Aliases == null || model.Aliases.Count == 0)
                {
                    _logger.Warning("No aliases to remove.");
                    await Task.Delay(1500);
                    break;
                }
                
                Console.Write("Enter alias to remove (without @): ");
                var aliasToRemove = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(aliasToRemove))
                {
                    try
                    {
                        await _secureConfig.RemoveAliasAsync(model.Name, aliasToRemove);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
                        await Task.Delay(1500);
                    }
                }
                break;
        }
    }
    
    private async Task ChangeMaxTokens(ModelConfiguration model)
    {
        Console.Clear();
        _logger.Information($"═══ Change Max Output Tokens: {model.Name} ═══");
        Console.WriteLine();
        
        _logger.Information($"Current max output tokens: {model.MaxOutputTokens}");
        Console.WriteLine();
        
        Console.Write("Enter new max output tokens (or press Enter to cancel): ");
        var input = Console.ReadLine();
        
        if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int newTokens) && newTokens > 0)
        {
            model.MaxOutputTokens = newTokens;
            await _secureConfig.UpdateModelAsync(model);
            _logger.Success($"{Constants.UI.CheckMark} Max output tokens updated to {newTokens}");
        }
        
        await Task.Delay(1500);
    }
    
    private async Task UpdateNote(ModelConfiguration model)
    {
        Console.Clear();
        _logger.Information($"═══ Update Note: {model.Name} ═══");
        Console.WriteLine();
        
        if (!string.IsNullOrWhiteSpace(model.Note))
        {
            _logger.Information($"Current note: {model.Note}");
        }
        else
        {
            _logger.Information("No note configured.");
        }
        
        Console.WriteLine();
        Console.Write("Enter new note (or press Enter to clear): ");
        var newNote = Console.ReadLine()?.Trim();
        
        model.Note = string.IsNullOrEmpty(newNote) ? null : newNote;
        await _secureConfig.UpdateModelAsync(model);
        
        _logger.Success($"{Constants.UI.CheckMark} Note updated");
        await Task.Delay(1500);
    }
    
    private async Task DeleteModel()
    {
        Console.Clear();
        _logger.Information("═══ Delete Model ═══");
        Console.WriteLine();
        
        var settings = await _secureConfig.LoadSettingsAsync();
        
        if (!settings.Models.Any())
        {
            _logger.Warning("No models configured.");
            await Task.Delay(1500);
            return;
        }
        
        // Display numbered list of models
        for (int i = 0; i < settings.Models.Count; i++)
        {
            var model = settings.Models[i];
            var defaultMarker = model.Id == settings.DefaultModelId ? " (default)" : "";
            _logger.Information($"{i + 1}. {model.Name}{defaultMarker}");
        }
        
        Console.WriteLine();
        Console.Write("Select model number to delete (0 to cancel): ");
        
        if (int.TryParse(Console.ReadLine(), out int selection) && 
            selection > 0 && selection <= settings.Models.Count)
        {
            var selectedModel = settings.Models[selection - 1];
            
            Console.Write($"Are you sure you want to delete '{selectedModel.Name}'? (y/N): ");
            var confirm = Console.ReadLine()?.Trim().ToLower();
            
            if (confirm == "y" || confirm == "yes")
            {
                try
                {
                    await _secureConfig.DeleteModelAsync(selectedModel.Name);
                    _logger.Success($"{Constants.UI.CheckMark} Model '{selectedModel.Name}' deleted successfully");
                }
                catch (Exception ex)
                {
                    _logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
                }
            }
            else
            {
                _logger.Information("Deletion cancelled.");
            }
        }
        
        await Task.Delay(1500);
    }
    
    private async Task TestModels()
    {
        Console.Clear();
        _logger.Information("═══ Test Models ═══");
        Console.WriteLine();
        
        var settings = await _secureConfig.LoadSettingsAsync();
        
        if (!settings.Models.Any())
        {
            _logger.Error("No models configured. Use 'Add new model' to configure one.");
            await Task.Delay(1500);
            return;
        }
        
        _logger.Information("1. Test all models");
        _logger.Information("2. Test specific model");
        _logger.Information("0. Back");
        
        Console.WriteLine();
        Console.Write("Select option: ");
        
        var choice = Console.ReadLine()?.Trim();
        
        switch (choice)
        {
            case "1":
                await TestAllModels(settings);
                break;
            case "2":
                await TestSpecificModel(settings);
                break;
            case "0":
            case "":
            case null:
                return;
            default:
                _logger.Warning("Invalid choice. Please try again.");
                await Task.Delay(1500);
                break;
        }
    }
    
    private async Task TestAllModels(GitGenSettings settings)
    {
        Console.Clear();
        _logger.Information("═══ Test All Models ═══");
        Console.WriteLine();
        
        _logger.Information($"🔍 Testing {settings.Models.Count} configured model{(settings.Models.Count > 1 ? "s" : "")}...");
        Console.WriteLine();
        
        var allSuccess = true;
        var successCount = 0;
        
        foreach (var model in settings.Models.OrderBy(m => m.Name))
        {
            var success = await TestSingleModel(model, indent: "  ");
            if (success)
                successCount++;
            else
                allSuccess = false;
                
            Console.WriteLine();
        }
        
        // Summary
        _logger.Information("═════════════════════════════════════════════════════════════");
        
        if (allSuccess)
        {
            _logger.Success($"✅ All {settings.Models.Count} models passed health check!");
        }
        else
        {
            _logger.Warning($"⚠️  {successCount} of {settings.Models.Count} models passed health check");
        }
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ConfigureAppSettings()
    {
        while (true)
        {
            Console.Clear();
            await DisplayAppSettingsMenu();
            
            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1":
                    await ToggleShowTokenUsage();
                    break;
                case "2":
                    await ToggleCopyToClipboard();
                    break;
                case "3":
                    await TogglePartialAliasMatching();
                    break;
                case "4":
                    await SetMinimumAliasMatchLength();
                    break;
                case "0":
                case "":
                case null:
                    return;
                default:
                    _logger.Warning("Invalid choice. Please try again.");
                    await Task.Delay(1500);
                    break;
            }
        }
    }
    
    private async Task DisplayAppSettingsMenu()
    {
        var settings = await _secureConfig.LoadSettingsAsync();
        var appSettings = settings.Settings;
        
        _logger.Information("═══ App Settings ═══");
        Console.WriteLine();
        
        var tokenUsageStatus = appSettings.ShowTokenUsage ? "ON" : "OFF";
        var clipboardStatus = appSettings.CopyToClipboard ? "ON" : "OFF";
        var partialMatchStatus = appSettings.EnablePartialAliasMatching ? "ON" : "OFF";
        
        _logger.Information($"1. Show token usage: {tokenUsageStatus}");
        _logger.Information($"2. Copy to clipboard: {clipboardStatus}");
        _logger.Information($"3. Enable partial alias matching: {partialMatchStatus}");
        _logger.Information($"4. Minimum alias match length: {appSettings.MinimumAliasMatchLength} chars");
        _logger.Information("0. Back to main menu");
        
        Console.WriteLine();
        Console.Write("Select option: ");
    }
    
    private async Task ToggleShowTokenUsage()
    {
        var settings = await _secureConfig.LoadSettingsAsync();
        settings.Settings.ShowTokenUsage = !settings.Settings.ShowTokenUsage;
        await _secureConfig.SaveSettingsAsync(settings);
        
        var status = settings.Settings.ShowTokenUsage ? "enabled" : "disabled";
        _logger.Success($"{Constants.UI.CheckMark} Token usage display {status}");
        await Task.Delay(1500);
    }
    
    private async Task ToggleCopyToClipboard()
    {
        var settings = await _secureConfig.LoadSettingsAsync();
        settings.Settings.CopyToClipboard = !settings.Settings.CopyToClipboard;
        await _secureConfig.SaveSettingsAsync(settings);
        
        var status = settings.Settings.CopyToClipboard ? "enabled" : "disabled";
        _logger.Success($"{Constants.UI.CheckMark} Copy to clipboard {status}");
        await Task.Delay(1500);
    }
    
    private async Task TogglePartialAliasMatching()
    {
        var settings = await _secureConfig.LoadSettingsAsync();
        settings.Settings.EnablePartialAliasMatching = !settings.Settings.EnablePartialAliasMatching;
        await _secureConfig.SaveSettingsAsync(settings);
        
        var status = settings.Settings.EnablePartialAliasMatching ? "enabled" : "disabled";
        _logger.Success($"{Constants.UI.CheckMark} Partial alias matching {status}");
        await Task.Delay(1500);
    }
    
    private async Task SetMinimumAliasMatchLength()
    {
        var settings = await _secureConfig.LoadSettingsAsync();
        
        Console.Clear();
        _logger.Information("═══ Set Minimum Alias Match Length ═══");
        Console.WriteLine();
        
        _logger.Information($"Current minimum length: {settings.Settings.MinimumAliasMatchLength} characters");
        Console.WriteLine();
        
        Console.Write("Enter new minimum length (2-10): ");
        if (int.TryParse(Console.ReadLine(), out int newLength) && newLength >= 2 && newLength <= 10)
        {
            settings.Settings.MinimumAliasMatchLength = newLength;
            await _secureConfig.SaveSettingsAsync(settings);
            
            _logger.Success($"{Constants.UI.CheckMark} Minimum alias match length set to {newLength} characters");
        }
        else
        {
            _logger.Warning("Invalid input. Length must be between 2 and 10.");
        }
        
        await Task.Delay(1500);
    }
    
    private async Task ResetConfiguration()
    {
        Console.Clear();
        _logger.Information("═══ Reset Configuration ═══");
        Console.WriteLine();
        
        _logger.Warning("⚠️  This will delete all models and reset all settings!");
        Console.WriteLine();
        
        Console.Write("Are you sure you want to reset all configuration? (y/N): ");
        var confirm = Console.ReadLine()?.Trim().ToLower();
        
        if (confirm == "y" || confirm == "yes")
        {
            // Clear secure storage
            var settings = await _secureConfig.LoadSettingsAsync();
            settings.Models.Clear();
            settings.DefaultModelId = null;
            await _secureConfig.SaveSettingsAsync(settings);
            
            _logger.Success($"{Constants.UI.CheckMark} All configuration has been reset");
        }
        else
        {
            _logger.Information("Reset cancelled.");
        }
        
        await Task.Delay(1500);
    }
    
    private async Task TestSpecificModel(GitGenSettings settings)
    {
        Console.Clear();
        _logger.Information("═══ Test Specific Model ═══");
        Console.WriteLine();
        
        // Display numbered list of models
        for (int i = 0; i < settings.Models.Count; i++)
        {
            var model = settings.Models[i];
            var defaultMarker = model.Id == settings.DefaultModelId ? " (default)" : "";
            _logger.Information($"{i + 1}. {model.Name}{defaultMarker}");
        }
        
        Console.WriteLine();
        Console.Write("Select model number to test (0 to cancel): ");
        
        if (int.TryParse(Console.ReadLine(), out int selection) && 
            selection > 0 && selection <= settings.Models.Count)
        {
            var selectedModel = settings.Models[selection - 1];
            Console.WriteLine();
            
            var success = await TestSingleModel(selectedModel);
            
            Console.WriteLine();
            if (success)
            {
                _logger.Success($"{Constants.UI.CheckMark} Model test passed!");
            }
            else
            {
                _logger.Error($"{Constants.UI.CrossMark} Model test failed!");
            }
        }
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task TestSingleModelFromMenu(ModelConfiguration model)
    {
        Console.Clear();
        _logger.Information($"═══ Test Model: {model.Name} ═══");
        Console.WriteLine();
        
        var success = await TestSingleModel(model);
        
        Console.WriteLine();
        if (success)
        {
            _logger.Success($"{Constants.UI.CheckMark} Model test passed!");
        }
        else
        {
            _logger.Error($"{Constants.UI.CrossMark} Model test failed!");
        }
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task<bool> TestSingleModel(ModelConfiguration model, string indent = "")
    {
        var defaultMarker = model.Id == (await _secureConfig.LoadSettingsAsync()).DefaultModelId ? " ⭐" : "";
        _logger.Information($"{indent}Testing: {model.Name}{defaultMarker}");
        _logger.Muted($"{indent}  Type: {model.Type} | Provider: {model.Provider} | Model: {model.ModelId}");
        _logger.Muted($"{indent}  URL: {model.Url}");
        _logger.Muted($"{indent}  Temperature: {model.Temperature} | Max Output Tokens: {model.MaxOutputTokens:N0}");
        
        try
        {
            // Test the connection
            try
            {
                var provider = _providerFactory.CreateProvider(model);
                _logger.Information($"{indent}  {Constants.UI.TestTubeSymbol} Testing connection...");
                
                var result = await provider.GenerateAsync(Constants.Api.TestLlmPrompt);
                _logger.Success($"{indent}  {Constants.UI.CheckMark} LLM Response: \"{result.Message}\"");
                return true;
            }
            catch (Exception testEx)
            {
                _logger.Error($"{indent}  {Constants.UI.CrossMark} Test failed: {testEx.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"{indent}  {Constants.UI.CrossMark} Error: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    ///     Checks if a model appears to be configured for public/free use based on its aliases and description.
    /// </summary>
    private bool AppearsToBePublicModel(ModelConfiguration model)
    {
        // Keywords that suggest public/free usage
        string[] publicKeywords = { "free", "public", "open" };
        
        // Check aliases
        if (model.Aliases != null)
        {
            foreach (var alias in model.Aliases)
            {
                if (publicKeywords.Any(keyword => alias.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        
        // Check note/description
        if (!string.IsNullOrWhiteSpace(model.Note))
        {
            if (publicKeywords.Any(keyword => model.Note.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        
        // Check if it's a known free provider endpoint
        if (!string.IsNullOrWhiteSpace(model.Url))
        {
            // Check for common free model endpoints
            if (model.Url.Contains("openrouter", StringComparison.OrdinalIgnoreCase) && 
                model.ModelId != null && model.ModelId.Contains(":free", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        // Check if pricing indicates it's free
        if (model.Pricing != null && 
            model.Pricing.InputPer1M == 0 && 
            model.Pricing.OutputPer1M == 0)
            return true;
        
        return false;
    }
}