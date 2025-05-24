using System.Runtime.InteropServices;
using System.Text;

namespace NetFront;

public class Serializer
{
    public static void CopyToBuffer(Span<byte> buffer, int offset, int length, Span<byte> value) => value.CopyTo(buffer.Slice(offset, length));

    public static void CopyToBuffer(Span<byte> buffer, ref int offset, Span<byte> value)
    {
        value.CopyTo(buffer.Slice(offset, value.Length));
        offset += value.Length;
    }

    public static string ReadStringUtf8WithTrimEnd(Span<byte> buffer, int offset, int length) => Encoding.UTF8.GetString(buffer.Slice(offset, length).TrimEnd((byte)' '));

    public static string ReadStringUtf8(Span<byte> buffer, int offset = 0) => Encoding.UTF8.GetString(buffer[offset..]);

    public static string ReadStringUtf8(Span<byte> buffer, int offset, int length) => Encoding.UTF8.GetString(buffer.Slice(offset, length));

    public static string ReadStringAsciiWithTrimEnd(Span<byte> buffer, int offset, int length) => Encoding.ASCII.GetString(buffer.Slice(offset, length).TrimEnd((byte)' '));

    public static string ReadStringAscii(Span<byte> buffer, int offset, int length) => Encoding.ASCII.GetString(buffer.Slice(offset, length));

    public static bool IsAscii(ReadOnlySpan<char> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] < 32 || value[i] >= 127)
            {
                return false;
            }
        }
        return true;
    }

    public static bool IsAscii(Span<byte> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] < 32 || value[i] >= 127)
            {
                return false;
            }
        }
        return true;
    }

    public static int GetUtf8ByteCount(ReadOnlySpan<char> value) => Encoding.UTF8.GetByteCount(value);

    public static int WriteStringAscii(Span<byte> buffer, int offset, ReadOnlySpan<char> value) => Encoding.ASCII.GetBytes(value, buffer[offset..]);

    public static void WriteStringAsciiWithPadRight(Span<byte> buffer, int offset, int length, ReadOnlySpan<char> value)
    {
        var byteLength = Encoding.ASCII.GetBytes(value, buffer.Slice(offset, length));
        var diff = length - byteLength;
        if (diff > 0)
        {
            buffer.Slice(offset + byteLength, diff).Fill((byte)' ');
        }
    }

    public static int WriteStringUtf8(Span<byte> buffer, int offset, ReadOnlySpan<char> value) => Encoding.UTF8.GetBytes(value, buffer[offset..]);

    public static void WriteStringUtf8WithPadRight(Span<byte> buffer, int offset, int length, ReadOnlySpan<char> value)
    {
        var byteLength = Encoding.UTF8.GetBytes(value, buffer.Slice(offset, length));
        var diff = length - byteLength;
        if (diff > 0)
        {
            buffer.Slice(offset + byteLength, diff).Fill((byte)' ');
        }
    }

    public static byte[] GetUtf8Bytes(string value) => Encoding.UTF8.GetBytes(value);

    public static T Read<T>(Span<byte> buffer, int offset = 0) where T : struct => MemoryMarshal.Read<T>(buffer[offset..]);

    public static T Read<T>(Span<byte> buffer, ref int offset) where T : struct
    {
        var value = Read<T>(buffer, offset);
        offset += Marshal.SizeOf<T>();
        return value;
    }


    public static void Write<T>(Span<byte> buffer, int offset, T value) where T : struct => MemoryMarshal.Write(buffer[offset..], in value);

    public static void Write<T>(Span<byte> buffer, ref int offset, T value) where T : struct
    {
        Write(buffer, offset, value);
        offset += Marshal.SizeOf<T>();
    }

    public static void WriteUint24(Span<byte> buffer, int offset, uint value)
    {
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 0] = (byte)(value & 0xFF);
    }

    public static void WriteUint24(Span<byte> buffer, ref int offset, uint value)
    {
        WriteUint24(buffer, offset, value);   
        offset += 3;
    }

    public static uint ReadUint24(Span<byte> buffer, int offset)
    {
        return buffer[offset]
            + ((uint)buffer[offset + 1] << 8)
            + ((uint)buffer[offset + 2] << 16);
    }

    public static uint ReadUint24(Span<byte> buffer,ref int offset)
    {
        var value = ReadUint24(buffer, offset);
        offset += 3;
        return value;
    }

    public static void WriteUint40(Span<byte> buffer, int offset, ulong value)
    {
        buffer[offset + 4] = (byte)((value >> 32) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 0] = (byte)(value & 0xFF);
    }

    public static ulong ReadUint40(Span<byte> buffer, int offset)
    {
        return buffer[offset]
            + ((ulong)buffer[offset + 1] << 8)
            + ((ulong)buffer[offset + 2] << 16)
            + ((ulong)buffer[offset + 3] << 24)
            + ((ulong)buffer[offset + 4] << 32);
    }
}
