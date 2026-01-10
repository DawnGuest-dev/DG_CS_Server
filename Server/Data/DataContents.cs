namespace Server.Data;

public class PlayerStat
{
    public int Level { get; set; }
    public int MaxHp { get; set; }
    public int Attack { get; set; }
    public float Speed { get; set; }
}

public interface ILoader<Key, Value>
{
    Dictionary<Key, Value> MakeDict();
}