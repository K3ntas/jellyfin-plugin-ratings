# Contributing to Jellyfin Ratings Plugin

Thank you for considering contributing to the Jellyfin Ratings Plugin! This document provides guidelines for contributing to the project.

## Repository

- **GitHub**: https://github.com/jellyfinratings/jellyfin-plugin-ratings
- **Issues**: https://github.com/jellyfinratings/jellyfin-plugin-ratings/issues
- **Pull Requests**: https://github.com/jellyfinratings/jellyfin-plugin-ratings/pulls

## How to Contribute

### Reporting Bugs

1. Check [existing issues](https://github.com/jellyfinratings/jellyfin-plugin-ratings/issues) to avoid duplicates
2. Create a new issue with:
   - Clear, descriptive title
   - Jellyfin version
   - Plugin version
   - Steps to reproduce
   - Expected vs actual behavior
   - Relevant logs from Dashboard → Advanced → Logs

### Suggesting Features

1. Check [existing issues](https://github.com/jellyfinratings/jellyfin-plugin-ratings/issues) for similar suggestions
2. Create a new issue with:
   - Clear description of the feature
   - Use case and benefits
   - Potential implementation approach (optional)

### Contributing Code

#### Setup Development Environment

1. Fork the repository on GitHub
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/jellyfin-plugin-ratings.git
   cd jellyfin-plugin-ratings
   ```
3. Add upstream remote:
   ```bash
   git remote add upstream https://github.com/jellyfinratings/jellyfin-plugin-ratings.git
   ```
4. Install .NET 8.0 SDK
5. Build the project:
   ```bash
   dotnet build
   ```

#### Making Changes

1. Create a new branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. Make your changes
3. Test thoroughly:
   - Build succeeds
   - Plugin loads in Jellyfin
   - New features work as expected
   - Existing features still work
4. Commit with clear messages:
   ```bash
   git commit -m "Add feature: description of what you added"
   ```

#### Code Style

- Follow C# naming conventions
- Use meaningful variable and method names
- Add XML documentation comments to public methods
- Keep methods focused and concise
- Handle errors gracefully
- Add logging for important operations

#### Submitting Pull Request

1. Push your branch to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```
2. Create a Pull Request on GitHub with:
   - Clear description of changes
   - Reference related issues (e.g., "Fixes #123")
   - Screenshots if UI changes
   - Test results
3. Wait for review and address feedback

### Code Review Process

- Maintainers will review PRs as time permits
- Be patient and responsive to feedback
- PRs may require changes before merging
- Once approved, maintainers will merge

## Development Guidelines

### Backend Development

- **Location**: `Api/`, `Data/`, `Models/`
- **Language**: C# (.NET 8.0)
- **Framework**: Jellyfin 10.11.0 SDK
- **Testing**: Test with actual Jellyfin instance

### Frontend Development

- **Location**: `Web/ratings.js`
- **Language**: Vanilla JavaScript (ES6+)
- **No Dependencies**: Must work without external libraries
- **Browser Compatibility**: Test in major browsers
- **API Communication**: Use Jellyfin's ApiClient

### Configuration

- **Location**: `Configuration/`
- **Settings**: Add to PluginConfiguration.cs
- **UI**: Update configPage.html
- **Defaults**: Provide sensible defaults

## Testing Checklist

Before submitting, verify:

- [ ] Code compiles without warnings
- [ ] Plugin loads in Jellyfin 10.11.0
- [ ] New features work as expected
- [ ] Existing features still work
- [ ] Configuration page loads
- [ ] API endpoints respond correctly
- [ ] JavaScript has no console errors
- [ ] Data persists correctly
- [ ] Works with multiple users
- [ ] Handles errors gracefully

## Security

If you discover a security vulnerability:

1. **DO NOT** open a public issue
2. Email security concerns privately (contact repository owner)
3. Provide detailed information
4. Allow time for a fix before public disclosure

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Questions?

- Open a [Discussion](https://github.com/jellyfinratings/jellyfin-plugin-ratings/discussions)
- Create an [Issue](https://github.com/jellyfinratings/jellyfin-plugin-ratings/issues)
- Check the [README](README.md) for documentation

## Thank You!

Your contributions help make this plugin better for everyone in the Jellyfin community!
