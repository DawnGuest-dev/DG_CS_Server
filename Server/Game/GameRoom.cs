using Common;
using Common.Packet;
using LiteNetLib;
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

    private void SendPacket<T>(Player player, T packet) where T : BasePacket
    {
        if (player.Session == null) return;

        int id = (int)packet.Id;

        // [100 ~ 199] : TCP
        if (id < 200)
        {
            player.Session.Send(PacketManager.Instance.Serialize(packet));
        }
        // [200 ~ 299] : UDP
        else if (id < 300)
        {
            player.Session.SendUDP(PacketManager.Instance.Serialize(packet), NetConfig.Ch_UDP, DeliveryMethod.Sequenced);
        }
        // [300 ~ ] : RUDP
        else
        {
            // TCP처럼 안정적이지만 UDP 기반 (채팅, 스킬 사용 등)
            player.Session.SendUDP(PacketManager.Instance.Serialize(packet), NetConfig.Ch_RUDP1, DeliveryMethod.ReliableOrdered);
        }
    }

    private void Broadcast<T>(float x, float z, T packet) where T : BasePacket
    {
        List<Cell> zones = GetNearCells(x, z);

        foreach (Cell cell in zones)
        {
            foreach (Player player in cell.Players)
            {
                SendPacket(player, packet);
            }
        }
    }

    public void BroadcastExcept<T>(int playerId, T packet) where T : BasePacket
    {
        _players.TryGetValue(playerId, out var myPlayer);

        if (myPlayer != null)
        {
            List<Cell> zones = GetNearCells(myPlayer.X, myPlayer.Z);

            foreach (Cell cell in zones)
            {
                foreach (Player player in cell.Players)
                {
                    if (player.Id == playerId) continue;
                    SendPacket(player, packet);
                }
            }
        }
    }
    
    public void BroadcastCell<T>(int playerId, T packet) where T : BasePacket
    {
        _players.TryGetValue(playerId, out var myPlayer);
        if (myPlayer != null) Broadcast(myPlayer.X, myPlayer.Z, packet);
    }

    public void BroadcastAll<T>(T packet) where T : BasePacket
    {
        foreach (Player p in _players.Values)
        {
            SendPacket(p, packet);
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
            
        LogManager.Info($"Player {newPlayer.Id} entered. Pos({newPlayer.X:F1},{newPlayer.Z:F1}) Cell({GetCellIndex(newPlayer.X, newPlayer.Z)})");
            
        // 1. 내 주변 유저 정보 수집 (AOI 적용)
        List<PlayerInfo> otherPlayerList = new List<PlayerInfo>();
            
        foreach(Cell nearCell in GetNearCells(newPlayer.X, newPlayer.Z))
        {
            foreach(Player p in nearCell.Players)
            {
                if(p.Id == newPlayer.Id) continue;
                otherPlayerList.Add(new PlayerInfo { playerId = p.Id, posX = p.X, posY = p.Y, posZ = p.Z });
            }
        }

        S_LoginRes res = new()
        {
            Success = true,
            MySessionId = newPlayer.Session.SessionId,
            SpawnPosX = newPlayer.X, SpawnPosY = newPlayer.Y, SpawnPosZ = newPlayer.Z,
            OtherPlayerInfos = otherPlayerList
        };
            
        SendPacket(newPlayer, res);
            
        // 2. 주변에 내 입장 알림
        S_OnPlayerJoined onPlayerJoined = new()
        {
            PlayerInfo = new PlayerInfo() { playerId = newPlayer.Id, posX = newPlayer.X, posY = newPlayer.Y, posZ = newPlayer.Z }
        };
            
        BroadcastExcept(newPlayer.Id, onPlayerJoined);
    }
    
    public void Leave(int playerId)
    {
        if (_players.ContainsKey(playerId))
        {
            // 주변에 퇴장 알림
            BroadcastExcept(playerId, new S_OnPlayerLeft { PlayerId = playerId });
                
            if (_players.Remove(playerId, out Player player))
            {
                Cell cell = GetCell(player.X, player.Z);
                cell.Remove(player);
                    
                if(player.Session != null) player.Session.MyPlayer = null;
                LogManager.Info($"Player {player.Id} left.");
            }
        }
    }

    public void HandleMove(Player player, C_Move packet)
    {
        if (player == null) return;
        
        // 1. East (오른쪽) 이동 판정
        // 500(경계) + 10(버퍼) = 510을 넘어야 이동!
        if (packet.X > MapSizeX + HandoverThreshold)
        {
            if (ConfigManager.Config.NeighborZones.TryGetValue("East", out ServerConfig.ZoneInfo neighbor))
            {
                // Console.WriteLine($"[Move] {player.Name}({player.Id}): X: {packet.X}, Y: {packet.Y}, Z: {packet.Z}, MapSizeX: {MapSizeX}, HandoverThreshold: {HandoverThreshold}, SpawnSafetyMargin: {SpawnSafetyMargin}");
                // 다음 서버의 서쪽 끝(-500) + 안전 거리(20) = -480 위치로 보냄
                float nextX = -MapSizeX + SpawnSafetyMargin;
                InitiateHandover(player, neighbor, nextX, player.Z);
                return;
            }
        }
        // 2. West (왼쪽) 이동 판정
        // -500(경계) - 10(버퍼) = -510을 넘어야 이동!
        else if (packet.X < -MapSizeX - HandoverThreshold)
        {
            if (ConfigManager.Config.NeighborZones.TryGetValue("West", out ServerConfig.ZoneInfo neighbor))
            {
                // 다음 서버의 동쪽 끝(500) - 안전 거리(20) = 480 위치로 보냄
                float nextX = MapSizeX - SpawnSafetyMargin;
                InitiateHandover(player, neighbor, nextX, player.Z);
                return;
            }
        }
        
        Cell oldCell = GetCell(player.X, player.Z);

        // 1. 서버 메모리 상의 좌표 업데이트
        player.X = packet.X;
        player.Y = packet.Y;
        player.Z = packet.Z;
        
        Cell newCell = GetCell(player.X, player.Z);

        if (oldCell != newCell)
        {
            oldCell.Remove(player);
            newCell.Add(player);
            // Console.WriteLine($"Player {player.Id} moved to New Cell {GetCellIndex(player.X, player.Z)}");
        }

        S_Move moveRes = new()
        {
            PlayerId = player.Id,
            X = player.X,
            Y = player.Y,
            Z = player.Z
        };
        
        BroadcastCell(player.Id, moveRes);
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
            
            // Redis로 발행 (내 서버 포함, 모든 서버가 듣게 됨)
            RedisManager.Publish("GlobalChat", pubMsg);
            
            LogManager.Info($"[Global Chat] {pubMsg}");
        }
        else
        {
            S_Chat chatRes = new()
            {
                PlayerId = player.Id,
                Msg = packet.Msg
            };
        
            // Console.WriteLine($"[Chat] {player.Name}({player.Id}): {packet.Msg}");
        
            BroadcastCell(player.Id, chatRes);   
        }
    }
    
    private void InitiateHandover(Player player, ServerConfig.ZoneInfo targetZone, float nextX, float nextZ)
    {
        // 1. 중복 처리 방지 (간단 체크)
        if (player.Session == null) return;

        // Console.WriteLine($"[Handover] {player.Name} leaving to Zone {targetZone.ZoneId} ({targetZone.Port})");

        // 2. 이관 토큰 생성
        string token = Guid.NewGuid().ToString();

        // 3. 상태 저장 (Redis)
        PlayerState state = player.GetState(token);
        state.X = nextX; // 다음 서버에서의 시작 위치 설정
        state.Z = nextZ;
    
        RedisManager.SavePlayerState(player.Session.AuthToken, state);

        // 4. 클라에게 명령 전송
        S_TransferReq transferPacket = new S_TransferReq()
        {
            TargetIp = targetZone.IpAddress, // Config에서 읽은 값 (127.0.0.1)
            TargetPort = targetZone.Port,    // Config에서 읽은 값 (12346 등)
            TransferToken = token
        };

        byte[] data = PacketManager.Instance.Serialize(transferPacket);
        player.Session.Send(data);

        // 5. 서버에서 유저 제거 (즉시 퇴장 처리)
        // LeaveGame을 호출하면 브로드캐스트도 됨
        Leave(player.Id);
    }
    
    private void OnRecvGlobalChat(string msg)
    {
        var job = JobPool<GlobalChatJob>.Get();
        job.RawMessage = msg;
        
        Push(job);
    }
}