using System.Text.Json;

namespace Server.Data;

public class DataManager
{
    public static DataManager Instance { get; } = new();

    public Dictionary<int, PlayerStat> StatDic { get; private set; } = new();

    public void LoadData()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Config", "PlayerStat.json");
            
        if (File.Exists(path) == false)
        {
            Console.WriteLine($"[Error] File Not Found: {path}");
            return;
        }

        string text = File.ReadAllText(path);
        
        List<PlayerStat> stats = JsonSerializer.Deserialize<List<PlayerStat>>(text);
            
        StatDic = new Dictionary<int, PlayerStat>();
        foreach (PlayerStat stat in stats)
        {
            StatDic.Add(stat.Level, stat);
        }

        Console.WriteLine($"[DataManager] Loaded {StatDic.Count} stats.");
    }
}