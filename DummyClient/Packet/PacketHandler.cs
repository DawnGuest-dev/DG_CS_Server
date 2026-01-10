using Common.Packet;

namespace DummyClient.Packet;

public class PacketHandler
{
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
        Console.WriteLine($"[Move] User: {packet.PlayerId} X: {packet.X}, Y: {packet.Y}, Z: {packet.Z}");
    }

    public static void S_Chat(S_Chat packet)
    {
        Console.WriteLine($"[Chat] User: {packet.PlayerId} Msg: {packet.Msg}");
    }
}