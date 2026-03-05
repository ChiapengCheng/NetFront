using System.Buffers.Binary;
using System.Net.Sockets;

namespace NetFront.Transport;

public static class FrameProtocol
{
    // Wire format: [uint16 frame_count][uint32 len1][data1][uint32 len2][data2]...
    public static byte[] Encode(List<byte[]> frames)
    {
        int total = 2;
        foreach (var f in frames) total += 4 + f.Length;
        var buf = new byte[total];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), (ushort)frames.Count);
        int pos = 2;
        foreach (var f in frames)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f.Length);
            f.CopyTo(buf, pos + 4);
            pos += 4 + f.Length;
        }
        return buf;
    }

    public static byte[] Encode(byte[] f0)
    {
        var buf = new byte[2 + 4 + f0.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), 1);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(2, 4), (uint)f0.Length);
        f0.CopyTo(buf, 6);
        return buf;
    }

    public static byte[] Encode(byte[] f0, byte[] f1)
    {
        var buf = new byte[2 + 4 + f0.Length + 4 + f1.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), 2);
        int pos = 2;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f0.Length); pos += 4;
        f0.CopyTo(buf, pos); pos += f0.Length;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f1.Length); pos += 4;
        f1.CopyTo(buf, pos);
        return buf;
    }

    public static byte[] Encode(byte[] f0, byte[] f1, byte[] f2)
    {
        var buf = new byte[2 + 4 + f0.Length + 4 + f1.Length + 4 + f2.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), 3);
        int pos = 2;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f0.Length); pos += 4;
        f0.CopyTo(buf, pos); pos += f0.Length;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f1.Length); pos += 4;
        f1.CopyTo(buf, pos); pos += f1.Length;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f2.Length); pos += 4;
        f2.CopyTo(buf, pos);
        return buf;
    }

    public static byte[] Encode(byte[] f0, byte[] f1, byte[] f2, byte[] f3)
    {
        var buf = new byte[2 + 4 + f0.Length + 4 + f1.Length + 4 + f2.Length + 4 + f3.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), 4);
        int pos = 2;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f0.Length); pos += 4;
        f0.CopyTo(buf, pos); pos += f0.Length;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f1.Length); pos += 4;
        f1.CopyTo(buf, pos); pos += f1.Length;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f2.Length); pos += 4;
        f2.CopyTo(buf, pos); pos += f2.Length;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(pos, 4), (uint)f3.Length); pos += 4;
        f3.CopyTo(buf, pos);
        return buf;
    }

    public static async ValueTask<bool> TryReadMessageAsync(NetworkStream stream, List<byte[]> output, CancellationToken ct)
    {
        output.Clear();
        var header = new byte[2];
        if (!await ReadExactlyAsync(stream, header, ct)) return false;
        var frameCount = BinaryPrimitives.ReadUInt16BigEndian(header);
        for (int i = 0; i < frameCount; i++)
        {
            var lenBuf = new byte[4];
            if (!await ReadExactlyAsync(stream, lenBuf, ct)) return false;
            var len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuf);
            var data = new byte[len];
            if (len > 0 && !await ReadExactlyAsync(stream, data, ct)) return false;
            output.Add(data);
        }
        return true;
    }

    private static async ValueTask<bool> ReadExactlyAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }
}
