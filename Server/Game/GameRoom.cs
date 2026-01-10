using Common;
using Common.Packet;
using LiteNetLib;
using Server.Core;
using Server.Game.Room;
using Server.Packet;
using Server.Utils;

namespace Server.Game;

public class GameRoom : IJobQueue
{
    // 편의상 일단 1개
    public static GameRoom Instance { get; } = new();
    
    private JobQueue _jobQueue = new();
    
    private Dictionary<int, Player> _players = new();
    
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

    public void Broadcast<T>(float x, float z, T packet) where T : BasePacket
    {
        byte[] data = PacketManager.Instance.Serialize(packet);
        
        List<Cell> zones = GetNearCells(x, z);

        foreach (Cell cell in zones)
        {
            foreach (Player player in cell.Players)
            {
                // 패킷 구분 (나중에 수정)
                if (packet.Id == PacketId.S_Move)
                {
                    player.Session.SendUDP(data, NetConfig.Ch_UDP, DeliveryMethod.Sequenced);
                }
                else if(packet.Id == PacketId.S_Chat)
                {
                    player.Session.SendUDP(data, NetConfig.Ch_RUDP1, DeliveryMethod.ReliableOrdered);
                }
            }
        }
    }
    
    public void Push(Action job)
    {
        _jobQueue.Push(job);
    }

    public void Update()
    {
        _jobQueue.Flush();
    }

    public void Enter(Player newPlayer)
    {
        if(newPlayer == null) return;
        
        if(_players.ContainsKey(newPlayer.Id)) return;
        
        _players.Add(newPlayer.Id, newPlayer);
        newPlayer.Session.MyPlayer = newPlayer;
        
        Cell cell = GetCell(newPlayer.X, newPlayer.Z);
        cell.Add(newPlayer);
        
        LogManager.Info($"Player {newPlayer.Id} entered the game room");
    }

    public void Leave(int playerId)
    {
        if (_players.Remove(playerId, out Player player))
        {
            Cell cell = GetCell(player.X, player.Z);
            cell.Remove(player);
            
            player.Session.MyPlayer = null;
            LogManager.Info($"Player {player.Id} left the game room");
        }
    }

    public void HandleMove(Player player, C_Move packet)
    {
        if (player == null) return;
        
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
            Console.WriteLine($"Player {player.Id} moved to New Cell {GetCellIndex(player.X, player.Z)}");
        }

        S_Move moveRes = new()
        {
            PlayerId = player.Id,
            X = player.X,
            Y = player.Y,
            Z = player.Z
        };
        
        Broadcast(player.X, player.Z, moveRes);
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
        
        S_Chat chatRes = new()
        {
            PlayerId = player.Id,
            Msg = packet.Msg
        };
        
        Console.WriteLine($"[Chat] {player.Name}({player.Id}): {packet.Msg}");
        
        Broadcast(player.X, player.Z, chatRes);
    }
}