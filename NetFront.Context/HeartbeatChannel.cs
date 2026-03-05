using NetFront.Channels;
using NetFront.Transport;

namespace NetFront.Context;

public class HeartbeatChannel : Channel
{
    private readonly TcpSubClient _socket;
    public HeartbeatChannel(TcpSubClient socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        _socket = socket;
        _socket.Connect(address);
        _socket.SendFrame([(byte)MessageTypeEnum.HEARTBEAT_SUB]);
    }

    public void SendHeartbeatMessage(byte[] data)
    {
        _socket.SendFrame(data);
    }
}
