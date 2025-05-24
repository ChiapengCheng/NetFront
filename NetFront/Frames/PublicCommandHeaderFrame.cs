namespace NetFront.Frames;

public class PublicCommandHeaderFrame
{
    private struct Length
    {
        public const int MIN_LENGTH = MSG_TYPE + COMMAND_TYPE;
        public const int MSG_TYPE = 1;
        public const int COMMAND_TYPE = 1;
    }

    private struct Index
    {
        public const int MSG_TYPE = 0;
        public const int COMMAND_TYPE = 1;
        public const int ARG_DATA = 2;
    }

    private struct ArgDataLength
    {
        public struct SPUB
        {
            public const int TOPIC_LENGTH = 1;
        }

        public struct SET
        {
            
        }
    }

    private struct ArgDataIndex
    {
        public struct SPUB
        {
            public const int TOPIC_LENGTH = Length.MIN_LENGTH + 0;
        }

        public struct SET
        {
            public const int KEY = Length.MIN_LENGTH + 0;
        }
    }


    public static bool TryGetCommandType(Span<byte> data, out byte commandType)
    {
        if (data.Length >= Length.MIN_LENGTH)
        {
            commandType = data[Index.COMMAND_TYPE];
            return true;
        }
        commandType = (byte)PublicCommandTypeEnum.NONE;
        return false;
    }

    public static bool TryGetArgData_SPUB(Span<byte> data, out int topicLength)
    {
        if (data.Length == Length.MIN_LENGTH + ArgDataLength.SPUB.TOPIC_LENGTH)
        {
            topicLength = data[ArgDataIndex.SPUB.TOPIC_LENGTH];
            return true;
        }
        topicLength = 0;
        return false;
    }

    public static bool TryGetArgData_SET(Span<byte> data, out int offset, out int length)
    {
        if (data.Length > Length.MIN_LENGTH)
        {
            offset = ArgDataIndex.SET.KEY;
            length = data.Length - Length.MIN_LENGTH;
            return true;
        }
        offset = 0;
        length = 0;
        return false;
    }

    public static bool TryGetBytes_SET(ReadOnlySpan<char> key, out byte[] commData)
    {
        if (!key.IsEmpty && Serializer.IsAscii(key))
        {
            commData = new byte[Length.MIN_LENGTH + key.Length];
            var s = commData.AsSpan();
            Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.PUBLIC_COMMAND);
            Serializer.Write(s, Index.COMMAND_TYPE, PublicCommandTypeEnum.SET);
            Serializer.WriteStringAscii(s, ArgDataIndex.SET.KEY, key);
            return true;
        }
        commData = [];
        return false;
    }

    public static bool TryGetBytes_PUB(out byte[] commData)
    {
        commData = new byte[Length.MIN_LENGTH];
        var s = commData.AsSpan();
        Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.PUBLIC_COMMAND);
        Serializer.Write(s, Index.COMMAND_TYPE, PublicCommandTypeEnum.PUB);
        return true;
    }

    public static bool TryGetBytes_SPUB(byte topicLength, byte[] data, out byte[] commData)
    {
        if (topicLength > 0 && Serializer.IsAscii(data.AsSpan()[..topicLength]))
        {
            commData = new byte[Length.MIN_LENGTH + ArgDataLength.SPUB.TOPIC_LENGTH];
            var s = commData.AsSpan();
            Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.PUBLIC_COMMAND);
            Serializer.Write(s, Index.COMMAND_TYPE, PublicCommandTypeEnum.SPUB);
            Serializer.Write(s, ArgDataIndex.SPUB.TOPIC_LENGTH, topicLength);
            return true;
        }
        commData = [];
        return false;
    }
}
