using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Server.Game;

namespace Server.Core;

public abstract class Session
{
    public int SessionId { get; set; }
    public NetPeer UdpPeer { get; set; }
    
    private Socket _socket;
    private int _disconnected = 0;
    
    // Recv
    private RecvBuffer _recvBuffer = new(1024);
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
            Console.WriteLine($"Disconnect: {e.Message}");
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
            Console.WriteLine($"Disconnect: {e.Message}");
        }
    }

    private void OnRecvCompleted(object? sender, SocketAsyncEventArgs args)
    {
        if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
        {
            try
            {
                // Write 커서 이동
                if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                {
                    Disconnect();
                    return;
                }
                
                int processLen = OnRecv(_recvBuffer.ReadSegment);
                if (processLen < 0 || _recvBuffer.OnRead(processLen) == false)
                {
                    Disconnect();
                    return;
                }
                
                RegisterRecv();
            }
            catch (Exception e)
            {
                Disconnect();
                Console.WriteLine($"Disconnect: {e.Message}");
            }
        }
        else
        {
            Disconnect();
        }
    }
    
    public abstract int OnRecv(ArraySegment<byte> buffer);
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