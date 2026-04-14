# WebREPL.Core Library

A C# library for interacting with MicroPython's WebREPL protocol. This library provides a complete implementation of the WebREPL client protocol, allowing you to:

- Connect to MicroPython devices via WebREPL
- Execute Python commands remotely
- Transfer files to and from the device
- Navigate the remote filesystem
- Perform device management operations

## Features

- **File Transfer**: Upload and download files with progress reporting
- **Remote Command Execution**: Execute Python code and retrieve results
- **Directory Operations**: List, create, delete, and navigate directories
- **Device Management**: Reset (soft/hard), interrupt running code
- **Async/Await Support**: Modern async API throughout
- **Progress Reporting**: IProgress<T> support for file transfers

## Installation

Add a project reference to `WebREPL.Core`:

```xml
<ItemGroup>
  <ProjectReference Include="..\WebREPL.Core\WebREPL.Core.csproj" />
</ItemGroup>
```

## Basic Usage

### Connecting to a Device

```csharp
using WebREPL.Core;

using var client = new WebReplClient();
await client.ConnectAsync("192.168.4.1", 8266, "your-password");

Console.WriteLine($"Connected! Version: {client.RemoteVersion}");
```

### Uploading a File

```csharp
var progress = new Progress<FileTransferProgress>(p =>
{
    Console.WriteLine($"Uploaded {p.BytesTransferred}/{p.TotalBytes} bytes ({p.PercentComplete:F1}%)");
});

await client.PutFileAsync("local_file.py", "/remote/path/file.py", progress);
```

### Downloading a File

```csharp
var progress = new Progress<FileTransferProgress>(p =>
{
    Console.WriteLine($"Downloaded {p.BytesTransferred} bytes");
});

await client.GetFileAsync("/remote/file.py", "local_file.py", progress);
```

### Executing Python Code

```csharp
var ws = client.GetWebSocket();
var output = await RemoteCommands.RemoteEvalAsync(ws, "import os; print(os.listdir())");
Console.WriteLine(output);
```

### Directory Operations

```csharp
var ws = client.GetWebSocket();

// List files
var files = await RemoteCommands.RemoteLsAsync(ws);
foreach (var file in files)
{
    Console.WriteLine($"{(file.IsDirectory ? "DIR" : "FILE")} {file.Name} ({file.Size} bytes)");
}

// Change directory
await RemoteCommands.RemoteCdAsync(ws, "/app");

// Get current directory
var currentDir = await RemoteCommands.RemotePwdAsync(ws);
Console.WriteLine($"Current directory: {currentDir}");

// Create directory
await RemoteCommands.RemoteMkdirAsync(ws, "new_folder");

// Delete file
await RemoteCommands.RemoteDeleteAsync(ws, "old_file.py");
```

### Device Management

```csharp
var ws = client.GetWebSocket();

// Interrupt running code
await client.InterruptAsync();

// Soft reset
await RemoteCommands.RemoteResetAsync(ws, hard: false);

// Hard reset
await RemoteCommands.RemoteResetAsync(ws, hard: true);
```

## API Reference

### WebReplClient

Main client class for WebREPL connections.

**Methods:**
- `Task<bool> ConnectAsync(string host, int port, string password, CancellationToken ct = default)`
- `Task PutFileAsync(string localPath, string remotePath, IProgress<FileTransferProgress>? progress = null, CancellationToken ct = default)`
- `Task GetFileAsync(string remotePath, string localPath, IProgress<FileTransferProgress>? progress = null, CancellationToken ct = default)`
- `Task<string> ExecuteAsync(string pythonCode, CancellationToken ct = default)`
- `Task InterruptAsync(CancellationToken ct = default)`
- `WebSocket GetWebSocket()`

**Properties:**
- `bool IsConnected` - Connection status
- `string? RemoteVersion` - Remote WebREPL version

### RemoteCommands

Static class providing remote filesystem and device operations.

**Methods:**
- `Task<string> RemoteEvalAsync(WebSocket ws, string pythonExpression, CancellationToken ct = default)`
- `Task<List<RemoteFileInfo>> RemoteLsAsync(WebSocket ws, string? path = null, CancellationToken ct = default)`
- `Task RemoteCdAsync(WebSocket ws, string path, CancellationToken ct = default)`
- `Task<string> RemotePwdAsync(WebSocket ws, CancellationToken ct = default)`
- `Task RemoteDeleteAsync(WebSocket ws, string path, CancellationToken ct = default)`
- `Task RemoteMkdirAsync(WebSocket ws, string path, CancellationToken ct = default)`
- `Task RemoteRmdirAsync(WebSocket ws, string path, CancellationToken ct = default)`
- `Task RemoteResetAsync(WebSocket ws, bool hard = false, CancellationToken ct = default)`
- `Task InterruptRunningCodeAsync(WebSocket ws, CancellationToken ct = default)`
- `Task ClearBufferAsync(WebSocket ws, int timeoutMs = 500, CancellationToken ct = default)`

### WebSocket

Low-level WebSocket wrapper for WebREPL protocol.

**Constants:**
- `byte WEBREPL_FRAME_TXT = 1` - Text frame type
- `byte WEBREPL_FRAME_BIN = 2` - Binary frame type

**Methods:**
- `Task WriteAsync(byte[] data, byte frameType, CancellationToken ct = default)`
- `Task<byte[]> ReadAsync(int count, CancellationToken ct = default)`
- `Task<byte[]> ReadAvailableAsync(int maxBytes, int timeoutMs = 100, CancellationToken ct = default)`
- `void SetBinaryMode()`
- `void SetTextMode()`

## Example Project: WebREPL-FTP

The included `WebREPL-FTP` console application demonstrates how to use the library to create an FTP-like client:

```bash
# Connect to a device
webrepl-ftp -p mypassword 192.168.4.1

# Specify initial directories
webrepl-ftp -p mypassword -l ./local_files -r /app 192.168.4.1:8266

# Interactive mode commands:
# ls, cd, pwd - remote navigation
# lcd, lls - local navigation
# get, put - file transfer
# mkdir, rm, rmdir - file operations
# reset, repl - device management
```

## Requirements

- .NET 10.0 or later
- Network connectivity to MicroPython device running WebREPL

## Protocol Details

The library implements the WebREPL protocol used by MicroPython:
- WebSocket-based communication
- Text frames for REPL commands
- Binary frames for file transfers
- Simple authentication via password

## License

This is a port of the Python `webrepl_cli.py` and `webrepl_ftp.py` tools to C#.
