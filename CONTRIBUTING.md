# Contributing to InControl-Desktop

Thank you for your interest in contributing! This document provides guidelines for contributing to InControl-Desktop.

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Assume good intentions

## How to Contribute

### Reporting Bugs

1. **Search existing issues** to avoid duplicates
2. **Use the bug report template** when creating issues
3. **Include**:
   - Clear steps to reproduce
   - Expected vs actual behavior
   - Version and system information
   - Relevant log snippets

### Suggesting Features

1. **Check existing feature requests** first
2. **Describe the problem** you're solving
3. **Explain your proposed solution**
4. **Consider alternatives** and trade-offs

### Pull Requests

1. **Fork the repository**
2. **Create a feature branch** from `main`
3. **Follow the coding standards** below
4. **Write tests** for new functionality
5. **Update documentation** if needed
6. **Submit a PR** with a clear description

## Development Setup

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or VS Code with C# extension
- Windows 10/11 SDK
- Git

### Building

```powershell
# Clone the repository
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol

# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test
```

### Running Locally

```powershell
dotnet run --project src/InControl.App
```

## Coding Standards

### C# Style

- Use file-scoped namespaces
- Prefer records for immutable data
- Use nullable reference types
- Follow standard C# naming conventions

### Architecture

- MVVM pattern for UI
- Repository pattern for data access
- Dependency injection throughout
- Local-first, offline-capable design

### Testing

- xUnit for unit tests
- FluentAssertions for readable assertions
- Aim for >80% code coverage on new code
- Mock external dependencies

### Documentation

- XML comments on public APIs
- README updates for new features
- Inline comments for complex logic

## Project Structure

```
InControl-Desktop/
├── src/
│   ├── InControl.App/        # WinUI 3 application
│   ├── InControl.Core/       # Core domain logic
│   ├── InControl.Inference/  # LLM integration
│   ├── InControl.Services/   # Application services
│   └── InControl.ViewModels/ # MVVM ViewModels
├── tests/
│   └── InControl.Core.Tests/ # Unit tests
├── docs/                     # Documentation
└── packaging/                # MSIX packaging
```

## Pull Request Process

1. **Create a focused PR** - one feature or fix per PR
2. **Update CHANGELOG.md** for notable changes
3. **Ensure CI passes** - all tests must pass
4. **Request review** from maintainers
5. **Address feedback** promptly
6. **Squash commits** if requested

### PR Title Format

```
type(scope): description

Examples:
feat(assistant): add memory search capability
fix(chat): resolve streaming cutoff issue
docs(readme): update installation instructions
refactor(core): simplify error handling
```

### Types

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting, no code change
- `refactor`: Code change, no feature/fix
- `test`: Adding tests
- `chore`: Maintenance tasks

## Trust Boundaries

InControl-Desktop has strict trust boundaries. Changes that affect these areas require extra scrutiny:

### Requires Careful Review

- Network connectivity code
- Update mechanism
- Data persistence
- Permission systems
- Audit logging

### Forbidden Changes

- Silent network activity
- Automatic updates without consent
- Data collection without disclosure
- Bypassing permission checks

See [RELEASE_CHARTER.md](./docs/RELEASE_CHARTER.md) for the complete trust envelope.

## Getting Help

- **Questions**: Open a Discussion
- **Bugs**: Open an Issue
- **Chat**: [Community channel if applicable]

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to InControl-Desktop!
