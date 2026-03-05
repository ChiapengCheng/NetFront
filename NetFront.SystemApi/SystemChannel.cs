using NetFront.Channels;
using NetFront.Transport;

namespace NetFront.SystemApi;

public class SystemChannel : Channel
{
    private readonly TcpSubClient _socket;

    public SystemChannel(TcpSubClient socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        _socket = socket;
        _socket.Connect(address);
    }

    public void SendHeartbeatSubMessage()
    {
        _socket.SendFrame([(byte)MessageTypeEnum.HEARTBEAT_SUB]);
    }

    public void SendUserRequestMessage(byte[] header, byte[] data)
    {
        _socket.SendMultipart(header, data);
    }

    public void SendUserUnsubMessage(byte[] topic)
    {
        _socket.SendFrame(topic);
    }

    public void SendUserRequestMessage(byte[] header, byte[] data0, byte[] data1, byte[] data2)
    {
        _socket.SendMultipart(header, data0, data1, data2);
    }
}
