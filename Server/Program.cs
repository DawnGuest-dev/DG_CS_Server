using System.Net;
using System.Net.Sockets;
using System.Text;
using Server.Core;

namespace Server
{
    class GameSession : Session
    {
        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnConnected: {endPoint}, ID: {SessionId}");

            string udpLoginData = $"LOGIN_ID:{SessionId}";
            Send(Encoding.UTF8.GetBytes(udpLoginData));
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnDisconnected: {endPoint}");
        }

        public override int OnRecv(ArraySegment<byte> buffer)
        {
            string recvData = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
            Console.WriteLine($"[From Client] {recvData}");
            
            // Echo
            string sendData = $"Server Echo: {recvData}";
            byte[] sendBuff = Encoding.UTF8.GetBytes(sendData);
            
            Send(sendBuff);
            
            return buffer.Count; 
        }
    }
    
    internal class Program
    {
        static Listener _listener = new();
        static RudpHandler _rudpHandler = new();
        
        static void Main(string[] args)
        {
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
                
                // 실제론 틱 관리 필요
                Thread.Sleep(15);
            }
        }
    }
}
