using System.Net.Sockets;
using System.Net;
using System.Text;

namespace WebREPL.Core;

public class WebReplClient : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private WebSocket? _websocket;
    private bool _disposed;

    public bool IsConnected => _tcpClient?.Connected ?? false;
    public string? RemoteVersion { get; private set; }

    public async Task<bool> ConnectAsync(string host, int port = 8266, string password = "", CancellationToken cancellationToken = default)
    {
        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port, cancellationToken);
            _stream = _tcpClient.GetStream();

            await PerformHandshakeAsync(cancellationToken);
            _websocket = new WebSocket(_stream);

            await LoginAsync(password, cancellationToken);
            RemoteVersion = await GetVersionAsync(cancellationToken);

            _websocket.SetBinaryMode();

            return true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private async Task PerformHandshakeAsync(CancellationToken cancellationToken)
    {
        if (_stream == null) throw new InvalidOperationException("Stream not initialized");

        var handshake = Encoding.ASCII.GetBytes(
            "GET / HTTP/1.1\r\n" +
            "Host: echo.websocket.org\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Key: foo\r\n" +
            "\r\n"
        );

        await _stream.WriteAsync(handshake, cancellationToken);

        var buffer = new byte[4096];
        var response = new StringBuilder();

        while (true)
        {
            var bytesRead = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0) break;

            var chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            response.Append(chunk);

            if (response.ToString().Contains("\r\n\r\n"))
                break;
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

        var versionCmd = Encoding.UTF8.GetBytes("\x06");
        await _websocket.WriteAsync(versionCmd, WebSocket.WEBREPL_FRAME_BIN, cancellationToken);

        var response = await _websocket.ReadAsync(4, cancellationToken);
        if (response.Length >= 3)
        {
            return $"{response[1]}.{response[2]}.{response[3]}";
        }

        return "unknown";
    }

    public async Task PutFileAsync(string localPath, string remotePath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        var fileInfo = new FileInfo(localPath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"Local file not found: {localPath}");

        var fileSize = (int)fileInfo.Length;

        using var fileStream = File.OpenRead(localPath);

        var remotePathBytes = Encoding.UTF8.GetBytes(remotePath);
        var header = new byte[1 + 1 + 2 + 4 + 2 + remotePathBytes.Length];
        header[0] = (byte)'W';
        header[1] = (byte)'A';

        BitConverter.GetBytes((ushort)1).CopyTo(header, 2);
        BitConverter.GetBytes(fileSize).CopyTo(header, 4);
        BitConverter.GetBytes((ushort)remotePathBytes.Length).CopyTo(header, 8);
        remotePathBytes.CopyTo(header, 10);

        await _websocket.WriteAsync(header, WebSocket.WEBREPL_FRAME_BIN, cancellationToken);

        var response = await _websocket.ReadAsync(4, cancellationToken);

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

        response = await _websocket.ReadAsync(4, cancellationToken);
    }

    public async Task GetFileAsync(string remotePath, string localPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        var remotePathBytes = Encoding.UTF8.GetBytes(remotePath);
        var header = new byte[1 + 1 + 2 + 4 + 2 + remotePathBytes.Length];
        header[0] = (byte)'W';
        header[1] = (byte)'A';

        BitConverter.GetBytes((ushort)2).CopyTo(header, 2);
        BitConverter.GetBytes(0).CopyTo(header, 4);
        BitConverter.GetBytes((ushort)remotePathBytes.Length).CopyTo(header, 8);
        remotePathBytes.CopyTo(header, 10);

        await _websocket.WriteAsync(header, WebSocket.WEBREPL_FRAME_BIN, cancellationToken);

        var response = await _websocket.ReadAsync(4, cancellationToken);

        using var fileStream = File.Create(localPath);
        int totalReceived = 0;

        while (true)
        {
            var sizeBytes = await _websocket.ReadAsync(2, cancellationToken);
            var chunkSize = BitConverter.ToUInt16(sizeBytes, 0);

            if (chunkSize == 0)
                break;

            var data = await _websocket.ReadAsync(chunkSize, cancellationToken);
            await fileStream.WriteAsync(data, cancellationToken);
            totalReceived += data.Length;

            progress?.Report(new FileTransferProgress(totalReceived, totalReceived, remotePath, localPath));
        }

        response = await _websocket.ReadAsync(4, cancellationToken);
    }

    public async Task<string> ExecuteAsync(string pythonCode, CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        return await RemoteCommands.RemoteEvalAsync(_websocket, pythonCode, cancellationToken);
    }

    public async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        if (_websocket == null) throw new InvalidOperationException("Not connected");

        await RemoteCommands.InterruptRunningCodeAsync(_websocket, cancellationToken);
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
        _stream?.Dispose();
        _tcpClient?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public record FileTransferProgress(int BytesTransferred, int TotalBytes, string SourcePath, string DestinationPath)
{
    public double PercentComplete => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
}
