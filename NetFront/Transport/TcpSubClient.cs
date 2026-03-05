using System.Net.Sockets;
using System.Threading.Channels;

namespace NetFront.Transport;

public sealed class TcpSubClient : IDisposable
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly System.Threading.Channels.Channel<byte[]> _writeQueue =
        System.Threading.Channels.Channel.CreateUnbounded<byte[]>(
            new UnboundedChannelOptions { SingleReader = true });
    private readonly System.Threading.Channels.Channel<List<byte[]>> _inboundQueue =
        System.Threading.Channels.Channel.CreateUnbounded<List<byte[]>>();
    private CancellationTokenSource? _cts;

    public event Action? ReceiveReady;

    public void Connect(string address)
    {
        var (ip, port) = AddressParser.ParseTcp(address);
        _cts = new CancellationTokenSource();
        _tcp = new TcpClient();
        _tcp.ConnectAsync(ip, port).GetAwaiter().GetResult();
        _stream = _tcp.GetStream();
        _ = WriteLoopAsync(_cts.Token);
        _ = ReceiveLoopAsync(_cts.Token);
        _ = PumpLoopAsync(_cts.Token);
    }

    public void SendFrame(byte[] data) => _writeQueue.Writer.TryWrite(FrameProtocol.Encode(data));

    public void SendMultipart(byte[] f0, byte[] f1) =>
        _writeQueue.Writer.TryWrite(FrameProtocol.Encode(f0, f1));

    public void SendMultipart(byte[] f0, byte[] f1, byte[] f2) =>
        _writeQueue.Writer.TryWrite(FrameProtocol.Encode(f0, f1, f2));

    public void SendMultipart(byte[] f0, byte[] f1, byte[] f2, byte[] f3) =>
        _writeQueue.Writer.TryWrite(FrameProtocol.Encode(f0, f1, f2, f3));

    public void SendMultipart(List<byte[]> frames) =>
        _writeQueue.Writer.TryWrite(FrameProtocol.Encode(frames));

    public bool TryReceiveMultipartBytes(ref List<byte[]> msg)
    {
        if (_inboundQueue.Reader.TryRead(out var item))
        {
            msg = item;
            return true;
        }
        return false;
    }

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var data in _writeQueue.Reader.ReadAllAsync(ct))
                await _stream!.WriteAsync(data, ct);
        }
        catch { }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buf = new List<byte[]>();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!await FrameProtocol.TryReadMessageAsync(_stream!, buf, ct))
                    break;
                _inboundQueue.Writer.TryWrite([.. buf]);
            }
        }
        catch { }
    }

    private async Task PumpLoopAsync(CancellationToken ct)
    {
        await foreach (var _ in _inboundQueue.Reader.ReadAllAsync(ct))
            ReceiveReady?.Invoke();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _writeQueue.Writer.TryComplete();
        _tcp?.Dispose();
        _cts?.Dispose();
    }
}
