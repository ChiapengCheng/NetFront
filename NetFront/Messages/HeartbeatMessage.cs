namespace NetFront.Messages;

public class HeartbeatMessage
{
    public static readonly byte[] Topic = [(byte)MessageTypeEnum.HEARTBEAT];
    private readonly byte[] _buffer = new byte[Length.LENGTH];

    private struct Length
    {
        public const int LENGTH = MSG_TYPE + SYSTEM_ID + TICKS;
        public const int MSG_TYPE = 1;
        public const int SYSTEM_ID = 7;
        public const int TICKS = 8;
    }

    private struct Index
    {
        public const int MSG_TYPE = 0;
        public const int SYSTEM_ID = 1;
        public const int TICKS = 8;
    }

    public HeartbeatMessage(string systemID)
    {
        var s = _buffer.AsSpan();
        Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.HEARTBEAT);
        Serializer.WriteStringAsciiWithPadRight(s, Index.SYSTEM_ID, Length.SYSTEM_ID, systemID);
    }

    public byte[] GetBuffer(long ticks)
    {
        Serializer.Write(_buffer, Index.TICKS, ticks);
        return _buffer;
    }

    public static bool TryParse(Span<byte> data, out HeartbeatMessageFiled field)
    {
        if (data.Length == Length.LENGTH && data[Index.MSG_TYPE] == (byte)MessageTypeEnum.HEARTBEAT)
        {
            field = new HeartbeatMessageFiled()
            {
                MsgType = (byte)MessageTypeEnum.HEARTBEAT,
                SystemID = Serializer.ReadStringAsciiWithTrimEnd(data,Index.SYSTEM_ID,Length.SYSTEM_ID),
                Ticks = Serializer.Read<long>(data,Index.TICKS)
            };
            return true;
        }
        field = new HeartbeatMessageFiled();
        return false;
    }
}
