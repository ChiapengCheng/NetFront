using NetFront.Channels;
using NetFront.Transport;

namespace NetFront.ClientApi;

public class ClientChannel : Channel
{
    private readonly TcpSubClient Socket;

    public ClientChannel(TcpSubClient socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        Socket = socket;
        Socket.Connect(address);
    }

    public void SendHeartbeatSubMessage()
    {
        Socket.SendFrame([(byte)MessageTypeEnum.HEARTBEAT_SUB]);
    }

    public void SendUserUnsubMessage(byte[] topic)
    {
        Socket.SendFrame(topic);
    }

    public void SendUserRequestMessage(byte[] header, byte[] data)
    {
        Socket.SendMultipart(header, data);
    }
}
