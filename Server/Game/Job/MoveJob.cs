using Server.Core;
using Server.Game;

namespace Server.Game.Job;

public class MoveJob : IJob
{
    public Player Player { get; set; }
    
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public void Execute()
    {
        if (Player != null && Player.Session != null)
        {
            // GameRoom에 데이터 전달
            GameRoom.Instance.HandleMove(Player, X, Y, Z);
        }

        Player = null;
        JobPool<MoveJob>.Return(this);
    }
}