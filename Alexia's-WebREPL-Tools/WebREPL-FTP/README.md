# WebREPL-FTP

An FTP-like command-line client for MicroPython WebREPL, written in C#.

## Features

- Interactive FTP-like shell for managing MicroPython devices
- File upload/download with progress display
- Remote and local directory navigation
- File and directory management (create, delete, list)
- Built-in REPL mode for executing Python commands
- Device reset capabilities (soft/hard)

## Usage

### Basic Connection

```bash
# Connect with password prompt
webrepl-ftp 192.168.4.1

# Connect with password as argument
webrepl-ftp -p mypassword 192.168.4.1

# Connect to custom port
webrepl-ftp -p mypassword 192.168.4.1:8080

# Set initial local and remote directories
webrepl-ftp -p mypassword -l ./local_files -r /app 192.168.4.1
```

### Command-Line Options

- `-p <password>` - Specify WebREPL password (prompts if not provided)
- `-l <directory>` - Set initial local working directory
- `-r <directory>` - Set initial remote working directory
- `-h, --help` - Show help message

## Interactive Commands

Once connected, you can use the following commands in the interactive shell:

### File Transfer
- `get <remote_file> [local_file]` - Download file from device
- `put <local_file> [remote_file]` - Upload file to device

### Remote Navigation
- `ls [path]` - List remote directory contents
- `pwd` - Print remote working directory
- `cd <directory>` - Change remote directory

### Local Navigation
- `lls [path]` - List local directory contents
- `lcd [directory]` - Change/show local directory

### File Operations
- `rm <file>` - Delete remote file
- `mkdir <directory>` - Create remote directory
- `rmdir <directory>` - Remove remote directory

### Device Management
- `reset [hard]` - Reset the board (soft reset default, 'hard' for hard reset)
- `repl` - Enter REPL mode (Ctrl+] to exit, Ctrl+C to interrupt)

### General
- `help` - Show available commands
- `exit`, `quit` - Exit the application

## Examples

### Upload Files

```
webrepl> put main.py /main.py
Sent 1234 bytes (100.0%)
File uploaded: /main.py
```

### Download Files

```
webrepl> get /boot.py boot_backup.py
Received 567 bytes (100.0%)
File saved: boot_backup.py
```

### Navigate and List

```
webrepl> pwd
/

webrepl> ls
d lib                                    0
- boot.py                              567
- main.py                             1234

webrepl> cd /lib
Remote directory: /lib
```

### Create Directory Structure

```
webrepl> mkdir myapp
Created directory: myapp

webrepl> cd myapp
Remote directory: /myapp

webrepl> put config.py
Sent 123 bytes (100.0%)
File uploaded: config.py
```

### REPL Mode

```
webrepl> repl
REPL mode - type Python commands, Ctrl+C to interrupt, Ctrl+] to exit
>>> import machine
>>> machine.freq()
160000000
>>> [Ctrl+] pressed]
Exiting REPL mode...
```

## Building from Source

```bash
dotnet build WebREPL-FTP\WebREPL-FTP.csproj
```

## Running

```bash
dotnet run --project WebREPL-FTP -- -p password 192.168.4.1
```

Or after building:

```bash
cd WebREPL-FTP\bin\Debug\net10.0
WebREPL-FTP.exe -p password 192.168.4.1
```

## Requirements

- .NET 10.0 or later
- MicroPython device with WebREPL enabled
- Network connectivity to the device

## Related Projects

This project uses the **WebREPL.Core** library, which can be used independently in your own C# applications to interact with MicroPython devices.

See `WebREPL.Core\README.md` for library documentation and examples.
