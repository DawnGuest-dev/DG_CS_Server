using System.Net;
using System.Net.Sockets;

namespace Server.Core;

public class Listener
{
    private Socket _listenSocket;
    private Func<Session> _sessionFactory;

    public void Init(IPEndPoint endPoint, Func<Session> sessionFactory)
    {
        _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _sessionFactory = sessionFactory;
            
        _listenSocket.Bind(endPoint);
            
        _listenSocket.Listen(100);
            
        SocketAsyncEventArgs args = new SocketAsyncEventArgs();
        args.Completed += OnAcceptCompeleted;

        RegisterAccept(args);
    }

    private void RegisterAccept(SocketAsyncEventArgs args)
    {
        args.AcceptSocket = null;
            
        bool pending = _listenSocket.AcceptAsync(args);

        if (pending == false)
        {
            OnAcceptCompeleted(null, args);
        }
    }
        
    private void OnAcceptCompeleted(object? sender, SocketAsyncEventArgs args)
    {
        if (args.SocketError == SocketError.Success)
        {
            // 세션 생성 및 시작
            Session session = _sessionFactory.Invoke();
            session.Start(args.AcceptSocket);
            session.OnConnected(args.AcceptSocket.RemoteEndPoint);
        }
        else
        {
            Console.WriteLine($"Accept Failed: {args.SocketError}");
        }
            
        RegisterAccept(args);
    }
}