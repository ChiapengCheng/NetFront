namespace NetFront.Messages;

public class FrontUnsubMessage
{
    private struct Length
    {
        public const int MSG_TYPE = 1;
    }

    private struct Index
    {
        public const int MSG_TYPE = 0;
        public const int TOPIC = 1;
    }

    public static bool IsValid(List<byte[]> msg)
    {
        if (msg.Count == 1)
            return true;
        return false;
    }

    public static bool TryGetTopic(Span<byte> data, out byte[] topic)
    {
        if (data.Length >= Length.MSG_TYPE)
        {
            topic = data[Index.TOPIC..].ToArray();
            return true;
        }
        topic = [];
        return false;
    }
}
