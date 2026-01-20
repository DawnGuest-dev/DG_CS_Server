using Common;
using ENet;
using Google.FlatBuffers;
using Google.Protobuf;
using Protocol;
using Server.Core;
using Server.Data;
using Server.DB;
using Server.Game.Job;
using Server.Game.Room;
using Server.Packet;
using Server.Utils;

namespace Server.Game;

public class GameRoom
{
    // 편의상 일단 1개
    public static GameRoom Instance { get; } = new();
    
    private JobQueue _jobQueue = new();
    
    private Dictionary<int, Player> _players = new();

    private const float MapSizeX = 500.0f;
    
    private const float HandoverThreshold = 10.0f;
    
    private const float SpawnSafetyMargin = 20.0f;
    
    // Cell 설정 (Test)
    public int MapMinX { get; } = -500;
    public int MapMaxX { get; } = 500;
    public int MapMinY { get; } = -500;
    public int MapMaxY { get; } = 500;
    
    public int CellSize { get; } = 100;

    private Cell[,] _cells;
    private int _cellCountX;
    private int _cellCountY;

    public GameRoom()
    {
        // 맵 크기에 맞춰 셀 배열 생성
        _cellCountX = (MapMaxX - MapMinX) / CellSize;
        _cellCountY = (MapMaxY - MapMinY) / CellSize;
            
        // 만약 딱 안 떨어지면 +1 (여유분)
        if ((MapMaxX - MapMinX) % CellSize != 0) _cellCountX++;
        if ((MapMaxY - MapMinY) % CellSize != 0) _cellCountY++;

        _cells = new Cell[_cellCountY, _cellCountX];

        for (int y = 0; y < _cellCountY; y++)
        {
            for (int x = 0; x < _cellCountX; x++)
            {
                _cells[y, x] = new Cell();
            }
        }
            
        LogManager.Info($"GameRoom Map Initialized. Grid: {_cellCountY}x{_cellCountX}");
        
        RedisManager.Subscribe("GlobalChat", OnRecvGlobalChat);
    }

    private Cell GetCell(float x, float z)
    {
        float checkX = x - MapMinX;
        float checkY = z - MapMinY;
        
        int idxX = (int)(checkX / CellSize);
        int idxY = (int)(checkY / CellSize);
        
        if (idxX < 0) idxX = 0;
        if (idxX >= _cellCountX) idxX = _cellCountX - 1;
        if (idxY < 0) idxY = 0;
        if (idxY >= _cellCountY) idxY = _cellCountY - 1;

        return _cells[idxY, idxX];
    }

    private (int, int) GetCellIndex(float x, float z)
    {
        float checkX = x - MapMinX;
        float checkY = z - MapMinY;
        
        int idxX = (int)(checkX / CellSize);
        int idxY = (int)(checkY / CellSize);
        
        if (idxX < 0) idxX = 0;
        if (idxX >= _cellCountX) idxX = _cellCountX - 1;
        if (idxY < 0) idxY = 0;
        if (idxY >= _cellCountY) idxY = _cellCountY - 1;
        
        return (idxX, idxY);
    }

    private List<Cell> GetNearCells(float x, float z)
    {
        List<Cell> zones = new ();
        
        (int centerX, int centerY) = GetCellIndex(x, z);

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int cellX = centerX + dx;
                int cellY = centerY + dy;
                
                if (cellX < 0 || cellX >= _cellCountX || cellY < 0 || cellY >= _cellCountY) continue;
                
                zones.Add(_cells[cellY, cellX]);
            }
        }
        
        return zones;
    }
    
    private void SendBytes(Player player, ArraySegment<byte> buffer, ushort packetId)
    {
        if (player.Session == null) return;

        // [100 ~ 199] : TCP
        if (packetId < 200)
        {
            player.Session.Send(buffer);
        }
        // [200 ~ 299] : UDP (Sequenced)
        else if (packetId < 300)
        {
            if (buffer.Array != null)
            {
                player.Session.SendUDP(buffer, NetConfig.Ch_UDP, PacketFlags.Unsequenced);
            }
        }
        // [300 ~ ] : RUDP (ReliableOrdered)
        else
        {
            if (buffer.Array != null)
            {
                player.Session.SendUDP(buffer, NetConfig.Ch_RUDP1, PacketFlags.Reliable);
            }
        }
    }

    private void SendPacket<T>(Player player, MsgId msgId, T packet) where T : IMessage
    {
        if (player.Session == null) return;

        ushort id = (ushort)msgId;
        
        // PacketManager의 SerializeProto 호출
        var segment = PacketManager.Instance.SerializeProto(id, packet);

        SendBytes(player, segment, id);
    }

    private void Broadcast<T>(float x, float z, MsgId msgId, T packet) where T : IMessage
    {
        ushort id = (ushort)msgId;
        var segment = PacketManager.Instance.SerializeProto(id, packet);
        
        List<Cell> zones = GetNearCells(x, z);
        foreach (Cell cell in zones)
        {
            foreach (Player player in cell.Players)
            {
                SendBytes(player, segment, id);
            }
        }
    }

    public void BroadcastExcept<T>(int playerId, MsgId msgId, T packet) where T : IMessage
    {
        ushort id = (ushort)msgId;
        var segment = PacketManager.Instance.SerializeProto(id, packet);
        
        _players.TryGetValue(playerId, out var myPlayer);

        if (myPlayer != null)
        {
            List<Cell> zones = GetNearCells(myPlayer.X, myPlayer.Z);

            foreach (Cell cell in zones)
            {
                foreach (Player player in cell.Players)
                {
                    if (player.Id == playerId) continue;
                    SendBytes(player, segment, id);
                }
            }
        }
    }
    
    public void BroadcastCell<T>(int playerId, MsgId msgId, T packet) where T : IMessage
    {
        _players.TryGetValue(playerId, out var myPlayer);
        if (myPlayer != null) Broadcast(myPlayer.X, myPlayer.Z, msgId, packet);
    }

    public void BroadcastAll<T>(MsgId msgId, T packet) where T : IMessage
    {
        ushort id = (ushort)msgId;
        var segment = PacketManager.Instance.SerializeProto(id, packet);
        
        foreach (Player p in _players.Values)
        {
            // SendPacket(p, packet);
            SendBytes(p, segment, id);
        }
    }

    public void Push(IJob job)
    {
        _jobQueue.Push(job);
    }

    public void Update()
    {
        _jobQueue.Flush();
    }
    
    public void Enter(Player newPlayer)
    {
        if(newPlayer == null || _players.ContainsKey(newPlayer.Id)) return;
            
        _players.Add(newPlayer.Id, newPlayer);
        newPlayer.Session.MyPlayer = newPlayer;
            
        Cell cell = GetCell(newPlayer.X, newPlayer.Z);
        cell.Add(newPlayer);

        if (_players.Count % 100 == 0)
        {
            LogManager.Info($"Player enter count: {_players.Count}");
        }
        
        S_LoginRes res = new S_LoginRes
        {
            Success = true,
            MySessionId = newPlayer.Session.SessionId,
            SpawnPosX = newPlayer.X, 
            SpawnPosY = newPlayer.Y, 
            SpawnPosZ = newPlayer.Z
        };

        List<PlayerInfo> otherPlayerList = new List<PlayerInfo>();
            
        // TODO: 너무 많음. 데이터 너무 커짐 S_LoginRes를 여러번 보내던가 cell 최대 인원을 잡던가
        foreach(Cell nearCell in GetNearCells(newPlayer.X, newPlayer.Z))
        {
            foreach(Player p in nearCell.Players)
            {
                if(p.Id == newPlayer.Id) continue;
                otherPlayerList.Add(new PlayerInfo { PlayerId = p.Id, PosX = p.X, PosY = p.Y, PosZ = p.Z });
            }
        }
        
        foreach(var info in otherPlayerList)
        {
            // Protocol.PlayerInfo 객체 생성
            res.OtherPlayerInfos.Add(new PlayerInfo { 
                PlayerId = info.PlayerId, 
                PosX = info.PosX, 
                PosY = info.PosY, 
                PosZ = info.PosZ 
            });
        }
            
        SendPacket(newPlayer, MsgId.IdSLoginRes, res);
            
        // 2. 주변에 내 입장 알림
        S_OnPlayerJoined onPlayerJoined = new()
        {
            PlayerInfo = new PlayerInfo() { PlayerId = newPlayer.Id, PosX = newPlayer.X, PosY = newPlayer.Y, PosZ = newPlayer.Z }
        };
            
        BroadcastExcept(newPlayer.Id, MsgId.IdSOnPlayerJoined, onPlayerJoined);
    }
    
    public void Leave(int playerId)
    {
        if (_players.ContainsKey(playerId))
        {
            // 주변에 퇴장 알림
            BroadcastExcept(playerId, MsgId.IdSOnPlayerLeft, new S_OnPlayerLeft { PlayerId = playerId });
                
            if (_players.Remove(playerId, out Player player))
            {
                Cell cell = GetCell(player.X, player.Z);
                cell.Remove(player);
                    
                if(player.Session != null) player.Session.MyPlayer = null;
                LogManager.Info($"Player {player.Id} left.");
            }
        }
    }

    public void HandleMove(Player player, float x, float y, float z)
    {
        if (player == null) return;
        
        // 1. East (오른쪽) 이동 판정
        if (x > MapSizeX + HandoverThreshold)
        {
            if (ConfigManager.Config.NeighborZones.TryGetValue("East", out ServerConfig.ZoneInfo neighbor))
            {
                float nextX = -MapSizeX + SpawnSafetyMargin;
                InitiateHandover(player, neighbor, nextX, player.Z);
                return;
            }
        }
        // 2. West (왼쪽) 이동 판정
        else if (x < -MapSizeX - HandoverThreshold)
        {
            if (ConfigManager.Config.NeighborZones.TryGetValue("West", out ServerConfig.ZoneInfo neighbor))
            {
                float nextX = MapSizeX - SpawnSafetyMargin;
                InitiateHandover(player, neighbor, nextX, player.Z);
                return;
            }
        }
        
        Cell oldCell = GetCell(player.X, player.Z);

        // 1. 서버 메모리 상의 좌표 업데이트
        player.X = x;
        player.Y = y;
        player.Z = z;
        
        Cell newCell = GetCell(player.X, player.Z);

        if (oldCell != newCell)
        {
            oldCell.Remove(player);
            newCell.Add(player);
        }

        // [중요] FlatBuffers를 이용해 주변에 이동 패킷 전송
        BroadcastMove(player);
    }
    
    private void BroadcastMove(Player player)
    {
        FlatBufferBuilder builder = PacketManager.Instance.GetFlatBufferBuilder();
        
        S_Move.StartS_Move(builder);
        var posOffset = Vec3.CreateVec3(builder, player.X, player.Y, player.Z);
        S_Move.AddPos(builder, posOffset);
        var sMoveOffset = S_Move.EndS_Move(builder);
        
        var packetOffset = Protocol.Packet.CreatePacket(builder, PacketData.S_Move, sMoveOffset.Value);
        
        builder.Finish(packetOffset.Value);

        var segment = PacketManager.Instance.FinalizeFlatBuffer(builder, (ushort)MsgId.IdSMove);
        
        // 7. 전송
        List<Cell> zones = GetNearCells(player.X, player.Z);
        foreach (Cell cell in zones)
        {
            foreach (Player p in cell.Players)
            {
                SendBytes(p, segment, (ushort)MsgId.IdSMove);
            }
        }
    }
    
    public void HandleChat(Player player, C_Chat packet)
    {
        if (player == null) return;
        
        long now = Environment.TickCount64;
        if(now - player.LastChatTime < 500) // 0.5초 이내에 채팅하면 무시
        {
            return;
        }
        player.LastChatTime = now;

        if (packet.Msg.StartsWith("/g "))
        {
            string content = packet.Msg.Substring(3); // "/g " 제거
            string pubMsg = $"{player.Name}:{content}";
            
            // Redis로 발행
            _ = RedisManager.PublishAsync("GlobalChat", pubMsg);
            
            // LogManager.Info($"[Global Chat] {pubMsg}");
        }
        else
        {
            S_Chat chatRes = new()
            {
                PlayerId = player.Id,
                Msg = packet.Msg
            };
        
            // Console.WriteLine($"[Chat] {player.Name}({player.Id}): {packet.Msg}");
        
            BroadcastCell(player.Id, MsgId.IdSChat, chatRes);   
        }
    }
    
    private void InitiateHandover(Player player, ServerConfig.ZoneInfo targetZone, float nextX, float nextZ)
    {
        // 1. 중복 처리 방지 (간단 체크)
        if (player.Session == null || player.IsTransferring) return;
        
        player.IsTransferring = true;

        // Console.WriteLine($"[Handover] {player.Name} leaving to Zone {targetZone.ZoneId} ({targetZone.Port})");

        // 2. 이관 토큰 생성
        string token = Guid.NewGuid().ToString();

        // 3. 상태 저장 (Redis)
        PlayerState state = player.GetState(token);
        state.X = nextX; // 다음 서버에서의 시작 위치 설정
        state.Z = nextZ;
        string authToken = player.Session.AuthToken;

        Task.Run(async () =>
        {
            await RedisManager.SavePlayerStateAsync(authToken, state);

            // 4. 저장이 끝나면 "마무리 작업"을 다시 GameRoom 큐에 넣음
            Push(new HandoverCompleteJob
            {
                PlayerId = player.Id,
                TargetZone = targetZone,
                TransferToken = token
            });
        });
    }
    
    public void FinishHandover(int playerId, ServerConfig.ZoneInfo targetZone, string token)
    {
        // 플레이어가 그사이 나갔을 수도 있으니 체크
        if (!_players.TryGetValue(playerId, out Player player)) return;
    
        // 패킷 전송
        S_TransferReq transferPacket = new S_TransferReq()
        {
            TargetIp = targetZone.IpAddress,
            TargetPort = targetZone.Port,
            TransferToken = token
        };

        ushort id = (ushort)MsgId.IdSTransferReq;
        var data = PacketManager.Instance.SerializeProto(id, transferPacket);
        player.Session.Send(data);

        // 진짜 퇴장 처리
        Leave(player.Id);
    }
    
    private void OnRecvGlobalChat(string msg)
    {
        var job = JobPool<GlobalChatJob>.Get();
        job.RawMessage = msg;
        
        Push(job);
    }
}