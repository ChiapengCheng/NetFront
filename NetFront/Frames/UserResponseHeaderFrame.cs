namespace NetFront.Frames;

public class UserResponseHeaderFrame
{
    private struct Length
    {
        public const int LENGTH = MSG_TYPE + USER_ID + SESSION_ID + REQUEST_ID + FUNCTION_ID;
        public const int SESSION_LENGTH = MSG_TYPE + USER_ID + SESSION_ID;
        public const int MSG_TYPE = 1;
        public const int USER_ID = Global.USER_ID.LENGTH;
        public const int SESSION_ID = Global.SESSION_ID.LENGTH;
        public const int REQUEST_ID = Global.REQUEST_ID.LENGTH;
        public const int FUNCTION_ID = 1;
    }

    private struct Index
    {
        public const int MSG_TYPE = 0;
        public const int USER_ID = 1;
        public const int SESSION_ID = 41;
        public const int REQUEST_ID = 73;
        public const int FUNCTION_ID = 77;
    }

    public static bool TryConvert(Span<byte> requestHeader)
    {
        if (requestHeader.Length == Length.LENGTH)
        {
            requestHeader[Index.MSG_TYPE] = (byte)MessageTypeEnum.USER_RSP;
            return true;
        }
        return false;
    }

    public static byte GetFunctionID(Span<byte> data)
    {
        return data[Index.FUNCTION_ID];
    }

    public static string GetUserID(Span<byte> data)
    {
        return Serializer.ReadStringAsciiWithTrimEnd(data, Index.USER_ID, Length.USER_ID);
    }

    public static Span<byte> GetBytesOfUserID(Span<byte> data)
    {
        return data.Slice(Index.USER_ID,Length.USER_ID);
    }

    public static string GetUserSession(Span<byte> requestHeader)
    {
        var output = new byte[Length.SESSION_LENGTH];
        var span =  output.AsSpan();
        Serializer.CopyToBuffer(span, 0, Length.SESSION_LENGTH, requestHeader[..Length.SESSION_LENGTH]);
        Serializer.Write(span, Index.MSG_TYPE, MessageTypeEnum.USER_RSP);
        return Serializer.ReadStringAscii(span,Index.MSG_TYPE,Length.SESSION_LENGTH);
    }

    public static bool TryParse(Span<byte> data, out UserResponseHeaderFrameField field)
    {
        if (data.Length == Length.LENGTH)
        {
            if (data[Index.MSG_TYPE] == (byte)MessageTypeEnum.USER_RSP)
            {
                field = new UserResponseHeaderFrameField()
                {
                    MsgType = (byte)MessageTypeEnum.USER_RSP,
                    UserID = Serializer.ReadStringAsciiWithTrimEnd(data, Index.USER_ID, Length.USER_ID),
                    SessionID = Serializer.ReadStringAscii(data, Index.SESSION_ID, Length.SESSION_ID),
                    RequestID = Serializer.Read<int>(data, Index.REQUEST_ID),
                    FunctionID = Serializer.Read<byte>(data, Index.FUNCTION_ID)
                };
                return true;
            }
        }
        field = new UserResponseHeaderFrameField();
        return false;
    }


}
