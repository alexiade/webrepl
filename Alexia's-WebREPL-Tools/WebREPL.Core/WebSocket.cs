using System.Net.WebSockets;

namespace WebREPL.Core;

public class WebSocket : IDisposable
{
    public const byte WEBREPL_FRAME_TXT = 0x81;
    public const byte WEBREPL_FRAME_BIN = 0x82;

    private readonly ClientWebSocket _clientWebSocket;
    private bool _disposed;
    private readonly List<byte> _textBuffer = new();
    private readonly List<byte> _binaryBuffer = new();
    private readonly SemaphoreSlim _textReadLock = new(1, 1);
    private readonly SemaphoreSlim _binaryReadLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _receiveLock = new(1, 1);

    public WebSocket(ClientWebSocket clientWebSocket)
    {
        _clientWebSocket = clientWebSocket ?? throw new ArgumentNullException(nameof(clientWebSocket));
    }

    public void ClearBuffer()
    {
        lock (_textBuffer)
            _textBuffer.Clear();
        lock (_binaryBuffer)
            _binaryBuffer.Clear();
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
        // Determine which buffer and lock to use based on what we're reading
        var targetBuffer = textOk ? _textBuffer : _binaryBuffer;
        var targetLock = textOk ? _textReadLock : _binaryReadLock;

        await targetLock.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                // Check if we have enough data in the target buffer
                int availableCount;
                lock (targetBuffer)
                {
                    availableCount = targetBuffer.Count;
                }

                if (availableCount >= count)
                    break;

                // Need more data - receive from WebSocket
                if (_clientWebSocket.State != WebSocketState.Open && _clientWebSocket.State != WebSocketState.CloseReceived)
                    throw new InvalidOperationException($"WebSocket is not in a valid state for reading. Current state: {_clientWebSocket.State}");

                await _receiveLock.WaitAsync(cancellationToken);
                try
                {
                    var tempBuffer = new byte[8192];
                    var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(tempBuffer), CancellationToken.None);

                    if (result.Count == 0 || result.MessageType == WebSocketMessageType.Close)
                        throw new IOException("Connection closed");

                    var receivedData = tempBuffer.Take(result.Count).ToArray();

                    // Route data to appropriate buffer based on message type
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        lock (_binaryBuffer)
                        {
                            _binaryBuffer.AddRange(receivedData);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        lock (_textBuffer)
                        {
                            _textBuffer.AddRange(receivedData);
                        }
                    }
                }
                finally
                {
                    _receiveLock.Release();
                }
            }

            // Extract data from target buffer
            byte[] data;
            lock (targetBuffer)
            {
                data = targetBuffer.Take(count).ToArray();
                targetBuffer.RemoveRange(0, count);
            }
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReadAsync] Exception: {ex.GetType().Name}: {ex.Message}, State: {_clientWebSocket.State}");
            throw;
        }
        finally
        {
            targetLock.Release();
        }
    }

    public async Task<byte[]> ReadAvailableAsync(int maxBytes, int timeoutMs = 100, CancellationToken cancellationToken = default)
    {
        if (_clientWebSocket.State != WebSocketState.Open && _clientWebSocket.State != WebSocketState.CloseReceived)
            return Array.Empty<byte>();

        var result = new List<byte>();
        var startTime = DateTime.UtcNow;

        // First, collect any data already in buffers (both text and binary)
        lock (_textBuffer)
        {
            var textAvailable = Math.Min(_textBuffer.Count, maxBytes - result.Count);
            if (textAvailable > 0)
            {
                result.AddRange(_textBuffer.Take(textAvailable));
                _textBuffer.RemoveRange(0, textAvailable);
            }
        }

        lock (_binaryBuffer)
        {
            var binaryAvailable = Math.Min(_binaryBuffer.Count, maxBytes - result.Count);
            if (binaryAvailable > 0)
            {
                result.AddRange(_binaryBuffer.Take(binaryAvailable));
                _binaryBuffer.RemoveRange(0, binaryAvailable);
            }
        }

        // If we already have data or timeout is zero, return immediately
        if (result.Count > 0 || timeoutMs <= 0)
            return result.ToArray();

        // Try to receive more data with timeout
        await _receiveLock.WaitAsync(cancellationToken);
        try
        {
            while (result.Count < maxBytes)
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
                    var extraWait = Task.Delay(50, cancellationToken);
                    var extraCompleted = await Task.WhenAny(receiveTask, extraWait);

                    if (extraCompleted == receiveTask)
                    {
                        try
                        {
                            var lateResult = await receiveTask;
                            if (lateResult.Count > 0 && lateResult.MessageType != WebSocketMessageType.Close)
                            {
                                var data = tempBuffer.Take(lateResult.Count).ToArray();
                                var toTake = Math.Min(data.Length, maxBytes - result.Count);
                                result.AddRange(data.Take(toTake));

                                // Store remainder in appropriate buffer
                                if (toTake < data.Length)
                                {
                                    var remainder = data.Skip(toTake).ToArray();
                                    if (lateResult.MessageType == WebSocketMessageType.Binary)
                                    {
                                        lock (_binaryBuffer)
                                            _binaryBuffer.AddRange(remainder);
                                    }
                                    else if (lateResult.MessageType == WebSocketMessageType.Text)
                                    {
                                        lock (_textBuffer)
                                            _textBuffer.AddRange(remainder);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    break;
                }

                var receiveResult = await receiveTask;

                if (receiveResult.Count == 0 || receiveResult.MessageType == WebSocketMessageType.Close)
                    break;

                var receivedData = tempBuffer.Take(receiveResult.Count).ToArray();
                var amountToTake = Math.Min(receivedData.Length, maxBytes - result.Count);
                result.AddRange(receivedData.Take(amountToTake));

                // Store any excess in appropriate buffer for future reads
                if (amountToTake < receivedData.Length)
                {
                    var excess = receivedData.Skip(amountToTake).ToArray();
                    if (receiveResult.MessageType == WebSocketMessageType.Binary)
                    {
                        lock (_binaryBuffer)
                            _binaryBuffer.AddRange(excess);
                    }
                    else if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        lock (_textBuffer)
                            _textBuffer.AddRange(excess);
                    }
                }

                // If we got data, we can return it now (don't wait for more)
                if (result.Count > 0)
                    break;

                // Reset timer after unsuccessful read for next iteration
                startTime = DateTime.UtcNow;
            }

            return result.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReadAvailableAsync] Exception: {ex.GetType().Name}: {ex.Message}, State: {_clientWebSocket.State}");
            return result.ToArray();
        }
        finally
        {
            _receiveLock.Release();
        }
    }

    public async Task DrainStreamAsync(CancellationToken cancellationToken = default)
    {
        ClearBuffer();

        if (_clientWebSocket.State != WebSocketState.Open && _clientWebSocket.State != WebSocketState.CloseReceived)
            return;

        await _receiveLock.WaitAsync(cancellationToken);
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
            _receiveLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _clientWebSocket?.Dispose();
        _textReadLock?.Dispose();
        _binaryReadLock?.Dispose();
        _receiveLock?.Dispose();
        _writeLock?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
