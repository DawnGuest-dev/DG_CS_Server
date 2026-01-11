using System.Net;
using Server.Core;
using Server.DB;
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
        
        static volatile bool _isRunning = true;
        
        static void Main(string[] args)
        {
            Console.CancelKeyPress += OnExit;
            
            // Logger Init
            LogManager.Init();
            ExceptionManager.Init();
            
            // Config Load
            Data.ConfigManager.LoadConfig();
            
            // Data Load
            Data.DataManager.Instance.LoadData();
            
            // Redis Init
            RedisManager.Init();
            // Redis Test
            RedisManager.SetString("test_key", "test_value");
            string value = RedisManager.GetString("test_key");
            LogManager.Info($"Redis Test: {value}");
            // RedisManager.DeleteKey("test_key");
            
            
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
            
            while (_isRunning)
            {
                long start = Environment.TickCount64;
                
                
                _rudpHandler.Update();
                GameRoom.Instance.Update();
                
                long end = Environment.TickCount64;
                long elapsed = end - start;
                
                int wait = tickMs - (int)elapsed;
                if (wait > 0) Thread.Sleep(wait);
            }

            CleanUp();
        }
        
        private static void OnExit(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; 
        
            _isRunning = false; // 메인 루프 탈출 신호
        
            LogManager.Info("Termination Signal Received. Shutting down...");
        }

        private static void CleanUp()
        {
            SessionManager.Instance.KickAll();
            
            LogManager.Info("Server Shutdown Complete.");
            LogManager.Stop();
        }
    }
}
