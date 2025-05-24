namespace NetFront.Messages;

public class WelcomeMessage
{
    private struct Length
    {
        public const int LENGTH = MSG_TYPE + SYSTEM_ID + ADDRESS;
        public const int MSG_TYPE = 1;
        public const int SYSTEM_ID = 7;
        public const int ADDRESS = 80;
    }

    private struct Index
    {
        public const int MSG_TYPE = 0;
        public const int SYSTEM_ID = 1;
        public const int ADDRESS = 8;
    }

    public static byte[] GetBytes(string systemID, string address)
    {
        var output = new byte[Length.LENGTH];
        var s = output.AsSpan();
        Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.WELCOME);
        Serializer.WriteStringAsciiWithPadRight(s, Index.SYSTEM_ID, Length.SYSTEM_ID, systemID);
        Serializer.WriteStringAsciiWithPadRight(s, Index.ADDRESS, Length.ADDRESS, address);
        return output;
    }

    public static bool TryParse(Span<byte> data, out WelcomeMessageField field)
    {
        if (data.Length == Length.LENGTH && data[Index.MSG_TYPE] == (byte)MessageTypeEnum.WELCOME)
        {
            field = new WelcomeMessageField()
            {
                MsgType = (byte)MessageTypeEnum.WELCOME,
                SystemID = Serializer.ReadStringAsciiWithTrimEnd(data,Index.SYSTEM_ID,Length.SYSTEM_ID),
                Address = Serializer.ReadStringAsciiWithTrimEnd(data, Index.ADDRESS, Length.ADDRESS),
            };
            return true;
        }
        field = new WelcomeMessageField();
        return false;
    }
}
