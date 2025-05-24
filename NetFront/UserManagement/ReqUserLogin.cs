namespace NetFront.UserManagement;

public class ReqUserLogin
{
    private struct Length
    {
        public const int MIN_LENGTH = PASSWORD;
        public const int PASSWORD = Global.PASSWORD.LENGTH;
    }

    private struct Index
    {
        public const int PASSWORD = 0;
        public const int DATA = 8;
    }

    public static bool TryParse(Span<byte> data, out ReqUserLoginField field)
    {
        if (data.Length >= Length.MIN_LENGTH)
        {
            field = new ReqUserLoginField()
            {
                Password = Serializer.ReadStringAsciiWithTrimEnd(data, Index.PASSWORD, Length.PASSWORD),
                Data = Serializer.ReadStringUtf8(data, Index.DATA)
            };
            return true;
        }
        field = new ReqUserLoginField();
        return false;
    }

    public static byte[] GetBytes(Span<byte> password, string data)
    {
        var s = data.AsSpan();
        var byteCount = Serializer.GetUtf8ByteCount(s);
        var output = new byte[Length.MIN_LENGTH + byteCount];
        var buffer = output.AsSpan();
        Serializer.CopyToBuffer(buffer, Index.PASSWORD, Length.PASSWORD, password);
        Serializer.WriteStringUtf8(buffer, Index.DATA, s);
        return output;
    }

}
