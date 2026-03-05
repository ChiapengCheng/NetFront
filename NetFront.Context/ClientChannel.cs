using NetFront.Channels;
using NetFront.Transport;

namespace NetFront.Context;

public class ClientChannel : Channel
{
    private readonly TcpPubServer _socket;

    public ClientChannel(TcpPubServer socket, string address, int receiveHighWatermark, int sendHighWatermark, byte[] welcomeMsg)
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

    public void SendUserResponseMessage(byte[] rspHeader, byte[] rspInfo, byte[] data)
    {
        _socket.SendMultipart(rspHeader, rspInfo, data);
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
