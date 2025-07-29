using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Helpers;
using GitGen.Models;
using GitGen.Providers;
using GitGen.Providers.OpenAI;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }

            switch (key.KeyChar)
            {
                case '1':
                    await AddNewModel();
                    break;
                case '2':
                    await ManageModels();
                    break;
                case '3':
                    await TestModels();
                    break;
                case '4':
                    await ConfigureAppSettings();
                    break;
                case '5':
                    await ResetConfiguration();
                    break;
                case '9': // Hidden debug option
                    await DiagnosticDump();
                    break;
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

        Console.WriteLine();
        _logger.Muted("Press ESC to exit...");
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

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }

            switch (key.KeyChar)
            {
                case '1':
                    await ListModels();
                    break;
                case '2':
                    await SetDefaultModel();
                    break;
                case '3':
                    await EditModel();
                    break;
                case '4':
                    await DeleteModel();
                    break;
                case '5':
                    await AddNewModel();
                    break;
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
        _logger.Information("5. Add new model");

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");
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

                var trimmedNote = model.Note?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedNote))
                    _logger.Muted($"    Note: {trimmedNote}");

                if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
                {
                    var truncatedPrompt = model.SystemPrompt.Length > 50
                        ? model.SystemPrompt.Substring(0, 50) + "..."
                        : model.SystemPrompt;
                    _logger.Muted($"    System Prompt: {truncatedPrompt}");
                }

                if (model.Aliases != null && model.Aliases.Count > 0)
                {
                    var aliasesStr = string.Join(", ", model.Aliases.OrderBy(a => a).Select(a => $"@{a}"));
                    _logger.Muted($"    Aliases: {aliasesStr}");
                }

                var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
                _logger.Muted($"    Pricing: {pricingInfo}");

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
            DisplayModelListItem(i, settings.Models[i], settings.DefaultModelId);
        }

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");
        Console.WriteLine();
        Console.Write("Select model number: ");

        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
        {
            return;
        }

        if (char.IsDigit(key.KeyChar))
        {
            Console.WriteLine(key.KeyChar);
            int selection = key.KeyChar - '0';
            if (selection > 0 && selection <= settings.Models.Count)
            {
                var selectedModel = settings.Models[selection - 1];

                // Show confirmation prompt
                Console.WriteLine();
                Console.Write($"Set '{selectedModel.Name}' as default model? (Y/n): ");
                var confirmKey = Console.ReadKey(true);

                // Accept Y, y, or Enter as confirmation
                if (confirmKey.Key == ConsoleKey.Enter ||
                    confirmKey.KeyChar == 'Y' ||
                    confirmKey.KeyChar == 'y')
                {
                    Console.WriteLine("Yes");

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
                else
                {
                    Console.WriteLine("No");
                    _logger.Information("Default model change cancelled.");
                }
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
            DisplayModelListItem(i, settings.Models[i], settings.DefaultModelId);
        }

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");
        Console.WriteLine();
        Console.Write("Select model number to edit: ");

        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
        {
            return;
        }

        if (char.IsDigit(key.KeyChar))
        {
            Console.WriteLine(key.KeyChar);
            int selection = key.KeyChar - '0';
            if (selection > 0 && selection <= settings.Models.Count)
            {
                var selectedModel = settings.Models[selection - 1];
                await EditModelMenu(selectedModel);
            }
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

            var trimmedNote = model.Note?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedNote))
                _logger.Muted($"    Note: {trimmedNote}");

            if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
            {
                var truncatedPrompt = model.SystemPrompt.Length > 50
                    ? model.SystemPrompt.Substring(0, 50) + "..."
                    : model.SystemPrompt;
                _logger.Muted($"    System Prompt: {truncatedPrompt}");
            }

            if (model.Aliases != null && model.Aliases.Count > 0)
            {
                var aliasesStr = string.Join(", ", model.Aliases.OrderBy(a => a).Select(a => $"@{a}"));
                _logger.Muted($"    Aliases: {aliasesStr}");
            }

            var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
            _logger.Muted($"    Pricing: {pricingInfo}");

            var lastUsed = DateTimeHelper.ToLocalDateTimeString(model.LastUsed);
            _logger.Muted($"    Last used: {lastUsed}");

            Console.WriteLine();

            _logger.Information("1. Manage aliases");
            _logger.Information("2. Change max output tokens");
            _logger.Information("3. Update note/description");
            _logger.Information("4. Edit pricing");
            _logger.Information("5. Edit system prompt");
            _logger.Information("6. Test this model");

            Console.WriteLine();
            _logger.Muted("Press ESC to go back...");

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }

            switch (key.KeyChar)
            {
                case '1':
                    await ManageAliases(model);
                    break;
                case '2':
                    await ChangeMaxTokens(model);
                    break;
                case '3':
                    await UpdateNote(model);
                    break;
                case '4':
                    await EditPricing(model);
                    break;
                case '5':
                    await UpdateSystemPrompt(model);
                    break;
                case '6':
                    await TestSingleModelFromMenu(model);
                    break;
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

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");

        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
        {
            return;
        }

        switch (key.KeyChar)
        {
            case '1':
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

            case '2':
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
                    // Check if the alias exists
                    if (!model.Aliases.Contains(aliasToRemove))
                    {
                        _logger.Warning($"Alias '@{aliasToRemove}' not found.");
                        await Task.Delay(1500);
                        break;
                    }

                    // Confirmation prompt
                    Console.Write($"Remove alias '@{aliasToRemove}' from '{model.Name}'? (Y/n): ");
                    var confirmKey = Console.ReadKey(true);

                    if (confirmKey.Key == ConsoleKey.Enter ||
                        confirmKey.KeyChar == 'Y' ||
                        confirmKey.KeyChar == 'y')
                    {
                        Console.WriteLine("Yes");
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
                    else
                    {
                        Console.WriteLine("No");
                        _logger.Information("Alias removal cancelled.");
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
        while (true)
        {
            Console.Clear();
            _logger.Information($"═══ Update Note: {model.Name} ═══");
            Console.WriteLine();

            var noteTrimmed = model.Note?.Trim();
            if (!string.IsNullOrWhiteSpace(noteTrimmed))
            {
                _logger.Information("Current note:");
                _logger.Information($"  {noteTrimmed}");
            }
            else
            {
                _logger.Information("No note configured.");
            }

            Console.WriteLine();
            _logger.Information("1. Update note");
            if (!string.IsNullOrWhiteSpace(model.Note))
            {
                _logger.Information("2. Clear note");
            }

            Console.WriteLine();
            _logger.Muted("Press ESC to go back...");

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }

            switch (key.KeyChar)
            {
                case '1':
                    Console.Write("Enter new note: ");
                    var newNote = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(newNote))
                    {
                        model.Note = newNote;
                        await _secureConfig.UpdateModelAsync(model);
                        _logger.Success($"{Constants.UI.CheckMark} Note updated");
                        await Task.Delay(1500);
                    }
                    break;

                case '2':
                    if (!string.IsNullOrWhiteSpace(model.Note))
                    {
                        Console.Write("Clear the existing note? (Y/n): ");
                        var confirmKey = Console.ReadKey(true);

                        if (confirmKey.Key == ConsoleKey.Enter ||
                            confirmKey.KeyChar == 'Y' ||
                            confirmKey.KeyChar == 'y')
                        {
                            Console.WriteLine("Yes");
                            model.Note = null;
                            await _secureConfig.UpdateModelAsync(model);
                            _logger.Success($"{Constants.UI.CheckMark} Note cleared");
                        }
                        else
                        {
                            Console.WriteLine("No");
                            _logger.Information("Note clearing cancelled.");
                        }
                        await Task.Delay(1500);
                    }
                    break;

                default:
                    _logger.Warning("Invalid choice. Please try again.");
                    await Task.Delay(1500);
                    break;
            }
        }
    }

    private async Task UpdateSystemPrompt(ModelConfiguration model)
    {
        while (true)
        {
            Console.Clear();
            _logger.Information($"═══ Update System Prompt: {model.Name} ═══");
            Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
            {
                _logger.Information("Current system prompt:");
                _logger.Information($"  {model.SystemPrompt}");
            }
            else
            {
                _logger.Information("No custom system prompt configured (using default).");
            }

            Console.WriteLine();
            _logger.Muted("This will be appended to GitGen's base instructions for this model.");
            _logger.Muted("Example: 'Always use conventional commit format' or 'Must start with a Haiku'");
            Console.WriteLine();

            _logger.Information("1. Update system prompt");
            if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
            {
                _logger.Information("2. Clear system prompt (use default)");
            }

            Console.WriteLine();
            _logger.Muted("Press ESC to go back...");

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }

            switch (key.KeyChar)
            {
                case '1':
                    Console.Write("Enter new system prompt: ");
                    var newPrompt = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(newPrompt))
                    {
                        model.SystemPrompt = newPrompt;
                        await _secureConfig.UpdateModelAsync(model);
                        _logger.Success($"{Constants.UI.CheckMark} System prompt updated");
                        await Task.Delay(1500);
                    }
                    break;

                case '2':
                    if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
                    {
                        Console.Write("Clear the existing system prompt? (Y/n): ");
                        var confirmKey = Console.ReadKey(true);

                        if (confirmKey.Key == ConsoleKey.Enter ||
                            confirmKey.KeyChar == 'Y' ||
                            confirmKey.KeyChar == 'y')
                        {
                            Console.WriteLine("Yes");
                            model.SystemPrompt = null;
                            await _secureConfig.UpdateModelAsync(model);
                            _logger.Success($"{Constants.UI.CheckMark} System prompt cleared (will use default)");
                        }
                        else
                        {
                            Console.WriteLine("No");
                            _logger.Information("System prompt clearing cancelled.");
                        }
                        await Task.Delay(1500);
                    }
                    break;

                default:
                    _logger.Warning("Invalid choice. Please try again.");
                    await Task.Delay(1500);
                    break;
            }
        }
    }

    private async Task EditPricing(ModelConfiguration model)
    {
        Console.Clear();
        _logger.Information($"═══ Edit Pricing: {model.Name} ═══");
        Console.WriteLine();

        // Display current pricing
        var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
        _logger.Information($"Current pricing: {pricingInfo}");
        _logger.Information($"Currency: {model.Pricing.CurrencyCode} ({CostCalculationService.GetCurrencySymbol(model.Pricing.CurrencyCode)})");
        Console.WriteLine();

        _logger.Information("1. Change currency");
        _logger.Information("2. Update input cost per million tokens");
        _logger.Information("3. Update output cost per million tokens");
        _logger.Information("4. Update all pricing values");

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");

        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
        {
            return;
        }

        switch (key.KeyChar)
        {
            case '1':
                await ChangeCurrency(model);
                break;
            case '2':
                await ChangeInputCost(model);
                break;
            case '3':
                await ChangeOutputCost(model);
                break;
            case '4':
                await ChangeAllPricing(model);
                break;
            default:
                _logger.Warning("Invalid choice. Please try again.");
                await Task.Delay(1500);
                break;
        }
    }

    private async Task ChangeCurrency(ModelConfiguration model)
    {
        Console.WriteLine();
        _logger.Information("Select currency:");
        _logger.Information("  1. USD ($)");
        _logger.Information("  2. EUR (€)");
        _logger.Information("  3. GBP (£)");
        _logger.Information("  4. AUD (A$)");
        _logger.Information("  5. Other");

        Console.Write("Enter your choice: ");
        var currencyChoice = Console.ReadLine()?.Trim();

        switch (currencyChoice)
        {
            case "1": model.Pricing.CurrencyCode = "USD"; break;
            case "2": model.Pricing.CurrencyCode = "EUR"; break;
            case "3": model.Pricing.CurrencyCode = "GBP"; break;
            case "4": model.Pricing.CurrencyCode = "AUD"; break;
            case "5":
                Console.Write("Enter currency code (e.g., JPY): ");
                var customCurrency = Console.ReadLine()?.Trim().ToUpper();
                if (!string.IsNullOrWhiteSpace(customCurrency) && customCurrency.Length == 3 && customCurrency.All(char.IsLetter))
                {
                    model.Pricing.CurrencyCode = customCurrency;
                }
                else
                {
                    _logger.Warning("Invalid currency code. Must be a 3-letter code.");
                    await Task.Delay(1500);
                    return;
                }
                break;
            default:
                return;
        }

        model.Pricing.UpdatedAt = DateTime.UtcNow;
        await _secureConfig.UpdateModelAsync(model);
        _logger.Success($"{Constants.UI.CheckMark} Currency updated to {model.Pricing.CurrencyCode}");
        await Task.Delay(1500);
    }

    private async Task ChangeInputCost(ModelConfiguration model)
    {
        Console.WriteLine();
        Console.Write($"Enter new input cost per million tokens (current: {model.Pricing.InputPer1M}): ");
        var input = Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(input) && decimal.TryParse(input, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out decimal newCost) && newCost >= 0)
        {
            model.Pricing.InputPer1M = newCost;
            model.Pricing.UpdatedAt = DateTime.UtcNow;
            await _secureConfig.UpdateModelAsync(model);
            _logger.Success($"{Constants.UI.CheckMark} Input cost updated to {newCost}");
        }
        else
        {
            _logger.Warning("Invalid input cost. Must be a non-negative number.");
        }

        await Task.Delay(1500);
    }

    private async Task ChangeOutputCost(ModelConfiguration model)
    {
        Console.WriteLine();
        Console.Write($"Enter new output cost per million tokens (current: {model.Pricing.OutputPer1M}): ");
        var input = Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(input) && decimal.TryParse(input, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out decimal newCost) && newCost >= 0)
        {
            model.Pricing.OutputPer1M = newCost;
            model.Pricing.UpdatedAt = DateTime.UtcNow;
            await _secureConfig.UpdateModelAsync(model);
            _logger.Success($"{Constants.UI.CheckMark} Output cost updated to {newCost}");
        }
        else
        {
            _logger.Warning("Invalid output cost. Must be a non-negative number.");
        }

        await Task.Delay(1500);
    }

    private async Task ChangeAllPricing(ModelConfiguration model)
    {
        Console.WriteLine();

        // Input cost
        Console.Write("Enter input cost per million tokens: ");
        var inputCostStr = Console.ReadLine();
        if (!decimal.TryParse(inputCostStr, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out decimal inputCost) || inputCost < 0)
        {
            _logger.Warning("Invalid input cost. Must be a non-negative number.");
            await Task.Delay(1500);
            return;
        }

        // Output cost
        Console.Write("Enter output cost per million tokens: ");
        var outputCostStr = Console.ReadLine();
        if (!decimal.TryParse(outputCostStr, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out decimal outputCost) || outputCost < 0)
        {
            _logger.Warning("Invalid output cost. Must be a non-negative number.");
            await Task.Delay(1500);
            return;
        }

        model.Pricing.InputPer1M = inputCost;
        model.Pricing.OutputPer1M = outputCost;
        model.Pricing.UpdatedAt = DateTime.UtcNow;
        await _secureConfig.UpdateModelAsync(model);

        _logger.Success($"{Constants.UI.CheckMark} Pricing updated");
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
            DisplayModelListItem(i, settings.Models[i], settings.DefaultModelId);
        }

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");
        Console.WriteLine();
        Console.Write("Select model number to delete: ");

        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
        {
            return;
        }

        if (char.IsDigit(key.KeyChar))
        {
            Console.WriteLine(key.KeyChar);
            int selection = key.KeyChar - '0';
            if (selection > 0 && selection <= settings.Models.Count)
            {
                var selectedModel = settings.Models[selection - 1];

                Console.Write($"Are you sure you want to delete '{selectedModel.Name}'? (y/N): ");
                var confirmKey = Console.ReadKey(true);

                // Only accept 'y' or 'Y' for deletion (more cautious for destructive action)
                if (confirmKey.KeyChar == 'y' || confirmKey.KeyChar == 'Y')
                {
                    Console.WriteLine("Yes");
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
                    Console.WriteLine("No");
                    _logger.Information("Deletion cancelled.");
                }
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

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");

        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
        {
            return;
        }

        switch (key.KeyChar)
        {
            case '1':
                await TestAllModels(settings);
                break;
            case '2':
                await TestSpecificModel(settings);
                break;
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

        // If 3 or more models, ask for confirmation
        if (settings.Models.Count >= 3)
        {
            Console.Write($"Test all {settings.Models.Count} models? This may take some time. (Y/n): ");
            var confirmKey = Console.ReadKey(true);

            if (confirmKey.Key != ConsoleKey.Enter &&
                confirmKey.KeyChar != 'Y' &&
                confirmKey.KeyChar != 'y')
            {
                Console.WriteLine("No");
                _logger.Information("Test cancelled.");
                await Task.Delay(1500);
                return;
            }
            Console.WriteLine("Yes");
            Console.WriteLine();
        }

        _logger.Information($"🔍 Testing {settings.Models.Count} configured model{(settings.Models.Count > 1 ? "s" : "")}...");
        Console.WriteLine();

        var allSuccess = true;
        var successCount = 0;
        var failedModels = new List<string>();

        foreach (var model in settings.Models.OrderBy(m => m.Name))
        {
            var success = await TestSingleModel(model, indent: "  ");
            if (success)
            {
                successCount++;
            }
            else
            {
                allSuccess = false;
                failedModels.Add(model.Name);
            }

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

            if (failedModels.Any())
            {
                _logger.Error("Failed models:");
                foreach (var failedModel in failedModels)
                {
                    _logger.Error($"  • {failedModel}");
                }
            }

            _logger.Information("");
            _logger.Information("💡 Tip: Failed models may be rate limited or have configuration issues.");
            _logger.Information("   The test automatically retries up to 3 times for transient errors.");
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

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }

            switch (key.KeyChar)
            {
                case '1':
                    await ToggleShowTokenUsage();
                    break;
                case '2':
                    await ToggleCopyToClipboard();
                    break;
                case '3':
                    await TogglePartialAliasMatching();
                    break;
                case '4':
                    await SetMinimumAliasMatchLength();
                    break;
                case '5':
                    await TogglePromptConfirmation();
                    break;
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

        DisplaySettingWithStatus("1", "Show token usage", appSettings.ShowTokenUsage);
        DisplaySettingWithStatus("2", "Copy to clipboard", appSettings.CopyToClipboard);
        DisplaySettingWithStatus("3", "Enable partial alias matching", appSettings.EnablePartialAliasMatching);
        DisplaySettingWithValue("4", "Minimum alias match length", $"{appSettings.MinimumAliasMatchLength} chars");
        DisplaySettingWithStatus("5", "Require prompt confirmation", appSettings.RequirePromptConfirmation);

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");
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

    private async Task TogglePromptConfirmation()
    {
        var settings = await _secureConfig.LoadSettingsAsync();
        settings.Settings.RequirePromptConfirmation = !settings.Settings.RequirePromptConfirmation;
        await _secureConfig.SaveSettingsAsync(settings);

        var status = settings.Settings.RequirePromptConfirmation ? "enabled" : "disabled";
        _logger.Success($"{Constants.UI.CheckMark} Prompt confirmation {status}");
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
        var confirmKey = Console.ReadKey(true);

        // Only accept 'y' or 'Y' for this destructive action
        if (confirmKey.KeyChar == 'y' || confirmKey.KeyChar == 'Y')
        {
            Console.WriteLine("Yes");
            // Clear secure storage
            var settings = await _secureConfig.LoadSettingsAsync();
            settings.Models.Clear();
            settings.DefaultModelId = null;
            await _secureConfig.SaveSettingsAsync(settings);

            _logger.Success($"{Constants.UI.CheckMark} All configuration has been reset");
        }
        else
        {
            Console.WriteLine("No");
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
            DisplayModelListItem(i, settings.Models[i], settings.DefaultModelId);
        }

        Console.WriteLine();
        _logger.Muted("Press ESC to go back...");
        Console.WriteLine();
        Console.Write("Select model number to test: ");

        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
        {
            return;
        }

        if (char.IsDigit(key.KeyChar))
        {
            Console.WriteLine(key.KeyChar);
            int selection = key.KeyChar - '0';
            if (selection > 0 && selection <= settings.Models.Count)
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

                Console.WriteLine();
                _logger.Information("Press any key to continue...");
                Console.ReadKey(true);
            }
        }
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
            _logger.Information("");
            _logger.Information("💡 The test automatically retried up to 3 times.");
            _logger.Information("   If this is a free model, rate limiting is common.");
            _logger.Information("   Consider waiting a moment before trying again.");
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

        if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
        {
            var truncatedPrompt = model.SystemPrompt.Length > 50
                ? model.SystemPrompt.Substring(0, 50) + "..."
                : model.SystemPrompt;
            _logger.Muted($"{indent}  System Prompt: {truncatedPrompt}");
        }

        const int maxAttempts = 3;
        int attempt = 0;

        while (attempt < maxAttempts)
        {
            attempt++;

            try
            {
                // Test the connection
                var provider = CreateProviderWithIndent(model, indent);
                _logger.Information($"{indent}  {Constants.UI.TestTubeSymbol} Testing connection...");

                var result = await provider.GenerateAsync(Constants.Api.TestLlmPrompt);
                _logger.Success($"{indent}  {Constants.UI.CheckMark} LLM Response: \"{result.Message}\"");
                return true;
            }
            catch (HttpResponseException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.Warning($"{indent}  ⚠️  Rate limited. This is common with free models.");

                if (attempt < maxAttempts)
                {
                    _logger.Information($"{indent}  Waiting before retry attempt {attempt + 1}/{maxAttempts}...");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }

                _logger.Error($"{indent}  {Constants.UI.CrossMark} Test failed: Rate limited after {maxAttempts} attempts");
                return false;
            }
            catch (Exception testEx)
            {
                _logger.Error($"{indent}  {Constants.UI.CrossMark} Test failed: {testEx.Message}");

                // Only retry for transient errors
                if (attempt < maxAttempts && IsTransientError(testEx))
                {
                    _logger.Information($"{indent}  Retrying ({attempt}/{maxAttempts})...");
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    continue;
                }

                return false;
            }
        }

        return false;
    }

    private bool IsTransientError(Exception ex)
    {
        // Check if it's a transient error that might succeed on retry
        if (ex is HttpRequestException || ex is TaskCanceledException)
            return true;

        if (ex is HttpResponseException httpEx)
        {
            // Retry on server errors and timeouts
            var statusCode = (int)httpEx.StatusCode;
            return statusCode >= 500 || statusCode == 408 || statusCode == 429;
        }

        return false;
    }

    private ICommitMessageProvider CreateProviderWithIndent(ModelConfiguration model, string indent)
    {
        // Create a custom LlmCallTracker with the indent
        var indentedCallTracker = new LlmCallTrackerWithIndent(_logger, indent);

        // Create the provider with the custom tracker
        var type = model.Type?.ToLowerInvariant();
        return type switch
        {
            "openai" => new OpenAIProvider(
                new HttpClientService(_logger),
                _logger,
                model,
                indentedCallTracker),
            "openai-compatible" => new OpenAIProvider(
                new HttpClientService(_logger),
                _logger,
                model,
                indentedCallTracker),
            _ => throw new NotSupportedException(
                $"API type '{model.Type}' is not supported. Supported types: openai, openai-compatible")
        };
    }

    /// <summary>
    /// A wrapper for LlmCallTracker that adds indentation to all output.
    /// </summary>
    private class LlmCallTrackerWithIndent : ILlmCallTracker
    {
        private readonly IConsoleLogger _logger;
        private readonly string _indent;

        public LlmCallTrackerWithIndent(IConsoleLogger logger, string indent)
        {
            _logger = logger;
            _indent = indent;
        }

        public async Task<LlmCallResult> TrackCallAsync(
            string operation,
            string prompt,
            ModelConfiguration? model,
            Func<Task<CommitMessageResult>> apiCall,
            string indent = "")
        {
            // Use our configured indent, ignoring the passed indent parameter
            // For configuration testing, we don't need usage tracking
            var tracker = new LlmCallTracker(_logger, new NullUsageTrackingService());
            return await tracker.TrackCallAsync(operation, prompt, model, apiCall, _indent);
        }
    }

    /// <summary>
    ///     Null implementation of IUsageTrackingService for testing purposes.
    /// </summary>
    private class NullUsageTrackingService : IUsageTrackingService
    {
        public Task RecordUsageAsync(UsageEntry entry) => Task.CompletedTask;
        public string GetSessionId() => "test-session";
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
        if (model.Pricing.InputPer1M == 0 &&
            model.Pricing.OutputPer1M == 0)
            return true;

        return false;
    }

    private async Task DiagnosticDump()
    {
        Console.Clear();
        _logger.Information("=== Configuration Diagnostic Dump ===");

        try
        {
            // Try to load settings
            var settings = await _secureConfig.LoadSettingsAsync();

            // Serialize to JSON for inspection
            var json = JsonSerializer.Serialize(settings, ConfigurationJsonContext.Default.GitGenSettings);

            _logger.Information("Current settings JSON:");
            Console.WriteLine(json);

            // Check file system
            var configPath = settings.Settings?.ConfigPath ?? "Unknown";
            _logger.Information($"\nConfiguration path: {configPath}");

            if (File.Exists(configPath))
            {
                var fileInfo = new FileInfo(configPath);
                _logger.Information($"File exists: Yes");
                _logger.Information($"File size: {fileInfo.Length} bytes");
                _logger.Information($"Last modified: {fileInfo.LastWriteTime}");
            }
            else
            {
                _logger.Warning("File exists: No");
            }

            // Try to save a test configuration
            _logger.Information("\nAttempting test save...");

            // Add a timestamp to verify save/load cycle
            if (settings.Settings == null)
                settings.Settings = new AppSettings();

            // Save current state first
            var originalShowToken = settings.Settings.ShowTokenUsage;

            // Toggle a setting to verify it saves
            settings.Settings.ShowTokenUsage = !settings.Settings.ShowTokenUsage;
            await _secureConfig.SaveSettingsAsync(settings);
            _logger.Success("Test save completed");

            // Clear cache by creating new instance (simulate app restart)
            var newService = new SecureConfigurationService(_logger);

            // Try to reload
            _logger.Information("\nAttempting reload with new service instance...");
            var reloaded = await newService.LoadSettingsAsync();
            _logger.Success($"Reload successful");
            _logger.Information($"ShowTokenUsage changed from {originalShowToken} to {reloaded.Settings?.ShowTokenUsage}");

            // Restore original value
            if (reloaded.Settings != null)
            {
                reloaded.Settings.ShowTokenUsage = originalShowToken;
                await newService.SaveSettingsAsync(reloaded);
                _logger.Information("Restored original setting value");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Diagnostic failed: {ex.Message}");
            _logger.Error($"Exception type: {ex.GetType().FullName}");
            _logger.Error($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    /// <summary>
    ///     Displays a model in a list with consistent formatting, showing default status and aliases.
    /// </summary>
    private void DisplayModelListItem(int index, ModelConfiguration model, string? defaultModelId)
    {
        // Write number and model name
        Console.Write($"{index + 1}. {model.Name}");

        // Add star and (default) if this is the default model
        if (model.Id == defaultModelId)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(" ⭐ (default)");
            Console.ResetColor();
        }

        // Add aliases in grey if any exist
        if (model.Aliases != null && model.Aliases.Any())
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" {string.Join(" ", model.Aliases.Select(a => $"@{a}"))}");
            Console.ResetColor();
        }

        Console.WriteLine(); // End the line
    }

    /// <summary>
    ///     Displays a setting item with colored ON/OFF status.
    /// </summary>
    private void DisplaySettingWithStatus(string number, string label, bool isOn)
    {
        Console.Write($"{number}. {label}: ");

        if (isOn)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("ON");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("OFF");
        }

        Console.ResetColor();
    }

    /// <summary>
    ///     Displays a setting item with a custom value (not ON/OFF).
    /// </summary>
    private void DisplaySettingWithValue(string number, string label, string value)
    {
        Console.WriteLine($"{number}. {label}: {value}");
    }
}