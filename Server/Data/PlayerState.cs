namespace Server.Data;

public class PlayerState
{
    public string Name { get; set; }
    public int Level { get; set; }
    public int Hp { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
        
    // 인증용 임시 토큰 (이동 시 검증)
    public string TransferToken { get; set; }
}