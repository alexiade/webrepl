using WebREPL.Core;

namespace WebREPL.Examples;

/// <summary>
/// Example demonstrating basic WebREPL operations
/// </summary>
public class BasicExample
{
    public static async Task RunAsync()
    {
        // Connect to the device
        using var client = new WebReplClient();
        
        Console.Write("Enter host (e.g., 192.168.4.1): ");
        var host = Console.ReadLine() ?? "192.168.4.1";
        
        Console.Write("Enter password: ");
        var password = Console.ReadLine() ?? "";
        
        Console.WriteLine($"Connecting to {host}...");
        await client.ConnectAsync(host, 8266, password);
        
        Console.WriteLine($"✓ Connected! WebREPL version: {client.RemoteVersion}");
        Console.WriteLine();
        
        // Get the WebSocket for advanced operations
        var ws = client.GetWebSocket();
        
        // Show current directory
        var currentDir = await RemoteCommands.RemotePwdAsync(ws);
        Console.WriteLine($"Current directory: {currentDir}");
        Console.WriteLine();
        
        // List files
        Console.WriteLine("Files and directories:");
        var files = await RemoteCommands.RemoteLsAsync(ws);
        foreach (var file in files)
        {
            var type = file.IsDirectory ? "DIR " : "FILE";
            Console.WriteLine($"  {type} {file.Name,-30} {file.Size,10} bytes");
        }
        Console.WriteLine();
        
        // Execute a simple Python command
        Console.WriteLine("Executing Python code...");
        var result = await client.ExecuteAsync("import sys; print(sys.version)");
        Console.WriteLine($"MicroPython version:\n{result}");
    }
}

/// <summary>
/// Example demonstrating file transfer operations
/// </summary>
public class FileTransferExample
{
    public static async Task RunAsync()
    {
        using var client = new WebReplClient();
        
        await client.ConnectAsync("192.168.4.1", 8266, "your-password");
        
        // Upload a file with progress reporting
        Console.WriteLine("Uploading file...");
        var uploadProgress = new Progress<FileTransferProgress>(p =>
        {
            var percent = p.PercentComplete;
            var bar = new string('█', (int)(percent / 2));
            Console.Write($"\r[{bar,-50}] {percent:F1}% ({p.BytesTransferred}/{p.TotalBytes} bytes)");
        });
        
        await client.PutFileAsync("test.py", "/test.py", uploadProgress);
        Console.WriteLine("\n✓ Upload complete!");
        
        // Download a file
        Console.WriteLine("\nDownloading file...");
        var downloadProgress = new Progress<FileTransferProgress>(p =>
        {
            Console.Write($"\rReceived {p.BytesTransferred} bytes");
        });
        
        await client.GetFileAsync("/boot.py", "downloaded_boot.py", downloadProgress);
        Console.WriteLine("\n✓ Download complete!");
    }
}

/// <summary>
/// Example demonstrating filesystem operations
/// </summary>
public class FilesystemExample
{
    public static async Task RunAsync()
    {
        using var client = new WebReplClient();
        await client.ConnectAsync("192.168.4.1", 8266, "your-password");
        
        var ws = client.GetWebSocket();
        
        // Create a new directory
        Console.WriteLine("Creating directory 'test_folder'...");
        await RemoteCommands.RemoteMkdirAsync(ws, "test_folder");
        
        // Navigate into it
        await RemoteCommands.RemoteCdAsync(ws, "test_folder");
        var currentDir = await RemoteCommands.RemotePwdAsync(ws);
        Console.WriteLine($"✓ Changed to: {currentDir}");
        
        // Go back to parent
        await RemoteCommands.RemoteCdAsync(ws, "..");
        
        // Remove the directory
        Console.WriteLine("Removing directory...");
        await RemoteCommands.RemoteRmdirAsync(ws, "test_folder");
        Console.WriteLine("✓ Directory removed");
    }
}

/// <summary>
/// Example demonstrating Python code execution
/// </summary>
public class CodeExecutionExample
{
    public static async Task RunAsync()
    {
        using var client = new WebReplClient();
        await client.ConnectAsync("192.168.4.1", 8266, "your-password");
        
        var ws = client.GetWebSocket();
        
        // Execute various Python commands
        Console.WriteLine("Getting system information...");
        
        var pythonCode = @"
import sys
import os
import gc

print('Platform:', sys.platform)
print('Version:', sys.version)
print('Memory free:', gc.mem_free())
print('Memory allocated:', gc.mem_alloc())
print('Flash size:', os.statvfs('/')[0] * os.statvfs('/')[2])
";
        
        var output = await RemoteCommands.RemoteEvalAsync(ws, pythonCode);
        Console.WriteLine(output);
    }
}

/// <summary>
/// Example demonstrating device management
/// </summary>
public class DeviceManagementExample
{
    public static async Task RunAsync()
    {
        using var client = new WebReplClient();
        await client.ConnectAsync("192.168.4.1", 8266, "your-password");
        
        var ws = client.GetWebSocket();
        
        // Interrupt any running code
        Console.WriteLine("Interrupting running code...");
        await client.InterruptAsync();
        Console.WriteLine("✓ Interrupted");
        
        // Get device info before reset
        Console.WriteLine("\nDevice info:");
        var info = await RemoteCommands.RemoteEvalAsync(ws, 
            "import machine; print('Freq:', machine.freq(), 'Hz')");
        Console.WriteLine(info);
        
        // Perform soft reset
        Console.WriteLine("\nPerforming soft reset...");
        Console.WriteLine("Note: Connection will be closed after reset");
        await RemoteCommands.RemoteResetAsync(ws, hard: false);
    }
}

/// <summary>
/// Example demonstrating bulk file operations
/// </summary>
public class BulkOperationsExample
{
    public static async Task UploadDirectoryAsync(string localPath, string remotePath)
    {
        using var client = new WebReplClient();
        await client.ConnectAsync("192.168.4.1", 8266, "your-password");
        
        var ws = client.GetWebSocket();
        
        // Create remote directory if it doesn't exist
        try
        {
            await RemoteCommands.RemoteMkdirAsync(ws, remotePath);
        }
        catch
        {
            // Directory might already exist
        }
        
        // Get all files in local directory
        var files = Directory.GetFiles(localPath, "*.py", SearchOption.AllDirectories);
        
        Console.WriteLine($"Uploading {files.Length} files...");
        
        var progress = new Progress<FileTransferProgress>(p =>
        {
            Console.WriteLine($"  {Path.GetFileName(p.SourcePath)}: {p.PercentComplete:F1}%");
        });
        
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(localPath, file);
            var remoteFile = $"{remotePath}/{relativePath.Replace('\\', '/')}";
            
            Console.WriteLine($"Uploading {relativePath}...");
            await client.PutFileAsync(file, remoteFile, progress);
        }
        
        Console.WriteLine("✓ All files uploaded!");
    }
}
