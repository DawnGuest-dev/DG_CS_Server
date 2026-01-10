using System.Net;
using System.Text;
using Server.Core;

namespace Server
{
    class GameSession : Session
    {
        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnConnected: {endPoint}");
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
        
        static void Main(string[] args)
        {
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 12345);
            
            _listener.Init(endPoint, () => { return new GameSession(); });

            Console.WriteLine("Listening...");
            while (true) { Thread.Sleep(100); }
        }
    }
}
