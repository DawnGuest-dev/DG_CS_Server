using System.Text.Json;
using Server.Data;
using Server.Utils;
using StackExchange.Redis;

namespace Server.DB;

public static class RedisManager
{
    private static ConnectionMultiplexer _conn;
    private static IDatabase _db;
    private static ISubscriber _sub;

    public static void Init()
    {
        string connStr = ConfigManager.Config.RedisConnectionString;

        try
        {
            ConfigurationOptions options = ConfigurationOptions.Parse(connStr);
            _conn = ConnectionMultiplexer.Connect(options);
            _db = _conn.GetDatabase();
            _sub = _conn.GetSubscriber();
            
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

    public static void Publish(string channel, string message)
    {
        if (_sub == null) return;
        
        _sub.Publish(channel, message);
    }

    public static void Subscribe(string channel, Action<string> callback)
    {
        if (_sub == null) return;

        _sub.Subscribe(channel, (channel, redisValue) =>
        {
            callback(redisValue);
        });
        
        LogManager.Info($"Subscribe to {channel}");
    }

    public static void SavePlayerState(string authToken, PlayerState state)
    {
        string json = JsonSerializer.Serialize(state);
        SetString($"PlayerState:{authToken}", json, TimeSpan.FromMinutes(10)); // Test: 10Min
    }

    public static PlayerState LoadPlayerState(string authToken)
    {
        // Key를 저장할 때와 똑같이 맞춰야 합니다.
        // SavePlayerState 할 때도 $"PlayerState:{authToken}"으로 저장했는지 확인 필요!
        string key = $"PlayerState:{authToken}";
        
        string json = GetString(key);
        if (string.IsNullOrEmpty(json)) return null;
        
        // JSON 역직렬화
        try 
        {
            return JsonSerializer.Deserialize<PlayerState>(json);
        }
        catch 
        {
            return null;
        }
    }
    
    
}