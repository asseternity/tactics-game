# JSON Editor Plugin for Godot 4.x

A powerful and user-friendly JSON editor plugin for Godot Engine 4.x that provides a comprehensive visual interface for editing JSON files with multi-language support and Excel-style table functionality.

## ✨ Features

### Core Functionality

- **Visual tree view** for JSON structure with hierarchical display
- **Excel-style table view** for data manipulation and editing
- **Direct JSON text editing** with syntax highlighting
- **Dual-view interface** with seamless switching between tree and table views
- **File management** (load/save) with integrated file browser
- **Type-safe value editing** with validation

### Data Types Support

- **Strings** with text editing
- **Numbers** (integers and floats) with validation
- **Booleans** (true/false) with dropdown selection
- **Arrays** with dynamic element management
- **Objects (Dictionaries)** with key-value pair editing

### Advanced Features

- **Multi-language support** (Chinese/English) with real-time switching
- **Excel-style table editing** with row/column operations
- **Real-time JSON validation** with error reporting
- **Add/Remove rows** functionality in table view
- **Column type editing** for data structure management
- **Integrated translation system** for internationalization
- **Seamless Godot editor integration**

## 🚀 Installation

1. **Download**: Copy the `addons/json_editor` folder to your Godot project
2. **Enable**: Go to `Project Settings -> Plugins` and enable "JSON Editor"
3. **Access**: Find the JSON Editor tab in the main editor screen

## 📖 Usage

### Basic Operations

1. **File Operations**:

   - Use the "Browse..." button to select JSON files
   - Click "Load" to open and parse JSON files
   - Click "Save" to write changes back to file

2. **Language Switching**:

   - Click "中文" for Chinese interface
   - Click "English" for English interface
   - All UI elements update in real-time

3. **Editing Modes**:
   - **Tree View**: Hierarchical display with double-click editing
   - **Table View**: Excel-style editing for structured data
   - **Text Editor**: Direct JSON text manipulation

### Advanced Operations

- **Tree View**:

  - Double-click items to edit values
  - Add new key-value pairs to objects
  - Add elements to arrays
  - Delete existing items
  - Type-safe editing with validation

- **Table View**:

  - Click "Add Row" to append new data rows
  - Edit cells directly in Excel-style interface
  - Column headers show data structure
  - Automatic data type detection and conversion

- **Text Editor**:
  - Direct JSON text editing
  - Real-time syntax validation
  - Automatic formatting and indentation

## 🌍 Multi-Language Support

The plugin includes comprehensive internationalization support:

- **Languages**: Chinese (中文) and English
- **Translation Coverage**: All UI elements, buttons, labels, and messages
- **Real-time Switching**: Language changes apply immediately without restart
- **Translation Files**: Standard GNU gettext (.po) format for easy localization

### Supported UI Elements

- Button labels (Load, Save, Browse, Add Row)
- Tab titles (Tree View, Table View)
- Column headers (Key, Value)
- Dialog labels and messages
- Input placeholders and tooltips

## 🎯 Use Cases

- **Game Configuration**: Edit game settings and configuration files
- **Data Management**: Manage player data, level definitions, item databases
- **Localization**: Edit translation files and language data
- **API Testing**: Create and modify API request/response data
- **Content Creation**: Manage game content in structured JSON format

## 🔧 Technical Details

- **Engine Compatibility**: Godot 4.x (tested with 4.5.beta1)
- **File Format**: Standard JSON with full specification support
- **Memory Efficient**: Streams large files without loading entire content
- **Type Safety**: Automatic data type validation and conversion
- **Error Handling**: Comprehensive error reporting and recovery

## 📝 Release Notes

### Version 2.0.0 (Latest)

- ✅ Added multi-language support (Chinese/English)
- ✅ Integrated Excel-style table view
- ✅ Enhanced UI with language switching buttons
- ✅ Improved translation system
- ✅ Better error handling and validation
- ✅ Comprehensive documentation

### Version 1.0.0

- Basic JSON editing functionality
- Tree view and text editor
- File load/save operations

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## 📄 License

MIT License - See LICENSE file for details

## 👨‍💻 Author

**meishijie**

## 📦 Version

2.0.0
