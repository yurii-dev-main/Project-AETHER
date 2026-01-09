using UnityEngine;
using System.Collections.Generic;

// Этот класс будет статическим помощником (Toolbox)
public static class Pathfinding
{

    // Класс узла для алгоритма
    private class Node
    {
        public GridPos pos;
        public Node parent;
        public int gCost; // Расстояние от старта
        public int hCost; // Примерное расстояние до финиша (эвристика)
        public int FCost => gCost + hCost;

        public Node(GridPos p) { pos = p; }
    }

    // Главный метод: Найти путь от Start до End
    // walls - список координат, где стоят стены
    // width, height - размеры карты
    public static List<GridPos> FindPath(GridPos start, GridPos end, HashSet<GridPos> walls, int width, int height)
    {

        // 1. Проверка на дурака: Если цель в стене или за картой - пути нет
        if (walls.Contains(end) || !IsValid(end, width, height)) return null;

        List<Node> openList = new List<Node>();  // Клетки, которые надо проверить
        HashSet<GridPos> closedList = new HashSet<GridPos>(); // Клетки, которые уже проверили

        Node startNode = new Node(start);
        Node endNode = new Node(end);

        openList.Add(startNode);

        while (openList.Count > 0)
        {
            // Ищем узел с самой низкой ценой пути (F)
            Node currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].FCost < currentNode.FCost ||
                   (openList[i].FCost == currentNode.FCost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode.pos);

            // Если дошли до финиша
            if (currentNode.pos == endNode.pos)
            {
                return RetracePath(startNode, currentNode);
            }

            // Проверяем соседей (Вверх, Вниз, Влево, Вправо)
            foreach (GridPos neighborPos in GetNeighbors(currentNode.pos, width, height))
            {
                if (walls.Contains(neighborPos) || closedList.Contains(neighborPos))
                    continue;

                // Цена перехода к соседу всегда 1
                int newMovementCostToNeighbor = currentNode.gCost + 1;

                Node neighborNode = openList.Find(n => n.pos == neighborPos);
                if (neighborNode == null || newMovementCostToNeighbor < neighborNode.gCost)
                {
                    if (neighborNode == null)
                    {
                        neighborNode = new Node(neighborPos);
                        openList.Add(neighborNode);
                    }

                    neighborNode.gCost = newMovementCostToNeighbor;
                    neighborNode.hCost = GetDistance(neighborPos, end);
                    neighborNode.parent = currentNode;
                }
            }
        }

        return null; // Путь не найден
    }

    // Восстанавливаем путь от финиша к старту по родителям
    private static List<GridPos> RetracePath(Node startNode, Node endNode)
    {
        List<GridPos> path = new List<GridPos>();
        Node currentNode = endNode;

        while (currentNode.pos != startNode.pos)
        {
            path.Add(currentNode.pos);
            currentNode = currentNode.parent;
        }
        path.Reverse(); // Разворачиваем, чтобы было от Старта к Финишу
        return path;
    }

    // Расстояние Манхэттена (для квадратной сетки без диагоналей)
    private static int GetDistance(GridPos a, GridPos b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // Получить соседей (Крестом)
    private static List<GridPos> GetNeighbors(GridPos pos, int width, int height)
    {
        List<GridPos> neighbors = new List<GridPos>();

        GridPos[] directions = {
            new GridPos(0, 1),  // Up
            new GridPos(0, -1), // Down
            new GridPos(1, 0),  // Right
            new GridPos(-1, 0)  // Left
        };

        foreach (var dir in directions)
        {
            GridPos checkPos = new GridPos(pos.x + dir.x, pos.y + dir.y);
            if (IsValid(checkPos, width, height))
            {
                neighbors.Add(checkPos);
            }
        }
        return neighbors;
    }

    private static bool IsValid(GridPos p, int w, int h)
    {
        return p.x >= 0 && p.x < w && p.y >= 0 && p.y < h;
    }
}