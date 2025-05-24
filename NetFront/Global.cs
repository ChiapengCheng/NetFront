namespace NetFront;

public class Global
{

    public class USER_ID
    {
        public const int LENGTH = 40;
        public static bool Check(ReadOnlySpan<char> value) => Serializer.IsAscii(value) && value.Length <= LENGTH;
        public static byte[] ToBytes(ReadOnlySpan<char> value)
        {
            var output = new byte[LENGTH];
            Serializer.WriteStringAsciiWithPadRight(output, 0, LENGTH, value);
            return output;
        }
    }

    public class PASSWORD
    {
        public const int LENGTH = 8;
        public static bool Check(ReadOnlySpan<char> value) => Serializer.IsAscii(value) && value.Length <= LENGTH;
        public static byte[] ToBytes(ReadOnlySpan<char> value)
        {
            var output = new byte[LENGTH];
            Serializer.WriteStringAsciiWithPadRight(output, 0, LENGTH, value);
            return output;
        }
    }

    public class SESSION_ID
    {
        public const int LENGTH = 32;

        public static string Generate() => $"{Guid.NewGuid():N}";
        public static bool Check(ReadOnlySpan<char> value) => Serializer.IsAscii(value) && value.Length == LENGTH;
        public static byte[] ToBytes(ReadOnlySpan<char> value)
        {
            var output = new byte[LENGTH];
            Serializer.WriteStringAscii(output, 0, value);
            return output;
        }
    }

    public class REQUEST_ID
    {
        public const int LENGTH = 4;

        public static byte[] ToBytes(int value)
        {
            var output = new byte[LENGTH];
            Serializer.Write(output, 0, value);
            return output;
        }

        public static int ToValue(Span<byte> data)
        {
            return Serializer.Read<int>(data);
        }
    }

    public class FUNCTION_CODE
    {
        public const int LENGTH = 4;

        public static byte[] ToBytes(int value)
        {
            var output = new byte[LENGTH];
            Serializer.Write(output, 0, value);
            return output;
        }

        public static int ToValue(Span<byte> data)
        {
            return Serializer.Read<int>(data);
        }
    }
}
