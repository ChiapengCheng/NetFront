namespace NetFront;

public class IniReader
{
    private readonly Dictionary<string, string> _map;
    public IniReader(string path)
    {
        _map = [];
        Read(path);
    }

    public string ShowMap(string[] ignoreKeys)
    {
        var output = "";
        foreach (var item in _map)
        {
            if (ignoreKeys == null || ignoreKeys.Contains(item.Key) == false)
            {
                output += string.Format("{0}={1}{2}", item.Key, item.Value, Environment.NewLine);
            }
        }
        return output;
    }

    public Dictionary<string, string> GetMap()
    {
        return _map;
    }

    public Dictionary<string, string> GetDictionary(string key, char dataSpliter, char itemSpliter)
    {

        if (_map.TryGetValue(key, out var content))
        {
            var output = new Dictionary<string, string>();
            var data = content.Split(dataSpliter);
            foreach (var item in data)
            {
                var tmp = item.Split(itemSpliter);
                if (tmp.Length == 2)
                {
                    output.Add(tmp[0], tmp[1]);
                }
            }
            return output;
        }
        else
        {
            return [];
        }
    }

    public bool TryGetStringArray(string key, char spliter, out string[] value)
    {
        if (_map.TryGetValue(key, out var output))
        {
            value = output.Split(spliter);
            return true;
        }
        else
        {
            value = [];
            return false;
        }
    }

    public string[] GetStringArray(string key, char spliter)
    {
        return _map.TryGetValue(key, out var output) ? output.Split(spliter) : [];
    }

    public bool TryGetChar(string key, out char value)
    {
        if (_map.TryGetValue(key, out var output))
        {
            if (output.Length == 1)
            {
                value = output[0];
                return true;
            }
            value = (char)255;
            return false;
        }
        value = (char)255;
        return false;
    }

    public char GetChar(string key)
    {
        return _map.TryGetValue(key, out var output) ? output.Length == 1 ? output[0] : (char)255 : (char)255;
    }

    public bool TryGetString(string key, out string? value)
    {
        return _map.TryGetValue(key, out value);
    }

    public string GetString(string key)
    {
        return _map.TryGetValue(key, out var output) ? output : String.Empty;
    }

    public bool TryGetDouble(string key, out double value)
    {
        if (_map.TryGetValue(key, out var content))
        {
            return double.TryParse(content, out value);
        }
        value = double.NaN;
        return false;
    }

    public double GetDouble(string key)
    {
        return _map.TryGetValue(key, out var value) ? double.TryParse(value, out var output) ? output : double.NaN : double.NaN;
    }

    public bool TryGetInt32(string key, out int value)
    {
        if (_map.TryGetValue(key, out var content))
        {
            return int.TryParse(content, out value);
        }
        value = 0;
        return false;
    }

    public int GetInt32(string key)
    {
        return _map.TryGetValue(key, out var value) ? int.TryParse(value, out var output) ? output : 0 : 0;
    }

    public bool TryGetInt64(string key, out long value)
    {
        if (_map.TryGetValue(key, out var content))
        {
            return long.TryParse(content, out value);
        }
        value = 0;
        return false;
    }

    public long GetInt64(string key)
    {
        return _map.TryGetValue(key, out var value) ? long.TryParse(value, out var output) ? output : 0 : 0;
    }

    public bool TryGetBoolean(string key, out bool value)
    {
        if (_map.TryGetValue(key, out var content))
        {
            return bool.TryParse(content, out value);
        }
        value = false;
        return false;
    }

    public bool GetBoolean(string key)
    {
        return _map.TryGetValue(key, out var value) && (bool.TryParse(value, out var output) && output);
    }

    private void Read(string path)
    {
        var lines = File.ReadAllLines(path);
        foreach (var line in lines)
        {
            if (line.Length >= 2)
            {
                if (line[..2] != "//")
                {
                    var newLine = line.Replace("==", Environment.NewLine);
                    var data = newLine.Split('=');
                    if (data.Length == 2)
                    {
                        var value = data[1].Replace(Environment.NewLine, "=");
                        _map.Add(data[0], value);
                    }
                }
            }
        }
    }
}
