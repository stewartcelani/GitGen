# CI/CD Setup for GitGen

This directory contains the GitHub Actions workflows for automated testing and deployment of GitGen.

## Workflow: `ci.yml`

The main CI workflow runs on:
- Every push to `master` or `main` branch
- Every pull request targeting `master` or `main`
- Manual workflow dispatch

### Jobs

#### 1. Build and Test
- **Matrix Strategy**: Tests on Ubuntu, Windows, and macOS
- **Steps**:
  - Checkout code with full history
  - Setup .NET 9.0
  - Cache NuGet packages for faster builds
  - Restore dependencies
  - Build in Release mode
  - Run tests with XPlat Code Coverage
  - Upload coverage to Codecov
  - Upload test results as artifacts

#### 2. Code Quality
- Runs on Ubuntu (fastest)
- Checks code formatting
- Runs .NET analyzers with warnings as errors

#### 3. Publish Test
- Verifies the application can be published for all platforms
- Tests single-file, self-contained, trimmed builds
- Targets: win-x64, linux-x64, osx-x64, osx-arm64

#### 4. Security Scan (CodeQL)
- Runs on Ubuntu (optimal performance)
- Performs static application security testing (SAST)
- Scans for:
  - Security vulnerabilities
  - Code quality issues
  - Common programming errors
- Results appear in GitHub's Security tab
- Free for public repositories

## Setting up Dependabot

Dependabot is GitHub's automated dependency update service:

1. Go to Settings > Code security and analysis
2. Enable:
   - Dependabot alerts
   - Dependabot security updates
   - Dependabot version updates (optional)

Dependabot will:
- Monitor NuGet packages for security vulnerabilities
- Create automatic PRs to update vulnerable dependencies
- Show security alerts in the Security tab

## Setting up Codecov

1. The repository owner should:
   - Go to https://codecov.io/
   - Sign in with GitHub
   - Add the repository
   - Copy the upload token
   - Add it as a GitHub secret named `CODECOV_TOKEN`

2. For public repositories:
   - Codecov works without authentication
   - The token in the badge URL can be removed

## Badge URLs

After the first successful run:
- **Build Status**: `![Build Status](https://github.com/stewartcelani/GitGen/actions/workflows/ci.yml/badge.svg)`
- **Code Coverage**: `[![codecov](https://codecov.io/gh/stewartcelani/GitGen/graph/badge.svg)](https://codecov.io/gh/stewartcelani/GitGen)`

## Local Testing

To test the workflow locally, you can use [act](https://github.com/nektos/act):
```bash
# Install act
# Windows: choco install act-cli
# macOS: brew install act
# Linux: See https://github.com/nektos/act

# Run the workflow
act push

# Run a specific job
act -j build-and-test
```

## Monitoring

- Check workflow runs: https://github.com/stewartcelani/GitGen/actions
- View coverage reports: https://codecov.io/gh/stewartcelani/GitGen