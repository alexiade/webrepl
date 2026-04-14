using System.Net.WebSockets;
using System.Text;

namespace WebREPL.Core;

public class WebReplClient : IDisposable
{
    private const byte WEBREPL_PUT_FILE = 1;
    private const byte WEBREPL_GET_FILE = 2;
    private const byte WEBREPL_GET_VERSION = 3;
    private ClientWebSocket? _clientWebSocket;
    private WebSocket? _websocket;
    private bool _disposed;

    public bool IsConnected => _clientWebSocket?.State == WebSocketState.Open;
    public string? RemoteVersion { get; private set; }

    public async Task<bool> ConnectAsync(string host, int port = 8266, string password = "", CancellationToken cancellationToken = default)
    {
        try
        {
            _clientWebSocket = new ClientWebSocket();
            var uri = new Uri($"ws://{host}:{port}");

            await _clientWebSocket.ConnectAsync(uri, cancellationToken);
            _websocket = new WebSocket(_clientWebSocket);

            await LoginAsync(password, cancellationToken);
            RemoteVersion = await GetVersionAsync(cancellationToken);

            return true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private async Task LoginAsync(string password, CancellationToken cancellationToken)
    {
        if (_websocket == null) throw new InvalidOperationException("WebSocket not initialized");

        var passwordBytes = Encoding.UTF8.GetBytes(password + "\r\n");
        await _websocket.WriteAsync(passwordBytes, WebSocket.WEBREPL_FRAME_TXT, cancellationToken);

        await Task.Delay(100, cancellationToken);
    }

    private async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        if (_websocket == null) throw new InvalidOperationException("WebSocket not initialized");

        _websocket.ClearBuffer();

        var versionCmd = new byte[82];
        versionCmd[0] = (byte)'W';
        versionCmd[1] = (byte)'A';
        versionCmd[2] = WEBREPL_GET_VERSION;  // operation type (B)
        versionCmd[3] = 0;                  // padding (B)
        await _websocket.WriteAsync(versionCmd, WebSocket.WEBREPL_FRAME_BIN, cancellationToken);

        // Wait for response to arrive
        await Task.Delay(200, cancellationToken);

        // Python: d = ws.read(3); d = struct.unpack("<BBB", d)     
        var response = await _websocket.ReadAsync(3, false, cancellationToken);

        if (response.Length >= 3)
        {
            return $"{response[0]}.{response[1]}.{response[2]}";
        }

        return "unknown";
    }

    private async Task<ushort> ReadResponseAsync(CancellationToken cancellationToken)
    {
        // Python: def read_resp(ws):
        //           data = ws.read(4)
        //           sig, code = struct.unpack("<2sH", data)
        //           assert sig == b"WB"
        //           return code
        var data = await _websocket!.ReadAsync(2, false, cancellationToken);
        var size = await _websocket!.ReadAsync(2, false, cancellationToken);

        if (data[0] != (byte)'W' || data[1] != (byte)'B')
            throw new InvalidOperationException($"Invalid response signature: expected 'WB', got '{(char)data[0]}{(char)data[1]}'");

        var code = BitConverter.ToUInt16(size, 0);

        return code;
    }

    public async Task PutFileAsync(string localPath, string remotePath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        // Clear any leftover data in buffer before file transfer
        _websocket.ClearBuffer();

        // Convert local path to absolute
        localPath = Path.GetFullPath(localPath);

        // Use remote path as-is (like Python's SANDBOX + remote_file)
        // DO NOT resolve against pwd - WebREPL expects the path exactly as given

        var fileInfo = new FileInfo(localPath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"Local file not found: {localPath}");

        var fileSize = (int)fileInfo.Length;

        using var fileStream = File.OpenRead(localPath);

        var remotePathBytes = Encoding.UTF8.GetBytes(remotePath);

        if (remotePathBytes.Length > 64)
            throw new ArgumentException($"Remote path too long: {remotePathBytes.Length} bytes (max 64)", nameof(remotePath));

        // WEBREPL_REQ_S = "<2sBBQLH64s" - fixed 82 byte structure
        // struct.pack(WEBREPL_REQ_S, b"WA", WEBREPL_PUT_FILE, 0, 0, sz, len(dest_fname), dest_fname)
        var header = new byte[82];
        header[0] = (byte)'W';
        header[1] = (byte)'A';
        header[2] = WEBREPL_PUT_FILE;  // operation type (B)
        header[3] = 0;                  // padding (B)

        // Offset 4-11: Q (8 bytes) - always 0 (little-endian)
        // All zeros, so endianness doesn't matter

        // Offset 12-15: L (4 bytes) - file size (little-endian)
        var sizeBytes = BitConverter.GetBytes((uint)fileSize);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(sizeBytes);
        sizeBytes.CopyTo(header, 12);

        // Offset 16-17: H (2 bytes) - filename length (little-endian)
        var lengthBytes = BitConverter.GetBytes((ushort)remotePathBytes.Length);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        lengthBytes.CopyTo(header, 16);

        // Offset 18-81: 64s - filename (padded with zeros)
        Array.Copy(remotePathBytes, 0, header, 18, remotePathBytes.Length);

        // Python: ws.write(rec[:10])
        //         ws.write(rec[10:])
        // Split header write for PUT (first 10 bytes, then remaining 72)
        await _websocket.WriteAsync(header.AsSpan(0, 10).ToArray(), WebSocket.WEBREPL_FRAME_BIN, cancellationToken);
        await _websocket.WriteAsync(header.AsSpan(10, 72).ToArray(), WebSocket.WEBREPL_FRAME_BIN, cancellationToken);

        // Python: assert read_resp(ws) == 0
        var responseCode = await ReadResponseAsync(cancellationToken);
        if (responseCode != 0)
            throw new IOException($"PUT file request failed with code: {responseCode}");

        var buffer = new byte[1024];
        int totalSent = 0;

        while (totalSent < fileSize)
        {
            var toRead = Math.Min(buffer.Length, fileSize - totalSent);
            var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);

            if (bytesRead > 0)
            {
                await _websocket.WriteAsync(buffer.AsMemory(0, bytesRead).ToArray(), WebSocket.WEBREPL_FRAME_BIN, cancellationToken);
                totalSent += bytesRead;

                progress?.Report(new FileTransferProgress(totalSent, fileSize, localPath, remotePath));
            }
        }

        // Python: assert read_resp(ws) == 0
        responseCode = await ReadResponseAsync(cancellationToken);
        if (responseCode != 0)
            throw new IOException($"PUT file transfer failed with code: {responseCode}");
    }

    public async Task GetFileAsync(string remotePath, string localPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        // Clear any leftover data in buffer before file transfer
        _websocket.ClearBuffer();

        // Convert local path to absolute
        localPath = Path.GetFullPath(localPath);

        // Use remote path as-is (like Python's SANDBOX + remote_file)
        // DO NOT resolve against pwd - WebREPL expects the path exactly as given
        var remotePathBytes = Encoding.UTF8.GetBytes(remotePath);

        if (remotePathBytes.Length > 64)
            throw new ArgumentException($"Remote path too long: {remotePathBytes.Length} bytes (max 64)", nameof(remotePath));

        // WEBREPL_REQ_S = "<2sBBQLH64s" - fixed 82 byte structure
        // struct.pack(WEBREPL_REQ_S, b"WA", WEBREPL_GET_FILE, 0, 0, 0, len(src_fname), src_fname)
        var header = new byte[82];
        header[0] = (byte)'W';
        header[1] = (byte)'A';
        header[2] = WEBREPL_GET_FILE;  // operation type (B)
        header[3] = 0;                  // padding (B)

        // Offset 4-11: Q (8 bytes) - always 0 (little-endian)
        // All zeros, so endianness doesn't matter

        // Offset 12-15: L (4 bytes) - always 0 for GET (little-endian)
        // All zeros, so endianness doesn't matter

        // Offset 16-17: H (2 bytes) - filename length (little-endian)
        var lengthBytes = BitConverter.GetBytes((ushort)remotePathBytes.Length);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        lengthBytes.CopyTo(header, 16);

        // Offset 18-81: 64s - filename (padded with zeros)
        Array.Copy(remotePathBytes, 0, header, 18, remotePathBytes.Length);

        await _websocket.WriteAsync(header, WebSocket.WEBREPL_FRAME_BIN, cancellationToken);

        // Wait for response to arrive
        await Task.Delay(200, cancellationToken);

        // Python: assert read_resp(ws) == 0
        var responseCode = await ReadResponseAsync(cancellationToken);
        if (responseCode != 0)
            throw new IOException($"GET file request failed with code: {responseCode}");

        using var fileStream = File.Create(localPath);
        int totalReceived = 0;

        while (true)
        {
            await _websocket.WriteAsync(new byte[] { 0 }, WebSocket.WEBREPL_FRAME_BIN, cancellationToken);

            var sizeBytes = await _websocket.ReadAsync(2, false, cancellationToken);
            var chunkSize = BitConverter.ToUInt16(sizeBytes, 0);

            if (chunkSize == 0)
                break;

            int remainingInChunk = chunkSize;
            while (remainingInChunk > 0)
            {
                var buf = await _websocket.ReadAsync(remainingInChunk, false, cancellationToken);
                if (buf.Length == 0)
                    throw new IOException("Connection closed while receiving file data");

                await fileStream.WriteAsync(buf, cancellationToken);
                totalReceived += buf.Length;
                remainingInChunk -= buf.Length;

                progress?.Report(new FileTransferProgress(totalReceived, totalReceived, remotePath, localPath));
            }
        }

        // Python: assert read_resp(ws) == 0
        responseCode = await ReadResponseAsync(cancellationToken);
        if (responseCode != 0)
            throw new IOException($"GET file transfer failed with code: {responseCode}");
    }

    public async Task<string> ExecuteAsync(string pythonCode, CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        return await RemoteCommands.RemoteEvalAsync(_websocket, pythonCode, cancellationToken);
    }

    public async Task<bool> InterruptAsync(CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        return await RemoteCommands.InterruptRunningCodeAsync(_websocket, cancellationToken);
    }

    public async Task TputFileAsync(string localPath, string remotePath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        localPath = Path.GetFullPath(localPath);
        var fileInfo = new FileInfo(localPath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"Local file not found: {localPath}");

        // Read the file content
        var content = await File.ReadAllTextAsync(localPath, cancellationToken);
        var totalBytes = content.Length;

        // Escape the content for Python string literal
        var escaped = content
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

        // Split into chunks to avoid command line length issues
        const int chunkSize = 512;
        var chunks = new List<string>();
        for (int i = 0; i < escaped.Length; i += chunkSize)
        {
            var chunk = escaped.Substring(i, Math.Min(chunkSize, escaped.Length - i));
            chunks.Add(chunk);
        }

        // Create Python code to write the file
        var remotePathEscaped = remotePath.Replace("'", "\\'");

        // Write in chunks
        for (int i = 0; i < chunks.Count; i++)
        {
            var mode = i == 0 ? "w" : "a";
            var pythonCode = $"f=open('{remotePathEscaped}','{mode}');f.write('{chunks[i]}');f.close()";
            await RemoteCommands.RemoteEvalAsync(_websocket, pythonCode, cancellationToken);

            var bytesTransferred = Math.Min((i + 1) * chunkSize, content.Length);
            progress?.Report(new FileTransferProgress(bytesTransferred, totalBytes, localPath, remotePath));
        }

        progress?.Report(new FileTransferProgress(totalBytes, totalBytes, localPath, remotePath));
    }

    public async Task TgetFileAsync(string remotePath, string localPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        localPath = Path.GetFullPath(localPath);
        var remotePathEscaped = remotePath.Replace("'", "\\'");

        // Read the file in chunks to avoid memory issues
        var pythonCode = $"f=open('{remotePathEscaped}','r');c=f.read();f.close();print(repr(c))";
        var output = await RemoteCommands.RemoteEvalAsync(_websocket, pythonCode, cancellationToken);

        // Parse the repr() output
        if (output.StartsWith("'") && output.EndsWith("'"))
        {
            output = output[1..^1];
        }

        // Unescape Python string literals
        var content = output
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\'", "'")
            .Replace("\\\\", "\\");

        // Write to local file
        await File.WriteAllTextAsync(localPath, content, cancellationToken);

        var bytes = content.Length;
        progress?.Report(new FileTransferProgress(bytes, bytes, remotePath, localPath));
    }

    public WebSocket GetWebSocket()
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");
        return _websocket;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _websocket?.Dispose();
        _clientWebSocket?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public record FileTransferProgress(int BytesTransferred, int TotalBytes, string SourcePath, string DestinationPath)
{
    public double PercentComplete => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
}
