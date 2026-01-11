using Server.Data;
using Server.Utils;
using StackExchange.Redis;

namespace Server.DB;

public static class RedisManager
{
    private static ConnectionMultiplexer _conn;
    private static IDatabase _db;

    public static void Init()
    {
        string connStr = ConfigManager.Config.RedisConnectionString;

        try
        {
            ConfigurationOptions options = ConfigurationOptions.Parse(connStr);
            _conn = ConnectionMultiplexer.Connect(options);
            _db = _conn.GetDatabase();
            
            LogManager.Info($"Redis Initialized to {connStr}");
        }
        catch (Exception ex)
        {
            LogManager.Exception(ex, "Redis Connection Failed");
            throw;
        }
    }
    
    public static bool SetString(string key, string value, TimeSpan? expiry = null)
    {
        if (_db == null) return false;
        
        if (expiry.HasValue)
        {
            return _db.StringSet(key, value, expiry.Value);
        }
        
        return _db.StringSet(key, value);
    }

    public static string GetString(string key)
    {
        if (_db == null) return null;
        return _db.StringGet(key);
    }

    public static bool DeleteKey(string key)
    {
        if (_db == null) return false;
        return _db.KeyDelete(key);
    }
    
    
}