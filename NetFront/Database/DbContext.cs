using System.Text;

namespace NetFront.Database;

public class DbContext
{
    private readonly Dictionary<string, DbItem> _map = [];

    public DbContext()
    { 
    }

    public void Set(Span<byte> key, byte[] value)
    {
        Set(Encoding.ASCII.GetString(key), value);
    }


    public void Set(byte[] key, byte[] value)
    {
        Set(Encoding.ASCII.GetString(key), value);
    }

    public void Set(string key, byte[] value)
    {
        if (_map.TryGetValue(key, out var data))
        {
            data.Set(value);
        }
        else
        {
            data = new DbItem(value);
            _map.Add(key, data);
        }            
    }

    public bool TryGet(string key, out byte[] value)
    {
        if (_map.TryGetValue(key, out var item))
        {
            value = [.. item.Buffer];
            return true;
        }
        else
        {
            value = [];
            return false;
        }
    }

    public bool TryGet(Span<byte> key, out byte[] value)
    {
        return TryGet(Encoding.ASCII.GetString(key), out value);
    }
}
