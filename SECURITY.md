# Security Policy

## Supported Versions

The following versions of GitGen are currently being supported with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 2.0.x   | :white_check_mark: |
| 1.x.x   | :x:                |

## Reporting a Vulnerability

We take the security of GitGen seriously. If you believe you have found a security vulnerability, please report it to us as described below.

### How to Report

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them via:

1. **GitHub Security Advisories (Preferred)**
   - Go to the [Security tab](https://github.com/stewartcelani/GitGen/security) of this repository
   - Click "Report a vulnerability"
   - Fill out the form with details

2. **Email**
   - Send details to: [maintainer email - update this]
   - Use subject line: "SECURITY: GitGen Vulnerability Report"

### What to Include

Please include the following information:

- Type of issue (e.g., buffer overflow, SQL injection, cross-site scripting, etc.)
- Full paths of source file(s) related to the manifestation of the issue
- The location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

### Response Timeline

- **Initial Response**: Within 48 hours
- **Status Update**: Within 5 business days
- **Resolution Target**: 
  - Critical: 7 days
  - High: 14 days
  - Medium: 30 days
  - Low: 90 days

## Security Measures

GitGen implements several security measures:

### API Key Protection
- All API keys are encrypted using platform-specific encryption
- Windows: DPAPI (Data Protection API)
- macOS: Keychain Services
- Linux: Kernel keyring
- Keys are never stored in plain text

### Secure Communications
- All API communications use HTTPS
- Certificate validation is enforced
- No telemetry or usage data is collected

### Code Security
- Regular dependency updates via Dependabot
- Automated security scanning with CodeQL
- Security-focused code reviews

### Best Practices for Users

1. **Keep GitGen Updated**
   - Always use the latest version
   - Enable automatic updates if available

2. **Protect Your Configuration**
   - Don't share your `models.json` file
   - Use different API keys for different environments
   - Regularly rotate API keys

3. **Be Cautious with Free Models**
   - Only use free/public models for public repositories
   - Never send proprietary code to free API endpoints
   - Use the alias system to clearly mark free models

## Security Features

### Automated Scanning
- **CodeQL**: Scans for common vulnerabilities
- **Dependabot**: Monitors and updates dependencies
- **OpenSSF Scorecard**: Evaluates security practices

### Secure by Design
- Single executable with no external dependencies
- No network calls except to configured AI providers
- No automatic updates or phoning home
- Clear warnings for insecure configurations

## Acknowledgments

We appreciate security researchers who help keep GitGen and our users safe. Responsible disclosure is appreciated and will be acknowledged in our release notes (unless you prefer to remain anonymous).

## Contact

For any security-related questions, please contact:
- GitHub Security Advisories (preferred)
- Email: [update with maintainer email]

Thank you for helping keep GitGen secure!