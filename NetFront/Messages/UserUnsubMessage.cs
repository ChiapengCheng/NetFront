namespace NetFront.Messages;

public class UserUnsubMessage
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

    public static byte[] GetBytes(string topic)
    {
        var data = Serializer.GetUtf8Bytes(topic);
        var output = new byte[Length.MSG_TYPE + data.Length];
        var s = output.AsSpan();
        Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.USER_UNSUB);
        Serializer.CopyToBuffer(s, Index.TOPIC, data.Length, data);
        return output;
    }
}
