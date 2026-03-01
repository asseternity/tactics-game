# Contributing to JSON Editor Plugin

Thank you for your interest in contributing to the JSON Editor plugin! We welcome contributions from the community.

## 🤝 How to Contribute

### Reporting Issues

- Use the GitHub issue tracker to report bugs
- Provide detailed information about your environment (Godot version, OS, etc.)
- Include steps to reproduce the issue
- Attach screenshots or sample files if relevant

### Suggesting Features

- Open an issue with the "enhancement" label
- Describe the feature and its benefits
- Provide use cases and examples
- Discuss implementation approaches if you have ideas

### Code Contributions

#### Getting Started

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Make your changes
4. Test your changes thoroughly
5. Commit with clear messages
6. Push to your fork
7. Open a pull request

#### Development Guidelines

##### Code Style

- Follow GDScript style guidelines
- Use consistent indentation (tabs)
- Add comments for complex logic
- Use meaningful variable and function names

##### File Organization

- Keep related functionality together
- Separate UI logic from data logic
- Use proper file naming conventions
- Maintain the existing folder structure

##### Translation Support

- Add new UI text to translation files (`zh.po`, `en.po`)
- Use `TranslationManager.get_text()` for all user-facing text
- Test language switching functionality
- Ensure all text is translatable

#### Testing

- Test your changes with different JSON file types
- Verify multi-language functionality works correctly
- Test both tree view and table view modes
- Ensure no regressions in existing features

#### Documentation

- Update README.md if adding new features
- Add entries to CHANGELOG.md for significant changes
- Include code comments for complex functionality
- Update plugin.cfg version if needed

## 🌍 Translation Contributions

We welcome translations to additional languages!

### Adding a New Language

1. Create a new `.po` file in `addons/json_editor/translations/`
2. Use the format: `language_code.po` (e.g., `fr.po` for French)
3. Copy the structure from existing translation files
4. Translate all `msgstr` values
5. Update `TranslationManager.gd` to include the new language
6. Test the translation in the editor

### Translation Guidelines

- Keep translations concise and user-friendly
- Maintain consistent terminology throughout
- Consider cultural context and conventions
- Test translations with actual UI to ensure they fit properly

## 📋 Pull Request Guidelines

### Before Submitting

- [ ] Code follows project style guidelines
- [ ] All tests pass
- [ ] Translation files are updated if needed
- [ ] Documentation is updated
- [ ] Commit messages are clear and descriptive

### Pull Request Template

```
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Documentation update
- [ ] Translation update
- [ ] Performance improvement

## Testing
- [ ] Tested in Godot editor
- [ ] Verified language switching works
- [ ] Tested with various JSON file types
- [ ] No regressions found

## Screenshots (if applicable)
Attach screenshots showing the changes

## Additional Notes
Any additional information or context
```

## 🎯 Priority Areas

We're particularly interested in contributions in these areas:

### High Priority

- Additional language translations
- Performance improvements for large JSON files
- Better error handling and user feedback
- Accessibility improvements

### Medium Priority

- Dark/Light theme support
- Undo/Redo functionality
- Advanced search and filtering
- Import/Export to other formats

### Future Features

- JSON schema validation
- Visual schema editor
- Plugin settings/preferences
- Cloud integration

## 💬 Communication

- Use GitHub issues for bug reports and feature requests
- Keep discussions focused and constructive
- Be respectful of different viewpoints and experience levels
- Help others when you can

## ⚖️ Code of Conduct

### Our Standards

- Be respectful and inclusive
- Focus on constructive feedback
- Help create a welcoming environment
- Respect different levels of experience

### Unacceptable Behavior

- Harassment or discrimination
- Trolling or insulting comments
- Publishing private information
- Inappropriate or unprofessional conduct

## 🙏 Recognition

Contributors will be acknowledged in:

- CHANGELOG.md for significant contributions
- README.md contributors section
- Release notes for major features

Thank you for helping make the JSON Editor plugin better for everyone!
