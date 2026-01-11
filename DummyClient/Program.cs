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
        // [접속 정보] (서버 이동 시 갱신됨)
        static string _currentIp = "127.0.0.1";
        static int _currentPort = 12345;
        static string _transferToken = ""; // 초기엔 없음

        // [좌표 시뮬레이션]
        // Zone 1(-500~500) -> Zone 2(-500~500) 연결됨
        // 테스트를 위해 전역으로 관리
        static float _currentX = 0;
        
        static bool _isUdpConnectSent = false;
        
        static void Main(string[] args)
        {
            // [재접속 루프]
            while (true)
            {
                Console.WriteLine($"\n>>> Starting Client Session to {_currentIp}:{_currentPort}...");

                // 1. 게임 세션 실행 (여기서 블로킹되다가, 이동 명령 오면 리턴됨)
                RunGameClient();

                // 2. 루프 탈출 후 상태 확인
                if (PacketHandler.IsTransfer)
                {
                    Console.WriteLine(">>> Moving to new server zone...");

                    // 목적지 정보 갱신
                    _currentIp = PacketHandler.TargetIp;
                    _currentPort = PacketHandler.TargetPort;
                    _transferToken = PacketHandler.TransferToken;

                    // [좌표 보정]
                    // Zone 1에서 오른쪽(>500)으로 나갔으니, Zone 2의 왼쪽(-480)에서 시작한다고 가정
                    // (실제로는 서버가 저장한 Redis 데이터를 믿어야 하지만, 더미 클라니까 강제 설정)
                    if (_currentX > 0) _currentX = -480;
                    else _currentX = 480;

                    // 플래그 초기화
                    PacketHandler.IsTransfer = false;
                    MySessionId = 0; // 세션 ID 초기화

                    // 잠시 대기
                    Thread.Sleep(1000);
                }
                else
                {
                    // 이동이 아닌데 종료된 경우 (에러 등)
                    Console.WriteLine(">>> Client Terminated.");
                    break;
                }
            }
        }
        
        static void RunGameClient()
        {
            // TCP 소켓 준비
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(_currentIp), _currentPort);

            // UDP 매니저 준비
            ClientListener listener = new();
            NetManager netManager = new(listener);
            netManager.ChannelsCount = 3;
            netManager.Start();
            bool isUdpConnectSent = false;

            try
            {
                // 1. TCP Connect
                tcpSocket.Connect(endPoint);
                Console.WriteLine($"[TCP] Connected: {tcpSocket.RemoteEndPoint}");

                // 2. Login Request (토큰 포함!)
                C_LoginReq loginPacket = new()
                {
                    AuthToken = "dummy_auth",
                    TransferToken = _transferToken // 이사 갈 땐 토큰이 들어있음
                };
                tcpSocket.Send(PacketManager.Instance.Serialize(loginPacket));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Connection Failed: {e.Message}");
                return;
            }

            // [게임 루프]
            while (true)
            {
                netManager.PollEvents();

                // 1. 이동 명령 감지 (가장 중요!)
                if (PacketHandler.IsTransfer)
                {
                    // 소켓 닫고 함수 종료 -> Main의 while문으로 돌아감
                    tcpSocket.Close();
                    netManager.Stop();
                    return; 
                }

                // 2. TCP 수신 처리
                if (tcpSocket.Poll(0, SelectMode.SelectRead))
                {
                    byte[] recvBuff = new byte[4096];
                    try 
                    {
                        int recvBytes = tcpSocket.Receive(recvBuff);
                        if (recvBytes > 0)
                        {
                            byte[] packetData = new byte[recvBytes];
                            Array.Copy(recvBuff, packetData, recvBytes);
                            PacketManager.Instance.OnRecvPacket(packetData);
                        }
                        else
                        {
                            // 0바이트 수신 = 서버가 끊음
                            tcpSocket.Close();
                            netManager.Stop();
                            return;
                        }
                    }
                    catch { return; }
                }

                // 3. UDP 연결 (LoginRes 받은 후 MySessionId가 세팅되면)
                if (MySessionId > 0 && !isUdpConnectSent)
                {
                    isUdpConnectSent = true;
                    Console.WriteLine($"[UDP] Connecting with SessionId: {MySessionId}...");
                    NetDataWriter authWriter = new NetDataWriter();
                    authWriter.Put(MySessionId);
                    netManager.Connect(_currentIp, _currentPort, authWriter); // 로컬호스트 대신 IP 변수 사용
                }

                // 4. 이동 패킷 전송 (UDP Connected 상태일 때)
                if (netManager.FirstPeer != null && netManager.FirstPeer.ConnectionState == ConnectionState.Connected)
                {
                    // 오른쪽으로 계속 이동
                    _currentX += 5.0f; // 속도 조금 줄임

                    C_Move movePacket = new()
                    {
                        X = _currentX,
                        Y = 0,
                        Z = 0
                    };

                    byte[] pd = PacketManager.Instance.Serialize(movePacket);
                    netManager.FirstPeer.Send(pd, NetConfig.Ch_RUDP1, DeliveryMethod.Sequenced);
                    
                    // 2초(2000ms)마다 글로벌 채팅 전송
                    if(Environment.TickCount64 % 2000 < 35)
                    {
                        // 포트 번호와 좌표를 같이 보내서 누가 보냈는지 확인
                        string msg = $"/g I'm at Port:{_currentPort} Pos:{_currentX:F0}";
                        
                        C_Chat chatPacket = new C_Chat() { Msg = msg };
                        byte[] data = PacketManager.Instance.Serialize(chatPacket);
                        
                        // 채팅은 중요하므로 ReliableOrdered (채널 1)
                        netManager.FirstPeer.Send(data, NetConfig.Ch_RUDP1, DeliveryMethod.ReliableOrdered);
                        
                        Console.WriteLine($"[Chat] Sent: {msg}");
                        
                        // 중복 전송 방지용 대기
                        Thread.Sleep(35); 
                    }

                    // 로그 출력 (너무 빠르면 보기 힘드니까 가끔)
                    if(Environment.TickCount64 % 1000 < 20)
                        Console.WriteLine($"[Move] X: {_currentX:F1}");
                }

                Thread.Sleep(33); // 약 30fps
            }
        }
    }
}