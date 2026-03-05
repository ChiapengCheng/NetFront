using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetFront.Transport;

public sealed class TcpPubServer : IDisposable
{
    private sealed class ConnectionState
    {
        public int Id;
        public NetworkStream Stream = default!;
        public System.Threading.Channels.Channel<byte[]> WriteQueue =
            System.Threading.Channels.Channel.CreateUnbounded<byte[]>(
                new UnboundedChannelOptions { SingleReader = true });
    }

    private readonly ConcurrentDictionary<int, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<int, List<byte[]>> _subscriptions = new();
    private readonly System.Threading.Channels.Channel<(int connId, List<byte[]> msg)> _inboundQueue =
        System.Threading.Channels.Channel.CreateUnbounded<(int, List<byte[]>)>();

    private byte[]? _welcomeMessage;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _nextId;

    public int CurrentConnectionId { get; private set; }
    public event Action? ReceiveReady;

    public void Bind(string address)
    {
        var (ip, port) = AddressParser.ParseTcp(address);
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(ip, port);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
        _ = PumpLoopAsync(_cts.Token);
    }

    public void SetWelcomeMessage(byte[] msg) => _welcomeMessage = msg;

    public void Subscribe(byte[] topic)
    {
        var subs = _subscriptions.GetOrAdd(CurrentConnectionId, _ => []);
        lock (subs)
        {
            if (!subs.Any(t => t.SequenceEqual(topic)))
                subs.Add(topic);
        }
    }

    public void Unsubscribe(byte[] topic)
    {
        if (_subscriptions.TryGetValue(CurrentConnectionId, out var subs))
        {
            lock (subs)
                subs.RemoveAll(t => t.SequenceEqual(topic));
        }
    }

    public void SendFrame(byte[] data) => SendToMatchingConnections(data, FrameProtocol.Encode(data));

    public void SendMultipart(byte[] f0, byte[] f1)
    {
        var encoded = FrameProtocol.Encode(f0, f1);
        SendToMatchingConnections(f0, encoded);
    }

    public void SendMultipart(byte[] f0, byte[] f1, byte[] f2)
    {
        var encoded = FrameProtocol.Encode(f0, f1, f2);
        SendToMatchingConnections(f0, encoded);
    }

    public void SendMultipart(byte[] f0, byte[] f1, byte[] f2, byte[] f3)
    {
        var encoded = FrameProtocol.Encode(f0, f1, f2, f3);
        SendToMatchingConnections(f0, encoded);
    }

    private void SendToMatchingConnections(byte[] routingFrame, byte[] encoded)
    {
        foreach (var kv in _subscriptions)
        {
            var connId = kv.Key;
            var subs = kv.Value;
            bool matched = false;
            lock (subs)
            {
                foreach (var topic in subs)
                {
                    if (routingFrame.Length >= topic.Length &&
                        routingFrame.AsSpan(0, topic.Length).SequenceEqual(topic))
                    {
                        matched = true;
                        break;
                    }
                }
            }
            if (matched && _connections.TryGetValue(connId, out var conn))
                conn.WriteQueue.Writer.TryWrite(encoded);
        }
    }

    public bool TryReceiveMultipartBytes(ref List<byte[]> msg)
    {
        if (_inboundQueue.Reader.TryRead(out var item))
        {
            CurrentConnectionId = item.connId;
            msg = item.msg;
            return true;
        }
        return false;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var tcp = await _listener!.AcceptTcpClientAsync(ct);
                var connId = Interlocked.Increment(ref _nextId);
                var state = new ConnectionState { Id = connId, Stream = tcp.GetStream() };
                _connections[connId] = state;
                _subscriptions[connId] = [];

                if (_welcomeMessage != null)
                    state.WriteQueue.Writer.TryWrite(FrameProtocol.Encode(_welcomeMessage));

                _ = WriteLoopAsync(state, ct);
                _ = ReceiveLoopAsync(connId, state, tcp, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private static async Task WriteLoopAsync(ConnectionState state, CancellationToken ct)
    {
        try
        {
            await foreach (var data in state.WriteQueue.Reader.ReadAllAsync(ct))
                await state.Stream.WriteAsync(data, ct);
        }
        catch { }
    }

    private async Task ReceiveLoopAsync(int connId, ConnectionState state, TcpClient tcp, CancellationToken ct)
    {
        var buf = new List<byte[]>();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!await FrameProtocol.TryReadMessageAsync(state.Stream, buf, ct))
                    break;
                _inboundQueue.Writer.TryWrite((connId, [.. buf]));
            }
        }
        catch { }
        finally
        {
            // Synthesize FRONT_UNSUB for each subscribed topic on disconnect
            if (_subscriptions.TryRemove(connId, out var subs))
            {
                lock (subs)
                {
                    foreach (var topic in subs)
                    {
                        var frame = new byte[1 + topic.Length];
                        frame[0] = 0x00;
                        topic.CopyTo(frame, 1);
                        _inboundQueue.Writer.TryWrite((connId, [frame]));
                    }
                }
            }
            _connections.TryRemove(connId, out _);
            state.WriteQueue.Writer.TryComplete();
            tcp.Dispose();
        }
    }

    private async Task PumpLoopAsync(CancellationToken ct)
    {
        await foreach (var item in _inboundQueue.Reader.ReadAllAsync(ct))
        {
            CurrentConnectionId = item.connId;
            ReceiveReady?.Invoke();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _cts?.Dispose();
    }
}
