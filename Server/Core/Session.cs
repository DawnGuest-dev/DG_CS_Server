using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Server.Game;
using Server.Packet;

namespace Server.Core;

public abstract class Session
{
    public int SessionId { get; set; }
    public NetPeer UdpPeer { get; set; }
    
    private Socket _socket;
    private int _disconnected = 0;
    
    // Recv
    private RecvBuffer _recvBuffer = new(65535);
    private SocketAsyncEventArgs _recvArgs = new();
    
    // Send
    private object _lock = new();
    private Queue<ArraySegment<byte>> _sendQueue = new();
    private List<ArraySegment<byte>> _pendingList = new();
    private SocketAsyncEventArgs _sendArgs = new();
    private bool _pending = false;
    
    // Game
    public Player MyPlayer { get; set; }
    
    public string AuthToken { get; set; }
    
    public void Start(Socket socket)
    {
        _socket = socket;
        
        // recv
        _recvArgs.Completed += OnRecvCompleted;
        
        // send
        _sendArgs.Completed += OnSendCompleted;
        
        RegisterRecv();
    }

    public void Send(ArraySegment<byte> buffer)
    {
        lock (_lock)
        {
            _sendQueue.Enqueue(buffer);

            if (_pending == false)
            {
                RegisterSend();
            }
        }
    }

    public void Send(byte[] buffer)
    {
        Send(new ArraySegment<byte>(buffer, 0, buffer.Length));
    }
    
    private void RegisterSend()
    {
        _pending = true;
        
        _pendingList.Clear();

        while (_sendQueue.Count > 0)
        {
            ArraySegment<byte> buffer = _sendQueue.Dequeue();
            _pendingList.Add(buffer);
        }
        
        _sendArgs.BufferList = _pendingList;

        try
        {
            bool pending = _socket.SendAsync(_sendArgs);
            if (pending == false)
            {
                OnSendCompleted(null, _sendArgs);
            }
        }
        catch (Exception e)
        {
            Disconnect();
            // Console.WriteLine($"Disconnect: {e.Message}");
        }
    }

    private void OnSendCompleted(object? sender, SocketAsyncEventArgs args)
    {
        lock (_lock)
        {
            if (args.SocketError == SocketError.Success)
            {
                _sendArgs.BufferList = null;
                _pendingList.Clear();

                if (_sendQueue.Count > 0)
                {
                    RegisterSend();
                }
                else
                {
                    _pending = false;
                }
            }
            else
            {
                Disconnect();
            }
        }
    }

    public void Disconnect()
    {
        if (Interlocked.Exchange(ref _disconnected, 1) == 1) return; // atomic이랑 같음
        
        OnDisconnected(_socket.RemoteEndPoint);

        if (_recvBuffer != null)
        {
            _recvBuffer.Dispose();
            _recvBuffer = null;
        }
        
        _socket.Shutdown(SocketShutdown.Both);
        _socket.Close();
        
        _pendingList.Clear();
    }

    private void RegisterRecv()
    {
        _recvBuffer.Clean();
        
        ArraySegment<byte> segment = _recvBuffer.WriteSegment;
        _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

        try
        {
            bool pending = _socket.ReceiveAsync(_recvArgs);
            if (pending == false) OnRecvCompleted(null, _recvArgs);
        }
        catch (Exception e)
        {
            Disconnect();
            // Console.WriteLine($"Disconnect: {e.Message}");
        }
    }

    void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
    {
        if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
        {
            try
            {
                if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                {
                    Disconnect();
                    return;
                }
                
                int processLen = OnRecv(_recvBuffer.ReadSegment);
                    
                if (processLen < 0)
                {
                    Disconnect();
                    return;
                }
                
                if (_recvBuffer.OnRead(processLen) == false)
                {
                    Disconnect();
                    return;
                }

                // 4. 다음 수신 대기
                RegisterRecv();
            }
            catch (Exception e)
            {
                Console.WriteLine($"OnRecvCompleted Error: {e}");
            }
        }
        else
        {
            Disconnect();
        }
    }
    
    public int OnRecv(ArraySegment<byte> buffer)
    {
        int processLen = 0;
        int packetCount = 0;

        while (true)
        {
            if (buffer.Count < 4)
                break;

            // 패킷 크기 확인
            ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);

            // 아직 패킷이 다 도착하지 않음
            if (buffer.Count < dataSize)
                break;
            
            // 패킷 데이터 전체를 ArraySegment로 잘라냄 (복사 아님, 참조만 생성)
            ArraySegment<byte> packetData = new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize);
            
            PacketManager.Instance.OnRecvPacket(this, packetData);
            
            processLen += dataSize;
            
            buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
                
            packetCount++;
        }

        return processLen;
    }
    
    public abstract void OnConnected(EndPoint endPoint);
    public abstract void OnDisconnected(EndPoint endPoint);
    
    // UDP
    public void SendUDP(byte[] data, byte channel, DeliveryMethod deliveryMethod)
    {
        if (UdpPeer != null)
        {
            UdpPeer.Send(data, channel, deliveryMethod);
        }
    }
    
}