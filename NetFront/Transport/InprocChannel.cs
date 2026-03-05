using System.Collections.Concurrent;
using System.Threading.Channels;

namespace NetFront.Transport;

public sealed class InprocChannel : IDisposable
{
    private static readonly ConcurrentDictionary<string, System.Threading.Channels.Channel<List<byte[]>>> _registry = new();

    private string? _serverAddress;
    private string? _clientAddress;
    private System.Threading.Channels.Channel<List<byte[]>>? _serverChannel;
    private CancellationTokenSource? _cts;

    public event Action? ReceiveReady;

    // ── Server (bind) side ──────────────────────────────────────────────────

    public void Bind(string address)
    {
        _serverAddress = address;
        _serverChannel = System.Threading.Channels.Channel.CreateUnbounded<List<byte[]>>();
        _registry[address] = _serverChannel;
        _cts = new CancellationTokenSource();
        _ = PumpLoopAsync(_cts.Token);
    }

    public bool TryReceiveMultipartBytes(ref List<byte[]> msg)
    {
        if (_serverChannel!.Reader.TryRead(out var item))
        {
            msg = item;
            return true;
        }
        return false;
    }

    // ── Client (connect) side ───────────────────────────────────────────────

    public void Connect(string address) => _clientAddress = address;

    public void SendFrame(byte[] data) => WriteToServer([data]);

    public void SendMultipart(byte[] f0, byte[] f1) => WriteToServer([f0, f1]);

    public void SendMultipart(byte[] f0, byte[] f1, byte[] f2) => WriteToServer([f0, f1, f2]);

    public void SendMultipart(byte[] f0, byte[] f1, byte[] f2, byte[] f3) => WriteToServer([f0, f1, f2, f3]);

    public void SendMultipart(byte[] f0, byte[] f1, byte[] f2, byte[] f3, byte[] f4) => WriteToServer([f0, f1, f2, f3, f4]);

    public void SendMultipart(List<byte[]> frames) => WriteToServer(frames);

    private void WriteToServer(List<byte[]> frames)
    {
        if (_clientAddress != null && _registry.TryGetValue(_clientAddress, out var ch))
            ch.Writer.TryWrite(frames);
    }

    // ── Pump ────────────────────────────────────────────────────────────────

    private async Task PumpLoopAsync(CancellationToken ct)
    {
        await foreach (var _ in _serverChannel!.Reader.ReadAllAsync(ct))
            ReceiveReady?.Invoke();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        if (_serverAddress != null)
            _registry.TryRemove(_serverAddress, out _);
        _cts?.Dispose();
    }
}
