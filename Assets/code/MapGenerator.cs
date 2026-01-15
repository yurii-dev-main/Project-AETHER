using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("Dungeon Settings")]
    public int minRoomSize = 4;
    public int maxRoomSize = 8;
    public int maxRooms = 3; // Ставим мало, чтобы тестить (комната, коридор, комната)

    // Структура пары "Замок - Ключ"
    public struct PuzzlePair
    {
        public GridPos SwitchPos;
        public GridPos DoorPos;
    }

    public class DungeonData
    {
        public HashSet<GridPos> Floors = new HashSet<GridPos>();
        public HashSet<GridPos> Walls = new HashSet<GridPos>();
        public List<PuzzlePair> Puzzles = new List<PuzzlePair>(); // Список пар
        public GridPos StartPos;
        public List<GridPos> EnemySpawnPoints = new List<GridPos>();
    }

    public DungeonData Generate(int width, int height)
    {
        DungeonData data = new DungeonData();
        List<RectInt> rooms = new List<RectInt>();

        // 1. ГЕНЕРАЦИЯ КОМНАТ
        for (int i = 0; i < maxRooms * 3; i++)
        {
            int w = Random.Range(minRoomSize, maxRoomSize);
            int h = Random.Range(minRoomSize, maxRoomSize);
            int x = Random.Range(2, width - w - 2);
            int y = Random.Range(2, height - h - 2);

            RectInt newRoom = new RectInt(x, y, w, h);
            bool overlaps = false;
            foreach (var r in rooms)
            {
                RectInt expanded = new RectInt(r.x - 2, r.y - 2, r.width + 4, r.height + 4);
                if (expanded.Overlaps(newRoom)) { overlaps = true; break; }
            }

            if (!overlaps)
            {
                rooms.Add(newRoom);
                // Пол
                for (int rx = newRoom.x; rx < newRoom.xMax; rx++)
                {
                    for (int ry = newRoom.y; ry < newRoom.yMax; ry++)
                    {
                        data.Floors.Add(new GridPos(rx, ry));
                    }
                }
                // Спавн врагов (во всех комнатах кроме первой)
                if (rooms.Count > 1)
                    data.EnemySpawnPoints.Add(new GridPos((int)newRoom.center.x, (int)newRoom.center.y));
            }
            if (rooms.Count >= maxRooms) break;
        }

        // 2. СОЕДИНЕНИЕ И СОЗДАНИЕ ПАЗЛОВ
        for (int i = 1; i < rooms.Count; i++)
        {
            RectInt prevRoom = rooms[i - 1];
            RectInt currRoom = rooms[i];

            Vector2Int prevCenter = Vector2Int.RoundToInt(prevRoom.center);
            Vector2Int currCenter = Vector2Int.RoundToInt(currRoom.center);

            // Рисуем коридор
            List<GridPos> corridorTiles = new List<GridPos>();

            // L-Shape коридор
            if (Random.value < 0.5f)
            {
                AddHCorridor(data, prevCenter.x, currCenter.x, prevCenter.y, corridorTiles);
                AddVCorridor(data, prevCenter.y, currCenter.y, currCenter.x, corridorTiles);
            }
            else
            {
                AddVCorridor(data, prevCenter.y, currCenter.y, prevCenter.x, corridorTiles);
                AddHCorridor(data, prevCenter.x, currCenter.x, currCenter.y, corridorTiles);
            }

            // --- ЛОГИКА ПАЗЛА (ДВЕРЬ + РЫЧАГ) ---
            // 1. Ищем место для Двери: Первая точка коридора, которая НЕ внутри предыдущей комнаты
            GridPos doorSpot = new GridPos(-1, -1);
            foreach (var tile in corridorTiles)
            {
                // Если тайл коридора не является частью пола комнаты A
                // (Мы проверяем границы rect, т.к. в data.Floors они уже слились)
                if (!prevRoom.Contains(new Vector2Int(tile.x, tile.y)))
                {
                    doorSpot = tile;
                    break; // Нашли выход из комнаты! Ставим дверь тут.
                }
            }

            // 2. Ищем место для Рычага: Случайная точка ВНУТРИ предыдущей комнаты
            GridPos switchSpot = new GridPos(
                Random.Range(prevRoom.x, prevRoom.xMax),
                Random.Range(prevRoom.y, prevRoom.yMax)
            );

            // Сохраняем пару, если нашли валидное место для двери
            if (doorSpot.x != -1)
            {
                PuzzlePair pair = new PuzzlePair { DoorPos = doorSpot, SwitchPos = switchSpot };
                data.Puzzles.Add(pair);
            }
        }

        // 3. СТЕНЫ ВОКРУГ
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridPos p = new GridPos(x, y);
                if (!data.Floors.Contains(p))
                {
                    if (HasFloorNeighbor(p, data.Floors)) data.Walls.Add(p);
                }
            }
        }

        if (rooms.Count > 0)
        {
            Vector2Int start = Vector2Int.RoundToInt(rooms[0].center);
            data.StartPos = new GridPos(start.x, start.y);
        }
        else
        {
            data.StartPos = new GridPos(width / 2, height / 2);
            data.Floors.Add(data.StartPos);
        }

        return data;
    }

    void AddHCorridor(DungeonData data, int x1, int x2, int y, List<GridPos> trackList)
    {
        for (int x = Mathf.Min(x1, x2); x <= Mathf.Max(x1, x2); x++)
        {
            GridPos p = new GridPos(x, y);
            data.Floors.Add(p);
            trackList.Add(p);
        }
    }

    void AddVCorridor(DungeonData data, int y1, int y2, int x, List<GridPos> trackList)
    {
        for (int y = Mathf.Min(y1, y2); y <= Mathf.Max(y1, y2); y++)
        {
            GridPos p = new GridPos(x, y);
            data.Floors.Add(p);
            trackList.Add(p);
        }
    }

    bool HasFloorNeighbor(GridPos p, HashSet<GridPos> floors)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (Mathf.Abs(x) == Mathf.Abs(y)) continue;
                if (floors.Contains(new GridPos(p.x + x, p.y + y))) return true;
            }
        }
        return false;
    }
}