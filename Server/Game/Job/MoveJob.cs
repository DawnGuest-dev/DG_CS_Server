using Common.Packet;
using Server.Core;

namespace Server.Game.Job;

public class MoveJob : IJob
{
    public Player Player;
    public C_Move Packet;

    public void Execute()
    {
        if (Player != null && Player.Session != null)
        {
            GameRoom.Instance.HandleMove(Player, Packet);
        }

        // 풀 반납
        Player = null;
        Packet = null; 
            
        JobPool<MoveJob>.Return(this);
    }
}