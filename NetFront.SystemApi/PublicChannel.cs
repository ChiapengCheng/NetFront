using NetFront.Channels;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.SystemApi;

public class PublicChannel : Channel
{
    private readonly XSubscriberSocket _socket;

    public PublicChannel(XSubscriberSocket socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        _socket = socket;
        _socket.Options.ReceiveHighWatermark = receiveHighWatermark;
        _socket.Options.SendHighWatermark = sendHighWatermark;
        _socket.Bind(address);
        _socket.SendFrame([1]);
    }
}
