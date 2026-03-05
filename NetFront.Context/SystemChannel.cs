using NetFront.Channels;
using NetFront.Transport;

namespace NetFront.Context;

public class SystemChannel : Channel
{
    private readonly TcpPubServer _socket;
    public SystemChannel(TcpPubServer socket, string address, int receiveHighWatermark, int sendHighWatermark, byte[] welcomeMsg)
    {
        _socket = socket;
        _socket.SetWelcomeMessage(welcomeMsg);
        _socket.Bind(address);
    }

    public void Subscribe(byte[] topic) => _socket.Subscribe(topic);
    public void Unsubscribe(byte[] topic) => _socket.Unsubscribe(topic);

    public void SendHeartbeatMessage(byte[] data)
    {
        _socket.SendFrame(data);
        OnMsgSend(data.Length);
    }

    public void SendUserResponseMessage(byte[] reqHeader, byte[] rspInfo, byte[] data)
    {
        _socket.SendMultipart(reqHeader, rspInfo, data);
        OnMsgSend(reqHeader.Length + rspInfo.Length + data.Length);
    }

    public void SendRouteMessage(byte[] routeHeader, byte[] rspHeader)
    {
        _socket.SendMultipart(routeHeader, rspHeader);
    }
}
