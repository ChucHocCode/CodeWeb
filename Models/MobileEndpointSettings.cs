namespace WNCAirline.Models;

public sealed class MobileEndpointSettings
{
    public MobileEndpointSettings(int httpPort)
    {
        HttpPort = httpPort;
    }

    public int HttpPort { get; }
}
