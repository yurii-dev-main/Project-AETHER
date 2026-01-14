using UnityEngine;
using System.Collections.Generic;

public class FogOfWar : MonoBehaviour
{
    [Header("Settings")]
    public Material fogMaterial; // Материал (Transparent)
    public int viewRadius = 6;
    public bool debugRevealMap = false;

    [Header("Visuals")]
    public float fogHeight = 1.0f; // Высота плоскости тумана

    // ТЕКСТУРА ТУМАНА
    private Texture2D _fogTexture;
    private Color[] _fogColors; // Массив пикселей
    private GameObject _fogPlane; // Плоскость

    // ЛОГИКА
    private HashSet<GridPos> _exploredTiles = new HashSet<GridPos>();
    private TacticalSystem _tacticalSystem;
    private int _width, _height;
    private float _tileSize = 1.1f; // Запоминаем размер тайла

    // Цвета состояний
    private Color c_Hidden = Color.black; // Полная тьма
    private Color c_Explored = new Color(0, 0, 0, 0.7f); // Туман войны (полупрозрачный)
    private Color c_Visible = Color.clear; // Видно (прозрачно)

    public void InitFog(int w, int h, float tileSize, TacticalSystem system)
    {
        _tacticalSystem = system;
        _width = w;
        _height = h;
        _tileSize = tileSize; // Сохраняем реальный размер
        _exploredTiles.Clear();

        // 1. Создаем текстуру
        _fogTexture = new Texture2D(w, h);
        _fogTexture.filterMode = FilterMode.Bilinear;
        _fogTexture.wrapMode = TextureWrapMode.Clamp;

        _fogColors = new Color[w * h];

        // Заливаем черным
        for (int i = 0; i < _fogColors.Length; i++) _fogColors[i] = c_Hidden;
        _fogTexture.SetPixels(_fogColors);
        _fogTexture.Apply();

        // 2. Создаем плоскость (Quad)
        if (_fogPlane != null) Destroy(_fogPlane);
        _fogPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fogPlane.name = "Fog_Overlay";
        _fogPlane.transform.parent = transform;

        Destroy(_fogPlane.GetComponent<Collider>());

        // Настраиваем размер и позицию
        float mapW = w * tileSize;
        float mapH = h * tileSize;

        _fogPlane.transform.localScale = new Vector3(mapW, mapH, 1);
        _fogPlane.transform.rotation = Quaternion.Euler(90, 0, 0);

        // Центрируем
        float centerX = (mapW / 2) - (tileSize / 2) + 0.5f;
        float centerY = (mapH / 2) - (tileSize / 2) + 0.5f;
        _fogPlane.transform.position = new Vector3(centerX, fogHeight, centerY);

        // 3. Настраиваем материал
        Renderer r = _fogPlane.GetComponent<Renderer>();
        r.material = new Material(fogMaterial);
        r.material.mainTexture = _fogTexture;
        if (r.material.HasProperty("_BaseMap")) r.material.SetTexture("_BaseMap", _fogTexture);
    }

    public void UpdateFog(GridPos heroPos)
    {
        if (debugRevealMap)
        {
            RevealAll();
            return;
        }

        // 1. Сбрасываем видимость (прошлый кадр -> в explored)
        for (int i = 0; i < _fogColors.Length; i++)
        {
            // Если было видно (прозрачно) -> ставим полумрак
            if (_fogColors[i].a < 0.1f)
            {
                _fogColors[i] = c_Explored;
            }
        }

        // 2. Считаем FOV (Пол)
        List<GridPos> visibleTiles = CalculateFOV(heroPos);

        // 3. Добавляем стены вокруг видимого пола (чтобы видеть контуры комнат)
        AddVisibleWalls(visibleTiles);

        // 4. Применяем состояние к миру
        // Мы проходим по ВСЕМ клеткам, чтобы скрыть то, что стало невидимым, и показать видимое
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                int index = y * _width + x;
                GridPos p = new GridPos(x, y);

                // Определяем состояние по цвету в массиве (он уже обновлен в CalculateFOV и AddVisibleWalls)
                bool isHidden = _fogColors[index] == c_Hidden;

                // РАДИКАЛЬНЫЙ ПОДХОД: Включаем/Выключаем рендер объектов мира
                ToggleWorldVisibility(p, !isHidden);
            }
        }

        // 5. Рисуем "Дырку" в текстуре тумана
        foreach (var p in visibleTiles)
        {
            if (IsValid(p))
            {
                int index = p.y * _width + p.x;
                _fogColors[index] = c_Visible; // Полностью прозрачно только то, что видим сейчас
                _exploredTiles.Add(p);
            }
        }

        // 6. Применяем текстуру
        _fogTexture.SetPixels(_fogColors);
        _fogTexture.Apply();

        _tacticalSystem.UpdateEnemyVisibility(visibleTiles);
    }

    List<GridPos> CalculateFOV(GridPos center)
    {
        List<GridPos> visible = new List<GridPos>();

        for (int x = -viewRadius; x <= viewRadius; x++)
        {
            for (int y = -viewRadius; y <= viewRadius; y++)
            {
                if (x * x + y * y > viewRadius * viewRadius) continue;
                GridPos target = new GridPos(center.x + x, center.y + y);

                if (_tacticalSystem.HasLineOfSightLogic(center, target))
                {
                    visible.Add(target);
                }
            }
        }
        return visible;
    }

    // Добавляет стены, соседствующие с видимым полом, в список "Explored"
    void AddVisibleWalls(List<GridPos> visibleFloors)
    {
        foreach (var p in visibleFloors)
        {
            // Проверяем 4 соседей
            GridPos[] neighbors = {
                new GridPos(p.x + 1, p.y), new GridPos(p.x - 1, p.y),
                new GridPos(p.x, p.y + 1), new GridPos(p.x, p.y - 1),
                // Можно добавить диагонали для красоты углов
                new GridPos(p.x + 1, p.y + 1), new GridPos(p.x - 1, p.y - 1),
                new GridPos(p.x - 1, p.y + 1), new GridPos(p.x + 1, p.y - 1)
            };

            foreach (var n in neighbors)
            {
                if (!IsValid(n)) continue;

                int index = n.y * _width + n.x;

                // Если это стена и она еще скрыта (черная) -> Делаем серой (Explored)
                // Мы не делаем её полностью Visible (прозрачной), чтобы сохранить атмосферу,
                // но игрок увидит, что там стена.
                if (_fogColors[index] == c_Hidden && CheckContentExists(n))
                {
                    _fogColors[index] = c_Explored;
                    _exploredTiles.Add(n);
                }
            }
        }
    }

    // Ищет объекты на клетке и включает/выключает их отображение
    void ToggleWorldVisibility(GridPos p, bool isVisible)
    {
        Vector3 origin = new Vector3(p.x * _tileSize, 10f, p.y * _tileSize);
        float radius = _tileSize * 0.4f;

        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, Vector3.down, 20f);
        foreach (var hit in hits)
        {
            // Не выключаем сам туман (Overlay)
            if (hit.collider.gameObject == _fogPlane) continue;

            Renderer r = hit.collider.GetComponent<Renderer>();
            if (r != null)
            {
                r.enabled = isVisible;
            }
        }
    }

    bool CheckContentExists(GridPos p)
    {
        Vector3 origin = new Vector3(p.x * _tileSize, 10f, p.y * _tileSize);
        float radius = _tileSize * 0.4f;
        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, 20f))
        {
            return true;
        }
        return false;
    }

    void RevealAll()
    {
        for (int i = 0; i < _fogColors.Length; i++) _fogColors[i] = c_Visible;
        _fogTexture.SetPixels(_fogColors);
        _fogTexture.Apply();

        // Включаем рендер всего мира
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                ToggleWorldVisibility(new GridPos(x, y), true);

        _tacticalSystem.RevealAllEnemies();
    }

    bool IsValid(GridPos p)
    {
        return p.x >= 0 && p.x < _width && p.y >= 0 && p.y < _height;
    }
}