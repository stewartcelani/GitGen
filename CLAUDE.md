# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GitGen is a multi-model AI-powered Git commit message generator written in .NET 9.0. It analyzes Git repository changes and uses AI to generate meaningful commit messages. The application supports multiple AI models with secure encrypted storage, cost tracking, and instant model switching via aliases.

## Building and Testing

### Build Commands
```bash
# Build the project
dotnet build src/GitGen/GitGen.csproj

# Build in Release mode
dotnet build src/GitGen/GitGen.csproj -c Release

# Run the application from source
dotnet run --project src/GitGen/GitGen.csproj -- [arguments]
```

### Publishing
The project includes a PowerShell script for publishing cross-platform executables:
```bash
# Publish for all platforms
./publish.ps1

# Publish only for current platform
./publish.ps1 -CurrentOnly

# Publish to custom directory
./publish.ps1 -OutputPath 'C:\temp'
```

### Testing
Currently, there are no unit tests in the project. When adding tests, consider creating a test project under `src/GitGen.Tests/`.

## Architecture and Key Components

### Dependency Injection & Entry Point
The application uses Microsoft.Extensions.DependencyInjection for dependency injection. The main entry point (`Program.cs`) sets up:
- Command-line parsing using System.CommandLine
- Service registration for all major components
- Special handling for @model syntax for quick model switching

### Configuration System
GitGen uses a multi-layered configuration system:
- **SecureConfigurationService**: Handles encrypted storage of API keys using platform-specific data protection APIs
- **ConfigurationService**: Manages loading and validation of configurations
- **ConfigurationMenuService**: Interactive menu system for configuration management
- **ConfigurationWizardService**: Guided setup wizard for new model configurations

Configuration is stored in:
- Windows: `%APPDATA%\GitGen\models.json` (encrypted)
- macOS/Linux: `~/.config/gitgen/models.json` (encrypted)

### AI Provider System
- **ProviderFactory**: Creates appropriate provider instances based on configuration
- **OpenAIProvider**: Handles all OpenAI-compatible APIs (OpenAI, Azure, Anthropic, Google, Groq, local models)
- **OpenAIParameterDetector**: Auto-detects optimal parameters for different providers

### Core Services
- **CommitMessageGenerator**: Orchestrates the commit message generation process
- **GitAnalysisService**: Analyzes Git repository state and generates diffs
- **HttpClientService**: Manages HTTP requests with retry policies using Polly
- **CostCalculationService**: Tracks token usage and calculates costs
- **ValidationService**: Comprehensive validation for all inputs

### Key Dependencies
- **LibGit2Sharp**: Git repository operations
- **System.CommandLine**: Command-line interface
- **Microsoft.AspNetCore.DataProtection**: Secure storage
- **Polly**: Resilient HTTP requests
- **TextCopy**: Clipboard operations

## Development Guidelines

### Adding New Features
When adding features, consider:
1. Following the existing service pattern with interfaces
2. Adding proper validation in ValidationService
3. Supporting the multi-model architecture
4. Ensuring cross-platform compatibility
5. Maintaining backward compatibility with existing configurations

### Model Configuration
Models support:
- Multiple aliases for quick access (@fast, @smart, etc.)
- Custom system prompts per model
- Pricing configuration for cost tracking
- Provider-specific parameters (max tokens, temperature)

### Error Handling
The application uses custom exceptions:
- `AuthenticationException`: API authentication failures
- `HttpResponseException`: HTTP request failures
- Comprehensive error messages guide users to solutions

### Platform-Specific Considerations
- Use `PlatformHelper` for OS detection
- File paths must handle different separators
- Secure storage uses platform-specific APIs
- The publish script handles platform-specific executable generation

## Common Development Tasks

### Adding a New Provider Type
Currently only OpenAI-compatible providers are supported. To add a new provider type:
1. Create a new provider class implementing `ICommitMessageProvider`
2. Update `ProviderFactory` to handle the new type
3. Add validation rules to `ValidationService`
4. Update the configuration wizard to support the new type

### Modifying Configuration Structure
When changing configuration:
1. Update `ModelConfiguration.cs` or `GitGenSettings.cs`
2. Ensure backward compatibility or add migration logic
3. Update `ConfigurationJsonContext` for JSON serialization
4. Test with existing encrypted configurations

### Debugging HTTP Requests
- Enable debug mode with `-d` or `--debug` flag
- Check `ConsoleLogger` output for detailed request/response information
- HTTP client includes automatic retry with exponential backoff

## Important Notes

- Always test cross-platform compatibility when making file system changes
- Maintain the single-file, self-contained publishing capability
- Respect the secure storage - never log or display API keys
- The application is designed to work without external runtime dependencies
- When working with Git operations, use LibGit2Sharp rather than shell commands