# WebREPL Commander - Norton Commander Interface

A classic Norton Commander-style dual-pane file manager for ESP8266/ESP32 devices running MicroPython with WebREPL.

## Features

### Classic Norton Commander Interface
- **Dual-Pane Layout**: Local files on the left, remote device files on the right
- **Classic DOS-style Theme**: Blue background, cyan borders, yellow text
- **Function Key Commands**: F3-F12 mapped to common operations

### Host Configuration Manager
- **Named Configurations**: Save multiple host configurations with friendly names
- **JSON Storage**: Configurations stored in `~/.webrepl-commander/hosts.json`
- **Quick Selection**: Dropdown to quickly switch between saved hosts
- **Auto-Select**: Automatically selects last used configuration on startup
- **Full Management**: Add, edit, delete, and manage all your devices
- **Configuration Details**:
  - Name (e.g., "Office ESP8266", "Living Room ESP32")
  - Host address (IP or hostname)
  - Port number (default: 8266)
  - Password (stored securely)
  - Last used timestamp for convenience

### Connection Management
- Connect to any WebREPL-enabled MicroPython device
- Select from saved host configurations
- Connection status with device version display
- Graceful disconnect and reconnect
- Visual indicator showing connected host name

### File Operations

#### F5 - Copy Files
- **Local → Remote**: Upload files from PC to device
- **Remote → Local**: Download files from device to PC
- Multi-file selection support
- Progress bar with transfer statistics
- Automatic directory refresh after transfer

#### F8 - Delete Files
- Delete files and directories on local or remote
- Multi-selection support
- Confirmation dialog for safety
- Smart detection of directories vs files

#### F7 - Create Directory
- Create directories on local filesystem
- Create directories on remote device
- Context-sensitive (works in focused panel)

#### F3 - View Files
- View local text files in built-in viewer
- Read-only text display with syntax preservation

#### F4 - Edit Files
- Edit local files with external editor (Notepad)
- Quick access to file modification

### Navigation
- **Double-click** or **Enter** to navigate into directories
- **[..]** entry to navigate to parent directory
- Path display showing current location
- Refresh buttons (↻) to reload directory listings

### Remote Device Operations

#### F9 - Execute Python Code
- Execute arbitrary Python expressions on device
- View execution results in popup window
- Direct access to MicroPython REPL functionality

#### F10 - Terminal
- Full interactive Python REPL terminal
- Send commands and view output in real-time
- Ctrl+C interrupt support for running code
- Clear history function
- Persistent session during terminal dialog

#### F11 - Reset Device
- **Yes**: Hard reset (power cycle)
- **No**: Soft reset (reload Python)
- Automatic disconnection after reset

### Core Library Features Exposed

All WebREPL.Core library features are integrated:

1. **WebReplClient**
   - `ConnectAsync()` - Connection management
   - `PutFileAsync()` - File upload with progress
   - `GetFileAsync()` - File download with progress
   - `ExecuteAsync()` - Python code execution
   - `InterruptAsync()` - Interrupt running code

2. **RemoteCommands**
   - `RemoteLsAsync()` - Directory listing
   - `RemoteCdAsync()` - Change directory
   - `RemotePwdAsync()` - Get current directory
   - `RemoteDeleteAsync()` - Delete files
   - `RemoteMkdirAsync()` - Create directories
   - `RemoteRmdirAsync()` - Remove directories
   - `RemoteResetAsync()` - Reset device (hard/soft)
   - `RemoteEvalAsync()` - Evaluate Python expressions

### User Interface Elements

- **Connection Bar**: Host configuration selector and connection management
- **Host Manager**: Full-featured dialog to manage saved configurations
- **Status Bar**: Operation status and progress display
- **Progress Bar**: Visual feedback for file transfers
- **Function Bar**: Quick access to all major operations
- **File Lists**: 
  - Directories shown in brackets [dirname]
  - File sizes displayed in formatted view
  - Directory entries marked as <DIR>

### Keyboard Shortcuts

- **F3**: View selected file
- **F4**: Edit selected file (local only)
- **F5**: Copy files (direction auto-detected)
- **F6**: Move files (not yet implemented)
- **F7**: Create new directory
- **F8**: Delete selected files/directories
- **F9**: Execute Python code on device
- **F10**: Open terminal session
- **F11**: Reset device
- **F12**: Quit application
- **Enter**: Navigate into selected directory
- **Multi-select**: Hold Ctrl/Shift for multiple file selection

## Usage

### First Time Setup

1. **Launch** the application
2. **Click "Manage Hosts..."** to open the Host Manager
3. **Click "New"** to create a new host configuration
4. **Enter details**:
   - Name: A friendly name (e.g., "Office ESP8266")
   - Host: IP address or hostname (e.g., "192.168.4.1")
   - Port: WebREPL port (default: 8266)
   - Password: Your device's WebREPL password
5. **Click "Save"** to save the configuration
6. **Click "Close"** to return to main window

### Connecting to a Device

1. **Select a host** from the dropdown in the connection bar
2. **Click "Connect"** to establish connection
3. **Wait for connection** - status will show device version
4. **Start managing files** once connected

### Managing Multiple Devices

- Save configurations for all your devices
- Quick switch between devices using the dropdown
- Edit existing configurations via "Manage Hosts..."
- Delete old configurations you no longer need
- The most recently used host is automatically selected on startup

### Configuration File Location

Your host configurations are stored at:
- **Windows**: `C:\Users\<YourUsername>\.webrepl-commander\hosts.json`
- **Linux/Mac**: `~/.webrepl-commander/hosts.json`

The configuration file is human-readable JSON and can be:
- Backed up for safekeeping
- Shared between machines
- Manually edited if needed

## Technical Details

- Built with WPF for .NET 10
- Async/await pattern for responsive UI
- CancellationToken support for long operations
- ObservableCollection for reactive data binding
- Progress reporting with IProgress<T>
- Proper resource disposal (IDisposable pattern)
- JSON configuration storage using System.Text.Json

## Classic Commander Experience

This interface recreates the classic Norton Commander experience beloved by DOS users:
- Dual-pane file management
- Function key based operations
- Status bar with operation feedback
- Classic color scheme (blue/cyan/yellow)
- Efficient keyboard-driven workflow
