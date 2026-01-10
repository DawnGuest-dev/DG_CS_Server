namespace Server.Game.Room;

public class Cell
{
    public List<Player> Players { get; set; } = new();
    
    // GameObjects도 추가

    public void Add(Player player)
    {
        Players.Add(player);
    }
    
    public void Remove(Player player)
    {
        Players.Remove(player);
    }
}