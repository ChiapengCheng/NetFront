using System.Net;

namespace NetFront.Transport;

internal static class AddressParser
{
    // Parses "tcp://*:port" or "tcp://host:port" → (IPAddress, int)
    public static (IPAddress ip, int port) ParseTcp(string address)
    {
        const string prefix = "tcp://";
        if (!address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Address must start with tcp://: {address}");

        var rest = address[prefix.Length..];
        var colonIdx = rest.LastIndexOf(':');
        if (colonIdx < 0)
            throw new ArgumentException($"No port found in address: {address}");

        var host = rest[..colonIdx];
        var port = int.Parse(rest[(colonIdx + 1)..]);
        var ip = host == "*" ? IPAddress.Any : IPAddress.Parse(host);
        return (ip, port);
    }
}
