using System.Net.Sockets;
using Google.FlatBuffers;
using Google.Protobuf;
using ENet;
using Protocol;
using C_LoginReq = Protocol.C_LoginReq;
using Packet = ENet.Packet;
using S_LoginRes = Protocol.S_LoginRes;

public class DummyBot
{
    private int _id;
    
    // [TCP]
    private Socket _tcpSocket;
    
    // [UDP - ENet]
    private Host _enetHost;
    private Peer _serverPeer;
    private bool _isUdpConnected = false;
    
    // [State]
    private int _sessionId = 0;
    private float _x = 0;
    private float _z = 0;
    private Random _rand = new Random();
    
    // [Memory] 재사용 빌더
    private FlatBufferBuilder _flatBuilder = new FlatBufferBuilder(1024);

    public DummyBot(int id)
    {
        _id = id;
    }

    public void Connect(string ip, int port)
    {
        _enetHost = new Host();
        _enetHost.Create();
        
        ConnectTcp(ip, port);
    }

    private void ConnectTcp(string ip, int port)
    {
        try
        {
            _tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpSocket.Connect(ip, port);
            
            C_LoginReq req = new C_LoginReq()
            {
                AuthToken = $"Bot_{_id}_{Guid.NewGuid()}", 
                TransferToken = ""
            };

            SendTcp(MsgId.IdCLoginReq, req);
            
            Thread t = new Thread(() => RecvTcpLoop(ip, port));
            t.IsBackground = true;
            t.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Bot {_id}] TCP Fail: {e.Message}");
        }
    }

    private void RecvTcpLoop(string ip, int port)
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
                    ushort id = BitConverter.ToUInt16(buffer, 2);
                    
                    if (id == (ushort)MsgId.IdSLoginRes)
                    {
                        var res = S_LoginRes.Parser.ParseFrom(new ReadOnlySpan<byte>(buffer, 4, recv - 4));
                        if (res.Success)
                        {
                            _sessionId = res.MySessionId;
                            
                            ConnectUdp(ip, port);
                        }
                    }
                }
            }
            catch { break; }
        }
    }

    private void ConnectUdp(string ip, int port)
    {
        Address address = new Address();
        address.SetHost(ip);
        address.Port = (ushort)port;

        // ENet Connect
        _serverPeer = _enetHost.Connect(address, 3);
    }

    public void Update()
    {
        if (_enetHost.IsSet)
        {
            if (_enetHost.Service(0, out Event netEvent) > 0)
            {
                do
                {
                    switch (netEvent.Type)
                    {
                        case EventType.Connect:
                            SendUdpAuth(_sessionId);
                            _isUdpConnected = true;
                            break;

                        case EventType.Receive:
                            netEvent.Packet.Dispose(); // 메모리 해제 필수
                            break;

                        case EventType.Disconnect:
                            _isUdpConnected = false;
                            break;
                    }
                } while (_enetHost.CheckEvents(out netEvent) > 0);
            }
        }

        // 2. 이동 패킷 전송
        if (_isUdpConnected)
        {
            _x += (float)(_rand.NextDouble() - 0.5) * 5f;
            _z += (float)(_rand.NextDouble() - 0.5) * 5f;

            _flatBuilder.Clear(); // 빌더 재사용
            
            C_Move.StartC_Move(_flatBuilder);
            
            var posOffset = Vec3.CreateVec3(_flatBuilder, _x, 0, _z);
            C_Move.AddPos(_flatBuilder, posOffset);
            
            var cMoveOffset = C_Move.EndC_Move(_flatBuilder);

            var packetOffset = Protocol.Packet.CreatePacket(_flatBuilder, PacketData.C_Move, cMoveOffset.Value);
        
            _flatBuilder.Finish(packetOffset.Value);

            SendUdpFlatBuffer(MsgId.IdCMove, _flatBuilder, 1, PacketFlags.None);
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

    private void SendUdpAuth(int sessionId)
    {
        byte[] buffer = BitConverter.GetBytes(sessionId);
        
        Packet packet = default;
        packet.Create(buffer, PacketFlags.Reliable);
        _serverPeer.Send(0, ref packet);
    }

    private void SendUdpFlatBuffer(MsgId msgId, FlatBufferBuilder builder, byte channel, PacketFlags flags)
    {
        var buf = builder.DataBuffer;
        int bodyStart = buf.Position;
        int bodyLen = buf.Length - bodyStart;
        
        int size = 4 + bodyLen;
        byte[] buffer = new byte[size];

        BitConverter.TryWriteBytes(new Span<byte>(buffer, 0, 2), (ushort)size);
        BitConverter.TryWriteBytes(new Span<byte>(buffer, 2, 2), (ushort)msgId);
        
        var bodySpan = buf.ToArray(bodyStart, bodyLen);
        bodySpan.CopyTo(new Span<byte>(buffer, 4, bodyLen));

        Packet packet = default;
        packet.Create(buffer, flags);
        _serverPeer.Send(channel, ref packet);
    }
}