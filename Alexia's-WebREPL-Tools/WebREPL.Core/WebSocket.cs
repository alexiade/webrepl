using System.Net.WebSockets;

namespace WebREPL.Core;

public class WebSocket : IDisposable
{
    public const byte WEBREPL_FRAME_TXT = 0x81;
    public const byte WEBREPL_FRAME_BIN = 0x82;

    private readonly ClientWebSocket _clientWebSocket;
    private byte _frameType = WEBREPL_FRAME_TXT;
    private bool _disposed;
    private readonly List<byte> _buffer = new();
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public WebSocket(ClientWebSocket clientWebSocket)
    {
        _clientWebSocket = clientWebSocket ?? throw new ArgumentNullException(nameof(clientWebSocket));
    }

    public void SetBinaryMode()
    {
        _frameType = WEBREPL_FRAME_BIN;
    }

    public void SetTextMode()
    {
        _frameType = WEBREPL_FRAME_TXT;
    }

    public void ClearBuffer()
    {
        _buffer.Clear();
    }

    public async Task DrainStreamAsync(CancellationToken cancellationToken = default)
    {
        _buffer.Clear();

        if (_clientWebSocket.State != WebSocketState.Open && _clientWebSocket.State != WebSocketState.CloseReceived)
            return;

        await _readLock.WaitAsync(cancellationToken);
        try
        {
            var drainBuffer = new byte[4096];
            var timeoutTask = Task.Delay(100, cancellationToken);
            var pendingReceive = (Task<WebSocketReceiveResult>?)null;

            while (_clientWebSocket.State == WebSocketState.Open || _clientWebSocket.State == WebSocketState.CloseReceived)
            {
                pendingReceive = _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(drainBuffer), CancellationToken.None);
                var completed = await Task.WhenAny(pendingReceive, timeoutTask);

                if (completed == timeoutTask)
                {
                    // CRITICAL: Must wait for pending receive before releasing lock
                    var extraWait = Task.Delay(50, cancellationToken);
                    var extraCompleted = await Task.WhenAny(pendingReceive, extraWait);

                    if (extraCompleted == pendingReceive)
                    {
                        try
                        {
                            await pendingReceive;
                        }
                        catch { }
                    }
                    break;
                }

                var result = await pendingReceive;
                if (result.Count == 0)
                    break;

                // Reset timeout for next iteration
                timeoutTask = Task.Delay(100, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DrainStreamAsync] Exception: {ex.GetType().Name}: {ex.Message}, State: {_clientWebSocket.State}");
        }
        finally
        {
            _readLock.Release();
        }
    }

    public async Task WriteAsync(byte[] data, byte frameType, CancellationToken cancellationToken = default)
    {
        if (_clientWebSocket.State != WebSocketState.Open)
            throw new InvalidOperationException($"WebSocket is not in Open state. Current state: {_clientWebSocket.State}");

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var messageType = frameType == WEBREPL_FRAME_TXT ? WebSocketMessageType.Text : WebSocketMessageType.Binary;
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(data), messageType, true, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WriteAsync] Exception: {ex.GetType().Name}: {ex.Message}, State: {_clientWebSocket.State}");
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<byte[]> ReadAsync(int count, bool textOk = false, CancellationToken cancellationToken = default)
    {
        await _readLock.WaitAsync(cancellationToken);
        try
        {
            while (_buffer.Count < count)
            {
                if (_clientWebSocket.State != WebSocketState.Open && _clientWebSocket.State != WebSocketState.CloseReceived)
                    throw new InvalidOperationException($"WebSocket is not in a valid state for reading. Current state: {_clientWebSocket.State}");

                while (true)
                {
                    var tempBuffer = new byte[8192];
                    var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(tempBuffer), CancellationToken.None);

                    if (result.Count == 0 || result.MessageType == WebSocketMessageType.Close)
                        throw new IOException("Connection closed");

                    bool acceptFrame = (result.MessageType == WebSocketMessageType.Binary) || 
                                       (textOk && result.MessageType == WebSocketMessageType.Text);

                    if (acceptFrame)
                    {
                        _buffer.AddRange(tempBuffer.Take(result.Count));
                        break;
                    }
                }
            }

            var data = _buffer.Take(count).ToArray();
            _buffer.RemoveRange(0, count);
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReadAsync] Exception: {ex.GetType().Name}: {ex.Message}, State: {_clientWebSocket.State}");
            throw;
        }
        finally
        {
            _readLock.Release();
        }
    }

    public async Task<byte[]> ReadAvailableAsync(int maxBytes, int timeoutMs = 100, CancellationToken cancellationToken = default)
    {
        if (_clientWebSocket.State != WebSocketState.Open && _clientWebSocket.State != WebSocketState.CloseReceived)
            return Array.Empty<byte>();

        await _readLock.WaitAsync(cancellationToken);
        try
        {
            var payload = new List<byte>();
            var startTime = DateTime.UtcNow;

            while (payload.Count < maxBytes)
            {
                if (_clientWebSocket.State != WebSocketState.Open && _clientWebSocket.State != WebSocketState.CloseReceived)
                    break;

                var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                var remaining = timeoutMs - elapsed;

                if (remaining <= 0)
                    break;

                var tempBuffer = new byte[8192];
                var timeoutTask = Task.Delay(remaining, cancellationToken);
                var receiveTask = _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(tempBuffer), CancellationToken.None);

                var completed = await Task.WhenAny(receiveTask, timeoutTask);

                if (completed == timeoutTask)
                {
                    // CRITICAL: Must wait for pending receive before releasing lock
                    // Give it 50ms more to complete, then return what we have
                    var extraWait = Task.Delay(50, cancellationToken);
                    var extraCompleted = await Task.WhenAny(receiveTask, extraWait);

                    if (extraCompleted == receiveTask)
                    {
                        try
                        {
                            var lateResult = await receiveTask;
                            if (lateResult.Count > 0 && lateResult.MessageType != WebSocketMessageType.Close)
                            {
                                payload.AddRange(tempBuffer.Take(lateResult.Count));
                            }
                        }
                        catch { }
                    }
                    break;
                }

                var result = await receiveTask;

                if (result.Count == 0 || result.MessageType == WebSocketMessageType.Close)
                    break;

                payload.AddRange(tempBuffer.Take(result.Count));

                // Reset timer after successful read for next iteration
                startTime = DateTime.UtcNow;
            }

            return payload.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReadAvailableAsync] Exception: {ex.GetType().Name}: {ex.Message}, State: {_clientWebSocket.State}");
            return Array.Empty<byte>();
        }
        finally
        {
            _readLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _clientWebSocket?.Dispose();
        _readLock?.Dispose();
        _writeLock?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
