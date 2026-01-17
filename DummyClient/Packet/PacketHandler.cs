using Google.FlatBuffers;
using Protocol;

namespace DummyClient.Packet;

public class PacketHandler
{
    public static bool IsTransfer = false;
    public static string TargetIp = "";
    public static int TargetPort = 0;
    public static string TransferToken = "";
    
    public static void S_LoginRes(S_LoginRes packet)
    {
        if (packet.Success)
        {
            Console.WriteLine($"[Login Success] Session: {packet.MySessionId}, Pos: ({packet.SpawnPosX:F1}, {packet.SpawnPosY:F1}, {packet.SpawnPosZ:F1})");
            Console.WriteLine($"[Login Success] Other Players: {packet.OtherPlayerInfos.Count}");
            
            Program.MySessionId = packet.MySessionId;
        }
        else
        {
            Console.WriteLine("[Login Failed]");
        }
    }
    
    // FlatBuffers 처리
    public static void S_Move(ByteBuffer bb)
    {
        // Root 가져오기
        var packet = Protocol.S_Move.GetRootAsS_Move(bb);
        
        // 데이터 접근 (Struct라 값 형식)
        var pos = packet.Pos.Value;
        // Console.WriteLine($"[Move] ID: {packet.PlayerId} ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
    }

    public static void S_Chat(S_Chat packet)
    {
        Console.WriteLine($"[Chat] User: {packet.PlayerId} Msg: {packet.Msg}");
    }
    
    public static void S_TransferReq(S_TransferReq packet)
    {
        Console.WriteLine($"[Client] Transfer Command! -> {packet.TargetIp}:{packet.TargetPort}");
        TargetIp = packet.TargetIp;
        TargetPort = packet.TargetPort;
        TransferToken = packet.TransferToken;
        IsTransfer = true;
    }

    public static void S_OnPlayerJoined(S_OnPlayerJoined obj)
    {
        Console.WriteLine($"[Client] Joined: {obj.PlayerInfo.PlayerId}");
    }

    public static void S_OnPlayerLeft(S_OnPlayerLeft obj)
    {
        Console.WriteLine($"[Client] Left: {obj.PlayerId}");
    }
}