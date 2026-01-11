using Server.Core;

namespace Server.Game.Job;

public class EnterJob : IJob
{
    public Player NewPlayer;

    public void Execute()
    {
        if (NewPlayer != null)
        {
            // GameRoom의 실제 입장 로직 호출
            GameRoom.Instance.Enter(NewPlayer);
        }
            
        NewPlayer = null; // 참조 해제
        JobPool<EnterJob>.Return(this); // 반납
    }
}