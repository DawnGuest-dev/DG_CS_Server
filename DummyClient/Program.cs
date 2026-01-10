using System.Net;
using System.Net.Sockets;
using System.Text;
using Common;
using Common.Packet;
using DummyClient.Packet;
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
        public static int MySessionId = 0;
        
        static void Main(string[] args)
        {
            // TCP
            string host = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(host);
            IPAddress ipAddr = ipHost.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 12345);
            
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            try
            {
                tcpSocket.Connect(endPoint);
                Console.WriteLine("[TCP] Connected to Server: " + tcpSocket.RemoteEndPoint);

                C_LoginReq loginPacket = new()
                {
                    AuthToken = "test"
                };

                byte[] sendData = PacketManager.Instance.Serialize(loginPacket);
                tcpSocket.Send(sendData);
                Console.WriteLine("[TCP] Sent Login Packet");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            
            
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

            while (true)
            {
                netManager.PollEvents();
                
                // [TCP 처리]
                if (tcpSocket.Poll(0, SelectMode.SelectRead))
                {
                    byte[] recvBuff = new byte[4096];
                    int recvBytes = tcpSocket.Receive(recvBuff);
                    if (recvBytes > 0)
                    {
                        // 테스트 1개만 온다고 가정
                        
                        byte[] packetData = new byte[recvBytes];
                        Array.Copy(recvBuff, packetData, recvBytes);
                    
                        PacketManager.Instance.OnRecvPacket(packetData);
                    }
                }
                
                if (MySessionId > 0 && netManager.IsRunning == false)
                {
                    authWriter.Put(mySessionId);

                    netManager.Connect("localhost", 12345, authWriter);
                }
                
                Thread.Sleep(15);
            }
        }
    }
}