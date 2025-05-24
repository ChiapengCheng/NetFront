using NetFront.Channels;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.Context;

public class HeartbeatChannel : Channel
{
    private readonly XSubscriberSocket _socket;
    public HeartbeatChannel(XSubscriberSocket socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        _socket = socket;
        _socket.Options.ReceiveHighWatermark = receiveHighWatermark;
        _socket.Options.SendHighWatermark = sendHighWatermark;
        _socket.Connect(address);
        Thread.Sleep(100);
        socket.SendFrame([(byte)MessageTypeEnum.HEARTBEAT_SUB]);
    }

    public void SendHeartbeatMessage(byte[] data)
    {
        _socket.SendFrame(data);
    }
}
