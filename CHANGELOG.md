# Changelog

All notable changes to GitGen will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2025-01-27

### Added

#### Multi-Model Support
- Support for unlimited AI models with individual configurations
- Model aliases for quick switching (e.g., `@fast`, `@smart`, `@reasoning`)
- Partial alias matching for convenience
- Per-model custom system prompts
- Per-model pricing configuration for cost tracking
- Context length configuration per model

#### Secure Configuration Storage
- Platform-specific encrypted storage using:
  - Windows: DPAPI (Data Protection API)
  - macOS: Keychain Services
  - Linux: Kernel keyring
- Automatic migration from v1.x environment variables
- Self-healing configuration with automatic recovery

#### Interactive Configuration Menu
- New `gitgen config` command with full-featured menu
- Add, edit, delete, and manage models interactively
- Test configurations before saving
- Visual feedback for all operations
- Model aliasing management
- Usage statistics and reporting

#### Enhanced User Experience
- Preview mode (`-p/--preview`) to see what will be sent to the AI
- Prompt confirmation option before sending to AI
- Free model warnings to prevent accidental proprietary code exposure
- Token usage display with cost estimates
- Improved error messages with actionable guidance
- Culture-aware date/time formatting

#### Context Length Management
- Automatic detection of context length errors
- Smart diff truncation to fit within model limits
- Retry mechanism with truncated content
- Detailed token usage breakdown on errors

#### Developer Experience
- Comprehensive test suite with xUnit, FluentAssertions, and NSubstitute
- CI/CD pipeline with multi-platform testing
- Code quality checks with CodeQL
- Test coverage reporting with Codecov
- CLAUDE.md file for AI-assisted development

#### Architecture Improvements
- Clean architecture with dependency injection
- Service abstractions for better testability
- Unified OpenAI-compatible provider for all AI services
- HTTP client service with retry logic using Polly
- Comprehensive error handling with custom exceptions

### Changed

#### Breaking Changes
- Configuration format completely redesigned (automatic migration provided)
- Environment variables no longer supported (migrated to secure storage)
- Provider type is now always "openai-compatible"
- Command-line syntax enhanced to support `@model` notation

#### Improvements
- Significantly enhanced configuration wizard
- Better handling of API parameter variations
- Improved clipboard integration
- More robust error recovery
- Enhanced debug logging with `-d/--debug` flag

### Removed
- Legacy environment variable support
- Separate provider types (now unified as "openai-compatible")
- Direct OpenAI/Anthropic provider classes (consolidated)

### Fixed
- API parameter detection for various providers
- Null reference exceptions in edge cases
- Context length handling for different AI providers
- Configuration corruption recovery
- Cross-platform path handling

### Security
- All API keys encrypted at rest
- No plain text storage of sensitive data
- Platform-specific secure storage mechanisms
- Clear warnings for insecure configurations
- Updated security policy with contact information

## [1.0.0] - 2024-12-15

### Added
- Initial release of GitGen
- Basic Git commit message generation
- Support for OpenAI and Anthropic Claude
- Environment variable configuration
- Simple configuration wizard
- Clipboard integration
- Cross-platform support (Windows, macOS, Linux)
- Single executable distribution
- Basic error handling

### Known Issues in v1.0.0 (Fixed in v2.0.0)
- API keys stored in environment variables (less secure)
- Limited to single model configuration
- No cost tracking or token usage display
- Basic error messages without guidance
- No context length handling

---

## Upgrade Guide

### From v1.x to v2.0.0

1. **Automatic Migration**: On first run, v2.0.0 will automatically detect and migrate your v1.x configuration from environment variables to secure storage.

2. **New Configuration**: Run `gitgen config` to explore the new configuration menu and add additional models.

3. **Model Aliases**: Set up convenient aliases for your models:
   ```bash
   gitgen config  # Then choose "Add alias to model"
   ```

4. **New Usage Patterns**:
   ```bash
   # Use specific model
   gitgen @gpt-4
   
   # Preview what will be sent
   gitgen -p
   
   # With custom instruction
   gitgen "Add tests" @fast
   ```

5. **Security**: Your API keys are now encrypted. The old environment variables can be safely removed after migration.

## Support

For issues, feature requests, or questions:
- GitHub Issues: https://github.com/stewartcelani/GitGen/issues
- Security Issues: See SECURITY.md

## Contributors

- Stewart Celani (@stewartcelani) - Creator and maintainer

Special thanks to all contributors and users who provided feedback for v2.0.0!