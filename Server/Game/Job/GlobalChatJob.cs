using Protocol;
using Server.Core;

namespace Server.Game.Job;

public class GlobalChatJob : IJob
{
    public string RawMessage; // "Name:Message" 형태의 원본 문자열

    public void Execute()
    {
        if (string.IsNullOrEmpty(RawMessage)) return;

        string[] parts = RawMessage.Split(':', 2);
        if (parts.Length >= 2)
        {
            string senderName = parts[0];
            string message = parts[1];

            S_Chat packet = new S_Chat()
            {
                PlayerId = 0, // 시스템 메시지나 글로벌 메시지는 보통 ID 0 사용
                Msg = $"[Global] {senderName}: {message}"
            };

            GameRoom.Instance.BroadcastAll(MsgId.IdSChat, packet);
        }

        // [중요] 사용 후 반납
        RawMessage = null; 
        JobPool<GlobalChatJob>.Return(this);
    }
}