namespace NetFront.Messages;

public class PublicMessage
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

    public static bool IsValidTopic(Span<byte> data)
    {
        if (data.Length > Length.MSG_TYPE)
        {
            if (data[Index.MSG_TYPE] == (byte)MessageTypeEnum.PUBLIC)
                return true;
        }
        return false;
    }

    public static string GetString(Span<byte> topic)
    {
        return Serializer.ReadStringUtf8(topic, 0);
    }
}
