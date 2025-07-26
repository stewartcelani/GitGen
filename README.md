# GitGen

![GitHub release (latest by date)](https://img.shields.io/github/v/release/stewartcelani/GitGen)
![GitHub license](https://img.shields.io/github/license/stewartcelani/GitGen)
![.NET](https://img.shields.io/badge/.NET-9.0-blueviolet)
![GitHub last commit](https://img.shields.io/github/last-commit/stewartcelani/GitGen)

### AI-Powered Git Commit Message Generator

GitGen analyzes your Git repository changes and uses AI to generate meaningful, descriptive commit messages automatically. Simply run the tool in any Git repository with uncommitted changes, and it will create a commit message based on your diff and copy it to your clipboard.

## Features

- 🔍 Automatic Git diff analysis
- 🤖 Supports any OpenAI-compatible API (OpenAI, Anthropic, Google, Azure, Groq, local models, and more)
- 📋 Commit messages automatically copy to clipboard
- 🔧 Interactive configuration wizard
- 🎨 Custom commit styles with `-p` flag
- 🔄 Self-healing API parameter detection
- 🌍 Cross-platform (Windows, macOS, Linux)
- 📦 No external runtime dependencies
- 🐛 Debug logging and connection testing

## Quick Start

```bash
# Configure your AI provider
gitgen configure

# Generate commit message for staged changes
gitgen

# Guide commit messages
gitgen -p "Must be a haiku"
gitgen -p "Focus just on changes to app.py and ignore other files"
```

## Installation

1. Download the appropriate release for your platform from the [Releases page](https://github.com/stewartcelani/GitGen/releases).
2. Extract the archive.
3. Add the executable to your PATH.
4. Run `gitgen configure` to set up your AI provider.


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
gitgen configure
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
  gitgen [command] [options]

Options:
  -p, --prompt <p>  Custom prompt to focus guide/focus LLM when generating commit message.
  -d, --debug       Enable debug logging.
  -v                Show version information
  --version         Show version information
  -?, -h, --help    Show help and usage information

Commands:
  configure           Run the interactive configuration wizard.
  test                Send 'Testing.' to the LLM and print the response.
  info                Display current configuration information.
  model <model-name>  Change the AI model for the current provider configuration.
  reset               Reset all GitGen environment variables and configuration.
  settings            Quick settings management
  prompt <prompt-text>  Generate commit message with custom prompt instruction.
  health              Display configuration info and test LLM connection.
  help                Display help information.
```

### Changing Models

You can quickly change the AI model for your current provider without reconfiguring the entire setup:

```bash
gitgen model gpt-4o-mini
```

#### Example: Switching from one model to another

```powershell
PS C:\> gitgen info
📋 Current GitGen Configuration:

✅ Configuration Status: Valid

🔧 Configuration Values:
   Provider Type:     openai
   Base URL:          https://openrouter.ai/api/v1/chat/completions
   Model:             qwen/qwen3-coder:free
   API Key:           sk-or-v1...*****************************************************************
   Requires Auth:     True
   Legacy Max Tokens: False
   Temperature:       0.2

🌍 Environment Variables:
   GITGEN_PROVIDERTYPE: openai
   GITGEN_BASEURL: https://openrouter.ai/api/v1/chat/completions
   GITGEN_MODEL: qwen/qwen3-coder:free
   GITGEN_APIKEY: sk-or-v1...*****************************************************************
   GITGEN_REQUIRESAUTH: True
   GITGEN_OPENAI_USE_LEGACY_MAX_TOKENS: False
   GITGEN_TEMPERATURE: 0.2

PS C:\> gitgen model qwen/qwq-32b
🔄 Changing model from 'qwen/qwen3-coder:free' to 'qwen/qwq-32b'...
ℹ️ Keeping provider: openai, Base URL: https://openrouter.ai/api/v1/chat/completions

🧪 Testing new model configuration and detecting optimal parameters...
✅ Parameter detection complete.
ℹ️ Token parameter: Modern (max_completion_tokens)
ℹ️ Temperature: 0.2
✅ Model test successful!
ℹ️ Detected API parameter style: Modern (max_completion_tokens)
ℹ️ Model temperature: 0.2

💾 Saving model configuration changes...
✅ Model configuration updated successfully!
🎯 Now using model: qwen/qwq-32b
⚠️ You may need to restart your terminal for the changes to take effect.
```

### Display Configuration Information

You can check your current configuration by running `gitgen info`. This command displays the loaded settings, including the provider, model, endpoint, and the environment variables being used.

#### Usage Example

```powershell
PS C:\path\to\your\project> gitgen info
📋 Current GitGen Configuration:

✅ Configuration Status: Valid

🔧 Configuration Values:
   Provider Type:     openai
   Base URL:          https://openrouter.ai/api/v1/chat/completions
   Model:             moonshotai/kimi-k2
   API Key:           sk-or-v1...*****************************************************************
   Requires Auth:     True
   Legacy Max Tokens: False
   Temperature:       0.2

🌍 Environment Variables:
   GITGEN_PROVIDERTYPE: openai
   GITGEN_BASEURL: https://openrouter.ai/api/v1/chat/completions
   GITGEN_MODEL: moonshotai/kimi-k2
   GITGEN_APIKEY: sk-or-v1...*****************************************************************
   GITGEN_REQUIRESAUTH: True
   GITGEN_OPENAI_USE_LEGACY_MAX_TOKENS: False
   GITGEN_TEMPERATURE: 0.2
```

### Health Check

The `gitgen health` command combines the configuration display of `gitgen info` with a live connection test to your AI provider. This is useful for ensuring your entire setup is working correctly.

#### Usage Example

```powershell
PS C:\path\to\your\project> gitgen health
📋 Current GitGen Configuration:

✅ Configuration Status: Valid

🔧 Configuration Values:
   Provider Type:     openai
   Base URL:          https://openrouter.ai/api/v1/chat/completions
   Model:             moonshotai/kimi-k2
   API Key:           sk-or-v1...*****************************************************************
   Requires Auth:     True
   Legacy Max Tokens: False
   Temperature:       0.2

🌍 Environment Variables:
   GITGEN_PROVIDERTYPE: openai
   GITGEN_BASEURL: https://openrouter.ai/api/v1/chat/completions
   GITGEN_MODEL: moonshotai/kimi-k2
   GITGEN_APIKEY: sk-or-v1...*****************************************************************
   GITGEN_REQUIRESAUTH: True
   GITGEN_OPENAI_USE_LEGACY_MAX_TOKENS: False
   GITGEN_TEMPERATURE: 0.2


═════════════════════════════════════════════════════════════

🧪 Testing LLM connection...
🔗 Using OpenAI provider via https://openrouter.ai/api/v1/chat/completions (moonshotai/kimi-k2)

✅ LLM Response:
"Test received—I'm here and ready. What's on your mind?"

Generated with 9 input tokens, 14 output tokens (23 total) • 54 characters

🎉 Test completed successfully!
```

## Provider Configuration Examples

The interactive `gitgen configure` command provides three main presets for setting up your AI provider. Below are detailed examples for each one.

### 1. OpenAI (Official Platform)

Use this option for the official OpenAI API.

#### Configuration

You will be prompted for your API key and to select a model.

```powershell
PS C:\> gitgen configure
...
Step 1: Select your provider's API compatibility type.
1. OpenAI Compatible (e.g., OpenAI, Azure, Groq, Ollama)
   Enter your choice: [1]

Step 2: Select your specific provider preset.
1. OpenAI (Official Platform)
2. Custom Provider (API Key required, e.g., Azure, Anthropic, Groq)
3. Custom Provider (No API Key required, e.g., Ollama, LM Studio)
   Enter your choice: [1]
   Enter your model name: [o4-mini] gpt-4.1-nano
   Enter your OpenAI API Key: ********************************************************
...
✅ Configuration saved successfully!
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

📋 Commit message copied to clipboard.
```

### 2. Custom Provider (API Key Required) - Examples: Azure, Anthropic, Google, Groq, OpenRouter

This preset is for any third-party service that offers an OpenAI-compatible API and requires an API key. **This includes Microsoft Azure**, Anthropic, Google, Groq, OpenRouter, Together AI, and others.

#### Example: Microsoft Azure OpenAI

##### Configuration

You will need your Azure endpoint URL, deployment name (which acts as the model name), and Azure API key. Select option `2` for a custom provider with an API key.

```powershell
PS C:\> gitgen configure
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
PS C:\> gitgen configure
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

##### Usage Example (Anthropic with claude-sonnet-4-20250514)

```powershell
PS C:\path\to\your\project> gitgen
Found 1 changed files
⏳ Generating commit message...

Using OpenAI provider (https://api.anthropic.com/v1/chat/completions, claude-sonnet-4-20250514) to generate commit message
✅ Generated Commit Message:
"Remove -Clean parameter from publish script and automatically clean up source folders after zipping. The script now removes the -Clean switch parameter, moves folder cleanup logic inside the zipping block to always execute after creating ZIP archives, updates comment to reflect automatic cleanup, and simplifies usage examples by removing the -Clean option reference."

Generated with 2,086 input tokens, 67 output tokens (2,153 total) • 368 characters

📋 Commit message copied to clipboard.
```

#### Example: Google

##### Configuration

You will need the provider's "Chat Completions" URL, the model name, and the provider's API key. Google's Generative AI offers an OpenAI-compatible endpoint.

```powershell
PS C:\> gitgen configure
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
   Enter the provider's chat completions URL (e.g., your Azure endpoint): https://generativelanguage.googleapis.com/v1beta/openai/chat/completions
   Enter the model name (e.g., your Azure deployment name): gemini-2.5-flash
   Enter the provider's API Key: ***************************************
...
✅ Configuration saved successfully!
```

##### Usage Example (Google with gemini-2.5-flash)

```powershell
PS C:\path\to\your\project> gitgen
Found 18 changed files
⏳ Generating commit message...

Using OpenAI provider (https://generativelanguage.googleapis.com/v1beta/openai/chat/completions, gemini-2.5-flash) to generate commit message
✅ Generated Commit Message:
"Refactor TTS app to batch processing, improving text chunking and adding content fetching. Replaced old FastAPI `app.py` with `src/app.py` for file-based processing. Tokenization logic refined (hardcoded `MAX_TOKENS=1900`). Added web scrapers, tokenizer & test tools. Reqs updated, dirs restructured."

Generated with 19,430 input tokens, 75 output tokens (21,788 total) • 300 characters

📋 Commit message copied to clipboard.
```

#### Example: Groq

##### Configuration

You will need the provider's "Chat Completions" URL, the model name, and the provider's API key.

```powershell
PS C:\> gitgen configure
...
Step 2: Select your specific provider preset.
...
   Enter your choice: [1] 2
   Enter the provider's chat completions URL (e.g., your Azure endpoint): https://api.groq.com/openai/v1/chat/completions
   Enter the model name (e.g., your Azure deployment name): deepseek-r1-distill-llama-70b
   Enter the provider's API Key: ********************************************************
...
✅ Configuration saved successfully!
```

##### Usage Example (Groq with deepseek-r1-distill-llama-70b)

```powershell
PS C:\path\to\your\project> gitgen
Found 19 changed files
⏳ Generating commit message...

Using OpenAI provider (https://api.groq.com/openai/v1/chat/completions, deepseek-r1-distill-llama-70b) to generate commit message
✅ Generated Commit Message:
"Improve code quality and maintainability by extracting duplicated environment persistence logic into a dedicated service, simplifying ConsoleLogger, removing magic values, enhancing security, adding comprehensive input validation, and improving error handling and configuration management."

Generated with 76,378 input tokens, 211 output tokens (76,589 total) • 289 characters

📋 Commit message copied to clipboard.
```

#### Example: OpenRouter

##### Configuration

```powershell
PS C:\> gitgen configure
...
Step 2: Select your specific provider preset.
...
   Enter your choice: [1] 2
   Enter the provider's chat completions URL (e.g., your Azure endpoint): https://openrouter.ai/api/v1/chat/completions
   Enter the model name (e.g., your Azure deployment name): moonshotai/kimi-k2
   Enter the provider's API Key: *************************************************************************
...
✅ Configuration saved successfully!
```

##### Usage Example (OpenRouter with moonshotai/kimi-k2)

```powershell
PS C:\path\to\your\project> gitgen
Found 18 changed files
⏳ Generating commit message...

Using OpenAI provider (https://openrouter.ai/api/v1/chat/completions, moonshotai/kimi-k2) to generate commit message
✅ Generated Commit Message:
"Add Claude settings and improve TTS chunking with actual tokenizer usage. Replaced MAX_TOKENS calculation with get_actual_token_count() using model.tokenizer.encode(). Enhanced split_into_safe_chunks() with better sentence splitting, clause fallback, and truncation for oversized chunks. Added debug logging throughout generation. Added get_lit_story.ps1 for story fetching and reorganized input/output directories."

Generated with 16,736 input tokens, 76 output tokens (16,812 total) • 415 characters

📋 Commit message copied to clipboard.
```

### 3. Custom Provider (No API Key) - Example: LM Studio / Ollama

This is the perfect choice for running models locally using tools like LM Studio, Ollama, Jan, etc. These tools typically expose an OpenAI-compatible server on your local machine that doesn't require an API key.

#### Configuration

You'll need the local server URL and the name of the loaded model. The wizard provides a common default URL (`http://localhost:11434/v1/chat/completions`).

```powershell
PS C:\> gitgen configure
...
Step 2: Select your specific provider preset.
...
   Enter your choice: [1] 3
   Enter your custom provider's chat completions URL: [http://localhost:11434/v1/chat/completions] http://localhost:1234/v1/chat/completions
   Enter the model name (e.g., llama3): qwen2.5-3b-instruct
...
✅ Configuration saved successfully!
```

#### Usage Example (LM Studio with qwen2.5-3b-instruct)

```powershell
PS C:\path\to\your\project> gitgen
Found 5 changed files
⏳ Generating commit message...

Using OpenAI provider (http://localhost:1234/v1/chat/completions, qwen2.5-3b-instruct) to generate commit message
✅ Generated Commit Message:
"Added comprehensive commit message generation with OpenAI provider, simplified `Program.cs` methods, and updated configuration wizard for user-friendly model changes. Consolidated codebase improvements maintain security, extensibility, and ease of use."

Generated with 6,193 input tokens, 47 output tokens (6,240 total) • 262 characters

📋 Commit message copied to clipboard.
```

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

## Building & Publishing (For Developers)

This section is for developers who want to build the project from source.

### Prerequisites

- .NET 9.0 Runtime or SDK

### Publish Script

Run the included PowerShell script from the project root. This script creates self-contained, single-file executables for all supported platforms (Windows, Linux, macOS).

```powershell
PS C:\path\to\your\project> .\publish.ps1
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
