# WebREPL C# Port - Project Overview

This repository contains a complete C# port of the Python `webrepl_ftp.py` and `webrepl_cli.py` tools for interacting with MicroPython devices via WebREPL.

## Project Structure

```
Alexia's-WebREPL-Tools/
│
├── WebREPL.Core/                    # Reusable library for WebREPL operations
│   ├── WebReplClient.cs             # Main client class
│   ├── WebSocket.cs                 # WebSocket protocol implementation
│   ├── RemoteCommands.cs            # Remote filesystem and device operations
│   ├── Examples.cs                  # Usage examples
│   └── README.md                    # Library documentation
│
└── WebREPL-FTP/                     # FTP-like console application
    ├── Program.cs                   # Interactive FTP client
    └── README.md                    # Application documentation
```

## Components

### WebREPL.Core Library

A standalone .NET 10 library that implements the WebREPL protocol. It can be used in any C# project to:

- Connect to MicroPython devices
- Transfer files (upload/download)
- Execute Python code remotely
- Navigate and manage the remote filesystem
- Control device operations (reset, interrupt)

**Key Classes:**

- `WebReplClient` - High-level client API
- `WebSocket` - Low-level WebSocket wrapper
- `RemoteCommands` - Static helpers for common operations
- `FileTransferProgress` - Progress reporting record

### WebREPL-FTP Application

A console application that provides an FTP-like interactive shell for managing MicroPython devices.

**Features:**

- Interactive command-line interface
- File transfer with progress display
- Both remote and local directory navigation
- REPL mode for direct Python interaction
- Command-line argument parsing

## Ported from Python

This project is a direct port of:

- `webrepl_ftp.py` → `WebREPL-FTP/Program.cs`
- `webrepl_cli.py` → `WebREPL.Core/WebReplClient.cs` + `WebSocket.cs`
- `remote_commands.py` → `WebREPL.Core/RemoteCommands.cs`

All functionality has been preserved and enhanced with:

- Modern async/await patterns
- IProgress<T> for progress reporting
- Strong typing with nullable reference types
- Proper resource disposal (IDisposable)
- Comprehensive error handling

## Quick Start

### Using the Library

```csharp
using WebREPL.Core;

using var client = new WebReplClient();
await client.ConnectAsync("192.168.4.1", 8266, "password");

// Upload a file
await client.PutFileAsync("local.py", "/remote.py");

// Execute Python
var ws = client.GetWebSocket();
var files = await RemoteCommands.RemoteLsAsync(ws);
foreach (var file in files)
{
    Console.WriteLine($"{file.Name} - {file.Size} bytes");
}
```

### Using the FTP Client

```bash
# Connect interactively
dotnet run --project WebREPL-FTP -- 192.168.4.1

# With password
dotnet run --project WebREPL-FTP -- -p mypassword 192.168.4.1

# Set initial directories
dotnet run --project WebREPL-FTP -- -p mypassword -l ./local -r /app 192.168.4.1
```

## Building

```bash
# Build everything
dotnet build

# Build just the library
dotnet build WebREPL.Core/WebREPL.Core.csproj

# Build just the FTP client
dotnet build WebREPL-FTP/WebREPL-FTP.csproj
```

## Testing

Connect to a MicroPython device with WebREPL enabled:

```bash
cd WebREPL-FTP
dotnet run -- -p your_password 192.168.4.1
```

## Requirements

- .NET 10.0 SDK or later
- MicroPython device with WebREPL enabled
- Network connectivity to the device

## Use Cases

1. **Device Development** - Upload and test code on MicroPython devices
2. **File Management** - Manage files on ESP32/ESP8266 devices
3. **Automation** - Integrate device operations into build pipelines
4. **Diagnostics** - Execute commands and retrieve logs remotely
5. **Batch Operations** - Process multiple devices programmatically

## Advantages over Python Version

- **Type Safety** - Compile-time checking prevents many runtime errors
- **Performance** - Generally faster than Python for I/O operations
- **Integration** - Easy to integrate into .NET applications and services
- **Tooling** - Full IDE support with IntelliSense
- **Deployment** - Single-file executables with .NET 10
- **Async** - Native async/await for better responsiveness

## Documentation

- [WebREPL.Core Library Documentation](WebREPL.Core/README.md)
- [WebREPL-FTP Application Documentation](WebREPL-FTP/README.md)
- [Code Examples](WebREPL.Core/Examples.cs)

## License

This is a port of the MicroPython WebREPL tools. Original Python code is maintained by the MicroPython project.
