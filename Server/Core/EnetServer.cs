using System.Collections.Concurrent;
using ENet;
using Server.Packet;
using Server.Utils;

namespace Server.Core;

public class EnetServer
{
    private Host _host;
    private const int MaxClients = 1000;
    
    private ConcurrentDictionary<uint, Session> _peerMap = new();

    public void Init(ushort port)
    {
        ENet.Library.Initialize();
        
        _host = new Host();
        Address address = new Address();
        address.Port = port;
        
        _host.Create(address, MaxClients, 3);
        
        LogManager.Info($"[Enet] UDP Server listening on port {port}");
    }

    public void Update()
    {
        if (_host.Service(0, out Event enetEvent) > 0)
        {
            do
            {
                switch (enetEvent.Type)
                {
                    case EventType.Connect:
                        // 인증 전
                        break;
                    
                    case EventType.Receive:
                        HandleReceive(enetEvent);
                        break;
                    
                    case EventType.Disconnect:
                    case EventType.Timeout:
                        HandleDisconnect(enetEvent);
                        break;
                }
            } while (_host.CheckEvents(out enetEvent) > 0);
        }
    }
    
    private void HandleReceive(Event enetEvent)
    {
        try
        {
            if (_peerMap.TryGetValue(enetEvent.Peer.ID, out Session session))
            {
                byte[] buffer = new byte[enetEvent.Packet.Length];
                enetEvent.Packet.CopyTo(buffer);

                PacketManager.Instance.OnRecvPacket(session, new ArraySegment<byte>(buffer));
            }
            else
            {
                HandleAuth(enetEvent);
            }
        }
        catch (Exception e)
        {
            LogManager.Exception(e, $"[Enet] Packet Receive Error: {e}");
        }
        finally
        {
            enetEvent.Packet.Dispose();
        }
    }

    private void HandleAuth(Event enetEvent)
    {
        // 약속: 첫 4바이트는 SessionID (Little Endian)
        if (enetEvent.Packet.Length < 4) 
        {
            enetEvent.Peer.DisconnectNow(0);
            return;
        }

        byte[] buffer = new byte[4];
        enetEvent.Packet.CopyTo(buffer);
        int sessionId = BitConverter.ToInt32(buffer, 0);
        
        Session session = SessionManager.Instance.Find(sessionId);

        if (session != null)
        {
            if (_peerMap.TryAdd(enetEvent.Peer.ID, session))
            {
                session.EnetPeer = enetEvent.Peer; 
                
                // LogManager.Info($"[UDP] Auth Success! Peer({enetEvent.Peer.ID}) <-> Session({sessionId})");
            }
        }
        else
        {
            enetEvent.Peer.DisconnectNow(0);
        }
    }

    private void HandleDisconnect(Event enetEvent)
    {
        if (_peerMap.TryRemove(enetEvent.Peer.ID, out Session session))
        {
            session.EnetPeer = default;
            LogManager.Info($"[UDP] Disconnected Peer({enetEvent.Peer.ID})");
        }
    }

    public void Stop()
    {
        _host.Flush();
        _host.Dispose();
        ENet.Library.Deinitialize();
    }
}