using System.Net.Sockets;

namespace WebREPL.Core;

public class WebSocket : IDisposable
{
    public const byte WEBREPL_FRAME_TXT = 0x81;
    public const byte WEBREPL_FRAME_BIN = 0x82;

    private readonly NetworkStream _stream;
    private byte _frameType = WEBREPL_FRAME_TXT;
    private bool _disposed;
    private readonly List<byte> _buffer = new(); // Persistent buffer for ReadAsync

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
        header[0] = (byte)(0x80 | (frameType & 0x0F));
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

    public async Task<byte[]> ReadAsync(int count, bool textOk = false, CancellationToken cancellationToken = default)
    {
        // Read frames until buffer has enough data (like Python's self.buf)
        while (_buffer.Count < count)
        {
            // Read frame header
            var header = new byte[2];
            await ReadExactlyAsync(header, cancellationToken);

            byte frameType = header[0];
            int length = header[1] & 0x7F;

            // Handle extended length
            if (length == 126)
            {
                var lenBytes = new byte[2];
                await ReadExactlyAsync(lenBytes, cancellationToken);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(lenBytes);
                length = BitConverter.ToUInt16(lenBytes, 0);
            }

            // Read frame payload
            var payload = new byte[length];
            await ReadExactlyAsync(payload, cancellationToken);

            // Accept binary frames, or text frames if textOk=true (like Python)
            if (frameType == WEBREPL_FRAME_BIN || (textOk && frameType == WEBREPL_FRAME_TXT))
            {
                _buffer.AddRange(payload);
            }
            // Skip other frame types (REPL echo during file transfer)
        }

        // Return requested bytes and remove from buffer (like Python's d = self.buf[:size]; self.buf = self.buf[size:])
        var result = _buffer.Take(count).ToArray();
        _buffer.RemoveRange(0, count);
        return result;
    }

    private async Task ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0) throw new IOException("Connection closed");
            offset += read;
        }
    }

    public async Task<byte[]> ReadAvailableAsync(int maxBytes, int timeoutMs = 100, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        var payload = new List<byte>();

        try
        {
            while (payload.Count < maxBytes && _stream.DataAvailable)
            {
                // Read frame header
                var header = new byte[2];
                await ReadExactlyAsync(header, cts.Token);

                byte frameType = header[0];
                int length = header[1] & 0x7F;

                // Handle extended length
                if (length == 126)
                {
                    var lenBytes = new byte[2];
                    await ReadExactlyAsync(lenBytes, cts.Token);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lenBytes);
                    length = BitConverter.ToUInt16(lenBytes, 0);
                }

                // Read frame payload
                var frameData = new byte[length];
                await ReadExactlyAsync(frameData, cts.Token);

                // Accept all frames for ReadAvailableAsync (text commands)
                payload.AddRange(frameData);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout - return what we have
        }

        return payload.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
