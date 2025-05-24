namespace NetFront.Frames;

public class UserRequestHeaderFrame
{
    private readonly byte[] _buffer = new byte[Length.LENGTH];
    private struct Length
    {
        public const int LENGTH = MSG_TYPE + USER_ID + SESSION_ID + REQUEST_ID + FUNCTION_ID;
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

    public UserRequestHeaderFrame()
    {
        _buffer[Index.MSG_TYPE] = (byte)MessageTypeEnum.USER_REQ;
    }

    public void Set(Span<byte> userID, string sessionID, Span<byte> requestID, UserRequestFunctionEnum functionID)
    {
        var span = _buffer.AsSpan();
        Serializer.CopyToBuffer(span, Index.USER_ID, Length.USER_ID, userID);
        Serializer.WriteStringAscii(span, Index.SESSION_ID, sessionID);
        Serializer.CopyToBuffer(span, Index.REQUEST_ID, Length.REQUEST_ID, requestID);
        Serializer.Write(span, Index.FUNCTION_ID, functionID);
    }

    public void Set(Span<byte> requestID, UserRequestFunctionEnum functionID)
    {
        var span = _buffer.AsSpan();
        Serializer.CopyToBuffer(span, Index.REQUEST_ID, Length.REQUEST_ID, requestID);
        Serializer.Write(span, Index.FUNCTION_ID, functionID);
    }

    public void Set(Span<byte> requestID, byte functionID)
    {
        var span = _buffer.AsSpan();
        Serializer.CopyToBuffer(span, Index.REQUEST_ID, Length.REQUEST_ID, requestID);
        Serializer.Write(span, Index.FUNCTION_ID, functionID);
    }

    public byte[] GetBuffer()
    {
        return _buffer;
    }

    public Span<byte> GetBytesOfUserID()
    {
        return _buffer.AsSpan(Index.USER_ID, Length.USER_ID);
    }

}
