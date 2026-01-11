using Common.Packet;

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
            Console.WriteLine("[Login Success] My Session ID: " + packet.MySessionId);
            Console.WriteLine("[Login Success] Spawn Pos: " + packet.SpawnPosX + ", " + packet.SpawnPosY + ", " + packet.SpawnPosZ);
            
            Program.MySessionId = packet.MySessionId;
        }
        else
        {
            Console.WriteLine("[Login Failed");
        }
    }
    
    public static void S_Move(S_Move packet)
    {
        // Console.WriteLine($"[Move] User: {packet.PlayerId} X: {packet.X}, Y: {packet.Y}, Z: {packet.Z}");
    }

    public static void S_Chat(S_Chat packet)
    {
        Console.WriteLine($"[Chat] User: {packet.PlayerId} Msg: {packet.Msg}");
    }
    
    public static void S_TransferReq(S_TransferReq packet)
    {
        Console.WriteLine($"[Client] ⚠️ Transfer Command Received! -> Destination: {packet.TargetIp}:{packet.TargetPort}");

        // 1. 목적지 정보 저장
        TargetIp = packet.TargetIp;
        TargetPort = packet.TargetPort;
        TransferToken = packet.TransferToken;
            
        // 2. 플래그 ON (메인 루프에서 감지)
        IsTransfer = true;
    }
}