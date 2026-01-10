using System.Net;
using Server.Core;
using Server.Game;
using Server.Packet;
using Server.Utils;

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
            // Logger Init
            LogManager.Init();
            ExceptionManager.Init();
            
            // Config Load
            Data.ConfigManager.LoadConfig();
            
            // Data Load
            Data.DataManager.Instance.LoadData();
            
            string host = Data.ConfigManager.Config.IpAddress;
            int port = Data.ConfigManager.Config.Port;
            
            IPAddress ipAddr = IPAddress.Parse(host);
            IPEndPoint endPoint = new IPEndPoint(ipAddr, port);
            
            // TCP 시작
            _listener.Init(endPoint, () => { return SessionManager.Instance.Generate<GameSession>();});
            
            // RUDP 시작
            _rudpHandler.Init(port);
            
            LogManager.Info($"Listening on {host}:{port}...");
            
            int frameRate = Server.Data.ConfigManager.Config.FrameRate;
            int tickMs = 1000 / frameRate;
            
            while (true)
            {
                long start = Environment.TickCount64;
                
                
                _rudpHandler.Update();
                GameRoom.Instance.Update();
                
                long end = Environment.TickCount64;
                long elapsed = end - start;
                
                int wait = tickMs - (int)elapsed;
                if (wait > 0) Thread.Sleep(wait);
                     
            }
        }
    }
}
