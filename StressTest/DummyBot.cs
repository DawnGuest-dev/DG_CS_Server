using System.Net;
using System.Net.Sockets;
using Common.Packet;
using Google.FlatBuffers;
using Google.Protobuf;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;
using Protocol;
using C_LoginReq = Protocol.C_LoginReq;
using C_Move = Protocol.C_Move;
using S_LoginRes = Protocol.S_LoginRes;

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
    
    private FlatBufferBuilder _flatBuilder = new FlatBufferBuilder(1024);

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

            SendTcp(MsgId.IdCLoginReq, req);
            
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
                    // ID 확인 (Offset 2)
                    ushort id = BitConverter.ToUInt16(buffer, 2);
                    
                    if (id == (ushort)MsgId.IdSLoginRes)
                    {
                        // Protobuf Parse
                        var res = S_LoginRes.Parser.ParseFrom(new ReadOnlySpan<byte>(buffer, 4, recv - 4));
                        if (res.Success)
                        {
                            ConnectUdp(res.MySessionId);
                        }
                    }
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
            // 좌표 업데이트
            _x += (float)(_rand.NextDouble() - 0.5) * 5f;
            _z += (float)(_rand.NextDouble() - 0.5) * 5f;

            // [수정] FlatBuffers C_Move 전송
            _flatBuilder.Clear(); // 빌더 재사용 (필수!)

            var posOffset = Vec3.CreateVec3(_flatBuilder, _x, 0, _z);
            
            C_Move.StartC_Move(_flatBuilder);
            C_Move.AddPos(_flatBuilder, posOffset);
            var offset = C_Move.EndC_Move(_flatBuilder);
            _flatBuilder.Finish(offset.Value);

            SendUdp(MsgId.IdCMove, _flatBuilder);
        }
    }

    private void SendTcp<T>(MsgId msgId, T packet) where T : IMessage
    {
        int size = 4 + packet.CalculateSize();
        byte[] buffer = new byte[size];

        BitConverter.TryWriteBytes(new Span<byte>(buffer, 0, 2), (ushort)size);
        BitConverter.TryWriteBytes(new Span<byte>(buffer, 2, 2), (ushort)msgId);
        
        packet.WriteTo(new Span<byte>(buffer, 4, size - 4));

        _tcpSocket.Send(buffer);
    }

    private void SendUdp(MsgId msgId, FlatBufferBuilder builder)
    {
        // Builder 데이터 추출
        var buf = builder.DataBuffer;
        int bodyStart = buf.Position;
        int bodyLen = buf.Length - bodyStart;
        
        int size = 4 + bodyLen;
        byte[] buffer = new byte[size]; // 최적화하려면 재사용 버퍼 사용 권장

        BitConverter.TryWriteBytes(new Span<byte>(buffer, 0, 2), (ushort)size);
        BitConverter.TryWriteBytes(new Span<byte>(buffer, 2, 2), (ushort)msgId);

        // Body Copy (Zero-Copy를 위해 Span 사용)
        buf.ToArray(bodyStart, bodyLen).CopyTo(new Span<byte>(buffer, 4, bodyLen));

        _serverPeer.Send(buffer, DeliveryMethod.Sequenced);
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