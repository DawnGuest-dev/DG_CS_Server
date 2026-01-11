using Server.Core;

namespace Server.Game.Job;

public class LeaveJob : IJob
{
    public int PlayerId;

    public void Execute()
    {
        // GameRoom의 실제 퇴장 로직 호출
        GameRoom.Instance.Leave(PlayerId);
            
        JobPool<LeaveJob>.Return(this);
    }
}