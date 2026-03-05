using NetFront.Channels;
using NetFront.Transport;

namespace NetFront.ClientApi;

public class ProcessChannel : Channel
{
    private readonly InprocChannel XSub;

    public ProcessChannel(InprocChannel socket, string address, int receiveHighWatermark, int sendHighWatermark)
    {
        XSub = socket;
        XSub.Bind(address);
    }
}
