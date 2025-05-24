namespace NetFront.Messages;

public class PrivateMessage
{
    private struct Length
    {
        public const int USER_TOPIC_LENGTH = MSG_TYPE + FUCNTION_CODE + USER_ID;
        public const int MSG_TYPE = 1;
        public const int FUCNTION_CODE = 4;
        public const int USER_ID = Global.USER_ID.LENGTH;
    }

    private struct Index
    {
        public const int MSG_TYPE = 0;
        public const int FUCNTION_CODE = 1;
        public const int USER_ID = 5;
        public const int MSG_DATA = 45;
    }

    public static bool IsValidUserTopic(byte[] data)
    {
        if (data.Length >= Length.USER_TOPIC_LENGTH)
        {
            if (data[Index.MSG_TYPE] == (byte)MessageTypeEnum.PRIVATE)
                return true;
        }
        return false;
    }

    public static byte[] GetUserTopic(Span<byte> functionCode, Span<byte> userID)
    {
        var output = new byte[Length.USER_TOPIC_LENGTH];
        var s = output.AsSpan();
        Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.PRIVATE);
        Serializer.CopyToBuffer(s, Index.FUCNTION_CODE, Length.FUCNTION_CODE, functionCode);
        Serializer.CopyToBuffer(s, Index.USER_ID, Length.USER_ID, userID);
        return output;
    }


    public static byte[] GetBytes(int functionCode, ReadOnlySpan<char> userID, ReadOnlySpan<char> msgData)
    {
        var byteCount = Serializer.GetUtf8ByteCount(msgData);
        var output = new byte[Length.USER_TOPIC_LENGTH + byteCount];
        var s = output.AsSpan();
        Serializer.Write(s,Index.MSG_TYPE,MessageTypeEnum.PRIVATE);
        Serializer.Write(s,Index.FUCNTION_CODE,functionCode);
        Serializer.WriteStringAsciiWithPadRight(s, Index.USER_ID, Length.USER_ID, userID);
        Serializer.WriteStringUtf8(s, Index.MSG_DATA, msgData);
        return output;
    }

    public static bool TryParse(Span<byte> data, out PrivateMessageField field)
    {
        if (data.Length >= Length.USER_TOPIC_LENGTH)
        {
            var msgData = new byte[data.Length - Length.USER_TOPIC_LENGTH];
            Serializer.CopyToBuffer(msgData, 0, msgData.Length, data[Index.MSG_DATA..]);
            field = new PrivateMessageField()
            {
                MsgType = Serializer.Read<byte>(data,Index.MSG_TYPE),
                FunctionCode = Serializer.Read<int>(data,Index.FUCNTION_CODE),
                UserID = Serializer.ReadStringAsciiWithTrimEnd(data,Index.USER_ID,Length.USER_ID),
                MsgData = msgData
            };
            return true;
        }
        field = new PrivateMessageField();
        return false;
    }

}
