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
                
                // Console.WriteLine($"Accepted: {peer.Address}");
            }
            else
            {
                // Console.WriteLine($"Session Not Found: {sessionId}");
                request.Reject();
            }
        }
        else
        {
            // Console.WriteLine("Invalid Session ID");
            request.Reject();
        }
    }
    
    // 접속 완료
    public void OnPeerConnected(NetPeer peer)
    {
        // Console.WriteLine($"Peer connected: {peer.Address}");
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