using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public struct GridPos
{
    public int x;
    public int y;
    public GridPos(int x, int y) { this.x = x; this.y = y; }
    public static bool operator ==(GridPos a, GridPos b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(GridPos a, GridPos b) => !(a == b);
    public override bool Equals(object obj) => obj is GridPos pos && this == pos;
    public override int GetHashCode() => (x, y).GetHashCode();
}

// --- 1. ОПРЕДЕЛЕНИЯ И СТРУКТУРЫ ---

public enum Element { None, Fire, Water, Ice, Earth, Air, Force }
public enum MotionType { LinearProjectile, ArcingProjectile, InstantRay, SelfBuff }
public enum ShapeType { SingleTile, Cross, LineBeam }

[System.Serializable]
public class SpellBlueprint
{
    public string Name;
    public Element MainElement;
    public MotionType Motion;
    public ShapeType Shape;
    public int PowerLevel;
    public int ManaCost;
    public int HeatCost;
    public Color VisualColor;

    public SpellBlueprint(string name, Element el, MotionType mot, ShapeType sh, int power, int mana, int heat, Color col)
    {
        Name = name;
        MainElement = el;
        Motion = mot;
        Shape = sh;
        PowerLevel = power;
        ManaCost = mana;
        HeatCost = heat;
        VisualColor = col;
    }
}

[System.Serializable]
public class SpellModule
{
    public string Name;
    public string Type;
    public int ManaCost;
    public int HeatCost;

    public MotionType MotionData;
    public ShapeType ShapeData;
    public Element ElementData;
    public Color VisualColor;

    public static SpellModule CreateMotion(string name, MotionType m, int mana, int heat)
    {
        return new SpellModule { Name = name, Type = "MOTION", MotionData = m, ManaCost = mana, HeatCost = heat };
    }
    public static SpellModule CreateShape(string name, ShapeType s, int mana, int heat)
    {
        return new SpellModule { Name = name, Type = "SHAPE", ShapeData = s, ManaCost = mana, HeatCost = heat };
    }
    public static SpellModule CreateElement(string name, Element e, Color c)
    {
        return new SpellModule { Name = name, Type = "ELEMENT", ElementData = e, VisualColor = c };
    }
}

public class ActionCommand
{
    public string Type;
    public GridPos TargetPos;
    public List<GridPos> Path;
    public SpellBlueprint SpellData;

    public ActionCommand(string type, GridPos target, List<GridPos> path = null, SpellBlueprint spell = null)
    {
        Type = type; TargetPos = target; Path = path; SpellData = spell;
    }
}

public enum BattleState { PlayerPlanning, PlayerExecuting, EnemyExecuting, Won, Lost }

public class TileData : MonoBehaviour
{
    public GridPos Pos;
    public Element CurrentElement = Element.None;
}

// --- ГЛАВНЫЙ КЛАСС ---

public class TacticalSystem : MonoBehaviour
{
    [Header("Map Settings")]
    public int width = 20;
    public int height = 15;
    public float tileSize = 1.1f;
    [Range(0, 100)] public int wallChance = 5;
    [Range(0, 100)] public int puddleChance = 10;
    [Range(0, 100)] public int objectChance = 15;

    [Header("Prefabs")]
    public GameObject tilePrefab;
    public GameObject heroPrefab;
    public GameObject enemyPrefab;
    public GameObject markerPrefab;
    public GameObject cratePrefab;
    public GameObject barrelPrefab;

    public Transform HeroTransform => _heroInstance != null ? _heroInstance.transform : null;
    public Vector3 GetGridCenter() => new Vector3(width * tileSize / 2f - tileSize / 2, 0, height * tileSize / 2f - tileSize / 2);

    // Данные
    private Dictionary<GridPos, GameObject> _gridVisuals = new Dictionary<GridPos, GameObject>();
    private Dictionary<GridPos, TileData> _tileDataMap = new Dictionary<GridPos, TileData>();
    private HashSet<GridPos> _walls = new HashSet<GridPos>();
    private List<GameObject> _dynamicWalls = new List<GameObject>();
    private Dictionary<GridPos, InteractiveObject> _interactiveObjects = new Dictionary<GridPos, InteractiveObject>();

    private GridPos _heroPos = new GridPos(1, 1);
    private GridPos _enemyPos;

    private GameObject _heroInstance;
    private GameObject _enemyInstance;
    private UnitStats _heroStats;
    private UnitStats _enemyStats;
    private EnemyAI _enemyAI;

    private List<ActionCommand> _commandQueue = new List<ActionCommand>();
    private List<GameObject> _spawnedMarkers = new List<GameObject>();
    private BattleState _currentState = BattleState.PlayerPlanning;

    // Гримуар
    private List<SpellBlueprint> _spellbook = new List<SpellBlueprint>();
    private int _selectedSpellIndex = -1;
    private bool _isGrimoireOpen = false;

    private List<SpellModule> _libraryMotion = new List<SpellModule>();
    private List<SpellModule> _libraryShape = new List<SpellModule>();
    private List<SpellModule> _libraryElement = new List<SpellModule>();

    private SpellModule _workMotion;
    private SpellModule _workShape;
    private SpellModule _workElement;
    private int _workPowerLevel = 1;

    void Start()
    {
        InitLibrary();
        _workMotion = _libraryMotion[0];
        _workShape = _libraryShape[0];
        _workElement = _libraryElement[0];
        _workPowerLevel = 1;
        CompileSpellToSlot(0);

        GenerateGrid();
        SpawnUnits();
    }

    void InitLibrary()
    {
        _libraryMotion.Add(SpellModule.CreateMotion("Projectile", MotionType.LinearProjectile, 5, 2));
        _libraryMotion.Add(SpellModule.CreateMotion("Grenade (Arc)", MotionType.ArcingProjectile, 10, 5));
        _libraryMotion.Add(SpellModule.CreateMotion("Raycast (Inst)", MotionType.InstantRay, 10, 5));

        _libraryShape.Add(SpellModule.CreateShape("Point", ShapeType.SingleTile, 0, 0));
        _libraryShape.Add(SpellModule.CreateShape("Cross", ShapeType.Cross, 10, 5));
        _libraryShape.Add(SpellModule.CreateShape("Laser Beam", ShapeType.LineBeam, 15, 10));

        _libraryElement.Add(SpellModule.CreateElement("Fire", Element.Fire, Color.red));
        _libraryElement.Add(SpellModule.CreateElement("Ice", Element.Ice, Color.cyan));
        _libraryElement.Add(SpellModule.CreateElement("Force", Element.Force, Color.magenta));
    }

    void Update()
    {
        if (_currentState == BattleState.Won || _currentState == BattleState.Lost)
        {
            if (Input.GetKeyDown(KeyCode.R)) UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab)) _isGrimoireOpen = !_isGrimoireOpen;
        if (_isGrimoireOpen) return;
        if (_currentState != BattleState.PlayerPlanning) return;

        if (Input.GetKeyDown(KeyCode.Escape)) _selectedSpellIndex = -1;
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (i < _spellbook.Count) _selectedSpellIndex = i;
            }
        }

        HandleMouseInput();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_commandQueue.Count == 0) _commandQueue.Add(new ActionCommand("WAIT", _heroPos));
            StartCoroutine(ExecutePlayerTurn());
        }

        if (Input.GetMouseButtonDown(1)) ClearQueue();
    }

    void OnGUI()
    {
        if (_isGrimoireOpen) { DrawGrimoireUI(); return; }
        DrawHUD();
        DrawUnitLabel(_heroInstance, _heroStats);
        DrawUnitLabel(_enemyInstance, _enemyStats);
    }

    // --- ГЕНЕРАЦИЯ ---
    void GenerateGrid()
    {
        _gridVisuals.Clear();
        _walls.Clear();
        _tileDataMap.Clear();
        foreach (var kvp in _interactiveObjects) Destroy(kvp.Value.gameObject);
        _interactiveObjects.Clear();
        foreach (var obj in _dynamicWalls) Destroy(obj);
        _dynamicWalls.Clear();

        Transform oldBoard = transform.Find("Board");
        if (oldBoard != null) Destroy(oldBoard.gameObject);
        GameObject boardHolder = new GameObject("Board");
        boardHolder.transform.parent = transform;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridPos pos = new GridPos(x, y);
                Vector3 worldPos = GetWorldPos(pos);
                GameObject tile = Instantiate(tilePrefab, worldPos, Quaternion.identity);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.parent = boardHolder.transform;
                _gridVisuals.Add(pos, tile);

                TileData data = tile.AddComponent<TileData>();
                data.Pos = pos;
                _tileDataMap.Add(pos, data);

                bool isStartPos = (x < 5 && y < 5);
                int rnd = Random.Range(0, 100);

                if (!isStartPos)
                {
                    if (rnd < wallChance)
                    {
                        _walls.Add(pos);
                        tile.GetComponent<Renderer>().material.color = Color.black;
                        tile.transform.localScale += Vector3.up * 1.5f;
                        tile.name = "WALL";
                    }
                    else if (rnd < wallChance + puddleChance)
                    {
                        data.CurrentElement = Element.Water;
                        tile.GetComponent<Renderer>().material.color = Color.blue;
                    }
                    else if (rnd < wallChance + puddleChance + objectChance)
                    {
                        SpawnObject(pos, (Random.Range(0, 2) == 0) ? ObjType.Crate : ObjType.Barrel);
                    }
                    else
                    {
                        ResetTileColor(pos);
                    }
                }
                else
                {
                    ResetTileColor(pos);
                }
            }
        }
    }

    void SpawnObject(GridPos pos, ObjType type)
    {
        GameObject prefab = (type == ObjType.Crate) ? cratePrefab : barrelPrefab;
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, GetWorldPos(pos) + Vector3.up * 0.5f, Quaternion.identity);
        InteractiveObject interact = obj.GetComponent<InteractiveObject>();
        if (interact == null) interact = obj.AddComponent<InteractiveObject>();

        interact.Type = type;
        interact.Pos = pos;

        _interactiveObjects.Add(pos, interact);
        _walls.Add(pos);
    }

    void SpawnUnits()
    {
        if (_heroInstance != null) Destroy(_heroInstance);
        if (_enemyInstance != null) Destroy(_enemyInstance);

        _heroInstance = Instantiate(heroPrefab, GetWorldPos(_heroPos) + Vector3.up * 0.5f, Quaternion.identity);
        _heroStats = _heroInstance.GetComponent<UnitStats>();

        _enemyPos = FindValidSpawnPos(width - 5, width - 1);
        _enemyInstance = Instantiate(enemyPrefab, GetWorldPos(_enemyPos) + Vector3.up * 0.5f, Quaternion.identity);
        _enemyStats = _enemyInstance.GetComponent<UnitStats>();
        _enemyAI = _enemyInstance.AddComponent<EnemyAI>();
        _enemyAI.Init(this, _enemyStats);
        _enemyInstance.SetActive(false);
    }

    // --- ОБНОВЛЕННАЯ ОБРАБОТКА ОБЪЕКТОВ ---
    IEnumerator HitObject(InteractiveObject obj, Element element, int damage)
    {
        obj.Shake();
        yield return new WaitForSeconds(0.2f);

        // 1. ЛЕД (Заморозка)
        if (element == Element.Ice)
        {
            if (!obj.IsFrozen)
            {
                Debug.Log("Object Frozen!");
                obj.Freeze();
            }
            yield break;
        }

        // 2. ОГОНЬ
        if (element == Element.Fire)
        {
            if (obj.IsFrozen)
            {
                Debug.Log("Object Unfrozen!");
                obj.Unfreeze();
            }
            else
            {
                if (obj.Type == ObjType.Barrel)
                {
                    Debug.Log("BARREL EXPLOSION!");
                    yield return TriggerExplosion(obj.Pos);
                }
                else if (obj.Type == ObjType.Crate)
                {
                    Debug.Log("Crate Burned!");
                    DestroyObject(obj.Pos);
                }
            }
            yield break;
        }

        // 3. СИЛА (Force) - ТОЛЧОК
        if (element == Element.Force)
        {
            yield return PushObjectRoutine(obj);
        }
    }

    IEnumerator PushObjectRoutine(InteractiveObject obj)
    {
        GridPos startPos = obj.Pos;

        // Вычисляем направление от героя к объекту
        Vector3 dirVector = (GetWorldPos(startPos) - GetWorldPos(_heroPos)).normalized;
        int pushX = 0;
        int pushY = 0;
        if (Mathf.Abs(dirVector.x) > Mathf.Abs(dirVector.z))
            pushX = (int)Mathf.Sign(dirVector.x);
        else
            pushY = (int)Mathf.Sign(dirVector.z);

        Debug.Log($"Pushing Object Dir: {pushX}, {pushY}");

        int slideDistance = 4; // Макс дальность полета
        bool hitSomething = false;

        // Удаляем объект из старой клетки (логически), чтобы он мог двигаться
        _interactiveObjects.Remove(startPos);
        _walls.Remove(startPos);

        for (int i = 0; i < slideDistance; i++)
        {
            GridPos nextPos = new GridPos(obj.Pos.x + pushX, obj.Pos.y + pushY);

            // Проверка столкновений
            if (!IsValid(nextPos) || _walls.Contains(nextPos))
            {
                hitSomething = true;
                Debug.Log("Object hit wall!");
                break;
            }

            // Попадание во врага
            if (nextPos == _enemyPos && _enemyInstance.activeSelf)
            {
                hitSomething = true;
                Debug.Log("CRITICAL HIT! Object crushed Enemy!");
                _enemyStats.TakeDamage(30);
                break;
            }

            // Попадание в героя
            if (nextPos == _heroPos)
            {
                hitSomething = true;
                Debug.Log("Friendly fire from object!");
                _heroStats.TakeDamage(10);
                break;
            }

            // Движение
            Vector3 startWorld = obj.transform.position;
            Vector3 endWorld = GetWorldPos(nextPos) + Vector3.up * 0.5f;
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 15f;
                obj.transform.position = Vector3.Lerp(startWorld, endWorld, t);
                yield return null;
            }

            obj.Pos = nextPos;
        }

        // Финал полета
        if (hitSomething)
        {
            if (obj.Type == ObjType.Barrel && !obj.IsFrozen)
            {
                // Бочка врезалась - взрыв!
                yield return TriggerExplosion(obj.Pos);
            }
            else
            {
                // Объект разбился
                Destroy(obj.gameObject);
            }
        }
        else
        {
            // Остановился на новой позиции
            _interactiveObjects.Add(obj.Pos, obj);
            _walls.Add(obj.Pos);
        }
    }

    void DestroyObject(GridPos pos)
    {
        if (_interactiveObjects.ContainsKey(pos))
        {
            InteractiveObject obj = _interactiveObjects[pos];
            _interactiveObjects.Remove(pos);
            _walls.Remove(pos);
            Destroy(obj.gameObject);
        }
    }

    IEnumerator TriggerExplosion(GridPos center)
    {
        // Уничтожаем саму бочку
        if (_interactiveObjects.ContainsKey(center))
            DestroyObject(center);

        // Создаем зону поражения (3x3)
        List<GridPos> boomZone = new List<GridPos>();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                boomZone.Add(new GridPos(center.x + x, center.y + y));
            }
        }

        // Эффект взрыва
        foreach (var p in boomZone)
        {
            StartCoroutine(FlashTile(p, new Color(1f, 0.5f, 0f)));

            // Урон врагам
            if (p == _enemyPos && _enemyInstance.activeSelf)
            {
                _enemyStats.TakeDamage(40);
            }
            if (p == _heroPos)
            {
                _heroStats.TakeDamage(20);
            }

            // Цепная реакция!
            if (_interactiveObjects.ContainsKey(p))
            {
                yield return new WaitForSeconds(0.1f);
                yield return HitObject(_interactiveObjects[p], Element.Fire, 0);
            }
        }
    }

    // --- ПРОЦЕССОР ЗАКЛИНАНИЙ (ОБНОВЛЕН) ---
    IEnumerator ProcessSpell(SpellBlueprint spell, GridPos targetCenter)
    {
        Color c = spell.VisualColor;
        float projectileSize = 0.3f * spell.PowerLevel;

        if (spell.Motion == MotionType.LinearProjectile || spell.Motion == MotionType.ArcingProjectile)
        {
            yield return ShootProjectile(_heroInstance, targetCenter, c, projectileSize);
        }
        else if (spell.Motion == MotionType.InstantRay)
        {
            yield return FlashTile(targetCenter, c);
        }

        // Определение зоны
        List<GridPos> affectedTiles = new List<GridPos>();
        if (spell.Shape == ShapeType.SingleTile)
            affectedTiles.Add(targetCenter);
        else if (spell.Shape == ShapeType.Cross)
        {
            affectedTiles.Add(targetCenter);
            affectedTiles.Add(new GridPos(targetCenter.x + 1, targetCenter.y));
            affectedTiles.Add(new GridPos(targetCenter.x - 1, targetCenter.y));
            affectedTiles.Add(new GridPos(targetCenter.x, targetCenter.y + 1));
            affectedTiles.Add(new GridPos(targetCenter.x, targetCenter.y - 1));
        }
        else if (spell.Shape == ShapeType.LineBeam)
        {
            Vector3 dir = (GetWorldPos(targetCenter) - GetWorldPos(_heroPos)).normalized;
            int dx = 0; int dy = 0;
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
                dx = (int)Mathf.Sign(dir.x);
            else
                dy = (int)Mathf.Sign(dir.z);
            GridPos checkPos = _heroPos;
            for (int k = 0; k < width; k++)
            {
                checkPos.x += dx; checkPos.y += dy;
                if (!IsValid(checkPos)) break;
                if (_walls.Contains(checkPos) && !_interactiveObjects.ContainsKey(checkPos)) break;
                affectedTiles.Add(checkPos);
            }
        }

        foreach (GridPos tilePos in affectedTiles)
        {
            if (!IsValid(tilePos)) continue;
            StartCoroutine(FlashTile(tilePos, c));

            // 1. ИНТЕРАКТИВНЫЕ ОБЪЕКТЫ (ОБНОВЛЕНО)
            if (_interactiveObjects.ContainsKey(tilePos))
            {
                yield return HitObject(_interactiveObjects[tilePos], spell.MainElement, 10 * spell.PowerLevel);
            }

            // 2. Реакция с полом
            if (_tileDataMap.ContainsKey(tilePos))
            {
                TileData tile = _tileDataMap[tilePos];
                if (spell.MainElement == Element.Fire && tile.CurrentElement == Element.Ice)
                {
                    tile.CurrentElement = Element.Water;
                    _walls.Remove(tilePos);
                    ResetTileColor(tilePos);
                }
                else if (spell.MainElement == Element.Ice && tile.CurrentElement == Element.Water)
                {
                    tile.CurrentElement = Element.Ice;
                    _walls.Add(tilePos);
                    ResetTileColor(tilePos);
                }
            }

            // 3. Урон врагу
            if (tilePos == _enemyPos && _enemyInstance.activeSelf)
            {
                float mult = spell.PowerLevel == 2 ? 1.5f : (spell.PowerLevel == 3 ? 2.5f : 1f);
                int damage = Mathf.RoundToInt(10 * mult);
                _enemyStats.TakeDamage(damage);

                if (spell.MainElement == Element.Force)
                {
                    Vector3 dir = (GetWorldPos(tilePos) - GetWorldPos(_heroPos)).normalized;
                    GridPos pushDest = new GridPos(tilePos.x + (int)Mathf.Sign(dir.x), tilePos.y + (int)Mathf.Sign(dir.z));
                    if (!_walls.Contains(pushDest) && IsValid(pushDest))
                    {
                        yield return MoveUnit(_enemyInstance, pushDest);
                        _enemyPos = pushDest;
                    }
                    else
                    {
                        _enemyStats.TakeDamage(10);
                    }
                }
            }
        }
    }

    // --- UI ---
    void DrawGrimoireUI()
    {
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
        float w = 700; float h = 550;
        float x = (Screen.width - w) / 2;
        float y = (Screen.height - h) / 2;
        GUI.Box(new Rect(x, y, w, h), "SPELL COMPILER");
        GUILayout.BeginArea(new Rect(x + 20, y + 40, w - 40, h - 60));
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical("box", GUILayout.Width(180));
        foreach (var m in _libraryMotion)
            if (GUILayout.Button($"{m.Name} ({m.ManaCost})")) _workMotion = m;
        GUILayout.Space(10);
        foreach (var m in _libraryShape)
            if (GUILayout.Button($"{m.Name} (+{m.ManaCost})")) _workShape = m;
        GUILayout.Space(10);
        foreach (var m in _libraryElement)
            if (GUILayout.Button(m.Name)) _workElement = m;
        GUILayout.EndVertical();
        GUILayout.BeginVertical("box", GUILayout.Width(250));
        GUILayout.Label($"<b>DRV:</b> {(_workMotion != null ? _workMotion.Name : "-")}");
        GUILayout.Label($"<b>MOD:</b> {(_workShape != null ? _workShape.Name : "-")}");
        GUILayout.Label($"<b>COR:</b> {(_workElement != null ? _workElement.Name : "-")}");
        GUILayout.Space(10);
        GUILayout.Label($"OVERCHARGE: {_workPowerLevel}");
        _workPowerLevel = Mathf.RoundToInt(GUILayout.HorizontalSlider(_workPowerLevel, 1, 3));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("SAVE [1]")) CompileSpellToSlot(0);
        if (GUILayout.Button("SAVE [2]")) CompileSpellToSlot(1);
        if (GUILayout.Button("SAVE [3]")) CompileSpellToSlot(2);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        if (GUILayout.Button("CLOSE [TAB]")) _isGrimoireOpen = false;
        GUILayout.EndArea();
    }

    void CompileSpellToSlot(int slotIndex)
    {
        if (_workMotion == null || _workShape == null || _workElement == null) return;
        string spellName = $"{_workElement.Name} {_workShape.Name}";
        int baseMana = _workMotion.ManaCost + _workShape.ManaCost;
        int baseHeat = _workMotion.HeatCost + _workShape.HeatCost;
        int finalMana = baseMana * _workPowerLevel;
        int finalHeat = (int)(baseHeat * Mathf.Pow(_workPowerLevel, 1.5f));
        SpellBlueprint newSpell = new SpellBlueprint(spellName, _workElement.ElementData, _workMotion.MotionData, _workShape.ShapeData, _workPowerLevel, finalMana, finalHeat, _workElement.VisualColor);
        while (_spellbook.Count <= slotIndex) _spellbook.Add(null);
        _spellbook[slotIndex] = newSpell;
    }

    void DrawHUD()
    {
        GUI.Box(new Rect(10, 10, 260, 500), "TACTICAL OS");
        GUILayout.BeginArea(new Rect(20, 40, 240, 460));
        if (_heroStats != null)
        {
            GUILayout.Label($"MANA: {_heroStats.currentMana} | HEAT: {_heroStats.currentHeat}");
            if (_heroStats.isOverheated) GUILayout.Label("<color=red><b>[ LOCKED ]</b></color>");
        }
        GUILayout.Space(5);
        if (GUILayout.Button("GRIMOIRE [TAB]")) _isGrimoireOpen = true;
        GUILayout.Space(10);
        for (int i = 0; i < _spellbook.Count; i++)
        {
            if (_spellbook[i] == null) continue;
            string style = _selectedSpellIndex == i ? "box" : "label";
            GUILayout.Label($"[{i + 1}] {_spellbook[i].Name}", GUI.skin.GetStyle(style));
        }
        GUILayout.Space(10);
        for (int i = 0; i < _commandQueue.Count; i++)
        {
            ActionCommand cmd = _commandQueue[i];
            string txt = cmd.Type == "MOVE" ? "MOVE" : (cmd.SpellData != null ? cmd.SpellData.Name : cmd.Type);
            GUILayout.Label($"{i + 1}. {txt} >> {cmd.TargetPos.x},{cmd.TargetPos.y}");
        }
        GUILayout.EndArea();
    }

    // --- PLANNING & INPUT ---
    void TryPlanCommand(GridPos targetPos)
    {
        GridPos currentPlanEnd = _heroPos;
        if (_commandQueue.Count > 0)
        {
            for (int i = _commandQueue.Count - 1; i >= 0; i--)
            {
                if (_commandQueue[i].Type == "MOVE")
                {
                    currentPlanEnd = _commandQueue[i].TargetPos;
                    break;
                }
            }
        }
        if (_selectedSpellIndex == -1)
        {
            if (_walls.Contains(targetPos) || targetPos == currentPlanEnd) return;
            List<GridPos> path = Pathfinding.FindPath(currentPlanEnd, targetPos, _walls, width, height);
            if (path != null && path.Count > 0)
            {
                _commandQueue.Add(new ActionCommand("MOVE", targetPos, path));
                foreach (GridPos step in path) _gridVisuals[step].GetComponent<Renderer>().material.color = Color.green;
                SpawnMarker(targetPos, Color.cyan);
            }
        }
        else
        {
            if (_selectedSpellIndex >= _spellbook.Count || _spellbook[_selectedSpellIndex] == null) return;
            SpellBlueprint spell = _spellbook[_selectedSpellIndex];
            if (targetPos == currentPlanEnd && spell.Motion != MotionType.SelfBuff) return;
            if (spell.Motion != MotionType.ArcingProjectile && spell.Shape != ShapeType.LineBeam)
            {
                if (!HasLineOfSight(currentPlanEnd, targetPos))
                {
                    StartCoroutine(FlashTile(targetPos, Color.gray));
                    return;
                }
            }
            _commandQueue.Add(new ActionCommand("SPELL", targetPos, null, spell));
            SpawnMarker(targetPos, spell.VisualColor);
        }
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0) && !Input.GetMouseButton(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                foreach (var kvp in _gridVisuals)
                {
                    if (kvp.Value == hit.collider.gameObject)
                    {
                        TryPlanCommand(kvp.Key);
                        break;
                    }
                }
            }
        }
    }

    // --- EXECUTION ---
    IEnumerator ExecutePlayerTurn()
    {
        _currentState = BattleState.PlayerExecuting;
        foreach (ActionCommand cmd in _commandQueue)
        {
            if (cmd.Type == "WAIT")
            {
                yield return new WaitForSeconds(0.5f);
                _heroStats.CoolDown(5);
            }
            else if (cmd.Type == "MOVE")
            {
                foreach (GridPos step in cmd.Path)
                {
                    yield return MoveUnit(_heroInstance, step);
                    _heroPos = step;
                    ResetTileColor(step);
                    if (Vector3.Distance(GetWorldPos(_heroPos), GetWorldPos(_enemyPos)) < tileSize * 3)
                        _enemyInstance.SetActive(true);
                }
            }
            else if (cmd.Type == "SPELL")
            {
                if (_heroStats.ConsumeMana(cmd.SpellData.ManaCost))
                {
                    _heroStats.AddHeat(cmd.SpellData.HeatCost);
                    _heroInstance.transform.LookAt(GetWorldPos(cmd.TargetPos));
                    yield return ProcessSpell(cmd.SpellData, cmd.TargetPos);
                }
            }
            GameObject usedMarker = _spawnedMarkers.Find(m => Vector3.Distance(m.transform.position, GetWorldPos(cmd.TargetPos)) < 0.5f);
            if (usedMarker != null)
            {
                _spawnedMarkers.Remove(usedMarker);
                Destroy(usedMarker);
            }
            yield return new WaitForSeconds(0.2f);
        }
        ClearQueue();
        if (_enemyStats.currentHP <= 0)
            _currentState = BattleState.Won;
        else
            StartCoroutine(ExecuteEnemyTurn());
    }

    IEnumerator ExecuteEnemyTurn()
    {
        _currentState = BattleState.EnemyExecuting;
        yield return new WaitForSeconds(0.5f);
        if (_enemyInstance.activeSelf && _enemyStats.currentHP > 0)
        {
            List<ActionCommand> aiMoves = _enemyAI.PlanTurn(_enemyPos, _heroPos, _walls);
            foreach (ActionCommand cmd in aiMoves)
            {
                if (cmd.Type == "MOVE")
                {
                    foreach (GridPos step in cmd.Path)
                    {
                        yield return MoveUnit(_enemyInstance, step);
                        _enemyPos = step;
                    }
                }
                else if (cmd.Type == "ATTACK")
                {
                    _enemyInstance.transform.LookAt(GetWorldPos(cmd.TargetPos));
                    _heroStats.TakeDamage(_enemyAI.damage);
                }
                yield return new WaitForSeconds(0.2f);
            }
        }
        if (_heroStats.currentHP <= 0)
            _currentState = BattleState.Lost;
        else
        {
            _heroStats.CoolDown(2);
            _currentState = BattleState.PlayerPlanning;
        }
    }

    // --- HELPERS ---
    IEnumerator ShootProjectile(GameObject shooter, GridPos target, Color color, float size)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.transform.localScale = Vector3.one * size;
        projectile.GetComponent<Renderer>().material.color = color;
        Vector3 startPos = shooter.transform.position + Vector3.up * 0.5f;
        projectile.transform.position = startPos;
        Vector3 targetWorld = GetWorldPos(target) + Vector3.up * 0.5f;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 10f;
            projectile.transform.position = Vector3.Lerp(startPos, targetWorld, t);
            yield return null;
        }
        Destroy(projectile);
    }

    IEnumerator MoveUnit(GameObject unit, GridPos targetStep)
    {
        Vector3 targetWorld = GetWorldPos(targetStep);
        unit.transform.LookAt(targetWorld);
        Vector3 startPos = unit.transform.position;
        Vector3 endPos = targetWorld + Vector3.up * 0.5f;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 8f;
            unit.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        unit.transform.position = endPos;
    }

    bool HasLineOfSight(GridPos start, GridPos end)
    {
        Vector3 startWorld = GetWorldPos(start) + Vector3.up * 0.5f;
        Vector3 endWorld = GetWorldPos(end) + Vector3.up * 0.5f;
        Vector3 direction = (endWorld - startWorld).normalized;
        float distance = Vector3.Distance(startWorld, endWorld);
        if (Physics.Raycast(startWorld, direction, out RaycastHit hit, distance))
        {
            if (hit.collider.gameObject.name == "WALL" || hit.collider.gameObject.name == "ICE_WALL")
                return false;
        }
        return true;
    }

    bool IsValid(GridPos p) => p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;

    Vector3 GetWorldPos(GridPos pos) => new Vector3(pos.x * tileSize, 0, pos.y * tileSize);

    GridPos FindValidSpawnPos(int minX, int maxX)
    {
        int attempts = 100;
        while (attempts > 0)
        {
            GridPos p = new GridPos(Random.Range(minX, maxX), Random.Range(0, height));
            if (!_walls.Contains(p)) return p;
            attempts--;
        }
        return new GridPos(width - 1, height - 1);
    }

    void SpawnMarker(GridPos pos, Color color)
    {
        if (markerPrefab == null) return;
        Vector3 spawnPos = GetWorldPos(pos) + Vector3.up * 0.5f;
        GameObject marker = Instantiate(markerPrefab, spawnPos, Quaternion.identity);
        marker.GetComponent<Renderer>().material.color = color;
        _spawnedMarkers.Add(marker);
    }

    void ClearQueue()
    {
        _commandQueue.Clear();
        foreach (var m in _spawnedMarkers) Destroy(m);
        _spawnedMarkers.Clear();
        foreach (var kvp in _gridVisuals) ResetTileColor(kvp.Key);
    }

    IEnumerator FlashTile(GridPos pos, Color flashColor)
    {
        if (_gridVisuals.ContainsKey(pos))
        {
            Renderer rend = _gridVisuals[pos].GetComponent<Renderer>();
            rend.material.color = flashColor;
            yield return new WaitForSeconds(0.2f);
            ResetTileColor(pos);
        }
    }

    void ResetTileColor(GridPos pos)
    {
        if (!_gridVisuals.ContainsKey(pos)) return;
        if (!_tileDataMap.ContainsKey(pos)) return;
        Renderer rend = _gridVisuals[pos].GetComponent<Renderer>();
        Element el = _tileDataMap[pos].CurrentElement;

        if (_walls.Contains(pos))
        {
            rend.material.color = (el == Element.Ice) ? Color.cyan : Color.black;
            return;
        }
        if (el == Element.Water)
            rend.material.color = Color.blue;
        else if (el == Element.Fire)
            rend.material.color = new Color(0.5f, 0, 0);
        else
        {
            bool isDark = (pos.x + pos.y) % 2 == 0;
            rend.material.color = isDark ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
        }
    }

    void DrawUnitLabel(GameObject unit, UnitStats stats)
    {
        if (unit == null || stats == null || !unit.activeSelf) return;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(unit.transform.position + Vector3.up * 1.5f);
        if (screenPos.z > 0)
            GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y, 100, 30), $"HP: {stats.currentHP}");
    }
}