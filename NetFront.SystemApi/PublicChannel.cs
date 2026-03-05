using NetFront.Channels;
using NetFront.Transport;

namespace NetFront.SystemApi;

public class PublicChannel : Channel
{
    private readonly InprocChannel _socket;

    public PublicChannel(InprocChannel socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        _socket = socket;
        _socket.Bind(address);
    }
}
