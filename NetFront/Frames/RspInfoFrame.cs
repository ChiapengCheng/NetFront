namespace NetFront.Frames;

public class RspInfoFrame
{
    private readonly byte[] _buffer = new byte[Length.LENGTH];
    
    private struct Length
    {
        public const int LENGTH = TIME + ERROR_CODE + ERROR_MSG + IS_LAST;
        public const int TIME = 8;
        public const int ERROR_CODE = 4;
        public const int ERROR_MSG = 32;
        public const int IS_LAST = 1;
    }

    private struct Index
    {
        public const int TIME = 0;
        public const int ERROR_CODE = 8;
        public const int ERROR_MSG = 12;
        public const int IS_LAST = 44;
    }

    private static void Set(Span<byte> s, long time, int errorCode, string errorMsg, bool isLast)
    {
        Serializer.Write(s, Index.TIME, time);
        Serializer.Write(s, Index.ERROR_CODE, errorCode);
        Serializer.WriteStringUtf8WithPadRight(s, Index.ERROR_MSG, Length.ERROR_MSG, errorMsg);
        Serializer.Write(s, Index.IS_LAST, isLast);
    }

    public byte[] GetBuffer(long time, int errorCode, string errorMsg, bool isLast)
    {
        Set(_buffer, time, errorCode, errorMsg, isLast);
        return _buffer;
    }

    public static byte[] GetBytes(long time, int errorCode, string errorMsg, bool isLast)
    {
        var output = new byte[Length.LENGTH];
        Set(output, time, errorCode, errorMsg, isLast);
        return output;
    }

    public static bool CheckErrorMsg(ReadOnlySpan<char> value) => Serializer.GetUtf8ByteCount(value) <= Length.ERROR_MSG;

    public static bool TryParse(Span<byte> data, out RspInfoFrameField field)
    {
        if (data.Length == Length.LENGTH)
        {
            field = new RspInfoFrameField()
            {
                Time = Serializer.Read<long>(data, Index.TIME),
                ErrorCode = Serializer.Read<int>(data, Index.ERROR_CODE),
                ErrorMsg = Serializer.ReadStringUtf8WithTrimEnd(data,Index.ERROR_MSG,Length.ERROR_MSG),
                IsLast = Serializer.Read<bool>(data, Index.IS_LAST),
            };
            return true;
        }
        field = new RspInfoFrameField();
        return false;
    }
}
