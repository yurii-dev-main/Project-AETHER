using UnityEngine;
using System.Collections.Generic;

// Архитипы поведения
public enum AIArchetype
{
    AggressiveMelee, // Зомби: Вижу -> Иду -> Бью
    RangedKiter,     // Лучник: Держу дистанцию, Стреляю
    Coward,          // Гоблин: Если мало ХП -> Убегаю
    StationaryTurret // Турель: Стою, стреляю
}

public class EnemyAI : MonoBehaviour
{
    [Header("Configuration")]
    public AIArchetype archetype = AIArchetype.AggressiveMelee;

    [Header("Stats")]
    public int damage = 15;
    public int attackRange = 1;      // 1 для ближнего, 5+ для дальнего
    public int movesPerTurn = 3;     // Скорость
    public int idealRange = 4;       // Для лучников: дистанция, которую они стараются держать
    [Range(0f, 1f)] public float fleeThreshold = 0.3f; // 30% ХП = Паника (для Coward)

    private TacticalSystem _system;
    private UnitStats _stats;

    public void Init(TacticalSystem system, UnitStats stats)
    {
        _system = system;
        _stats = stats;
    }

    // ГЛАВНЫЙ МОЗГ
    public List<ActionCommand> PlanTurn(GridPos myPos, GridPos playerPos, HashSet<GridPos> walls)
    {
        List<ActionCommand> aiQueue = new List<ActionCommand>();

        // 0. Проверка смерти (на всякий случай)
        if (_stats.currentHP <= 0) return aiQueue;

        // 1. ПРОВЕРКА ПАНИКИ (Только для Трусов)
        float hpPercent = (float)_stats.currentHP / (float)_stats.maxHP;
        if (archetype == AIArchetype.Coward && hpPercent <= fleeThreshold)
        {
            // Бежим ОТ игрока
            return RunAwayLogic(myPos, playerPos, walls);
        }

        // 2. ПРОВЕРКА АТАКИ (Могу ли я ударить прямо сейчас?)
        bool hasLOS = _system.HasLineOfSightLogic(myPos, playerPos);
        float dist = Vector3.Distance(new Vector3(myPos.x, 0, myPos.y), new Vector3(playerPos.x, 0, playerPos.y));

        // Для Рендж-юнитов обязательно нужен LOS (Линия видимости)
        // Для Мили-юнитов (range=1) LOS обычно не критичен, если они в упоре, но проверим
        if (dist <= attackRange && hasLOS)
        {
            // Я уже на позиции - АТАКУЮ
            aiQueue.Add(new ActionCommand("ATTACK", playerPos));

            // Если я Лучник и игрок СЛИШКОМ близко -> Отбегаю после выстрела (Kiting)
            if (archetype == AIArchetype.RangedKiter && dist < 2)
            {
                // Пытаемся отступить на 1 шаг назад
                GridPos retreatPos = FindFleeTile(myPos, playerPos, walls, 1);
                if (retreatPos != myPos)
                {
                    List<GridPos> path = new List<GridPos> { retreatPos }; // Короткий путь
                    aiQueue.Add(new ActionCommand("MOVE", retreatPos, path));
                }
            }
            return aiQueue;
        }

        // 3. ПЕРЕМЕЩЕНИЕ (Если не могу атаковать)
        if (archetype == AIArchetype.StationaryTurret)
        {
            return aiQueue; // Турель не ходит
        }

        // Логика для Лучника: Идти не к игроку, а на "Идеальную дистанцию"
        if (archetype == AIArchetype.RangedKiter)
        {
            // Если мы далеко или не видим цель -> Идем ближе
            if (!hasLOS || dist > idealRange)
            {
                return MoveTowardsLogic(myPos, playerPos, walls);
            }
            // Если мы слишком близко -> Отходим
            else if (dist < idealRange - 1)
            {
                return RunAwayLogic(myPos, playerPos, walls);
            }
            // Иначе стоим на месте (хорошая позиция), пропускаем ход
            return aiQueue;
        }

        // Логика для Мили (Зомби/Гоблин): Просто идти к лицу
        return MoveTowardsLogic(myPos, playerPos, walls);
    }

    // --- ЛОГИКА ДВИЖЕНИЯ: К ЦЕЛИ ---
    List<ActionCommand> MoveTowardsLogic(GridPos start, GridPos target, HashSet<GridPos> walls)
    {
        List<ActionCommand> cmds = new List<ActionCommand>();

        // Ищем путь до игрока
        List<GridPos> path = Pathfinding.FindPath(start, target, walls, _system.width, _system.height);

        if (path != null && path.Count > 0)
        {
            // Убираем последнюю точку (сам игрок), на него встать нельзя
            if (path[path.Count - 1] == target) path.RemoveAt(path.Count - 1);

            if (path.Count > 0)
            {
                // Обрезаем по скорости
                int steps = Mathf.Min(path.Count, movesPerTurn);
                List<GridPos> movePath = path.GetRange(0, steps);
                GridPos dest = movePath[movePath.Count - 1];

                cmds.Add(new ActionCommand("MOVE", dest, movePath));

                // ЕСЛИ ПОСЛЕ ХОДА Я БУДУ РЯДОМ - ДОБАВИТЬ АТАКУ
                float distAfterMove = Vector3.Distance(new Vector3(dest.x, 0, dest.y), new Vector3(target.x, 0, target.y));
                if (distAfterMove <= attackRange)
                {
                    // Проверим LOS с новой точки (упрощенно считаем что будет)
                    cmds.Add(new ActionCommand("ATTACK", target));
                }
            }
        }
        return cmds;
    }

    // --- ЛОГИКА ДВИЖЕНИЯ: ОТ ЦЕЛИ (БЕГСТВО) ---
    List<ActionCommand> RunAwayLogic(GridPos start, GridPos danger, HashSet<GridPos> walls)
    {
        List<ActionCommand> cmds = new List<ActionCommand>();

        // Ищем клетку подальше
        GridPos fleeTarget = FindFleeTile(start, danger, walls, movesPerTurn);

        if (fleeTarget != start)
        {
            // Строим путь туда
            List<GridPos> path = Pathfinding.FindPath(start, fleeTarget, walls, _system.width, _system.height);
            if (path != null && path.Count > 0)
            {
                cmds.Add(new ActionCommand("MOVE", fleeTarget, path));
            }
        }
        return cmds;
    }

    // Найти валидную клетку в противоположной стороне
    GridPos FindFleeTile(GridPos start, GridPos danger, HashSet<GridPos> walls, int range)
    {
        Vector3 dir = (new Vector3(start.x, 0, start.y) - new Vector3(danger.x, 0, danger.y)).normalized;

        // Пробуем несколько точек в направлении "ОТ ВРАГА"
        // 1. Идеальная точка (прямо назад)
        GridPos bestPos = start;
        float bestDist = 0;

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                GridPos p = new GridPos(start.x + x, start.y + y);
                // Пропускаем стены и выходы за карту
                if (!_system.IsValidForMove(p, walls)) continue;

                // Считаем, насколько эта точка далеко от опасности
                float d = Vector3.Distance(new Vector3(p.x, 0, p.y), new Vector3(danger.x, 0, danger.y));

                // Если точка дальше чем текущая лучшая
                if (d > bestDist)
                {
                    // И если до нее РЕАЛЬНО дойти (есть путь)
                    if (Pathfinding.FindPath(start, p, walls, _system.width, _system.height) != null)
                    {
                        bestDist = d;
                        bestPos = p;
                    }
                }
            }
        }
        return bestPos;
    }
}