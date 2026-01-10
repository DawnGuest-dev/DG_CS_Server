using System.Text;
using System.Text.Json;
using Server.Utils;

namespace Server.Web;

public static class HttpManager
{
    private static readonly HttpClient _client = new();

    public static void Init()
    {
        _client.Timeout = TimeSpan.FromSeconds(10);
        LogManager.Info("HttpManager Initialized");
    }
    
    public static async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest data)
    {
        try
        {
            string jsonString = JsonSerializer.Serialize(data);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            
            HttpResponseMessage response = await _client.PostAsync(url, content);
            
            response.EnsureSuccessStatusCode();
            
            string responseString = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrEmpty(responseString)) 
                return default;

            return JsonSerializer.Deserialize<TResponse>(responseString);
        }
        catch (Exception ex)
        {
            LogManager.Exception(ex, $"[HttpManager] Failed POST to {url}");
            return default;
        }
    }
}