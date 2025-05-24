namespace NetFront.Channels;

public class Channel
{
    public ChannelStatisticsField Statistics;

    private const int INDEX_OF_FRAME_WITH_MESSAGE_TYPE = 0;
    private const int INDEX_OF_MESSAGE_TYPE_IN_FRAME = 0;

    public Channel()
    {
        Statistics = new ChannelStatisticsField()
        {
            RcvMsgCount = 0,
            SendMsgCount = 0,            
            RcvMsgBytes = 0,
            SendMsgBytes = 0
        };
    }

    public bool TryGetMsgType(List<byte[]> msg, out byte type)
    {
        OnMsgReceived(msg);
        if (msg.Count > INDEX_OF_FRAME_WITH_MESSAGE_TYPE)
        {
            if (msg[INDEX_OF_FRAME_WITH_MESSAGE_TYPE].Length > INDEX_OF_MESSAGE_TYPE_IN_FRAME)
            {
                type = msg[INDEX_OF_FRAME_WITH_MESSAGE_TYPE][INDEX_OF_MESSAGE_TYPE_IN_FRAME];
                return true;
            }
        }
        type = (byte)MessageTypeEnum.NONE;
        return false;
    }

    private void OnMsgReceived(List<byte[]> msg)
    {
        Statistics.RcvMsgCount++;
        for (int i = 0; i < msg.Count; i++)
        {
            Statistics.RcvMsgBytes += msg[i].Length;
        }
    }

    public void OnMsgSend(int length)
    {
        Statistics.SendMsgCount++;
        Statistics.SendMsgBytes += length;
    }
}
