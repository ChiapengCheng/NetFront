using NetFront.Channels;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.Context;

public class SystemChannel : Channel
{
    private readonly XPublisherSocket _socket;
    public SystemChannel(XPublisherSocket socket, string address, int receiveHighWatermark, int sendHighWatermark, byte[] welcomeMsg)
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

    public void SendUserResponseMessage(byte[] reqHeader, byte[] rspInfo, byte[] data)
    {
        _socket.SendMoreFrame(reqHeader).SendMoreFrame(rspInfo).SendFrame(data);
        OnMsgSend(reqHeader.Length + rspInfo.Length + data.Length);
    }

    public void SendRouteMessage(byte[] routeHeader, byte[] rspHeader)
    {
        _socket.SendMoreFrame(routeHeader).SendFrame(rspHeader);
    }
}
