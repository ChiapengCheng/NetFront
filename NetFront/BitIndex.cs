namespace NetFront;

public class BitIndex
{
    public readonly int ByteLength;
    private readonly int _baseBitCount;
    private readonly int _itemCount;
    private readonly int _mask;
    private readonly byte[] _buffer;

    public BitIndex(int baseBitCount, int itemCount)
    {        
        ByteLength = GetByteLength(baseBitCount, itemCount);
        _buffer = new byte[ByteLength];
        _baseBitCount = baseBitCount;
        _itemCount = itemCount;
        _mask = _baseBitCount switch
        {
            1 => 0b_0000_0001,
            2 => 0b_0000_0011,
            3 => 0b_0000_0111,
            4 => 0b_0000_1111,
            5 => 0b_0001_1111,
            6 => 0b_0011_1111,
            7 => 0b_0111_1111,
            8 => 0b_1111_1111,
            _ => 0,
        };
    }

    public BitIndex(int baseBitCount, int itemCount, Span<byte> source, ref int offset)
    {        
        ByteLength = GetByteLength(baseBitCount, itemCount);
        _buffer = source.Slice(offset,ByteLength).ToArray();
        offset += ByteLength;
        _baseBitCount = baseBitCount;
        _itemCount = itemCount;
        _mask = _baseBitCount switch
        {
            1 => 0b_0000_0001,
            2 => 0b_0000_0011,
            3 => 0b_0000_0111,
            4 => 0b_0000_1111,
            5 => 0b_0001_1111,
            6 => 0b_0011_1111,
            7 => 0b_0111_1111,
            8 => 0b_1111_1111,
            _ => 0,
        };
    }

    private static int GetByteLength(int baseBitCount, int itemCount)
    {
        var totalBits = (baseBitCount * itemCount);
        var div = totalBits / 8;
        var rem = totalBits % 8;
        return div + (rem != 0 ? 1 : 0);
    }

    private int GetCalSection(int index, out int offset, out int rem, out int consumeBytes)
    {
        var usedBits = (index * _baseBitCount);
        offset = usedBits / 8;
        rem = usedBits % 8;
        var tmp = rem + _baseBitCount;
        consumeBytes = (tmp / 8) + ((tmp % 8) == 0 ? 0 : 1);
        return consumeBytes switch
        {
            1 => _buffer[offset],
            2 => (_buffer[offset + 1] << 8) | _buffer[offset],
            _ => 0,
        };
    }

    public void Set(int index, int value)    
    {
        var section = GetCalSection(index, out var offset, out var rem, out var consumeBytes);
        section = (section & ~(_mask << rem)) | (value << rem);
        switch (consumeBytes)
        {
            case 1:
                _buffer[offset] = (byte)section;
                break;
            case 2:
                _buffer[offset] = (byte)section;
                _buffer[offset + 1] = (byte)(section >> 8);
                break;
            default:
                break;
        }
    }

    public int Get(int index)
    {
        var section = GetCalSection(index, out _, out var rem, out _);
        return (section & (_mask << rem)) >> rem;
    }

    public byte[] ToBytes()
    {
        return _buffer;
    }

    public int Count(int value)
    {
        var cnt = 0;
        for (int i = 0; i < _itemCount; i++)
        {
            if (Get(i) == value)
            {
                cnt++;
            }
        }
        return cnt;
    }

}
