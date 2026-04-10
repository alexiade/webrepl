using System.Text;

namespace WebREPL.Core;

public static class RemoteCommands
{
    public static async Task<string> RemoteEvalAsync(WebSocket ws, string pythonExpression, CancellationToken cancellationToken = default)
    {
        await ClearBufferAsync(ws, 500, cancellationToken);

        var command = pythonExpression + "\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(command);
        await ws.WriteAsync(commandBytes, WebSocket.WEBREPL_FRAME_TXT, cancellationToken);

        await Task.Delay(100, cancellationToken);

        var output = await ws.ReadAvailableAsync(65536, 1000, cancellationToken);
        return Encoding.UTF8.GetString(output);
    }

    public static async Task ClearBufferAsync(WebSocket ws, int timeoutMs = 500, CancellationToken cancellationToken = default)
    {
        await ws.ReadAvailableAsync(65536, timeoutMs, cancellationToken);
    }

    public static async Task InterruptRunningCodeAsync(WebSocket ws, CancellationToken cancellationToken = default)
    {
        await ClearBufferAsync(ws, 500, cancellationToken);

        var ctrlC = new byte[] { 0x03, 0x03 };
        await ws.WriteAsync(ctrlC, WebSocket.WEBREPL_FRAME_TXT, cancellationToken);

        await Task.Delay(100, cancellationToken);
        await ClearBufferAsync(ws, 500, cancellationToken);
    }

    public static async Task<List<RemoteFileInfo>> RemoteLsAsync(WebSocket ws, string? path = null, CancellationToken cancellationToken = default)
    {
        var pathArg = path != null ? $"'{path.Replace("'", "\\'")}'" : "";
        var pyExpr = $@"
import os
try:
    items = os.listdir({pathArg}) if '{pathArg}' else os.listdir()
    for item in items:
        try:
            stat = os.stat(item if not '{pathArg}' else '{pathArg}/' + item)
            isdir = (stat[0] & 0x4000) != 0
            size = stat[6]
            print(f'{{item}},{{isdir}},{{size}}')
        except:
            pass
except Exception as e:
    print(f'Error: {{e}}')
";

        var output = await RemoteEvalAsync(ws, pyExpr, cancellationToken);
        var files = new List<RemoteFileInfo>();

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(">>>") || trimmed.StartsWith("import") || 
                trimmed.StartsWith("try:") || trimmed.StartsWith("Error:"))
                continue;

            var parts = trimmed.Split(',');
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

        return files;
    }

    public static async Task RemoteCdAsync(WebSocket ws, string path, CancellationToken cancellationToken = default)
    {
        var pathEscaped = path.Replace("'", "\\'");
        var pyExpr = $"import os; os.chdir('{pathEscaped}')";
        await RemoteEvalAsync(ws, pyExpr, cancellationToken);
    }

    public static async Task<string> RemotePwdAsync(WebSocket ws, CancellationToken cancellationToken = default)
    {
        var pyExpr = "import os;print(os.getcwd())";
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
        var pyExpr = $"import os; os.remove('{pathEscaped}')";
        await RemoteEvalAsync(ws, pyExpr, cancellationToken);
    }

    public static async Task RemoteMkdirAsync(WebSocket ws, string path, CancellationToken cancellationToken = default)
    {
        var pathEscaped = path.Replace("'", "\\'");
        var pyExpr = $"import os; os.mkdir('{pathEscaped}')";
        await RemoteEvalAsync(ws, pyExpr, cancellationToken);
    }

    public static async Task RemoteRmdirAsync(WebSocket ws, string path, CancellationToken cancellationToken = default)
    {
        var pathEscaped = path.Replace("'", "\\'");
        var pyExpr = $"import os; os.rmdir('{pathEscaped}')";
        await RemoteEvalAsync(ws, pyExpr, cancellationToken);
    }

    public static async Task RemoteResetAsync(WebSocket ws, bool hard = false, CancellationToken cancellationToken = default)
    {
        await ClearBufferAsync(ws, 500, cancellationToken);

        var pyExpr = hard 
            ? "import machine; machine.reset()" 
            : "import machine; machine.soft_reset()";

        var command = pyExpr + "\r\n";
        var commandBytes = Encoding.UTF8.GetBytes(command);
        await ws.WriteAsync(commandBytes, WebSocket.WEBREPL_FRAME_TXT, cancellationToken);

        await Task.Delay(500, cancellationToken);
    }
}

public record RemoteFileInfo(string Name, bool IsDirectory, int Size);
