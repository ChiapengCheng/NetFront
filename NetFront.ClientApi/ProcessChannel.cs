using NetFront.Channels;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.ClientApi;

public class ProcessChannel : Channel
{
    private readonly XSubscriberSocket XSub;

    public ProcessChannel(XSubscriberSocket socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        XSub = socket;
        XSub.Options.ReceiveHighWatermark = receiveHighWatermark;
        XSub.Options.SendHighWatermark = sendHighWatermark;
        XSub.Bind(address);
        XSub.SendFrame([1]);
    }
}
