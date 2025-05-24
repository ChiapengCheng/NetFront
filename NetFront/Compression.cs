using System.IO.Compression;

namespace NetFront;

public class Compression
{
    public static byte[] Compress(byte[] data)
    {
        if (data != null)
        {
            MemoryStream output = new();
            using (DeflateStream dstream = new(output, CompressionLevel.Optimal))
            {
                dstream.Write(data);
            }
            return output.ToArray();
        }
        else
        {
            return [];
        }
    }

    public static byte[] Decompress(byte[] data)
    {
        if (data != null)
        {
            MemoryStream input = new(data);
            MemoryStream output = new();
            using (DeflateStream dstream = new(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }
            return output.ToArray();
        }
        else
        {
            return [];
        }
    }
}
