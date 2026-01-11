using System.Text.Json;
using Server.Utils;

namespace Server.Data;

public class ConfigManager
{
    public static ServerConfig Config { get; private set; }

    public static void LoadConfig(string zoneIndex = "1")
    {
        string fileName = $"ServerConfig_{zoneIndex}.json";
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        
        if (File.Exists(path) == false)
        {
            LogManager.Error($"[Error] Config File Not Found: {path}");
            // 기본값 로드하거나 종료
            return;
        }

        string text = File.ReadAllText(path);
        Config = JsonSerializer.Deserialize<ServerConfig>(text);
        LogManager.Info($"[Config] Loaded - Port: {Config.Port}, FPS: {Config.FrameRate}");
    }
}