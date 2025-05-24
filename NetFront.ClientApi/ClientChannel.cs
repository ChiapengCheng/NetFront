using NetFront.Channels;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.ClientApi;

public class ClientChannel : Channel
{
    private readonly XSubscriberSocket Socket;

    public ClientChannel(XSubscriberSocket socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        Socket = socket;
        Socket.Options.SendHighWatermark = sendHighWatermark;
        Socket.Options.ReceiveHighWatermark = receiveHighWatermark;
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
        Socket.SendMoreFrame(header).SendFrame(data);
    }
}
