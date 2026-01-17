using Protocol;
using Server.Core;

namespace Server.Game.Job;

public class ChatJob : IJob
{
    public Player Player { get; set; }
    public C_Chat Packet { get; set; }

    public void Execute()
    {
        if (Player != null && Player.Session != null)
        {
            GameRoom.Instance.HandleChat(Player, Packet);
        }

        Player = null;
        Packet = null;
        
        JobPool<ChatJob>.Return(this);
    }
}