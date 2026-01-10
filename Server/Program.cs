using System.Net;
using System.Net.Sockets;
using System.Text;
using Server.Core;
using Server.Game;
using Server.Packet;

namespace Server
{
    class GameSession : Session
    {
        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnConnected: {endPoint}, ID: {SessionId}");
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnDisconnected: {endPoint}");
            
            int playerId = MyPlayer?.Id ?? 0;
            
            if(playerId > 0) Game.GameRoom.Instance.Leave(playerId);
            {
                GameRoom.Instance.Push(() =>
                {
                    GameRoom.Instance.Leave(playerId);
                });
            }
        }

        public override int OnRecv(ArraySegment<byte> buffer)
        {
            byte[] packetData = buffer.ToArray();
            
            PacketManager.Instance.OnRecvPacket(this, packetData);
            
            return buffer.Count; 
        }
    }
    
    internal class Program
    {
        static Listener _listener = new();
        static RudpHandler _rudpHandler = new();
        
        static void Main(string[] args)
        {
            // Data Load
            Data.DataManager.Instance.LoadData();
            
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 12345);
            
            // TCP 시작
            _listener.Init(endPoint, () => { return SessionManager.Instance.Generate<GameSession>();});
            
            // RUDP 시작
            _rudpHandler.Init(12345);
            
            Console.WriteLine("Listening...");
            while (true)
            {
                _rudpHandler.Update();
                
                Game.GameRoom.Instance.Update();
                
                // 실제론 틱 관리 필요
                Thread.Sleep(15);
            }
        }
    }
}
