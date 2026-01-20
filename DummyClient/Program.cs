using System.Net;
using System.Net.Sockets;
using DummyClient.Packet;
using Google.FlatBuffers;
using ENet;
using Protocol;

namespace DummyClient
{
    class Program
    {
        public static int MySessionId = 0;
        
        static string _currentIp = "127.0.0.1";
        static int _currentPort = 12345;
        static string _transferToken = ""; 
        
        static float _currentX = 0;
        
        static Host _enetHost;
        static Peer _serverPeer;
        static bool _isUdpConnected = false;
        
        static void Main(string[] args)
        {
            ENet.Library.Initialize();

            try 
            {
                while (true)
                {
                    Console.WriteLine($"\n>>> Starting Client Session to {_currentIp}:{_currentPort}...");

                    RunGameClient();

                    if (PacketHandler.IsTransfer)
                    {
                        Console.WriteLine(">>> Moving to new server zone...");

                        _currentIp = PacketHandler.TargetIp;
                        _currentPort = PacketHandler.TargetPort;
                        _transferToken = PacketHandler.TransferToken;

                        // 좌표 보정 (Zone 이동 시뮬레이션)
                        if (_currentX > 0) _currentX = -480;
                        else _currentX = 480;

                        // 상태 초기화
                        PacketHandler.IsTransfer = false;
                        MySessionId = 0;
                        _isUdpConnected = false;

                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine(">>> Client Terminated.");
                        break;
                    }
                }
            }
            finally
            {
                ENet.Library.Deinitialize();
            }
        }

        static void RunGameClient()
        {
            // 1. TCP 연결 준비
            IPAddress ipAddr = IPAddress.Parse(_currentIp);
            IPEndPoint endPoint = new IPEndPoint(ipAddr, _currentPort);

            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.NoDelay = true;

            _enetHost = new Host();
            _enetHost.Create();
            _serverPeer = default;
            _isUdpConnected = false;

            List<byte> _tcpBuffer = new List<byte>();

            try
            {
                // 3. TCP 접속 및 로그인
                tcpSocket.Connect(endPoint);
                Console.WriteLine($"[TCP] Connected: {tcpSocket.RemoteEndPoint}");

                C_LoginReq loginPacket = new C_LoginReq
                {
                    AuthToken = "dummy_auth",
                    TransferToken = _transferToken ?? ""
                };
                
                byte[] data = PacketManager.Instance.SerializeProto(MsgId.IdCLoginReq, loginPacket);
                tcpSocket.Send(data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Connection Failed: {e.Message}");
                return;
            }

            // [게임 루프]
            while (true)
            {
                if (_enetHost.Service(0, out Event netEvent) > 0)
                {
                    do
                    {
                        switch (netEvent.Type)
                        {
                            case EventType.Connect:
                                Console.WriteLine("[UDP] Connected to Server!");
                                SendUdpAuth(_serverPeer, MySessionId);
                                _isUdpConnected = true;
                                break;

                            case EventType.Receive:
                                byte[] packetData = new byte[netEvent.Packet.Length];
                                netEvent.Packet.CopyTo(packetData);
                                PacketManager.Instance.OnRecvPacket(packetData);
                                netEvent.Packet.Dispose(); // 필수!
                                break;

                            case EventType.Disconnect:
                                Console.WriteLine("[UDP] Disconnected");
                                _isUdpConnected = false;
                                break;
                        }
                    } while (_enetHost.CheckEvents(out netEvent) > 0);
                }

                if (PacketHandler.IsTransfer)
                {
                    tcpSocket.Close();
                    _enetHost.Flush();
                    _enetHost.Dispose();
                    return;
                }

                if (tcpSocket.Poll(0, SelectMode.SelectRead))
                {
                    byte[] recvBuff = new byte[4096];
                    try
                    {
                        int recvBytes = tcpSocket.Receive(recvBuff);
                        if (recvBytes > 0)
                        {
                            byte[] receivedData = new byte[recvBytes];
                            Array.Copy(recvBuff, receivedData, recvBytes);
                            _tcpBuffer.AddRange(receivedData);

                            while (_tcpBuffer.Count >= 4)
                            {
                                byte[] sizeBytes = _tcpBuffer.GetRange(0, 2).ToArray();
                                ushort packetSize = BitConverter.ToUInt16(sizeBytes, 0);

                                if (_tcpBuffer.Count < packetSize) break;

                                byte[] packetData = _tcpBuffer.GetRange(0, packetSize).ToArray();
                                _tcpBuffer.RemoveRange(0, packetSize);

                                PacketManager.Instance.OnRecvPacket(packetData);
                            }
                        }
                        else
                        {
                            tcpSocket.Close();
                            _enetHost.Dispose();
                            return;
                        }
                    }
                    catch { return; }
                }

                // TCP로 SessionID를 받았고, UDP 연결 
                if (MySessionId > 0 && !_serverPeer.IsSet)
                {
                    Console.WriteLine($"[UDP] Connecting to {_currentIp}:{_currentPort}...");
                    Address addr = new Address();
                    addr.SetHost(_currentIp);
                    addr.Port = (ushort)_currentPort;
                    
                    // 채널 3개 할당
                    _serverPeer = _enetHost.Connect(addr, 3);
                }

                if (_isUdpConnected)
                {
                    _currentX += 0.5f; 
                    
                    FlatBufferBuilder builder = new FlatBufferBuilder(1024);
                    
                    C_Move.StartC_Move(builder);
                    var posOffset = Vec3.CreateVec3(builder, _currentX, 0, 0);
                    C_Move.AddPos(builder, posOffset);
                    var cMoveOffset = C_Move.EndC_Move(builder);
                    
                    var packetOffset = Protocol.Packet.CreatePacket(builder, PacketData.C_Move, cMoveOffset.Value);
                    builder.Finish(packetOffset.Value);

                    byte[] moveBytes = PacketManager.Instance.SerializeFlatBuffer(MsgId.IdCMove, builder);
                    
                    SendUdpPacket(_serverPeer, moveBytes, 1, PacketFlags.None);


                    if (Environment.TickCount64 % 2000 < 35)
                    {
                        string msg = $"/g I'm at Port:{_currentPort} Pos:{_currentX:F0}";

                        C_Chat chatPacket = new C_Chat { Msg = msg };
                        byte[] chatBytes = PacketManager.Instance.SerializeProto(MsgId.IdCChat, chatPacket);

                        SendUdpPacket(_serverPeer, chatBytes, 0, PacketFlags.Reliable);
                        
                        Console.WriteLine($"[Chat] Sent: {msg}");
                    }
                }

                Thread.Sleep(33);
            }
        }

        static void SendUdpAuth(Peer peer, int sessionId)
        {
            byte[] buffer = BitConverter.GetBytes(sessionId);
            
            ENet.Packet packet = default;
            packet.Create(buffer, PacketFlags.Reliable);
            peer.Send(0, ref packet);
        }
        
        static void SendUdpPacket(Peer peer, byte[] data, byte channel, PacketFlags flags)
        {
            ENet.Packet packet = default;
            packet.Create(data, flags);
            peer.Send(channel, ref packet);
        }
    }
}