using Server.Core;

namespace Server.Game;

public class Player : GameObject
{
    public Session Session { get; set; }
    
    // 이런 부분 나중에 다 컴포넌트 형식으로 수정
    // GAS 느낌도 고려
    public long LastChatTime { get; set; } = 0;

    public Player()
    {
        // Player 초기화
    }
}