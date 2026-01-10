namespace Server.Game;

public class GameObject
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public GameObject()
    {
        // GameObject 초기화
    }
}