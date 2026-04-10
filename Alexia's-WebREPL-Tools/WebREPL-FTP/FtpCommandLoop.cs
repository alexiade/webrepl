using WebREPL.Core;

namespace WebREPL;

public class FtpCommandLoop
{
    private readonly WebReplClient _client;
    private readonly WebSocket _ws;

    public FtpCommandLoop(WebReplClient client)
    {
        _client = client;
        _ws = client.GetWebSocket();
    }

    public async Task RunAsync()
    {
        Console.WriteLine("\nWebREPL FTP Client");
        Console.WriteLine("Type 'help' for available commands\n");

        while (true)
        {
            Console.Write("webrepl> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower();
            var args = parts.Skip(1).ToArray();

            try
            {
                switch (command)
                {
                    case "exit":
                    case "quit":
                        return;

                    case "help":
                        ShowHelp();
                        break;

                    case "ls":
                    case "dir":
                        var path = args.Length > 0 ? args[0] : null;
                        var files = await RemoteCommands.RemoteLsAsync(_ws, path);
                        PrintFileList(files);
                        break;

                    case "pwd":
                        var remotePath = await RemoteCommands.RemotePwdAsync(_ws);
                        Console.WriteLine(remotePath);
                        break;

                    case "cd":
                        if (args.Length == 0)
                        {
                            Console.WriteLine("Usage: cd <directory>");
                            break;
                        }
                        await RemoteCommands.RemoteCdAsync(_ws, args[0]);
                        var newPath = await RemoteCommands.RemotePwdAsync(_ws);
                        Console.WriteLine($"Remote directory: {newPath}");
                        break;

                    case "lcd":
                        if (args.Length == 0)
                        {
                            Console.WriteLine($"Local directory: {Directory.GetCurrentDirectory()}");
                            break;
                        }
                        Directory.SetCurrentDirectory(args[0]);
                        Console.WriteLine($"Local directory: {Directory.GetCurrentDirectory()}");
                        break;

                    case "lls":
                    case "ldir":
                        var localPath = args.Length > 0 ? args[0] : ".";
                        var localFiles = Directory.GetFileSystemEntries(localPath);
                        foreach (var file in localFiles)
                        {
                            var info = new FileInfo(file);
                            var dirInfo = new DirectoryInfo(file);
                            var isDir = dirInfo.Exists;
                            var size = isDir ? 0 : info.Length;
                            var flag = isDir ? "d" : "-";
                            Console.WriteLine($"{flag} {Path.GetFileName(file),-30} {size,8}");
                        }
                        break;

                    case "get":
                        if (args.Length < 1)
                        {
                            Console.WriteLine("Usage: get <remote_file> [local_file]");
                            break;
                        }
                        var remoteFile = args[0];
                        var localFile = args.Length > 1 ? args[1] : Path.GetFileName(remoteFile);

                        var progress = new Progress<FileTransferProgress>(p =>
                        {
                            Console.Write($"\rReceived {p.BytesTransferred} bytes ({p.PercentComplete:F1}%)");
                        });

                        await _client.GetFileAsync(remoteFile, localFile, progress);
                        Console.WriteLine($"\nFile saved: {localFile}");
                        break;

                    case "put":
                        if (args.Length < 1)
                        {
                            Console.WriteLine("Usage: put <local_file> [remote_file]");
                            break;
                        }
                        localFile = args[0];
                        remoteFile = args.Length > 1 ? args[1] : Path.GetFileName(localFile);

                        progress = new Progress<FileTransferProgress>(p =>
                        {
                            Console.Write($"\rSent {p.BytesTransferred} bytes ({p.PercentComplete:F1}%)");
                        });

                        await _client.PutFileAsync(localFile, remoteFile, progress);
                        Console.WriteLine($"\nFile uploaded: {remoteFile}");
                        break;

                    case "rm":
                    case "del":
                        if (args.Length < 1)
                        {
                            Console.WriteLine("Usage: rm <file>");
                            break;
                        }
                        await RemoteCommands.RemoteDeleteAsync(_ws, args[0]);
                        Console.WriteLine($"Deleted: {args[0]}");
                        break;

                    case "mkdir":
                        if (args.Length < 1)
                        {
                            Console.WriteLine("Usage: mkdir <directory>");
                            break;
                        }
                        await RemoteCommands.RemoteMkdirAsync(_ws, args[0]);
                        Console.WriteLine($"Created directory: {args[0]}");
                        break;

                    case "rmdir":
                        if (args.Length < 1)
                        {
                            Console.WriteLine("Usage: rmdir <directory>");
                            break;
                        }
                        await RemoteCommands.RemoteRmdirAsync(_ws, args[0]);
                        Console.WriteLine($"Removed directory: {args[0]}");
                        break;

                    case "reset":
                        var hard = args.Length > 0 && args[0].ToLower() == "hard";
                        await RemoteCommands.RemoteResetAsync(_ws, hard);
                        Console.WriteLine("Reset command sent. Connection will be closed.");
                        return;

                    case "repl":
                        Console.WriteLine("Entering REPL mode (Ctrl+] to exit)...");
                        await ReplModeAsync();
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        Console.WriteLine("Type 'help' for available commands");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  ls [path]         # List remote directory");
        Console.WriteLine("  pwd               # Print remote working directory");
        Console.WriteLine("  cd <dir>          # Change remote directory");
        Console.WriteLine("  lcd [dir]         # Change/show local directory");
        Console.WriteLine("  lls [path]        # List local directory");
        Console.WriteLine("  get <remote> [local]  # Download file");
        Console.WriteLine("  put <local> [remote]  # Upload file");
        Console.WriteLine("  rm <file>         # Delete remote file");
        Console.WriteLine("  mkdir <dir>       # Create remote directory");
        Console.WriteLine("  rmdir <dir>       # Remove remote directory");
        Console.WriteLine("  reset [hard]      # Reset the board (soft/hard)");
        Console.WriteLine("  repl              # Enter REPL mode");
        Console.WriteLine("  exit, quit        # Exit");
        Console.WriteLine("  help              # This help message");
    }

    private static void PrintFileList(List<RemoteFileInfo> files)
    {
        if (files.Count == 0)
        {
            Console.WriteLine("(empty directory)");
            return;
        }

        foreach (var file in files)
        {
            var flag = file.IsDirectory ? "d" : "-";
            Console.WriteLine($"{flag} {file.Name,-30} {file.Size,8}");
        }
    }

    private async Task ReplModeAsync()
    {
        Console.WriteLine("REPL mode - type Python commands, Ctrl+C to interrupt, Ctrl+] to exit");

        var readTask = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            while (true)
            {
                try
                {
                    var data = await _ws.ReadAvailableAsync(buffer.Length, 50);
                    if (data.Length > 0)
                    {
                        var text = System.Text.Encoding.UTF8.GetString(data);
                        Console.Write(text);
                    }
                }
                catch
                {
                    break;
                }
            }
        });

        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                // Ctrl+] to exit
                if (key.Key == ConsoleKey.Oem6 && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    Console.WriteLine("\nExiting REPL mode...");
                    break;
                }

                // Ctrl+C to interrupt
                if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    await _ws.WriteAsync(new byte[] { 0x03 }, WebSocket.WEBREPL_FRAME_TXT);
                }
                else
                {
                    var charBytes = System.Text.Encoding.UTF8.GetBytes(key.KeyChar.ToString());
                    await _ws.WriteAsync(charBytes, WebSocket.WEBREPL_FRAME_TXT);
                }
            }

            await Task.Delay(10);
        }
    }

    public static string ReadPassword()
    {
        var password = "";
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write("*");
            }
        }
        return password;
    }
}
