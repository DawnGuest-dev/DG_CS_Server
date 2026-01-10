using System.Text.Json;
using Server.Utils;

namespace Server.Data;

public class ConfigManager
{
    public static ServerConfig Config { get; private set; }

    public static void LoadConfig()
    {
        string path = "ServerConfig.json"; // 실행 파일 옆

        if (File.Exists(path) == false)
        {
            // 파일 없으면 기본값 생성 후 저장 (편의성)
            Config = new ServerConfig()
            {
                IpAddress = "127.0.0.1",
                Port = 12345,
                MaxConnection = 100,
                FrameRate = 60,
                DataPath = "Data/Config"
            };
            string jsonString = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, jsonString);
            LogManager.Info("[Config] Created default ServerConfig.json");
            return;
        }

        string text = File.ReadAllText(path);
        Config = JsonSerializer.Deserialize<ServerConfig>(text);
        LogManager.Info($"[Config] Loaded - Port: {Config.Port}, FPS: {Config.FrameRate}");
    }
}