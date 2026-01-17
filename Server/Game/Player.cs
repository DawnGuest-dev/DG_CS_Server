using Server.Core;
using Server.Data;

namespace Server.Game;

public class Player : GameObject
{
    public Session Session { get; set; }
    
    public bool IsTransferring { get; set; } = false;
    
    // 이런 부분 나중에 다 컴포넌트 형식으로 수정
    // GAS 느낌도 고려
    public long LastChatTime { get; set; } = 0;
    
    public PlayerStat Stat { get; private set; }
    public int CurrentHp { get; set; }

    public Player()
    {
        // Player 초기화
        Init(1);
    }

    public void Init(int level)
    {
        if (DataManager.Instance.StatDic.TryGetValue(level, out PlayerStat baseStat))
        {
            Stat = new PlayerStat()
            {
                Level = baseStat.Level,
                MaxHp = baseStat.MaxHp,
                Attack = baseStat.Attack,
                Speed = baseStat.Speed
            };
                
            CurrentHp = Stat.MaxHp;
            // Console.WriteLine($"[Player] Init Stat Level:{level} HP:{CurrentHp}/{Stat.MaxHp}");
        }
        else
        {
            // Console.WriteLine($"[Error] No Stat Data for Level {level}");
        }
    }

    public PlayerState GetState(string token = "")
    {
        return new PlayerState()
        {
            Name = this.Name,
            Level = this.Stat.Level,
            Hp = this.CurrentHp,
            X = this.X,
            Y = this.Y,
            Z = this.Z,
            TransferToken = token
        };
    }
}