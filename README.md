# GitGen

![GitHub release (latest by date)](https://img.shields.io/github/v/release/stewartcelani/GitGen)
![GitHub license](https://img.shields.io/github/license/stewartcelani/GitGen)
![.NET](https://img.shields.io/badge/.NET-9.0-blueviolet)
![GitHub last commit](https://img.shields.io/github/last-commit/stewartcelani/GitGen)

### Multi-Model AI-Powered Git Commit Message Generator

GitGen analyzes your Git repository changes and uses AI to generate meaningful, descriptive commit messages automatically. With support for multiple AI models, you can optimize for cost, capability, or privacy depending on your needs. Simply run the tool in any Git repository with uncommitted changes, and it will create a commit message based on your diff and copy it to your clipboard.

**New in v2.0**: Secure multi-model support, encrypted API key storage, cost tracking, and instant model switching with aliases.

## Features

- 🔍 Automatic Git diff analysis
- 🤖 Supports any OpenAI-compatible API (OpenAI, Anthropic, Google, Azure, Groq, local models, and more)
- 🔐 Secure encrypted storage for API keys and configurations
- 💰 Shows token usage and cost per generation
- 🚀 Multi-model support with instant switching
- 📋 Commit messages automatically copy to clipboard
- 🔧 Streamlined interactive configuration wizard
- 🎨 Custom commit styles and per-model system prompts
- 🔄 Self-healing API parameter detection
- ⚡ Model aliases for quick access (@fast, @smart, @local)
- 🌍 Cross-platform (Windows, macOS, Linux)
- 📦 No external runtime dependencies
- 🐛 Debug logging and comprehensive health checks

## Multi-Model Support & Aliases

GitGen supports configuring multiple AI models and switching between them instantly using aliases. This allows you to:

- **Use different models for different tasks**: Configure a reasoning model (@ultrathink) for complex changes and a fast model (@fast) for simple commits
- **Save money on public repositories**: Configure a free model as @free for repos where privacy isn't a concern
- **Switch models on the fly**: Use `gitgen @modelname` to generate with any configured model

### Setting Up Models

When you run `gitgen config`, you can:
1. Name your model (e.g., "gpt-4-work", "claude-personal", "llama-free")
2. Set up aliases for quick access (e.g., @opus, @free, @fast)
3. Add notes to remember what each model is best for

### Example Configuration Strategy

```bash
# Configure a premium model for work
Model name: claude-work
Aliases: @claude, @work
Note: For proprietary code - never use on public repos

# Configure a free model for open source
Model name: qwen-free  
Aliases: @free, @public
Note: For public repos where privacy isn't an issue

# Configure a local model for sensitive work
Model name: llama-local
Aliases: @local, @private  
Note: Runs locally - safe for any code
```

## Quick Start

```bash
# Configure your first AI model
gitgen config

# Generate commit message for staged changes
gitgen

# Use a specific model via alias
gitgen @sonnet
gitgen @free  # Use for public repos to save money

# Guide commit messages with custom prompts
gitgen "Must be a haiku"
gitgen "Focus on security changes" @ultrathink
gitgen @free "Explain the refactoring"
```

## Installation

1. Download the appropriate release for your platform from the [Releases page](https://github.com/stewartcelani/GitGen/releases).
2. Extract the archive.
3. Add the executable to your PATH.
4. Run `gitgen config` to set up your AI provider.


### Build from Source

Requirements: .NET 9.0 SDK, PowerShell
```bash
git clone https://github.com/stewartcelani/GitGen.git
cd GitGen
./publish.ps1
# Executable will be in dist/
```

## Prerequisites

- A Git repository with uncommitted changes
- An API key and endpoint for a supported AI provider (e.g., OpenAI, Anthropic, Google, Azure, OpenRouter, Groq, or a local model)

## Usage

### First Time Setup

The first time you run `gitgen`, it will detect that it's not configured and automatically launch the configuration wizard to help you set up your AI provider.

```bash
gitgen config
```


### Generate Commit Messages

In any Git repository with uncommitted changes, simply run:

```bash
gitgen
```

### Options & Commands

```
Description:
  GitGen - AI-Powered Git Commit Message Generator

Usage:
  gitgen                       Generate commit message with default model
  gitgen [prompt]              Generate with custom prompt
  gitgen @<model>              Generate with specific model
  gitgen [prompt] @<model>     Generate with custom prompt and model
  gitgen @<model> [prompt]     Alternative syntax
  gitgen [command] [options]   Run a specific command

Examples:
  gitgen
  gitgen "must be a haiku"
  gitgen @free
  gitgen "focus on security" @ultrathink
  gitgen @sonnet "explain the refactoring"

Options:
  -d, --debug       Enable debug logging
  -v, --version     Show version information
  -?, -h, --help    Show help and usage information

Commands:
  config            Run the interactive configuration wizard
  help              Display help information
```


### Model Management

All model configuration and management is done through the interactive configuration menu:

```bash
gitgen config
```

This opens an interactive menu where you can:
- Add new models with full configuration
- Switch between configured models
- Manage aliases and tokens
- Test all models
- Configure app-wide settings
- View and edit model details

To quickly switch models while generating commit messages, use the @alias syntax:

```bash
# Use a specific model via alias
gitgen @fast
gitgen @claude
```

### Token Usage & Cost Display

When generating commit messages, GitGen can display token usage and estimated cost (if pricing is configured). This is controlled by the "Show token usage" setting in the app settings menu.

### Display Configuration Information

You can check your current configuration through the configuration menu:

```bash
gitgen config
# Then select the option to view configured models
```

This displays all configured models with their details:
- Model names and aliases
- Provider information
- Pricing (if configured)
- Last used timestamp
- Default model indicator

### Health Check

You can test all configured models through the configuration menu:

```bash
gitgen config
# Then select the option to test models
```

This tests each configured model to ensure they're working correctly and displays the results.

## Configuration Wizard

The `gitgen config` command launches a streamlined wizard that guides you through setting up and managing your AI models. The wizard now includes:

- Model naming and aliasing
- Note/description for each model
- Provider configuration
- Automatic parameter detection
- Connection testing
- Pricing configuration (optional)
- Custom system prompts (optional)

### Configuration Storage

GitGen stores configurations securely:
- **Windows**: `%APPDATA%\GitGen\models.json` (encrypted)
- **macOS/Linux**: `~/.config/gitgen/models.json` (encrypted)
- API keys are encrypted using platform-specific data protection APIs
- No sensitive data is stored in plain text

## Provider Configuration Examples

The interactive `gitgen config` command provides three main presets for setting up your AI provider. Below are detailed examples for each one.

### 1. OpenAI (Official Platform)

Use this option for the official OpenAI API.

#### Configuration Flow

```powershell
PS C:\> gitgen config
🎉 Welcome to the GitGen Multi-Model Configuration Wizard
This will guide you through setting up a new AI model configuration.

Step 1: Choose a name for this model configuration.
Enter model name: gpt-4-work

Step 2: Configure aliases for quick access (optional)
You can use aliases like @gpt4 or @work to quickly reference this model.
Enter aliases (comma-separated) [@gpt-4-work]: @gpt4, @work, @smart

Step 3: Add a description for this model (optional)
Enter description: [none] High-capability GPT-4 for complex tasks

Step 4: Select your provider's API compatibility type.
1. OpenAI Compatible (e.g., OpenAI, Azure, Groq, Ollama)
   Enter your choice: [1]

Step 5: Select your specific provider preset.
1. OpenAI (Official Platform)
2. Custom Provider (API Key required, e.g., Azure, Anthropic, Groq)
3. Custom Provider (No API Key required, e.g., Ollama, LM Studio)
   Enter your choice: [1]
   Enter your model name: [gpt-4o-mini] gpt-4-turbo
   Enter your OpenAI API Key: ********************************************************

🧪 Testing configuration and detecting optimal parameters...
✅ Parameter detection complete.
💾 Saving configuration...
✅ Configuration saved successfully!

Step 6: Configure pricing information (optional)
Would you like to configure pricing for cost tracking? (y/N): y
Enter currency code (e.g., USD, EUR, AUD): [USD] 
Enter cost per million input tokens: 10
Enter cost per million output tokens: 30

📋 Model Configuration Summary:
   Name: gpt-4-work
   Aliases: @gpt4, @work, @smart
   Description: High-capability GPT-4 for complex tasks
   Type: openai
   Provider: OpenAI
   URL: https://api.openai.com/v1/chat/completions
   Model ID: gpt-4-turbo
   API Key: sk-****************************
   Max Tokens: 5000
   Pricing: Input: $10.00/M tokens, Output: $30.00/M tokens
   
✅ Model 'gpt-4-work' configured successfully!
```

#### Usage Example

```powershell
PS C:\path\to\your\project> gitgen
Found 3 changed files
⏳ Generating commit message...

Using OpenAI provider (https://api.openai.com/v1/chat/completions, gpt-4.1-nano) to generate commit message
✅ Generated Commit Message:
"Refactor user authentication to use a more secure JWT-based approach, replacing the previous session cookie implementation. Added middleware for token validation and updated login/logout endpoints accordingly."

Generated with 2,451 input tokens, 58 output tokens (2,509 total) • 241 characters
Estimated cost: $0.03 USD

📋 Commit message copied to clipboard.
```

### 2. Custom Provider (API Key Required) - Examples: Azure, Anthropic, Google, Groq, OpenRouter

This preset is for any third-party service that offers an OpenAI-compatible API and requires an API key. **This includes Microsoft Azure**, Anthropic, Google, Groq, OpenRouter, Together AI, and others.

#### Example: Microsoft Azure OpenAI

##### Configuration

You will need your Azure endpoint URL, deployment name (which acts as the model name), and Azure API key. Select option `2` for a custom provider with an API key.

```powershell
PS C:\> gitgen config
...
Step 2: Select your specific provider preset.
...
   Enter your choice: [1] 2
   Enter the provider's chat completions URL (e.g., your Azure endpoint): https://intrasight.openai.azure.com/openai/deployments/gpt-4.1-nano/chat/completions?api-version=2025-01-01-preview
   Enter the model name (e.g., your Azure deployment name): gpt-4.1-nano
   Enter the provider's API Key: ********************************************************
...
✅ Configuration saved successfully!
```

##### Usage Example (Azure with gpt-4.1-nano)

```powershell
PS C:\path\to\your\project> gitgen
Found 19 changed files
⏳ Generating commit message...

Using OpenAI provider (https://intrasight.openai.azure.com/openai/deployments/gpt-4.1-nano/chat/completions?api-version=2025-01-01-preview, gpt-4.1-nano) to generate commit message
✅ Generated Commit Message:
"Enhanced environment variable management with IEnvironmentPersistenceService, centralizing config saving, updating, and clearing; added validation for all inputs; refactored shell profile handling for cross-platform atomic updates; improved error handling; introduced MessageCleaningService for response cleanup; expanded ValidationService with comprehensive rules for models, URLs, API keys, tokens, and temperatures; added Constants.cs for centralized magic values; improved CLI commands for quick settings and configuration management; refactored Program.cs for better flow and user prompts; all to ensure security, maintainability, and usability within 300 characters."

Generated with 78,296 input tokens, 115 output tokens (78,411 total) • 672 characters

📋 Commit message copied to clipboard.
```

#### Example: Anthropic

##### Configuration

You will need the provider's "Chat Completions" URL, the model name, and the provider's API key.

```powershell
PS C:\> gitgen config
ℹ️ Welcome to the GitGen configuration wizard.
This will guide you through setting up your AI provider.

...

Step 1: Select your provider's API compatibility type.
1. OpenAI Compatible (e.g., OpenAI, Azure, Groq, Ollama)
   Enter your choice: [1] 1

Step 2: Select your specific provider preset.
1. OpenAI (Official Platform)
2. Custom Provider (API Key required, e.g., Azure, Anthropic, Groq)
3. Custom Provider (No API Key required, e.g., Ollama, LM Studio)
   Enter your choice: [1] 2
   Enter the provider's chat completions URL (e.g., your Azure endpoint): https://api.anthropic.com/v1/chat/completions
   Enter the model name (e.g., your Azure deployment name): claude-sonnet-4-20250514
   Enter the provider's API Key: ************************************************************************************************************
...
✅ Configuration saved successfully!
```

##### Usage Example (Azure OpenAI)

```powershell
PS C:\path\to\your\project> gitgen @azure
Found 1 changed files
⏳ Generating commit message...

Using model 'azure-work' via OpenAI provider
✅ Generated Commit Message:
"Refactor authentication middleware to use JWT tokens with refresh capability, add token validation endpoints, update user session handling to support concurrent devices, and improve error responses with detailed status codes for better client-side handling."

Generated with 2,086 input tokens, 67 output tokens (2,153 total) • 264 characters
Estimated cost: $0.02 USD

📋 Commit message copied to clipboard.
```

#### Example: Free Model via OpenRouter

##### Configuration for Cost-Conscious Usage

```powershell
PS C:\> gitgen config
🎉 Welcome to the GitGen Multi-Model Configuration Wizard

Step 1: Choose a name for this model configuration.
Enter model name: qwen-free

Step 2: Configure aliases for quick access (optional)
Enter aliases (comma-separated) [@qwen-free]: @free, @public

Step 3: Add a description for this model (optional)
Enter description: [none] Free model for public repositories

Step 4-5: Provider configuration...
   Enter the provider's chat completions URL: https://openrouter.ai/api/v1/chat/completions
   Enter the model name: qwen/qwen-32b:free
   Enter the provider's API Key: sk-or-v1-********************************

Step 6: Configure pricing information (optional)
Would you like to configure pricing? (y/N): n
[Model is free - no pricing needed]

📋 Model Configuration Summary:
   Name: qwen-free
   Aliases: @free, @public
   Description: Free model for public repositories
   Pricing: Free
```

##### Usage Example

```powershell
PS C:\public-repo> gitgen @free
Found 5 changed files
⏳ Generating commit message...

Using model 'qwen-free' via OpenAI provider
✅ Generated Commit Message:
"Update documentation with installation instructions, add example configuration files, fix typos in README, reorganize project structure for clarity, and add MIT license file for open source distribution."

Generated with 3,245 input tokens, 38 output tokens (3,283 total) • 195 characters
Estimated cost: Free

📋 Commit message copied to clipboard.
```

#### Example: Groq (Ultra-Fast Inference)

##### Configuration

```powershell
PS C:\> gitgen config
🎉 Welcome to the GitGen Multi-Model Configuration Wizard

Step 1: Choose a name for this model configuration.
Enter model name: groq-fast

Step 2: Configure aliases (comma-separated) [@groq-fast]: @groq, @fast, @quick

Step 3: Add a description: [none] Groq's ultra-fast inference for quick commits

Step 4-5: Provider configuration...
   Enter the provider's chat completions URL: https://api.groq.com/openai/v1/chat/completions
   Enter the model name: llama-3.1-70b-versatile
   Enter the provider's API Key: gsk_********************************

Step 6: Configure pricing (y/N): y
Enter cost per million input tokens: 0.59
Enter cost per million output tokens: 0.79

📋 Model Configuration Summary:
   Name: groq-fast
   Aliases: @groq, @fast, @quick
   Pricing: Input: $0.59/M tokens, Output: $0.79/M tokens
```

##### Usage Example

```powershell
PS C:\path\to\your\project> gitgen @fast
Found 19 changed files
⏳ Generating commit message...

Using model 'groq-fast' via OpenAI provider
✅ Generated Commit Message:
"Improve code quality and maintainability by extracting duplicated environment persistence logic into a dedicated service, simplifying ConsoleLogger, removing magic values, enhancing security, adding comprehensive input validation, and improving error handling and configuration management."

Generated with 76,378 input tokens, 211 output tokens (76,589 total) • 289 characters
Estimated cost: $0.05 USD

📋 Commit message copied to clipboard.
```

#### Example: Google Gemini

##### Configuration

```powershell
PS C:\> gitgen config
🎉 Welcome to the GitGen Multi-Model Configuration Wizard

Step 1: Choose a name for this model configuration.
Enter model name: gemini-flash

Step 2: Configure aliases (comma-separated) [@gemini-flash]: @gemini, @google

Step 3: Add a description: [none] Google's fast Gemini model

Step 4-5: Provider configuration...
   Enter the provider's chat completions URL: https://generativelanguage.googleapis.com/v1beta/openai/chat/completions
   Enter the model name: gemini-2.5-flash
   Enter the provider's API Key: ***************************************

📋 Model Configuration Summary:
   Name: gemini-flash
   Aliases: @gemini, @google
   Type: openai-compatible
   Provider: Google
```

##### Usage Example

```powershell
PS C:\path\to\your\project> gitgen @gemini
Found 18 changed files
⏳ Generating commit message...

Using model 'gemini-flash' via OpenAI provider
✅ Generated Commit Message:
"Refactor TTS app to batch processing, improving text chunking and adding content fetching. Replaced old FastAPI `app.py` with `src/app.py` for file-based processing. Tokenization logic refined (hardcoded `MAX_TOKENS=1900`). Added web scrapers, tokenizer & test tools. Reqs updated, dirs restructured."

Generated with 19,430 input tokens, 75 output tokens (21,788 total) • 300 characters

📋 Commit message copied to clipboard.
```

#### Example: OpenRouter (Access Multiple Models)

##### Configuration

```powershell
PS C:\> gitgen config
🎉 Welcome to the GitGen Multi-Model Configuration Wizard

Step 1: Choose a name for this model configuration.
Enter model name: kimi-reasoning

Step 2: Configure aliases (comma-separated) [@kimi-reasoning]: @kimi, @reason

Step 3: Add a description: [none] Advanced reasoning model via OpenRouter

Step 4-5: Provider configuration...
   Enter the provider's chat completions URL: https://openrouter.ai/api/v1/chat/completions
   Enter the model name: moonshotai/kimi-k2
   Enter the provider's API Key: sk-or-v1-********************************

📋 Model Configuration Summary:
   Name: kimi-reasoning
   Aliases: @kimi, @reason
   Type: openai-compatible
   Provider: OpenRouter
```

##### Usage Example

```powershell
PS C:\path\to\your\project> gitgen @kimi
Found 18 changed files
⏳ Generating commit message...

Using model 'kimi-reasoning' via OpenAI provider
✅ Generated Commit Message:
"Add Claude settings and improve TTS chunking with actual tokenizer usage. Replaced MAX_TOKENS calculation with get_actual_token_count() using model.tokenizer.encode(). Enhanced split_into_safe_chunks() with better sentence splitting, clause fallback, and truncation for oversized chunks. Added debug logging throughout generation. Added get_lit_story.ps1 for story fetching and reorganized input/output directories."

Generated with 16,736 input tokens, 76 output tokens (16,812 total) • 415 characters
Estimated cost: $0.01 USD

📋 Commit message copied to clipboard.
```

## Key Improvements in This Release

### 🔐 Security First
- API keys are encrypted using platform-specific data protection
- All sensitive data is stored securely
- Secure storage locations per OS

### 🚀 Multi-Model Workflow
- Configure unlimited AI models
- Switch instantly with aliases
- Different models for different tasks
- Cost optimization strategies

### 💰 Cost Management
- Track token usage per model
- Configure pricing for accurate cost estimates
- Use free models for public repos
- Monitor spending through the config menu

### 🎆 Enhanced User Experience
- Streamlined configuration wizard
- Interactive settings menu
- Automatic migration from v1.0.0
- Better error messages and recovery

### 3. Custom Provider (No API Key) - Example: LM Studio / Ollama

This is the perfect choice for running models locally using tools like LM Studio, Ollama, Jan, etc. These tools typically expose an OpenAI-compatible server on your local machine that doesn't require an API key.

#### Configuration

You'll need the local server URL and the name of the loaded model. The wizard provides a common default URL (`http://localhost:11434/v1/chat/completions`).

```powershell
PS C:\> gitgen config
...
Step 2: Select your specific provider preset.
...
   Enter your choice: [1] 3
   Enter your custom provider's chat completions URL: [http://localhost:11434/v1/chat/completions] http://localhost:1234/v1/chat/completions
   Enter the model name (e.g., llama3): qwen2.5-3b-instruct
...
✅ Configuration saved successfully!
```

#### Usage Example (Local Model for Privacy)

```powershell
PS C:\confidential-project> gitgen @local
Found 5 changed files
⏳ Generating commit message...

Using model 'llama-local' via OpenAI provider
✅ Generated Commit Message:
"Implement secure data encryption for user credentials, add AES-256 encryption utility class, update database schema with encrypted fields, modify authentication service to handle encrypted passwords, and add unit tests for encryption/decryption functionality."

Generated with 6,193 input tokens, 47 output tokens (6,240 total) • 262 characters
Estimated cost: Free (local model)

📋 Commit message copied to clipboard.
```

### Best Practices for Model Configuration

1. **Use Descriptive Names**: Name models based on their purpose (e.g., "gpt-work", "claude-personal")
2. **Set Clear Aliases**: Use memorable aliases that reflect the model's strengths (@fast, @smart, @free)
3. **Add Notes**: Document when to use each model in the description field
4. **Configure Pricing**: Set up pricing to track costs accurately
5. **Test Regularly**: Use the test option in `gitgen config` to ensure all models are working

## Support & Feedback

- 🐛 **Bug reports & feature requests**: [Submit an issue](https://github.com/stewartcelani/GitGen/issues)
- 💡 **Questions & discussions**: Check existing issues or start a new one

## Project Structure

```
src/
└── GitGen/                 # Main application
    ├── Configuration/      # Configuration management
    ├── Constants.cs        # Application constants
    ├── Exceptions/         # Custom exception types
    ├── Helpers/            # Utility classes
    ├── Providers/          # AI provider implementations
    │   └── OpenAI/         # OpenAI-compatible provider
    ├── Services/           # Core business logic
    ├── Program.cs          # Application entry point
    └── GitGen.csproj       # Project file
```

## Configuration Menu

The `gitgen config` command opens an interactive configuration menu:

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
```

From this menu you can:
- Add new models with the full configuration wizard
- Edit existing models (aliases, tokens, descriptions)
- Test all models at once
- Configure app-wide settings (token display, clipboard)
- View usage statistics and costs
- Reset configuration if needed

## Building & Publishing (For Developers)

This section is for developers who want to build the project from source.

### Prerequisites

- .NET 9.0 Runtime or SDK

### Publish Script

Run the included PowerShell script from the project root. This script creates self-contained, single-file executables for all supported platforms (Windows, Linux, macOS).

```powershell
# Publish for all platforms
PS C:\path\to\your\project> .\publish.ps1

# Publish only for current platform
PS C:\path\to\your\project> .\publish.ps1 -CurrentOnly

# Publish to custom directory
PS C:\path\to\your\project> .\publish.ps1 -OutputPath 'C:\temp'
```

This will:
- Clean the output directory (`dist/`)
- Build and publish trimmed, self-contained executables for `win-x64`, `linux-x64`, `osx-x64`, and `osx-arm64`.
- Test the native executable (on the host OS).
- Copy the native build to the root of `dist/` for convenience.
- Create ZIP archives for each platform's build.
- Clean up the uncompressed build folders, leaving only the ZIP files and the native executable.

### Publish Output Example

Here is an example of the output from the publish script:
```powershell
PS C:\path\to\your\project> .\publish.ps1
🚀 GitGen Publisher
Publishing self-contained, trimmed, single-file executables

🔍 Running pre-flight validation...
Checking dotnet availability...
✅ dotnet available (v9.0.302)
Checking project file...
✅ Project file exists
Testing version extraction...
✅ Version extracted: 1.0.0
Testing project build...
✅ Build successful

🧹 Cleaning output directory...
Removing: C:\path\to\your\project\dist
✅ Output directory cleaned
✅ Fresh output directory created

ℹ️ Detected project version: 1.0.0

📋 Publishing for all supported platforms...

📦 Publishing Windows x64 (v1.0.0, win-x64)...
✅ Success: C:\path\to\your\project\dist\GitGen-v1.0.0-win-x64\GitGen\gitgen.exe
📏 Size: 30.9 MB
🧪 Testing executable...
✅ Executable test passed: GitGen v1.0.0.0

📦 Publishing Linux x64 (v1.0.0, linux-x64)...
✅ Success: C:\path\to\your\project\dist\GitGen-v1.0.0-linux-x64\GitGen\gitgen
📏 Size: 32.7 MB
⚠️  Cross-platform build (cannot test)

📦 Publishing macOS x64 (v1.0.0, osx-x64)...
✅ Success: C:\path\to\your\project\dist\GitGen-v1.0.0-osx-x64\GitGen\gitgen
📏 Size: 32.0 MB
⚠️  Cross-platform build (cannot test)

📦 Publishing macOS ARM64 (v1.0.0, osx-arm64)...
✅ Success: C:\path\to\your\project\dist\GitGen-v1.0.0-osx-arm64\GitGen\gitgen
📏 Size: 34.1 MB
⚠️  Cross-platform build (cannot test)

🚀 Copying current platform's build to root output path for convenience...
Copying from C:\path\to\your\project\dist\GitGen-v1.0.0-win-x64\GitGen to C:\path\to\your\project\dist
✅ Current platform build copied successfully.

📦 Zipping release artifacts...
Creating C:\path\to\your\project\dist\GitGen-v1.0.0-win-x64.zip...
✅ Successfully created zip file.
Creating C:\path\to\your\project\dist\GitGen-v1.0.0-linux-x64.zip...
✅ Successfully created zip file.
Creating C:\path\to\your\project\dist\GitGen-v1.0.0-osx-x64.zip...
✅ Successfully created zip file.
Creating C:\path\to\your\project\dist\GitGen-v1.0.0-osx-arm64.zip...
✅ Successfully created zip file.

🧹 Cleaning up source folders...
Removing C:\path\to\your\project\dist\GitGen-v1.0.0-win-x64...
Removing C:\path\to\your\project\dist\GitGen-v1.0.0-linux-x64...
Removing C:\path\to\your\project\dist\GitGen-v1.0.0-osx-x64...
Removing C:\path\to\your\project\dist\GitGen-v1.0.0-osx-arm64...

📊 Publish Summary:
✅ Successful: 4/4
📂 Output location: C:\path\to\your\project\dist

🎉 GitGen publishing complete!
```
