using UnityEngine;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    [Header("AI Settings")]
    public int attackRange = 1; // Ближний бой
    public int damage = 15;
    public int movesPerTurn = 3; // Скорость паука

    private TacticalSystem _system;
    private UnitStats _stats;

    public void Init(TacticalSystem system, UnitStats stats)
    {
        _system = system;
        _stats = stats;
    }

    // Главная функция: "Придумай, что делать"
    // Возвращает список команд, которые враг хочет выполнить
    public List<ActionCommand> PlanTurn(GridPos myPos, GridPos playerPos, HashSet<GridPos> walls)
    {
        List<ActionCommand> aiQueue = new List<ActionCommand>();

        // 1. Проверка дистанции
        float dist = Vector3.Distance(new Vector3(myPos.x, 0, myPos.y), new Vector3(playerPos.x, 0, playerPos.y));

        // Если игрок рядом - АТАКУЕМ
        // (Упрощение: считаем диагонали за 1.5 клетки или просто по Манхэттену)
        bool inRange = Mathf.Abs(myPos.x - playerPos.x) <= attackRange && Mathf.Abs(myPos.y - playerPos.y) <= attackRange;

        if (inRange)
        {
            // Кусаем игрока
            aiQueue.Add(new ActionCommand("ATTACK", playerPos));
            Debug.Log("AI: Player in range! Attacking.");
        }
        else
        {
            // 2. Если далеко - ИДЕМ К ИГРОКУ
            // Находим путь до игрока
            List<GridPos> path = Pathfinding.FindPath(myPos, playerPos, walls, _system.width, _system.height);

            if (path != null && path.Count > 0)
            {
                // Враг не может встать ПРЯМО на игрока, поэтому идем до "соседней" клетки
                // Обрезаем путь до последних movesPerTurn шагов

                // Удаляем последнюю точку (это сам игрок, на него встать нельзя)
                if (path.Count > 0 && path[path.Count - 1] == playerPos)
                {
                    path.RemoveAt(path.Count - 1);
                }

                if (path.Count > 0)
                {
                    // Ограничиваем дальность хода
                    int steps = Mathf.Min(path.Count, movesPerTurn);
                    List<GridPos> movePath = path.GetRange(0, steps);
                    GridPos dest = movePath[movePath.Count - 1];

                    aiQueue.Add(new ActionCommand("MOVE", dest, movePath));
                    Debug.Log($"AI: Moving to {dest.x},{dest.y}");

                    // Если после движения мы окажемся рядом - добавим атаку в очередь (Combo!)
                    // (Опционально, для начала пусть просто ходит)
                }
            }
        }

        return aiQueue;
    }
}