using NetFront.Channels;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.Context;

public class ClientChannel : Channel
{
    private readonly XPublisherSocket _socket;

    public ClientChannel(XPublisherSocket socket, string address, int receiveHighWatermark, int sendHighWatermark, byte[] welcomeMsg)
    {
        _socket = socket;
        _socket.SetWelcomeMessage(welcomeMsg);
        _socket.Options.ReceiveHighWatermark = receiveHighWatermark;
        _socket.Options.SendHighWatermark = sendHighWatermark;
        _socket.Options.ManualPublisher = true;
        _socket.Bind(address);
    }

    public void Subscribe(byte[] topic)
    {
        _socket.Subscribe(topic);
    }

    public void Unsubscribe(byte[] topic)
    {
        _socket.Unsubscribe(topic);
    }

    public void SendHeartbeatMessage(byte[] data)
    {
        _socket.SendFrame(data);
        OnMsgSend(data.Length);
    }

    public void SendUserResponseMessage(byte[] rspHeader, byte[] rspInfo, byte[] data)
    {
        _socket.SendMoreFrame(rspHeader).SendMoreFrame(rspInfo).SendFrame(data);
        OnMsgSend(rspHeader.Length + rspInfo.Length + data.Length);
    }

    public void SendPublicMessage(byte[] data)
    {
        _socket.SendFrame(data);
        OnMsgSend(data.Length);
    }

    public void SendPrivateMessage(byte[] data)
    {
        _socket.SendFrame(data);
        OnMsgSend(data.Length);
    }
}
