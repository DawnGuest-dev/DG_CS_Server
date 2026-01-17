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

    public static async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (_db == null) return;
        if (expiry.HasValue)
        {
            await _db.StringSetAsync((RedisKey)key, (RedisValue)value, expiry.Value);
        }
        else
        {
            await _db.StringSetAsync((RedisKey)key, (RedisValue)value);
        }
    }

    public static async Task<string> GetStringAsync(string key)
    {
        if (_db == null) return null;
        // RedisValue는 string으로 암시적 형변환됨
        return await _db.StringGetAsync(key);
    }
    
    public static async Task<bool> DeleteKeyAsync(string key)
    {
        if (_db == null) return false;
        return await _db.KeyDeleteAsync(key);
    }

    public static async Task PublishAsync(string channel, string message)
    {
        if (_sub == null) return;
        await _sub.PublishAsync(channel, message);
    }

    public static async Task SavePlayerStateAsync(string authToken, PlayerState state)
    {
        string json = JsonSerializer.Serialize(state);
        
        await SetStringAsync($"PlayerState:{authToken}", json, TimeSpan.FromMinutes(10)); 
    }

    public static async Task<PlayerState> LoadPlayerStateAsync(string authToken)
    {
        string key = $"PlayerState:{authToken}";
        
        // 여기서 네트워크 대기 발생 -> 스레드 해방 (Non-blocking)
        string json = await GetStringAsync(key);
        
        if (string.IsNullOrEmpty(json)) return null;
        
        try 
        {
            return JsonSerializer.Deserialize<PlayerState>(json);
        }
        catch 
        {
            return null;
        }
    }

    public static void Subscribe(string channel, Action<string> callback)
    {
        if (_sub == null) return;

        _sub.Subscribe(channel, (redisChannel, redisValue) =>
        {
            callback(redisValue);
        });
        
        LogManager.Info($"Subscribe to {channel}");
    }
    
    
}