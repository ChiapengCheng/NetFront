using NetFront.Channels;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.SystemApi;

public class SystemChannel : Channel
{
    private readonly XSubscriberSocket _socket;

    public SystemChannel(XSubscriberSocket socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        _socket = socket;
        _socket.Options.ReceiveHighWatermark = receiveHighWatermark;
        _socket.Options.SendHighWatermark = sendHighWatermark;
        _socket.Connect(address);
    }

    public void SendHeartbeatSubMessage()
    {
        _socket.SendFrame([(byte)MessageTypeEnum.HEARTBEAT_SUB]);
    }

    public void SendUserRequestMessage(byte[] header, byte[] data)
    {
        _socket.SendMoreFrame(header).SendFrame(data);
    }

    public void SendUserUnsubMessage(byte[] topic)
    {
        _socket.SendFrame(topic);
    }

    public void SendUserRequestMessage(byte[] header, byte[] data0, byte[] data1, byte[] data2)
    {
        _socket.SendMoreFrame(header).SendMoreFrame(data0).SendMoreFrame(data1).SendFrame(data2);
    }
}
