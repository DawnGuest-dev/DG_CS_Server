using System.Net;
using System.Net.Sockets;
using System.Text;
using Common;
using LiteNetLib;
using LiteNetLib.Utils;

namespace DummyClient
{
    class ClientListener : INetEventListener
    {
        public void OnPeerConnected(NetPeer peer)
        {
            Console.WriteLine("Connected to server");

            NetDataWriter udpWriter = new NetDataWriter();
            udpWriter.Put("UDP from client");
            peer.Send(udpWriter, NetConfig.Ch_UDP, DeliveryMethod.Sequenced);
            
            NetDataWriter rudp1Writer = new NetDataWriter();
            rudp1Writer.Put("RUDP1 from client");
            peer.Send(rudp1Writer, NetConfig.Ch_RUDP1, DeliveryMethod.ReliableOrdered);
            
            NetDataWriter rudp2Writer = new NetDataWriter();
            rudp2Writer.Put("RUDP2 from client");
            peer.Send(rudp2Writer, NetConfig.Ch_RUDP2, DeliveryMethod.ReliableOrdered);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) { }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            Console.WriteLine($"[Recv Ch-{channelNumber}] {reader.GetString()}");
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

        public void OnConnectionRequest(ConnectionRequest request) { }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            // TCP
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 12345);
            
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Connect(endPoint);
            
            byte[] buff = new byte[1024];
            int n = tcpSocket.Receive(buff);
            string msg = Encoding.UTF8.GetString(buff, 0, n); // "LOGIN_ID:{SessionId}"

            int mySessionId = 0;
            if (msg.StartsWith("LOGIN_ID:"))
            {
                string idStr = msg.Split(':')[1];
                mySessionId = int.Parse(idStr);
                Console.WriteLine($"[Client] Connected to Server: {mySessionId}");
            }
            
            // UDP
            ClientListener listener = new();
            NetManager netManager = new(listener);
            netManager.ChannelsCount = 3;
            netManager.Start();
            
            NetDataWriter authWriter = new NetDataWriter();
            authWriter.Put(mySessionId);

            netManager.Connect("localhost", 12345, authWriter);

            while (true)
            {
                netManager.PollEvents();
                
                Thread.Sleep(15);
                
                // try
                // {
                //     Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                //     
                //     socket.Connect(endPoint);
                //     Console.WriteLine($"[Connected] To Server: {socket.RemoteEndPoint}");
                //     
                //     for (int i = 0; i < 5; i++)
                //     {
                //         string msg = $"Hello Server! Count: {i}";
                //         byte[] sendBuff = Encoding.UTF8.GetBytes(msg);
                //         socket.Send(sendBuff);
                //
                //         byte[] recvBuff = new byte[1024];
                //         int recvBytes = socket.Receive(recvBuff);
                //         
                //         string recvData = Encoding.UTF8.GetString(recvBuff, 0, recvBytes);
                //         Console.WriteLine($"[From Server] {recvData}");
                //         
                //         Thread.Sleep(1000); 
                //     }
                //     
                //     socket.Shutdown(SocketShutdown.Both);
                //     socket.Close();
                //     Console.WriteLine("[Disconnected] Session Closed.");
                // }
                // catch (Exception e)
                // {
                //     Console.WriteLine(e.ToString());
                // }
                //
                // Thread.Sleep(3000);
            }
        }
    }
}