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
            IPAddress ipAddr;
            if (!IPAddress.TryParse(_currentIp, out ipAddr))
            {
                try
                {
                    IPHostEntry entry = Dns.GetHostEntry(_currentIp);
                    ipAddr = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (ipAddr == null) return;
                }
                catch
                {
                    return;
                }
            }

            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.NoDelay = true; // 반응성 향상
            IPEndPoint endPoint = new IPEndPoint(ipAddr, _currentPort);

            ClientListener listener = new();
            NetManager netManager = new(listener);
            netManager.ChannelsCount = 3;
            netManager.Start();
            bool isUdpConnectSent = false;

            // [TCP 패킷 조립용 버퍼]
            List<byte> _tcpBuffer = new List<byte>();

            try
            {
                tcpSocket.Connect(endPoint);
                Console.WriteLine($"[TCP] Connected: {tcpSocket.RemoteEndPoint}");

                C_LoginReq loginPacket = new()
                {
                    AuthToken = "dummy_auth",
                    TransferToken = _transferToken
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

                if (PacketHandler.IsTransfer)
                {
                    tcpSocket.Close();
                    netManager.Stop();
                    return;
                }

                // --- [TCP 수신 및 조립 로직 시작] ---
                if (tcpSocket.Poll(0, SelectMode.SelectRead))
                {
                    byte[] recvBuff = new byte[4096];
                    try
                    {
                        int recvBytes = tcpSocket.Receive(recvBuff);
                        if (recvBytes > 0)
                        {
                            // 1. 받은 데이터를 버퍼 뒤에 붙인다.
                            byte[] receivedData = new byte[recvBytes];
                            Array.Copy(recvBuff, receivedData, recvBytes);
                            _tcpBuffer.AddRange(receivedData);

                            // 2. 버퍼에 처리할 수 있는 패킷이 있는지 확인하고 반복 처리
                            while (_tcpBuffer.Count >= 4) // 헤더(Size 2 + Id 2) 최소 크기 확인
                            {
                                // 2-1. 패킷 크기 파악 (앞 2바이트 읽기)
                                byte[] sizeBytes = _tcpBuffer.GetRange(0, 2).ToArray();
                                ushort packetSize = BitConverter.ToUInt16(sizeBytes, 0);

                                // 2-2. 버퍼에 아직 전체 패킷이 다 안 왔으면 대기 (다음 Receive 때 처리)
                                if (_tcpBuffer.Count < packetSize)
                                    break;

                                // 2-3. 완전한 패킷 하나를 떼어냄
                                byte[] packetData = _tcpBuffer.GetRange(0, packetSize).ToArray();
                                _tcpBuffer.RemoveRange(0, packetSize); // 버퍼에서 제거

                                // 2-4. 패킷 처리
                                PacketManager.Instance.OnRecvPacket(packetData);
                            }
                        }
                        else
                        {
                            tcpSocket.Close();
                            netManager.Stop();
                            return;
                        }
                    }
                    catch
                    {
                        return;
                    }
                }
                // --- [TCP 수신 및 조립 로직 끝] ---

                if (MySessionId > 0 && !isUdpConnectSent)
                {
                    isUdpConnectSent = true;
                    Console.WriteLine($"[UDP] Connecting with SessionId: {MySessionId}...");
                    NetDataWriter authWriter = new NetDataWriter();
                    authWriter.Put(MySessionId);
                    netManager.Connect(_currentIp, _currentPort, authWriter);
                }

                if (netManager.FirstPeer != null && netManager.FirstPeer.ConnectionState == ConnectionState.Connected)
                {
                    // (이동 로직은 동일하여 생략, 기존 코드 그대로 두시면 됩니다)
                    _currentX += 5.0f;

                    C_Move movePacket = new()
                    {
                        X = _currentX,
                        Y = 0,
                        Z = 0
                    };

                    byte[] pd = PacketManager.Instance.Serialize(movePacket);
                    netManager.FirstPeer.Send(pd, NetConfig.Ch_RUDP1, DeliveryMethod.Sequenced);

                    // 2초(2000ms)마다 글로벌 채팅 전송
                    if (Environment.TickCount64 % 2000 < 35)
                    {
                        // 포트 번호와 좌표를 같이 보내서 누가 보냈는지 확인
                        string msg = $"/g I'm at Port:{_currentPort} Pos:{_currentX:F0}";

                        C_Chat chatPacket = new C_Chat() { Msg = msg };
                        byte[] data = PacketManager.Instance.Serialize(chatPacket);

                        // 채팅은 중요하므로 ReliableOrdered (채널 1)
                        netManager.FirstPeer.Send(data, NetConfig.Ch_RUDP1, DeliveryMethod.ReliableOrdered);

                        Console.WriteLine($"[Chat] Sent: {msg}");
                    }
                }

                Thread.Sleep(33);
            }
        }
    }
}