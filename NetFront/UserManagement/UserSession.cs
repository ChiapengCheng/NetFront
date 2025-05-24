namespace NetFront.UserManagement;

public class UserSession
{
    private struct Length
    {
        public const int LENGTH = MSG_TYPE + USER_ID + SESSION_ID;
        public const int MSG_TYPE = 1;
        public const int USER_ID = Global.USER_ID.LENGTH;
        public const int SESSION_ID = Global.SESSION_ID.LENGTH;
    }

    private struct Index
    {
        public const int MSG_TYPE = 0;
        public const int USER_ID = 1;
        public const int SESSION_ID = 41;
    }

    public static string GetUserID(ReadOnlySpan<char> userSession)
    {
        return userSession.Slice(Index.USER_ID, Length.USER_ID).TrimEnd().ToString();
    }

    public static bool TryGetUserRspSession(Span<byte> topic, out string session)
    {
        if (topic.Length == Length.LENGTH)
        {
            if (topic[Index.MSG_TYPE] == (byte)MessageTypeEnum.USER_RSP)
            {
                session = Serializer.ReadStringAsciiWithTrimEnd(topic, Index.MSG_TYPE, Length.LENGTH);
                return true;
            }
        }
        session = string.Empty;
        return false;
    }

    public static string GetUserRspSession(Span<byte> rspHeader)
    {
        return Serializer.ReadStringAsciiWithTrimEnd(rspHeader, Index.MSG_TYPE, Length.LENGTH);
    }

    public static byte[] GetUserRspSessionData(Span<byte> rspHeader) => rspHeader[..Length.LENGTH].ToArray();

    public static ReadOnlySpan<char> GetSessionID(ReadOnlySpan<char> userSession)
    {
        return userSession.Slice(Index.SESSION_ID, Length.SESSION_ID);
    }


}
