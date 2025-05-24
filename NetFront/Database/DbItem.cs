namespace NetFront.Database;

public class DbItem(byte[] data)
{
    public byte[] Buffer { get; set; } = data;

    public void Set(Span<byte> data)
    {
        if (Buffer.Length == data.Length)
        {
            data.CopyTo(Buffer.AsSpan());
        }
        else
        {
            Buffer = data.ToArray();
        }
    }
}
