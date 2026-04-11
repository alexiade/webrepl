using System.Text;
using System.Text.RegularExpressions;

namespace WebREPL.Core;

public static class RemoteCommands
{
    private static string DecodeResponse(byte[] data)
    {
        var decoder = Encoding.UTF8.GetDecoder();
        decoder.Fallback = new DecoderReplacementFallback("");

        var charCount = decoder.GetCharCount(data, 0, data.Length, flush: true);
        var chars = new char[charCount];
        decoder.GetChars(data, 0, data.Length, chars, 0, flush: true);

        var text = new string(chars);

        text = Regex.Replace(text, @"\x1b\[[0-9;]*[a-zA-Z]", "");
        text = Regex.Replace(text, @"[\x00-\x08\x0B-\x0C\x0E-\x1F]", "");

        return text;
    }

    public static async Task<string> RemoteEvalAsync(WebSocket ws, string pythonExpression, CancellationToken cancellationToken = default)
    {
        await ClearBufferAsync(ws, 500, cancellationToken);

        var fullCommand = PythonSnippets.WrapWithMarker(pythonExpression) + "\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(fullCommand);
        await ws.WriteAsync(commandBytes, WebSocket.WEBREPL_FRAME_TXT, cancellationToken);

        var buffer = new List<byte>();
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(30);
        var marker = PythonSnippets.Marker;

        while ((DateTime.UtcNow - startTime) < timeout)
        {
            try
            {
                var chunk = await ws.ReadAvailableAsync(Int32.MaxValue, 500, cancellationToken);
                if (chunk.Length > 0)
                {
                    buffer.AddRange(chunk);

                    var tempString = DecodeResponse(buffer.ToArray());
                    var markerCount = CountStringOccurrences(tempString, marker);
                    if (markerCount >= 2)
                    {
                        break;
                    }
                }

                await Task.Delay(10, cancellationToken);
            }
            catch (Exception)
            {
                break;
            }
        }

        var response = DecodeResponse(buffer.ToArray());
        var markerFinalCount = CountStringOccurrences(response, marker);

        if (markerFinalCount < 2)
        {
            return "";
        }

        var parts = response.Split(marker);
        if (parts.Length < 2)
        {
            return "";
        }

        var output = parts[1];

        if (output.StartsWith("')"))
        {
            output = output[2..];
        }

        var cleanOutput = output.Trim();

        while (cleanOutput.StartsWith(">>>") || cleanOutput.EndsWith(">>>"))
        {
            if (cleanOutput.StartsWith(">>>"))
                cleanOutput = cleanOutput[3..].Trim();
            if (cleanOutput.EndsWith(">>>"))
                cleanOutput = cleanOutput[..^3].Trim();
        }

        return cleanOutput.Trim();
    }

    private static int CountStringOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return 0;

        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    public static async Task ClearBufferAsync(WebSocket ws, int timeoutMs = 500, CancellationToken cancellationToken = default)
    {
        await ws.ReadAvailableAsync(65536, timeoutMs, cancellationToken);
    }

    public static async Task<bool> InterruptRunningCodeAsync(WebSocket ws, CancellationToken cancellationToken = default)
    {
        await ClearBufferAsync(ws, 500, cancellationToken);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await ws.WriteAsync(new byte[] { 0x03 }, WebSocket.WEBREPL_FRAME_TXT, cancellationToken);
            await Task.Delay(100, cancellationToken);
        }

        await Task.Delay(500, cancellationToken);

        var buffer = new List<byte>();
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(500);

        while ((DateTime.UtcNow - startTime) < timeout)
        {
            var chunk = await ws.ReadAvailableAsync(1024, 100, cancellationToken);
            if (chunk.Length > 0)
            {
                buffer.AddRange(chunk);
            }
            await Task.Delay(10, cancellationToken);
        }

        var response = DecodeResponse(buffer.ToArray());

        if (response.Contains(">>>"))
        {
            return true;
        }

        await ws.WriteAsync(Encoding.UTF8.GetBytes("\r\n"), WebSocket.WEBREPL_FRAME_TXT, cancellationToken);
        await Task.Delay(300, cancellationToken);

        buffer.Clear();
        startTime = DateTime.UtcNow;
        timeout = TimeSpan.FromMilliseconds(300);

        while ((DateTime.UtcNow - startTime) < timeout)
        {
            var chunk = await ws.ReadAvailableAsync(1024, 100, cancellationToken);
            if (chunk.Length > 0)
            {
                buffer.AddRange(chunk);
            }
            await Task.Delay(10, cancellationToken);
        }

        response = DecodeResponse(buffer.ToArray());
        return response.Contains(">>>");
    }

    public static async Task<List<RemoteFileInfo>> RemoteLsAsync(WebSocket ws, string? path = null, CancellationToken cancellationToken = default)
    {
        var pyExpr = PythonSnippets.ListDirectory();
        var output = await RemoteEvalAsync(ws, pyExpr, cancellationToken);
        var files = new List<RemoteFileInfo>();

        var lines = output.Trim().Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains(','))
            {
                var entries = line.Trim().Split(';');
                foreach (var entry in entries)
                {
                    var parts = entry.Split(',');
                    if (parts.Length == 3)
                    {
                        var name = parts[0];
                        if (name is "." or "..")
                            continue;

                        if (bool.TryParse(parts[1], out var isDir) && int.TryParse(parts[2], out var size))
                        {
                            files.Add(new RemoteFileInfo(name, isDir, size));
                        }
                    }
                }
            }
        }

        return files;
    }

    public static async Task RemoteCdAsync(WebSocket ws, string path, CancellationToken cancellationToken = default)
    {
        var pathEscaped = path.Replace("'", "\\'");
        var pyExpr = PythonSnippets.ChangeDirectory(pathEscaped);
        await RemoteEvalAsync(ws, pyExpr, cancellationToken);
    }

    public static async Task<string> RemotePwdAsync(WebSocket ws, CancellationToken cancellationToken = default)
    {
        var pyExpr = PythonSnippets.GetCurrentDirectory();
        var output = await RemoteEvalAsync(ws, pyExpr, cancellationToken);

        var lines = output.Trim().Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line) && !line.StartsWith(">>>") && 
                !line.StartsWith("import os") && !line.StartsWith("print("))
            {
                return line;
            }
        }

        return "/";
    }

    public static async Task RemoteDeleteAsync(WebSocket ws, string path, CancellationToken cancellationToken = default)
    {
        var pathEscaped = path.Replace("'", "\\'");
        var pyExpr = PythonSnippets.DeleteFile(pathEscaped);
        await RemoteEvalAsync(ws, pyExpr, cancellationToken);
    }

    public static async Task RemoteMkdirAsync(WebSocket ws, string path, CancellationToken cancellationToken = default)
    {
        var pathEscaped = path.Replace("'", "\\'");
        var pyExpr = PythonSnippets.MakeDirectory(pathEscaped);
        await RemoteEvalAsync(ws, pyExpr, cancellationToken);
    }

    public static async Task RemoteRmdirAsync(WebSocket ws, string path, CancellationToken cancellationToken = default)
    {
        var pathEscaped = path.Replace("'", "\\'");
        var pyExpr = PythonSnippets.RemoveDirectory(pathEscaped);
        await RemoteEvalAsync(ws, pyExpr, cancellationToken);
    }

    public static async Task RemoteResetAsync(WebSocket ws, bool hard = false, CancellationToken cancellationToken = default)
    {
        await ClearBufferAsync(ws, 500, cancellationToken);

        var pyExpr = hard 
            ? PythonSnippets.HardReset()
            : PythonSnippets.SoftReset();

        var command = pyExpr + "\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(command);
        await ws.WriteAsync(commandBytes, WebSocket.WEBREPL_FRAME_TXT, cancellationToken);

        await Task.Delay(500, cancellationToken);
    }
}

public record RemoteFileInfo(string Name, bool IsDirectory, int Size);
