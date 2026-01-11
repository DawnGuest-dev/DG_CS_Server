namespace Server.Data;

public class ServerConfig
{
    public int ZoneId { get; set; }
    public string IpAddress { get; set; }
    public int Port { get; set; }
    public int MaxConnection { get; set; }
    public int FrameRate { get; set; }
    public string DataPath { get; set; }
    public string RedisConnectionString { get; set; }
    
    public Dictionary<string, ZoneInfo> NeighborZones { get; set; } = new();

    public class ZoneInfo
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int ZoneId { get; set; }
    }
}