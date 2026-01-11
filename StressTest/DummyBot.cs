using System.Net;
using System.Net.Sockets;
using Common.Packet;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;

public class DummyBot : INetEventListener
{
    private int _id;
    private NetManager _netManager;
    private NetPeer _serverPeer;
    private Socket _tcpSocket;
    private bool _isConnected = false;

    // 이동을 위한 좌표
    private float _x = 0;
    private float _z = 0;
    private Random _rand = new Random();

    public DummyBot(int id)
    {
        _id = id;
    }

    public void Connect(string ip, int port)
    {
        // 1. UDP 초기화
        _netManager = new NetManager(this);
        _netManager.Start();

        // 2. TCP 접속 시도
        ConnectTcp(ip, port);
    }

    private void ConnectTcp(string ip, int port)
    {
        try
        {
            _tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpSocket.Connect(ip, port);

            // 3. 로그인 패킷 전송
            C_LoginReq req = new C_LoginReq()
            {
                AuthToken = $"Dummy_{Guid.NewGuid()}", // 랜덤 아이디
                TransferToken = null
            };

            SendTcp(req);

            // 4. TCP 응답 대기 (간단하게 블로킹으로 처리)
            Thread t = new Thread(RecvTcpLoop);
            t.IsBackground = true;
            t.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Bot {_id}] TCP Fail: {e.Message}");
        }
    }

    private void RecvTcpLoop()
    {
        byte[] buffer = new byte[4096];
        while (_tcpSocket != null && _tcpSocket.Connected)
        {
            try
            {
                int recv = _tcpSocket.Receive(buffer);
                if (recv == 0) break;


                if (recv > 4)
                {
                    byte[] body = new byte[recv - 4];
                    Array.Copy(buffer, 4, body, 0, body.Length);
                    
                    try 
                    {
                        var res = MemoryPackSerializer.Deserialize<S_LoginRes>(body);
                        if (res != null && res.Success)
                        {
                            // 5. 로그인 성공 시 UDP 접속
                            ConnectUdp(res.MySessionId);
                        }
                    }
                    catch { }
                }
            }
            catch { break; }
        }
    }

    private void ConnectUdp(int sessionId)
    {
        NetDataWriter writer = new NetDataWriter();
        writer.Put(sessionId);
        // 로컬호스트 & 포트 하드코딩 (테스트 대상 서버)
        _netManager.Connect("127.0.0.1", 12345, writer); 
    }

    public void Update()
    {
        _netManager?.PollEvents();

        if (_isConnected)
        {
            // 0.1초마다 랜덤 이동
            _x += (float)(_rand.NextDouble() - 0.5) * 30f;
            _z += (float)(_rand.NextDouble() - 0.5) * 30f;

            // 맵 밖으로 안 나가게
            if (_x < -500) _x = -500; if (_x > 500) _x = 500;
            if (_z < -500) _z = -500; if (_z > 500) _z = 500;

            C_Move move = new C_Move() { X = _x, Y = 0, Z = _z };
            SendUdp(move);
        }
    }

    private void SendTcp<T>(T packet) where T : BasePacket
    {
        // 간단한 직렬화 (PacketManager 로직 복사)
        byte[] body = MemoryPackSerializer.Serialize(packet);
        ushort size = (ushort)(4 + body.Length);
        ushort id = (ushort)packet.Id;
        byte[] sendBuffer = new byte[size];
        Array.Copy(BitConverter.GetBytes(size), 0, sendBuffer, 0, 2);
        Array.Copy(BitConverter.GetBytes(id), 0, sendBuffer, 2, 2);
        Array.Copy(body, 0, sendBuffer, 4, body.Length);

        _tcpSocket.Send(sendBuffer);
    }

    private void SendUdp<T>(T packet) where T : BasePacket
    {
        byte[] body = MemoryPackSerializer.Serialize(packet);
        ushort size = (ushort)(4 + body.Length);
        ushort id = (ushort)packet.Id;
        byte[] sendBuffer = new byte[size];
        Array.Copy(BitConverter.GetBytes(size), 0, sendBuffer, 0, 2);
        Array.Copy(BitConverter.GetBytes(id), 0, sendBuffer, 2, 2);
        Array.Copy(body, 0, sendBuffer, 4, body.Length);

        _serverPeer.Send(sendBuffer, DeliveryMethod.Sequenced);
    }

    // --- LiteNetLib Interface ---
    public void OnPeerConnected(NetPeer peer)
    {
        _isConnected = true;
        _serverPeer = peer;
        // Console.WriteLine($"[Bot {_id}] UDP Connected!");
    }
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte ch, DeliveryMethod dm) { }
    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info) { _isConnected = false; }
    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    public void OnConnectionRequest(ConnectionRequest request) { }
}