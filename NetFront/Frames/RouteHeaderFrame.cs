namespace NetFront.Frames;

public class RouteHeaderFrame
{
    private struct Length
    {
        public const int FUNCTION_TOPIC_LENGTH = MSG_TYPE + FUNCTION_CODE;
        public const int USER_TOPIC_LENGTH = MSG_TYPE + FUNCTION_CODE + USER_ID;        
        public const int MSG_TYPE = 1;
        public const int FUNCTION_CODE = 4;
        public const int USER_ID = Global.USER_ID.LENGTH;
    }

    private struct Index
    {
        public const int MSG_TYPE = 0;
        public const int FUNCTION_CODE = 1;
        public const int USER_ID = 5;
        public const int MSG_DATA = 45;
    }

    public static bool IsValidUserTopic(byte[] data)
    {
        if (data.Length >= Length.USER_TOPIC_LENGTH)
        {
            if (data[Index.MSG_TYPE] == (byte)MessageTypeEnum.ROUTE)
                return true;
        }
        return false;
    }

    public static bool IsValidFunctionTopic(byte[] data)
    {
        if (data.Length >= Length.FUNCTION_TOPIC_LENGTH)
        {
            if (data[Index.MSG_TYPE] == (byte)MessageTypeEnum.ROUTE)
                return true;
        }
        return false;
    }

    public static byte[] GetBytesOfFuntionTopic(Span<byte> functionCode)
    {
        var output = new byte[Length.FUNCTION_TOPIC_LENGTH];
        var s = output.AsSpan();
        Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.ROUTE);
        Serializer.CopyToBuffer(s, Index.FUNCTION_CODE, Length.FUNCTION_CODE, functionCode);
        return output;
    }

    public static bool TryParse(Span<byte> data, out RouteHeaderFrameField field)
    {
        if (data.Length >= Length.USER_TOPIC_LENGTH && data[Index.MSG_TYPE] == (byte)MessageTypeEnum.ROUTE)
        {
            var msgDataLength = data.Length - Length.USER_TOPIC_LENGTH;
            var msgData = new byte[msgDataLength];
            data[Index.MSG_DATA..].CopyTo(msgData);
            field = new RouteHeaderFrameField()
            {
                MsgType = (byte)MessageTypeEnum.ROUTE,
                FunctionCode = Serializer.Read<int>(data,Index.FUNCTION_CODE),
                UserID = Serializer.ReadStringAsciiWithTrimEnd(data,Index.USER_ID,Length.USER_ID),
                MsgData = msgData
            };
            return true;
        }
        field = new RouteHeaderFrameField();
        return false;
    }

    public static byte[] GetBytes(Span<byte> functionCode, Span<byte> userID, Span<byte> data)
    {
        var output = new byte[Length.USER_TOPIC_LENGTH + data.Length];
        var s = output.AsSpan();
        Serializer.Write(s, Index.MSG_TYPE, MessageTypeEnum.ROUTE);
        Serializer.CopyToBuffer(s, Index.FUNCTION_CODE, Length.FUNCTION_CODE, functionCode);
        Serializer.CopyToBuffer(s, Index.USER_ID, Length.USER_ID, userID);
        Serializer.CopyToBuffer(s, Index.MSG_DATA, data.Length, data);
        return output;
    }

}
