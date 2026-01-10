using System.Net;
using System.Net.Sockets;
using Common;
using LiteNetLib;
using LiteNetLib.Utils;
using Server.Packet;
using Server.Utils;

namespace Server.Core;

public class RudpHandler : INetEventListener
{
    private NetManager _netManager;

    public void Init(int port)
    {
        _netManager = new NetManager(this);
        _netManager.ChannelsCount = 3;
        _netManager.Start(port);
        
        LogManager.Info("Server Started");
    }

    public void Update()
    {
        _netManager.PollEvents();
    }
    
    // 접속 요청
    public void OnConnectionRequest(ConnectionRequest request)
    {
        if (request.Data.AvailableBytes >= 4)
        {
            int sessionId = request.Data.GetInt();

            Session session = SessionManager.Instance.Find(sessionId);

            if (session != null)
            {
                NetPeer peer = request.Accept();
                
                session.UdpPeer = peer;
                peer.Tag = session;
                
                Console.WriteLine($"Accepted: {peer.Address}");
            }
            else
            {
                Console.WriteLine($"Session Not Found: {sessionId}");
                request.Reject();
            }
        }
        else
        {
            Console.WriteLine("Invalid Session ID");
            request.Reject();
        }
    }
    
    // 접속 완료
    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"Peer connected: {peer.Address}");
    }

    // 
    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        // TODO
    }
    
    // 데이터 수신
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        Session session = peer.Tag as Session;

        if (session != null)
        {
            byte[] data = reader.GetRemainingBytes();
            PacketManager.Instance.OnRecvPacket(session, data);
        }
        
        // switch (channelNumber)
        // {
        //     case NetConfig.Ch_UDP:
        //         HandleUDP(peer, reader);
        //         break;
        //     case NetConfig.Ch_RUDP1:
        //         HandleRUDP1(peer, reader);
        //         break;
        //     case NetConfig.Ch_RUDP2:
        //         HandleRUDP2(peer, reader);
        //         break;
        //     default:
        //         Console.WriteLine($"Unknown Channel: {channelNumber}");
        //         break;
        //     
        // }
    }

    // 임시 핸들러
    private void HandleUDP(NetPeer peer, NetPacketReader reader)
    {
        string msg = reader.GetString();
        
        Console.WriteLine($"UDP: {msg}");
    }

    private void HandleRUDP1(NetPeer peer, NetPacketReader reader)
    {
        string msg = reader.GetString();
        Console.WriteLine($"RUDP1: {msg}");
        
        NetDataWriter writer = new();
        writer.Put("RUDP1 OK");
        peer.Send(writer, NetConfig.Ch_RUDP1, DeliveryMethod.ReliableOrdered);
    }
    
    private void HandleRUDP2(NetPeer peer, NetPacketReader reader)
    {
        string msg = reader.GetString();
        Console.WriteLine($"RUDP2: {msg}");
        
        NetDataWriter writer = new();
        writer.Put("RUDP2 OK");
        peer.Send(writer, NetConfig.Ch_RUDP2, DeliveryMethod.ReliableOrdered);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        // TODO
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        // TODO
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        // TODO
    }
}