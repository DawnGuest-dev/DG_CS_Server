using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DummyClient
{
    class Program
    {
        static void Main(string[] args)
        {
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 12345);

            Console.WriteLine($"[Client] Trying to connect to {endPoint}...");

            while (true)
            {
                try
                {
                    Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    
                    socket.Connect(endPoint);
                    Console.WriteLine($"[Connected] To Server: {socket.RemoteEndPoint}");
                    
                    for (int i = 0; i < 5; i++)
                    {
                        string msg = $"Hello Server! Count: {i}";
                        byte[] sendBuff = Encoding.UTF8.GetBytes(msg);
                        socket.Send(sendBuff);

                        byte[] recvBuff = new byte[1024];
                        int recvBytes = socket.Receive(recvBuff);
                        
                        string recvData = Encoding.UTF8.GetString(recvBuff, 0, recvBytes);
                        Console.WriteLine($"[From Server] {recvData}");
                        
                        Thread.Sleep(1000); 
                    }
                    
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                    Console.WriteLine("[Disconnected] Session Closed.");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                
                Thread.Sleep(3000);
            }
        }
    }
}