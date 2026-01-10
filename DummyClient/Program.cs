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
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) { }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            byte[] data = reader.GetRemainingBytes();
            
            PacketManager.Instance.OnRecvPacket(data);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

        public void OnConnectionRequest(ConnectionRequest request) { }
    }
    
    class Program
    {
        public static int MySessionId = 0;
        static bool _isUdpConnectSent = false;
        
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
                return;
            }
            
            // UDP
            ClientListener listener = new();
            NetManager netManager = new(listener);
            netManager.ChannelsCount = 3;
            netManager.Start();
            
            Console.WriteLine("[UDP] Started");
            
            float currentX = 0;
            
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
                
                // udp 연결 시도
                if (MySessionId > 0 && _isUdpConnectSent == false)
                {
                    _isUdpConnectSent = true; // 중복 요청 방지

                    Console.WriteLine($"[UDP] Connecting with SessionId: {MySessionId}...");

                    NetDataWriter authWriter = new NetDataWriter();
                    authWriter.Put(MySessionId); // 내 ID를 담음

                    netManager.Connect("localhost", 12345, authWriter);
                }
                
                // 이동 패킷 테스트
                if (netManager.FirstPeer != null &&
                    netManager.FirstPeer.ConnectionState == ConnectionState.Connected)
                {
                    currentX += 10;
                    
                    C_Move movePacket = new()
                    {
                        X = currentX,
                        Y = 0,
                        Z = 100
                    };
                
                    byte[] pd = PacketManager.Instance.Serialize(movePacket);
                    
                    netManager.FirstPeer.Send(pd, NetConfig.Ch_RUDP1, DeliveryMethod.ReliableOrdered);
                    
                    if (Environment.TickCount64 % 2000 < 20) 
                    {
                        C_Chat chatPacket = new C_Chat() { Msg = "Hello RUDP!" };
                        byte[] data = PacketManager.Instance.Serialize(chatPacket);
                        // 채팅은 RUDP (Channel 1)
                        netManager.FirstPeer.Send(data, NetConfig.Ch_RUDP1, DeliveryMethod.ReliableOrdered);
                        Console.WriteLine("Sent Chat Packet");
                        Thread.Sleep(20); // 중복 전송 방지용 살짝 대기
                    }
                }
                
                Thread.Sleep(15);
            }
        }
    }
}