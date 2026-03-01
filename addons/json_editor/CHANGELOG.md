# Changelog

All notable changes to the JSON Editor plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2024-12-19

### 🎉 Major Features Added

#### Multi-Language Support

- **NEW**: Complete internationalization system with Chinese and English support
- **NEW**: Real-time language switching buttons in main toolbar
- **NEW**: GNU gettext (.po) translation files for easy localization
- **NEW**: TranslationManager system for centralized translation handling
- **NEW**: All UI elements now support multi-language (buttons, labels, tooltips, placeholders)

#### Excel-Style Table View

- **NEW**: Dual-view interface with Tree View and Table View tabs
- **NEW**: Excel-style table editing with direct cell modification
- **NEW**: "Add Row" functionality for appending new data entries
- **NEW**: Column type editing and management
- **NEW**: Automatic data type detection and conversion
- **NEW**: Seamless switching between tree and table views

#### Enhanced User Interface

- **NEW**: Language selection buttons (中文/English) in header toolbar
- **NEW**: Visual feedback for current language selection (disabled state + gray appearance)
- **NEW**: Improved header layout with proper spacing and separators
- **NEW**: Better organization of UI elements

### 🔧 Technical Improvements

#### Code Architecture

- **IMPROVED**: Modular translation system with centralized management
- **IMPROVED**: Better signal handling and disconnection management
- **IMPROVED**: Enhanced error handling and validation
- **IMPROVED**: Improved component communication between table and main editor
- **IMPROVED**: Type-safe value editing with comprehensive validation

#### Data Management

- **IMPROVED**: Better JSON parsing and validation
- **IMPROVED**: Enhanced file loading and saving mechanisms
- **IMPROVED**: Improved data structure handling for complex JSON
- **IMPROVED**: Better memory management for large files

### 📚 Documentation

- **NEW**: Comprehensive README with feature descriptions and usage examples
- **NEW**: Multi-language support documentation
- **NEW**: Technical details and use cases
- **NEW**: Release notes and changelog
- **IMPROVED**: Better code comments and documentation

### 🐛 Bug Fixes

- **FIXED**: String formatting errors in various components
- **FIXED**: Translation key consistency issues
- **FIXED**: Signal connection/disconnection edge cases
- **FIXED**: Data type conversion validation
- **FIXED**: UI element update synchronization

### 📦 Files Added/Modified

#### New Files

- `addons/json_editor/translations/zh.po` - Chinese translations
- `addons/json_editor/translations/en.po` - English translations
- `addons/json_editor/scripts/translation_manager.gd` - Translation management system
- `addons/json_editor/CHANGELOG.md` - This changelog file

#### Modified Files

- `addons/json_editor/scripts/json_editor.gd` - Enhanced with multi-language support
- `addons/json_editor/scripts/excel_table.gd` - Improved table functionality
- `addons/json_editor/scenes/json_editor.tscn` - Added language switching buttons
- `addons/json_editor/README.md` - Comprehensive documentation update
- `addons/json_editor/plugin.cfg` - Version and description update

### 🎯 Breaking Changes

- **BREAKING**: Updated plugin version from 1.0.0 to 2.0.0
- **BREAKING**: TranslationManager is now required for proper functionality
- **NOTE**: Existing projects will need to reload the plugin to access new features

---

## [1.0.0] - 2024-12-01

### 🎉 Initial Release

#### Core Features

- **NEW**: Visual tree view for JSON structure display
- **NEW**: Direct JSON text editing with syntax highlighting
- **NEW**: File management (load/save) functionality
- **NEW**: Type-safe value editing with validation
- **NEW**: Support for all JSON data types (String, Number, Boolean, Array, Object)
- **NEW**: Real-time JSON validation and error reporting
- **NEW**: Integrated Godot editor plugin architecture

#### Basic Operations

- **NEW**: Load JSON files from filesystem
- **NEW**: Save changes back to JSON files
- **NEW**: Add/Remove items from JSON structure
- **NEW**: Edit values with type validation
- **NEW**: Double-click editing in tree view

#### Technical Foundation

- **NEW**: Godot 4.x compatibility
- **NEW**: Plugin architecture with proper lifecycle management
- **NEW**: Error handling and user feedback
- **NEW**: Basic UI layout and component organization

---

## 🔮 Future Plans

### Planned Features (v2.1.0)

- [ ] Additional language support (Spanish, French, German)
- [ ] Dark/Light theme support
- [ ] Import/Export to other formats (XML, CSV, YAML)
- [ ] Advanced search and filtering capabilities
- [ ] Undo/Redo functionality
- [ ] JSON schema validation
- [ ] Plugin settings and preferences

### Long-term Goals (v3.0.0)

- [ ] Visual JSON schema editor
- [ ] Collaborative editing support
- [ ] Cloud integration
- [ ] Advanced data visualization
- [ ] Custom data type extensions
- [ ] API integration capabilities

---

**Note**: This changelog follows semantic versioning. Major version changes indicate breaking changes, minor versions add features, and patch versions fix bugs.
