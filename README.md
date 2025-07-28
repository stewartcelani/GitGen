# GitGen

![GitHub release (latest by date)](https://img.shields.io/github/v/release/stewartcelani/GitGen)
![GitHub license](https://img.shields.io/github/license/stewartcelani/GitGen)
![.NET](https://img.shields.io/badge/.NET-9.0-blueviolet)

#### AI-Powered Git Commit Message Generator



A multi-model AI commit message generator that seamlessly integrates with your Git workflow. Configure multiple AI providers, switch between models instantly, and generate contextual commit messages that capture the essence of your changes.

## Key Features

- **Multi-Model Architecture** - Configure unlimited AI models from different providers
- **Instant Model Switching** - Use aliases like `@fast`, `@smart`, `@free` to switch models on the fly
- **Secure Configuration** - API keys encrypted using platform-specific data protection
- **Cost Tracking** - Monitor token usage and costs per model with multi-currency support
- **OpenAI-Compatible APIs** - Works with OpenAI, Anthropic, Azure, Google, Groq, local models, and any provider offering OpenAI-compatible endpoints
- **Smart Parameter Detection** - Automatically adapts to provider-specific API variations
- **Zero Dependencies** - Single executable, no runtime requirements
- **Interactive Configuration** - Guided wizard with testing and validation
- **Partial Alias Matching** - Type partial aliases like `@ult` to match `@ultrathink`
- **Per-Model System Prompts** - Customize behavior for each model

## Quick Start

```bash
# Download from releases and add to PATH
# https://github.com/stewartcelani/GitGen/releases

# First-time setup - launches configuration wizard
gitgen config

# Generate a commit message with default model
gitgen

# Use a specific model via alias
gitgen @smart

# Guide the generation with custom instructions
gitgen "focus on security changes"
gitgen @fast "explain the refactoring"
```

### Command Reference

```
$ gitgen help
GitGen - AI-Powered Git Commit Message Generator

Usage:
  gitgen                       Generate commit message with default model
  gitgen [prompt]              Generate with custom prompt
  gitgen @<model>              Generate with specific model or alias
  gitgen [prompt] @<model>     Generate with custom prompt and model
  gitgen @<model> [prompt]     Alternative syntax
  gitgen [command] [options]   Run a specific command

Examples:
  gitgen
  gitgen "must be a haiku"
  gitgen @fast                 # Use your fast model
  gitgen @free                 # Use free model for public repos
  gitgen -p @fast              # Preview model selection and cost
  gitgen "focus on security" @ultrathink
  gitgen @sonnet "explain the refactoring"

💡 Tip: Configure a free model as @free to save money on public repositories
   where sending code to free APIs doesn't matter.

Options:
  -d, --debug            Enable debug logging
  -p, --preview          Preview mode - show what would happen without calling LLM
  -v, --version          Show version information
  -?, -h, --help         Show help and usage information

Commands:
  config                 Run the interactive configuration menu
  help                   Display help information
```

## Installation

### Download Binary

1. Download the appropriate release for your platform from [Releases](https://github.com/stewartcelani/GitGen/releases)
2. Extract the archive to a directory in your PATH
3. Run `gitgen config` to set up your first model

### Build from Source

Requirements: .NET 9.0 SDK

```bash
git clone https://github.com/stewartcelani/GitGen.git
cd GitGen
./publish.ps1  # Creates platform-specific executables in dist/
```

## Complete User Experience Guide

### Main Configuration Menu

Running `gitgen config` opens the main configuration interface:

```
╔════════════════════════════════════════╗
║         GitGen Configuration           ║
╚════════════════════════════════════════╝

1. Add new model
2. Manage models (3 configured)
3. Test models
4. App settings
5. Reset all configuration
0. Exit

Select option: _
```

### Adding a New Model - Complete Wizard Flow

The configuration wizard guides you through 10 comprehensive steps:

#### Step 1: Model Name
```
🎉 Welcome to the GitGen Multi-Model Configuration Wizard
This will guide you through setting up a new AI model configuration.

Step 1: Choose a name for this model configuration.
This is a friendly name to identify this configuration, NOT the model ID the provider uses.
Examples: 'gpt-4-work', 'sonnet', 'kimik2', 'llama-local'
Enter model name: claude-work
```

#### Step 2: Aliases Configuration
```
Step 2: Configure aliases for quick access (optional).
Aliases allow you to quickly reference models with memorable shortcuts.

Examples:
  @ultrathink - For complex reasoning tasks
  @sonnet    - For general coding tasks
  @free      - For public repos where privacy isn't an issue

💡 Tip: Configure a free model as @free to save money on public repositories
⚠️  Important: Avoid setting a free model as your default

Enter aliases (comma-separated) [@claudework]: @claude, @work, @smart
✅ Configured aliases: @claude, @work, @smart
```

#### Step 3: Description
```
Step 3: Add a description for this model (optional).
This helps you remember what this model is best used for.
Enter description [none]: Company API key - high capability model for complex tasks
```

#### Step 4-5: Provider Configuration
```
Step 4: Select your provider's API compatibility type.
  1. OpenAI Compatible (e.g., OpenAI, Azure, Groq, Ollama)
Enter your choice: [1] 1

Step 5: Select your specific provider preset.
  1. OpenAI (Official Platform)
  2. Custom Provider (API Key required, e.g., Azure, Anthropic, Google, OpenRouter, Groq)
  3. Custom Provider (No API Key required, e.g., Ollama, LM Studio)
Enter your choice: [1] 2
Enter the provider's chat completions URL: https://api.anthropic.com/v1/chat/completions
Provider name [anthropic.com]: Anthropic
Enter the model ID used by the provider's API: claude-sonnet-4-20250514
Enter the provider's API Key: **************************************************
```

#### Step 6-7: Configuration & Testing
```
Step 6: Configure maximum output tokens.
ℹ️ Suggested: 2000 tokens (Standard model - lower limit sufficient)
Enter max output tokens: [2000] 3000

Step 7: Test the configuration.
Testing your configuration and detecting optimal API parameters...
🧪 Testing LLM connection...
🔗 Using Anthropic provider via https://api.anthropic.com/v1/chat/completions

✅ LLM Response:
"Hello! I'm working great. Ready to help you generate meaningful commit messages!"

Generated with 24 input tokens, 18 output tokens (42 total) • 73 characters
🎉 Configuration test successful!
```

#### Step 8-10: Optional Configuration
```
Step 8: Configure pricing information (optional).
Select currency:
  1. USD ($)
  2. EUR (€)
  3. GBP (£)
  4. AUD (A$)
  5. Other
Enter your choice: [1] 1
Input cost per million tokens [0]: 3
Output cost per million tokens [0]: 15

Step 9: Configure custom system prompt (optional).
Example: 'Always use conventional commit format'
Enter custom system prompt: Focus on why changes were made, not just what changed

Step 10: Review configuration summary.
📋 Model Configuration Summary:
   Name: claude-work
   Aliases: @claude, @work, @smart
   Description: Company API key - high capability model for complex tasks
   Type: openai-compatible
   Provider: Anthropic
   URL: https://api.anthropic.com/v1/chat/completions
   Model ID: claude-sonnet-4-20250514
   Max Output Tokens: 3000
   Pricing: Input: $3.00/M tokens, Output: $15.00/M tokens
   System Prompt: Focus on why changes were made, not just what changed

Save this model configuration? [y]: y
✅ Model 'claude-work' saved successfully!
```

### Model Management Interface

The model management submenu provides comprehensive model control:

```
═══ Model Management ═══

1. List models
2. Set default model
3. Edit model (aliases, tokens, etc.)
4. Delete model
0. Back to main menu
```

#### Listing Models
```
═══ Configured Models ═══

  claude-work ⭐ (default)
    Type: openai-compatible | Provider: Anthropic | Model: claude-sonnet-4-20250514
    URL: https://api.anthropic.com/v1/chat/completions
    Temperature: 0.3 | Max Output Tokens: 3,000
    Note: Company API key - high capability model for complex tasks
    Aliases: @claude, @work, @smart
    Pricing: Input: $3.00/M tokens, Output: $15.00/M tokens
    Last used: 2025-07-28 10:45 AM

  groq-fast
    Type: openai-compatible | Provider: Groq | Model: llama-3.1-70b-versatile
    URL: https://api.groq.com/openai/v1/chat/completions
    Temperature: 0.3 | Max Output Tokens: 2,000
    Note: Ultra-fast inference for quick commits
    Aliases: @fast, @quick, @groq
    Pricing: Input: $0.59/M tokens, Output: $0.79/M tokens
    Last used: 2025-07-28 09:30 AM
```

### Generation Output Examples

When generating commit messages, GitGen provides rich feedback:

```bash
$ gitgen
Found 5 changed files
🔗 Using claude-work (claude-sonnet-4-20250514 via Anthropic)
✅ Generated Commit Message:
"Refactor authentication middleware to support JWT refresh tokens, add concurrent device handling, and improve error responses with detailed status codes for better debugging"

Generated with 3,847 input tokens, 38 output tokens (3,885 total) • 178 characters
Estimated cost: $0.07 USD

📋 Commit message copied to clipboard.
```

With cost preview for large diffs:
```bash
$ gitgen @ultrathink
Found 47 changed files
⚠️  Large diff detected: ~18,000 tokens

💰 Estimated cost:
   • Input: ~$0.18
   • Output: ~$0.02
   • Total: ~$0.20

Continue? (y/N): y
```

## Provider Configuration Examples

### OpenAI Configuration
```
Step 5: Select your specific provider preset.
Enter your choice: [1] 1
Enter the model ID: [gpt-4o-mini] gpt-4-turbo
Enter your OpenAI API Key: sk-**************************************************
```

### Claude (Anthropic) Configuration
```
Step 5: Select your specific provider preset.
Enter your choice: [1] 2
Enter the provider's chat completions URL: https://api.anthropic.com/v1/chat/completions
Enter the model ID: claude-sonnet-4-20250514
Enter the provider's API Key: sk-ant-**************************************************
```

### Gemini (Google) Configuration
```
Step 5: Select your specific provider preset.
Enter your choice: [1] 2
Enter the provider's chat completions URL: https://generativelanguage.googleapis.com/v1beta/openai/chat/completions
Enter the model ID: gemini-2.5-flash
Enter the provider's API Key: **************************************************
```

### Groq (Ultra-Fast) Configuration
```
Step 5: Select your specific provider preset.
Enter your choice: [1] 2
Enter the provider's chat completions URL: https://api.groq.com/openai/v1/chat/completions
Enter the model ID: llama-3.1-70b-versatile
Enter the provider's API Key: gsk-**************************************************
```

### OpenRouter Configuration (Including Free Models)
```
# For paid models
Enter the provider's chat completions URL: https://openrouter.ai/api/v1/chat/completions
Enter the model ID: anthropic/claude-sonnet-4
Enter the provider's API Key: sk-or-v1-**************************************************

# For free models (great for public repos)
Model name: qwen-free
Aliases: @free, @public
Enter the model ID: qwen/qwen-32b:free
Note: Free model - PUBLIC REPOS ONLY
```

### Local Models (Ollama/LM Studio)
```
Step 5: Select your specific provider preset.
Enter your choice: [1] 3
Enter your custom provider's chat completions URL: [http://localhost:11434/v1/chat/completions]
Enter the model ID: llama3.2
```

## Model Configuration Best Practices

### Security-First Setup

Always configure models in order of security importance:

```bash
# 1. First: Your most secure model (becomes default)
Model name: gpt-4-work
Aliases: @work, @secure
Note: Company API - never use for public code

# 2. Second: General purpose model
Model name: claude-personal
Aliases: @claude, @smart
Note: Personal projects and complex tasks

# 3. Last: Free/public models
Model name: qwen-free
Aliases: @free, @public
Note: Free tier - PUBLIC REPOSITORIES ONLY
```

### Alias Strategy

Create meaningful aliases that indicate usage:
- `@work`, `@company` - Corporate/secure models
- `@fast`, `@quick` - Speed-optimized models
- `@smart`, `@think` - High-capability models
- `@free`, `@public` - Cost-free models
- `@local`, `@private` - Self-hosted models

## Advanced Usage

### Model Selection Patterns
```bash
# Quick model switching
gitgen @fast              # Speed over quality
gitgen @smart             # Complex changes
gitgen @free              # Public repositories

# Custom instructions with models
gitgen "explain architecture changes" @smart
gitgen @quick "just the facts"

# Preview mode - see model selection and cost without calling LLM
gitgen -p
gitgen --preview @fast
```

### App Settings Configuration

Fine-tune GitGen behavior through the settings menu:

```
═══ App Settings ═══

1. Show token usage: ON
2. Copy to clipboard: ON  
3. Enable partial alias matching: ON
4. Minimum alias match length: 3 chars
0. Back to main menu
```

- **Token Usage**: Shows input/output token counts after generation
- **Clipboard**: Automatically copies commit messages
- **Partial Matching**: Type `@ult` to match `@ultrathink`
- **Match Length**: Minimum characters for partial matching

## Configuration Storage

GitGen stores all configuration securely:

- **Windows**: `%APPDATA%\GitGen\models.json` (DPAPI encrypted)
- **macOS**: `~/.config/gitgen/models.json` (Keychain encrypted)
- **Linux**: `~/.config/gitgen/models.json` (Kernel keyring encrypted)

Configuration includes:
- Model definitions with all parameters
- Encrypted API keys
- Usage statistics and last-used timestamps
- App-wide settings
- Default model selection

## Building & Contributing

GitGen is built with .NET 9.0 and designed for cross-platform compatibility:

```bash
# Clone and build
git clone https://github.com/stewartcelani/GitGen.git
cd GitGen
dotnet build src/GitGen/GitGen.csproj

# Run tests
./test.ps1

# Publish for all platforms
./publish.ps1
```

### Running Tests

GitGen includes a comprehensive test suite built with xUnit, FluentAssertions, and NSubstitute:

```bash
# Run all tests
./test.ps1

# Run tests with code coverage
./test.ps1 -Coverage

# Run specific test categories
./test.ps1 -Filter "FullyQualifiedName~ValidationService"
./test.ps1 -Filter "Category=Unit"

# Watch mode - re-runs tests on file changes
./test.ps1 -Watch

# Alternative: Direct dotnet test commands
dotnet test tests/GitGen.Tests/GitGen.Tests.csproj
dotnet test --collect:"XPlat Code Coverage"
```

#### Test Coverage

The test suite covers:
- **Service Layer**: Validation, message cleaning, cost calculations
- **Providers**: OpenAI provider implementation and parameter detection
- **Configuration**: Model configuration validation and management
- **Helpers**: Platform detection, date/time formatting
- **Integration**: Configuration wizard and secure storage

Coverage reports are generated in the `TestResults/` directory when using the `-Coverage` flag.

#### Test Organization

```
tests/GitGen.Tests/
├── Services/           # Unit tests for service classes
├── Providers/          # Provider implementation tests
├── Configuration/      # Configuration system tests
├── Helpers/           # Utility class tests
├── IntegrationTests/  # Cross-component integration tests
└── TestBase.cs        # Shared test utilities
```

For development guidance, see [CLAUDE.md](CLAUDE.md) in the repository.

## Security

GitGen takes security seriously:

### 🔒 Secure Configuration
- API keys are encrypted using platform-specific data protection (DPAPI on Windows, Keychain on macOS, Kernel keyring on Linux)
- Never stored in plain text or environment variables
- Automatic cleanup of sensitive data from memory

### 🛡️ Automated Security Scanning
- **CodeQL Analysis**: Automated scanning for security vulnerabilities in every push
- **Dependabot**: Monitors dependencies for known vulnerabilities and auto-creates update PRs
- **OpenSSF Scorecard**: Comprehensive security scoring and best practices evaluation

### 📋 Security Best Practices
- No telemetry or data collection
- All API communications use HTTPS
- Regular security updates and dependency maintenance
- Clear security warnings for free/public models

### 🚨 Reporting Security Issues
If you discover a security vulnerability, please report it via:
- GitHub Security Advisories (preferred)
- Email: security@[domain] (see SECURITY.md)

For more details, see our [Security Policy](SECURITY.md).

## Support & Feedback

- 🐛 [Report bugs or request features](https://github.com/stewartcelani/GitGen/issues)
- 💬 [Start a discussion](https://github.com/stewartcelani/GitGen/discussions)
- 📚 [View documentation](https://github.com/stewartcelani/GitGen/wiki)

## License

MIT License - see [LICENSE](LICENSE) file for details.