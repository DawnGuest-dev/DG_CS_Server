using Server.Core;

namespace Server.Game.Job;

public class EnterJob : IJob
{
    public Player NewPlayer { get; set; }

    public void Execute()
    {
        if (NewPlayer != null && NewPlayer.Session != null)
        {
            GameRoom.Instance.Enter(NewPlayer);
        }

        NewPlayer = null;
        JobPool<EnterJob>.Return(this);
    }
}