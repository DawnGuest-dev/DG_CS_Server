using Server.Core;

namespace Server.Game;

public class Player : GameObject
{
    public Session Session { get; set; }

    public Player()
    {
        // Player 초기화
    }
}