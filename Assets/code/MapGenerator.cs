using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    // Настройки генерации
    [Header("Dungeon Settings")]
    public int minRoomSize = 3;
    public int maxRoomSize = 7;
    public int maxRooms = 10;

    // Структура для возврата данных
    public class DungeonData
    {
        public HashSet<GridPos> Floors = new HashSet<GridPos>();
        public HashSet<GridPos> Walls = new HashSet<GridPos>();
        public GridPos StartPos;
        public List<GridPos> EnemySpawnPoints = new List<GridPos>();
    }

    public DungeonData Generate(int width, int height)
    {
        DungeonData data = new DungeonData();
        List<RectInt> rooms = new List<RectInt>();

        // 1. Пытаемся разместить комнаты
        for (int i = 0; i < maxRooms; i++)
        {
            int w = Random.Range(minRoomSize, maxRoomSize);
            int h = Random.Range(minRoomSize, maxRoomSize);
            int x = Random.Range(1, width - w - 1);
            int y = Random.Range(1, height - h - 1);

            RectInt newRoom = new RectInt(x, y, w, h);

            // Проверка на наложение
            bool overlaps = false;
            foreach (var r in rooms)
            {
                // Делаем отступ в 1 клетку, чтобы комнаты не слипались
                RectInt expanded = new RectInt(r.x - 1, r.y - 1, r.width + 2, r.height + 2);
                if (expanded.Overlaps(newRoom))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                rooms.Add(newRoom);
                // Добавляем пол
                for (int rx = newRoom.x; rx < newRoom.xMax; rx++)
                {
                    for (int ry = newRoom.y; ry < newRoom.yMax; ry++)
                    {
                        data.Floors.Add(new GridPos(rx, ry));
                    }
                }

                // Центр комнаты - потенциальный спавн врага (кроме первой)
                if (rooms.Count > 1)
                {
                    data.EnemySpawnPoints.Add(new GridPos(newRoom.center.x, newRoom.center.y));
                }
            }
        }

        if (rooms.Count == 0) return null; // Ошибка генерации

        // 2. Соединяем комнаты коридорами
        // Идем от центра предыдущей к центру следующей
        for (int i = 1; i < rooms.Count; i++)
        {
            Vector2Int prev = Vector2Int.RoundToInt(rooms[i - 1].center);
            Vector2Int curr = Vector2Int.RoundToInt(rooms[i].center);

            // Случайно решаем: сначала по горизонтали или по вертикали (L-shape)
            if (Random.value < 0.5f)
            {
                CreateHCorridor(data, prev.x, curr.x, prev.y);
                CreateVCorridor(data, prev.y, curr.y, curr.x);
            }
            else
            {
                CreateVCorridor(data, prev.y, curr.y, prev.x);
                CreateHCorridor(data, prev.x, curr.x, curr.y);
            }
        }

        // 3. Расставляем стены
        // Проходим по всей карте. Если клетка НЕ пол, но имеет соседа-пол -> это стена.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridPos p = new GridPos(x, y);
                if (!data.Floors.Contains(p))
                {
                    // Проверяем соседей
                    if (HasFloorNeighbor(p, data.Floors))
                    {
                        data.Walls.Add(p);
                    }
                }
            }
        }

        // Старт в центре первой комнаты
        Vector2Int start = Vector2Int.RoundToInt(rooms[0].center);
        data.StartPos = new GridPos(start.x, start.y);

        return data;
    }

    void CreateHCorridor(DungeonData data, int x1, int x2, int y)
    {
        for (int x = Mathf.Min(x1, x2); x <= Mathf.Max(x1, x2); x++)
        {
            data.Floors.Add(new GridPos(x, y));
        }
    }

    void CreateVCorridor(DungeonData data, int y1, int y2, int x)
    {
        for (int y = Mathf.Min(y1, y2); y <= Mathf.Max(y1, y2); y++)
        {
            data.Floors.Add(new GridPos(x, y));
        }
    }

    bool HasFloorNeighbor(GridPos p, HashSet<GridPos> floors)
    {
        // Проверяем 4 направления + диагонали (чтобы углы были красивыми)
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                if (floors.Contains(new GridPos(p.x + x, p.y + y))) return true;
            }
        }
        return false;
    }
}