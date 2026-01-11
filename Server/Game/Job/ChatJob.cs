using Common.Packet;
using Server.Core;

namespace Server.Game.Job;

public class ChatJob : IJob
{
    public Player Player;
    public C_Chat Packet;

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