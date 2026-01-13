using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("Dungeon Settings")]
    public int minRoomSize = 4;
    public int maxRoomSize = 8;
    public int maxRooms = 8;

    public class DungeonData
    {
        public HashSet<GridPos> Floors = new HashSet<GridPos>();
        public HashSet<GridPos> Walls = new HashSet<GridPos>();
        public HashSet<GridPos> DoorSpots = new HashSet<GridPos>(); // Места для дверей
        public GridPos StartPos;
        public List<GridPos> EnemySpawnPoints = new List<GridPos>();
    }

    public DungeonData Generate(int width, int height)
    {
        DungeonData data = new DungeonData();
        List<RectInt> rooms = new List<RectInt>();

        // 1. ГЕНЕРАЦИЯ КОМНАТ
        for (int i = 0; i < maxRooms * 2; i++)
        { // Делаем больше попыток
            int w = Random.Range(minRoomSize, maxRoomSize);
            int h = Random.Range(minRoomSize, maxRoomSize);
            int x = Random.Range(1, width - w - 1);
            int y = Random.Range(1, height - h - 1);

            RectInt newRoom = new RectInt(x, y, w, h);
            bool overlaps = false;
            foreach (var r in rooms)
            {
                // Отступ в 2 клетки, чтобы комнаты не слипались
                RectInt expanded = new RectInt(r.x - 2, r.y - 2, r.width + 4, r.height + 4);
                if (expanded.Overlaps(newRoom)) { overlaps = true; break; }
            }

            if (!overlaps)
            {
                rooms.Add(newRoom);
                // Заполняем пол комнаты
                for (int rx = newRoom.x; rx < newRoom.xMax; rx++)
                {
                    for (int ry = newRoom.y; ry < newRoom.yMax; ry++)
                    {
                        data.Floors.Add(new GridPos(rx, ry));
                    }
                }
                if (rooms.Count > 1)
                    data.EnemySpawnPoints.Add(new GridPos((int)newRoom.center.x, (int)newRoom.center.y));
            }
            if (rooms.Count >= maxRooms) break;
        }

        // 2. ГЕНЕРАЦИЯ КОРИДОРОВ (L-Shape)
        for (int i = 1; i < rooms.Count; i++)
        {
            Vector2Int prev = Vector2Int.RoundToInt(rooms[i - 1].center);
            Vector2Int curr = Vector2Int.RoundToInt(rooms[i].center);

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

        // 3. ПОИСК МЕСТ ДЛЯ ДВЕРЕЙ
        // Дверь ставится там, где коридор (пол) граничит с комнатой, но по бокам стены
        // Упрощенно: Если тайл пола имеет ровно 2 соседа-пола (коридор) и входит в комнату?
        // Лучше так: Мы просто не ставим двери пока, а гарантируем проходы. 
        // Но если хочешь двери:
        /*
        foreach (var floor in data.Floors) {
            // Простая логика: если это узкий проход (стены с двух сторон) - можно ставить дверь
            // Но чтобы не заблокировать всё, пока оставим пустыми проходы.
        }
        */

        // 4. ГЕНЕРАЦИЯ СТЕН ВОКРУГ ПОЛА
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridPos p = new GridPos(x, y);
                if (!data.Floors.Contains(p))
                {
                    // Если рядом есть пол — значит это стена
                    if (HasFloorNeighbor(p, data.Floors))
                    {
                        data.Walls.Add(p);
                    }
                }
            }
        }

        // Старт
        if (rooms.Count > 0)
        {
            Vector2Int start = Vector2Int.RoundToInt(rooms[0].center);
            data.StartPos = new GridPos(start.x, start.y);
        }
        else
        {
            // Фолбек если комнаты не создались
            data.StartPos = new GridPos(width / 2, height / 2);
            data.Floors.Add(data.StartPos);
        }

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
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (Mathf.Abs(x) == Mathf.Abs(y)) continue; // Проверяем только Крест (не диагонали)
                if (floors.Contains(new GridPos(p.x + x, p.y + y))) return true;
            }
        }
        return false;
    }
}