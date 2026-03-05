using NetFront.Channels;
using NetFront.Transport;

namespace NetFront.SystemApi;

public class ProcesslChannel : Channel
{
    private readonly InprocChannel _socket;

    public ProcesslChannel(InprocChannel socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        _socket = socket;
        _socket.Bind(address);
    }
}
