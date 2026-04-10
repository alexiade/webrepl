using System.Net.Sockets;

namespace WebREPL.Core;

public class WebSocket : IDisposable
{
    public const byte WEBREPL_FRAME_TXT = 1;
    public const byte WEBREPL_FRAME_BIN = 2;

    private readonly NetworkStream _stream;
    private byte _frameType = WEBREPL_FRAME_TXT;
    private bool _disposed;

    public WebSocket(NetworkStream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public void SetBinaryMode()
    {
        _frameType = WEBREPL_FRAME_BIN;
    }

    public void SetTextMode()
    {
        _frameType = WEBREPL_FRAME_TXT;
    }

    public async Task WriteAsync(byte[] data, byte frameType, CancellationToken cancellationToken = default)
    {
        var header = new byte[2];
        header[0] = (byte)(0x80 | frameType);
        header[1] = (byte)data.Length;

        if (data.Length < 126)
        {
            header[1] = (byte)data.Length;
            await _stream.WriteAsync(header, cancellationToken);
        }
        else if (data.Length < 65536)
        {
            header[1] = 126;
            await _stream.WriteAsync(header, cancellationToken);

            var lengthBytes = BitConverter.GetBytes((ushort)data.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            await _stream.WriteAsync(lengthBytes, cancellationToken);
        }
        else
        {
            throw new NotSupportedException("Messages larger than 64KB are not supported");
        }

        await _stream.WriteAsync(data, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    public async Task<byte[]> ReadAsync(int count, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[count];
        var totalRead = 0;

        while (totalRead < count)
        {
            var bytesRead = await _stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), cancellationToken);
            if (bytesRead == 0)
                throw new IOException("Connection closed while reading data");

            totalRead += bytesRead;
        }

        return buffer;
    }

    public async Task<byte[]> ReadAvailableAsync(int maxBytes, int timeoutMs = 100, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        var buffer = new List<byte>();
        var tempBuffer = new byte[Math.Min(1024, maxBytes)];

        try
        {
            while (buffer.Count < maxBytes)
            {
                if (!_stream.DataAvailable)
                {
                    await Task.Delay(10, cts.Token);
                    if (!_stream.DataAvailable)
                        break;
                }

                var toRead = Math.Min(tempBuffer.Length, maxBytes - buffer.Count);
                var bytesRead = await _stream.ReadAsync(tempBuffer.AsMemory(0, toRead), cts.Token);

                if (bytesRead == 0)
                    break;

                buffer.AddRange(tempBuffer.Take(bytesRead));
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached, return what we have
        }

        return buffer.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
