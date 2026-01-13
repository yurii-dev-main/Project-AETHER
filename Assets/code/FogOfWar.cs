using UnityEngine;
using System.Collections.Generic;

public class FogOfWar : MonoBehaviour
{
    [Header("Settings")]
    public GameObject fogPrefab; // Черный квадрат (Quad)
    public int viewRadius = 6;
    public bool debugRevealMap = false; // Твой ТУМБЛЕР

    private Dictionary<GridPos, GameObject> _fogTiles = new Dictionary<GridPos, GameObject>();
    private HashSet<GridPos> _exploredTiles = new HashSet<GridPos>();

    // Ссылка на тактическую систему для проверки стен (Line of Sight)
    private TacticalSystem _tacticalSystem;

    public void InitFog(int w, int h, float tileSize, TacticalSystem system)
    {
        _tacticalSystem = system;

        // Очистка старого
        foreach (var f in _fogTiles.Values) Destroy(f);
        _fogTiles.Clear();
        _exploredTiles.Clear();

        // Создаем пелену над всей картой
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                GridPos p = new GridPos(x, y);

                // Спавним чуть выше уровня пола (Y=2), чтобы перекрывать всё
                Vector3 pos = new Vector3(x * tileSize, 2.5f, y * tileSize);

                // Поворачиваем на 90 градусов (чтобы лежал плашмя, если это Quad)
                GameObject fog = Instantiate(fogPrefab, pos, Quaternion.Euler(90, 0, 0));
                fog.transform.parent = transform;

                // Красим в "Неизведанное" (Черный, непрозрачный)
                SetFogColor(fog, Color.black);

                _fogTiles.Add(p, fog);
            }
        }
    }

    public void UpdateFog(GridPos heroPos)
    {
        if (debugRevealMap)
        {
            RevealAll();
            return;
        }

        // 1. Сначала затемняем всё, что было видимым (превращаем в "память")
        foreach (var p in _exploredTiles)
        {
            if (_fogTiles.ContainsKey(p))
            {
                // Серый полупрозрачный (Видим карту, не видим врагов)
                // Alpha 0.5f - полумрак
                SetFogColor(_fogTiles[p], new Color(0, 0, 0, 0.6f));
                _fogTiles[p].SetActive(true);
            }
        }

        // 2. Вычисляем поле зрения (Field of View)
        // Используем простой Raycast от героя ко всем клеткам в радиусе
        List<GridPos> visibleTiles = CalculateFOV(heroPos);

        // 3. Открываем видимое
        foreach (var p in visibleTiles)
        {
            if (_fogTiles.ContainsKey(p))
            {
                _fogTiles[p].SetActive(false); // Полностью убираем туман
                _exploredTiles.Add(p); // Запоминаем, что мы тут были
            }
        }

        // 4. Скрываем/Показываем врагов
        _tacticalSystem.UpdateEnemyVisibility(visibleTiles);
    }

    List<GridPos> CalculateFOV(GridPos center)
    {
        List<GridPos> visible = new List<GridPos>();

        // Простой перебор в квадрате радиуса
        for (int x = -viewRadius; x <= viewRadius; x++)
        {
            for (int y = -viewRadius; y <= viewRadius; y++)
            {
                // Округляем до круга (опционально, но красивее)
                if (x * x + y * y > viewRadius * viewRadius) continue;

                GridPos target = new GridPos(center.x + x, center.y + y);

                // Проверяем Line of Sight через систему тактики (она знает где стены)
                if (_tacticalSystem.HasLineOfSightLogic(center, target))
                {
                    visible.Add(target);
                }
            }
        }
        return visible;
    }

    void RevealAll()
    {
        foreach (var kvp in _fogTiles)
        {
            kvp.Value.SetActive(false);
        }
        // Сказать тактике показать всех врагов
        _tacticalSystem.RevealAllEnemies();
    }

    void SetFogColor(GameObject obj, Color c)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r) r.material.color = c;
    }

    // Метод для тумблера в UI
    public void ToggleDebugMap(bool show)
    {
        debugRevealMap = show;
        // Нужно принудительно обновить, передав текущую позицию героя.
        // Но так как у нас нет ссылки на героя тут, просто ждем следующего апдейта хода
    }
}
