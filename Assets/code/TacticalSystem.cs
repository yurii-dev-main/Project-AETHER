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

// --- 1. ÎÏÐÅÄÅËÅÍÈß È ÑÒÐÓÊÒÓÐÛ ---

public enum Element { None, Fire, Water, Ice, Earth, Air, Lightning, Force }
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
    public bool IsOptimized;
    public int ManaCost;
    public int HeatCost;
    public Color VisualColor;

    public SpellBlueprint(string name, Element el, MotionType mot, ShapeType sh, int power, bool optimized, int mana, int heat, Color col)
    {
        Name = name;
        MainElement = el;
        Motion = mot;
        Shape = sh;
        PowerLevel = power;
        IsOptimized = optimized;
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

// --- ÃËÀÂÍÛÉ ÊËÀÑÑ ---

public class TacticalSystem : MonoBehaviour
{
    [Header("Map Settings")]
    public int width = 20;
    public int height = 15;
    public float tileSize = 1.1f;
    [Range(0, 100)] public int wallChance = 5;
    [Range(0, 100)] public int puddleChance = 10;
    [Range(0, 100)] public int objectChance = 15;

    public MapGenerator mapGenerator;
    public FogOfWar fogOfWar;

    [Header("Prefabs")]
    public GameObject tilePrefab;
    public GameObject heroPrefab;
    public GameObject enemyPrefab;
    public GameObject markerPrefab;
    public GameObject cratePrefab;
    public GameObject barrelPrefab;
    public GameObject doorPrefab;
    public GameObject switchPrefab;
    public GameObject earthWallPrefab;
    public List<GameObject> enemyPrefabs;

    [Header("VFX")]
    public GameObject vfxFireball;
    public GameObject vfxIceWall;
    public GameObject vfxForceBeam;
    public GameObject vfxForcePillar;
    public GameObject vfxExplosion;

    private string[] _lootPool =
    {
        "Grenade (Arc)", "Raycast (Inst)",
        "Cross", "Laser Beam",
        "Ice", "Air"
    };

    public Transform HeroTransform => _heroInstance != null ? _heroInstance.transform : null;
    public Vector3 GetGridCenter() => new Vector3(width * tileSize / 2f - tileSize / 2, 0, height * tileSize / 2f - tileSize / 2);

    // Äàííûå
    private Dictionary<GridPos, GameObject> _gridVisuals = new Dictionary<GridPos, GameObject>();
    private Dictionary<GridPos, TileData> _tileDataMap = new Dictionary<GridPos, TileData>();
    private HashSet<GridPos> _walls = new HashSet<GridPos>();
    private List<GameObject> _dynamicWalls = new List<GameObject>();
    private Dictionary<GridPos, InteractiveObject> _interactiveObjects = new Dictionary<GridPos, InteractiveObject>();
    private MapGenerator.DungeonData _dungeonData;
    private List<GridPos> _enemySpawnPoints = new List<GridPos>();
    private List<GridPos> _validFloors = new List<GridPos>();
    private List<GameObject> _enemies = new List<GameObject>();
    private List<UnitStats> _enemyStatsList = new List<UnitStats>();
    private List<EnemyAI> _enemyAIList = new List<EnemyAI>();

    private GridPos _heroPos = new GridPos(1, 1);
    private GridPos _enemyPos;

    private GameObject _heroInstance;
    private GameObject _enemyInstance;
    private UnitStats _heroStats;
    private UnitStats _enemyStats;
    private EnemyAI _enemyAI;

    // ПУБЛИЧНЫÅ ÄÀÍÍÛÅ ÄËß UI
    public List<ActionCommand> CommandQueue = new List<ActionCommand>();
    private List<GameObject> _spawnedMarkers = new List<GameObject>();
    private BattleState _currentState = BattleState.PlayerPlanning;

    public List<SpellBlueprint> Spellbook = new List<SpellBlueprint>();
    private int _selectedSpellIndex = -1;
    private bool _isGrimoireOpen = false;
    private int _currentLevel = 1;

    private List<SpellModule> _libraryMotion = new List<SpellModule>();
    private List<SpellModule> _libraryShape = new List<SpellModule>();
    private List<SpellModule> _libraryElement = new List<SpellModule>();

    // ÂÅÐÑÒÀÊ (ÏÓÁËÈ×ÍÛÉ ÄËß UI)
    public SpellModule WorkMotion;
    public SpellModule WorkShape;
    public SpellModule WorkElement;
    public int WorkPowerLevel = 1;
    public bool WorkIsOptimized = false;

    // Геттеры для UI
    public List<SpellModule> GetMotionLibrary() => _libraryMotion;
    public List<SpellModule> GetShapeLibrary() => _libraryShape;
    public List<SpellModule> GetElementLibrary() => _libraryElement;

    private bool _victoryShown = false;
    void Awake()
    {
        if (DungeonManager.Instance != null) Spellbook = DungeonManager.Instance.SavedSpellbook;
    }

    void Start()
    {
        if (DungeonManager.Instance != null)
        {
            _currentLevel = DungeonManager.Instance.CurrentLevel;
            Spellbook = DungeonManager.Instance.SavedSpellbook;
        }
        else
        {
            Spellbook.Add(new SpellBlueprint("Temp Fire", Element.Fire, MotionType.LinearProjectile, ShapeType.SingleTile, 1, false, 10, 5, Color.red));
        }

        InitLibrary();

        WorkMotion = _libraryMotion[0];
        WorkShape = _libraryShape[0];
        WorkElement = _libraryElement[0];
        WorkPowerLevel = 1;
        WorkIsOptimized = false;

        GenerateGrid();
        SpawnUnits();
    }

    void InitLibrary()
    {
        _libraryMotion.Clear();
        _libraryShape.Clear();
        _libraryElement.Clear();

        bool IsUnlocked(string name)
        {
            if (DungeonManager.Instance == null) return false;
            return DungeonManager.Instance.IsModuleUnlocked(name)
                   || DungeonManager.Instance.IsModuleUnlocked(name)
                   || DungeonManager.Instance.IsModuleUnlocked(name);
        }

        if (IsUnlocked("Projectile")) _libraryMotion.Add(SpellModule.CreateMotion("Projectile", MotionType.LinearProjectile, 5, 2));
        if (IsUnlocked("Grenade (Arc)")) _libraryMotion.Add(SpellModule.CreateMotion("Grenade (Arc)", MotionType.ArcingProjectile, 10, 5));
        if (IsUnlocked("Raycast (Inst)")) _libraryMotion.Add(SpellModule.CreateMotion("Raycast (Inst)", MotionType.InstantRay, 10, 5));

        if (IsUnlocked("Point")) _libraryShape.Add(SpellModule.CreateShape("Point", ShapeType.SingleTile, 0, 0));
        if (IsUnlocked("Cross")) _libraryShape.Add(SpellModule.CreateShape("Cross", ShapeType.Cross, 10, 5));
        if (IsUnlocked("Laser Beam")) _libraryShape.Add(SpellModule.CreateShape("Laser Beam", ShapeType.LineBeam, 15, 10));

        if (IsUnlocked("Fire")) _libraryElement.Add(SpellModule.CreateElement("Fire", Element.Fire, Color.red));
        if (IsUnlocked("Ice")) _libraryElement.Add(SpellModule.CreateElement("Ice", Element.Ice, Color.cyan));
        if (IsUnlocked("Air")) _libraryElement.Add(SpellModule.CreateElement("Air", Element.Air, Color.white));
        if (IsUnlocked("Lightning")) _libraryElement.Add(SpellModule.CreateElement("Lightning", Element.Lightning, Color.yellow));
    }

    void Update()
    {
        UpdateUI();

        if (_currentState == BattleState.Won || _currentState == BattleState.Lost)
        {
            if (!_victoryShown && UIManager.Instance != null)
            {
                UIManager.Instance.ShowVictory(_currentState == BattleState.Won);
                _victoryShown = true;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (DungeonManager.Instance != null)
                {
                    if (_currentState == BattleState.Won)
                        DungeonManager.Instance.CompleteLevel(_heroStats.currentHP, _heroStats.currentMana, _heroStats.currentHeat);
                    else
                        DungeonManager.Instance.RestartGame();
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(0);
                }
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            _isGrimoireOpen = !_isGrimoireOpen;
            if (UIManager.Instance != null) UIManager.Instance.ToggleGrimoire(_isGrimoireOpen);
        }
        if (_isGrimoireOpen) return;
        if (_currentState != BattleState.PlayerPlanning) return;

        if (Input.GetKeyDown(KeyCode.Escape)) _selectedSpellIndex = -1;
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (i < Spellbook.Count) _selectedSpellIndex = i;
            }
        }

        HandleMouseInput();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (CommandQueue.Count == 0) CommandQueue.Add(new ActionCommand("WAIT", _heroPos));
            StartCoroutine(ExecutePlayerTurn());
        }

        if (Input.GetMouseButtonDown(1)) ClearQueue();
    }

    // --- ÌÅÒÎÄÛ ÄËß UI ---
    public void UI_SelectModule(int typeIndex, int index)
    {
        if (typeIndex == 0 && index < _libraryMotion.Count) WorkMotion = _libraryMotion[index];
        if (typeIndex == 1 && index < _libraryShape.Count) WorkShape = _libraryShape[index];
        if (typeIndex == 2 && index < _libraryElement.Count) WorkElement = _libraryElement[index];
    }

    public void UI_SetPower(float val) => WorkPowerLevel = Mathf.RoundToInt(val);

    public void UI_Compile(int slot) => CompileSpellToSlot(slot);

    public void UI_ToggleOptimization(bool isOpt) => WorkIsOptimized = isOpt;

    // --- ÃÅÍÅÐÀÖÈß ---
    void GenerateGrid()
    {
        _gridVisuals.Clear();
        _walls.Clear();
        _tileDataMap.Clear();
        _validFloors.Clear();
        foreach (var kvp in _interactiveObjects) Destroy(kvp.Value.gameObject);
        _interactiveObjects.Clear();
        foreach (var obj in _dynamicWalls) Destroy(obj);
        _dynamicWalls.Clear();
        _enemySpawnPoints.Clear();

        Transform oldBoard = transform.Find("Board");
        if (oldBoard != null) Destroy(oldBoard.gameObject);
        GameObject boardHolder = new GameObject("Board");
        boardHolder.transform.parent = transform;

        if (mapGenerator == null)
        {
            Debug.LogError("MapGenerator is not assigned.");
            return;
        }

        var data = mapGenerator.Generate(width, height);
        _dungeonData = data;
        if (data == null)
        {
            Debug.LogError("MapGenerator failed to create dungeon data.");
            return;
        }

        _enemySpawnPoints = new List<GridPos>(data.EnemySpawnPoints);
        _validFloors = new List<GridPos>(data.Floors);
        _heroPos = data.StartPos;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridPos p = new GridPos(x, y);

                if (data.Floors.Contains(p) || data.Walls.Contains(p))
                {
                    Vector3 worldPos = GetWorldPos(p);
                    GameObject tile = Instantiate(tilePrefab, worldPos, Quaternion.identity);
                    tile.name = $"Tile_{x}_{y}";
                    tile.transform.parent = boardHolder.transform;
                    _gridVisuals.Add(p, tile);
                    TileData td = tile.AddComponent<TileData>();
                    td.Pos = p;
                    _tileDataMap.Add(p, td);

                    if (data.Walls.Contains(p))
                    {
                        _walls.Add(p);
                        tile.name = "WALL";
                        tile.GetComponent<Renderer>().material.color = Color.black;
                        tile.transform.localScale += Vector3.up * 1.5f;
                    }
                    else
                    {
                        ResetTileColor(p);
                    }
                }
            }
        }

        foreach (var pair in data.Puzzles)
        {
            InteractiveObject door = SpawnObjectReturn(pair.DoorPos, ObjType.Door);
            InteractiveObject sw = SpawnObjectReturn(pair.SwitchPos, ObjType.Switch);

            if (sw != null && door != null)
            {
                sw.LinkedObject = door;
            }
        }

        foreach (var p in data.Floors)
        {
            if (p == _heroPos) continue;
            if (_interactiveObjects.ContainsKey(p)) continue;

            int rnd = Random.Range(0, 100);
            if (rnd < puddleChance)
            {
                _tileDataMap[p].CurrentElement = Element.Water;
                ResetTileColor(p);
            }
            else if (rnd < puddleChance + objectChance)
            {
                if (Random.value < 0.1f) SpawnObjectReturn(p, ObjType.Chest);
                else SpawnObjectReturn(p, (Random.value < 0.5f) ? ObjType.Crate : ObjType.Barrel);
            }
        }

        if (fogOfWar != null)
        {
            fogOfWar.InitFog(width, height, tileSize, this);
            fogOfWar.UpdateFog(_heroPos);
        }
        else
        {
            Debug.LogWarning("FogOfWar is not assigned.");
        }
    }

    InteractiveObject SpawnObjectReturn(GridPos pos, ObjType type)
    {
        if (_interactiveObjects.ContainsKey(pos)) return null;

        GameObject prefab = cratePrefab;
        switch (type)
        {
            case ObjType.Barrel:
                prefab = barrelPrefab;
                break;
            case ObjType.Door:
                prefab = doorPrefab;
                break;
            case ObjType.Switch:
                prefab = switchPrefab;
                break;
            case ObjType.Chest:
                prefab = cratePrefab;
                break;
        }

        if (prefab == null) return null;

        GameObject obj = Instantiate(prefab, GetWorldPos(pos) + Vector3.up * 0.5f, Quaternion.identity);
        InteractiveObject interact = obj.GetComponent<InteractiveObject>();
        if (interact == null) interact = obj.AddComponent<InteractiveObject>();

        interact.Type = type;
        interact.Pos = pos;

        if (type == ObjType.Chest)
        {
            interact.UpdateColor();
            interact.LootModuleID = _lootPool[Random.Range(0, _lootPool.Length)];
        }
        else
        {
            interact.UpdateColor();
        }

        _interactiveObjects.Add(pos, interact);

        if (type != ObjType.Switch)
        {
            _walls.Add(pos);
        }

        return interact;
    }

    void CreateTileVisual(GridPos pos, Transform parent, bool isWall)
    {
        if (_gridVisuals.ContainsKey(pos)) return;

        Vector3 worldPos = GetWorldPos(pos);
        GameObject tile = Instantiate(tilePrefab, worldPos, Quaternion.identity);
        tile.name = $"Tile_{pos.x}_{pos.y}";
        tile.transform.parent = parent;
        _gridVisuals.Add(pos, tile);

        TileData data = tile.AddComponent<TileData>();
        data.Pos = pos;
        _tileDataMap.Add(pos, data);

        if (isWall)
        {
            _walls.Add(pos);
            tile.GetComponent<Renderer>().material.color = Color.black;
            tile.transform.localScale += Vector3.up * 1.5f;
            tile.name = "WALL";
        }
        else
        {
            ResetTileColor(pos);
        }
    }

    void SpawnObject(GridPos pos, ObjType type)
    {
        GameObject prefab = cratePrefab;
        if (type == ObjType.Barrel) prefab = barrelPrefab;
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, GetWorldPos(pos) + Vector3.up * 0.5f, Quaternion.identity);
        InteractiveObject interact = obj.GetComponent<InteractiveObject>();
        if (interact == null) interact = obj.AddComponent<InteractiveObject>();

        interact.Type = type;
        interact.Pos = pos;
        if (type == ObjType.Chest)
        {
            interact.UpdateColor();
            interact.LootModuleID = _lootPool[Random.Range(0, _lootPool.Length)];
        }
        else
        {
            interact.UpdateColor();
        }

        _interactiveObjects.Add(pos, interact);
        _walls.Add(pos);
    }

    void SpawnDoor(GridPos pos)
    {
        if (doorPrefab == null) return;

        GameObject obj = Instantiate(doorPrefab, GetWorldPos(pos) + Vector3.up * 0.5f, Quaternion.identity);
        InteractiveObject interact = obj.GetComponent<InteractiveObject>();
        if (interact == null) interact = obj.AddComponent<InteractiveObject>();

        interact.Type = ObjType.Door;
        interact.Pos = pos;
        interact.UpdateColor();

        _interactiveObjects.Add(pos, interact);
        _walls.Add(pos);
    }

    void SpawnUnits()
    {
        if (_heroInstance != null) Destroy(_heroInstance);
        foreach (var enemy in _enemies) if (enemy != null) Destroy(enemy);
        _enemies.Clear();
        _enemyStatsList.Clear();
        _enemyAIList.Clear();

        _heroInstance = Instantiate(heroPrefab, GetWorldPos(_heroPos) + Vector3.up * 0.5f, Quaternion.identity);
        _heroStats = _heroInstance.GetComponent<UnitStats>();
        if (DungeonManager.Instance != null)
        {
            _heroStats.currentHP = DungeonManager.Instance.SavedHP;
            _heroStats.currentMana = DungeonManager.Instance.SavedMana;
            _heroStats.currentHeat = DungeonManager.Instance.SavedHeat;
        }

        int enemyCount = 1 + (_currentLevel / 2);
        for (int i = 0; i < enemyCount; i++)
        {
            GridPos spawnPos = FindValidSpawnPosFromList();
            if (spawnPos.x == -1) continue;
            GameObject randomPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
            GameObject newEnemy = Instantiate(randomPrefab, GetWorldPos(spawnPos) + Vector3.up * 0.5f, Quaternion.identity);
            UnitStats stats = newEnemy.GetComponent<UnitStats>();
            stats.maxHP += (_currentLevel - 1) * 20;
            stats.currentHP = stats.maxHP;
            EnemyAI ai = newEnemy.AddComponent<EnemyAI>();
            ai.Init(this, stats);
            newEnemy.SetActive(false);
            _enemies.Add(newEnemy);
            _enemyStatsList.Add(stats);
            _enemyAIList.Add(ai);
        }

        _enemyInstance = _enemies.Count > 0 ? _enemies[0] : null;
    }

    // --- ÎÁÍÎÂËÅÍÍÀß ÎÁÐÀÁÎÒÊÀ ÎÁÚÅÊÒÎÂ ---
    IEnumerator HitObject(InteractiveObject obj, Element element, int damage)
    {
        if (obj == null) yield break;
        obj.Shake();
        yield return new WaitForSeconds(0.1f);
        if (obj == null) yield break;

        if (obj.Type == ObjType.EarthWall && element == Element.Fire)
        {
            Debug.Log("Melting Stone to Magma!");

            if (_tileDataMap.ContainsKey(obj.Pos))
            {
                _tileDataMap[obj.Pos].CurrentElement = Element.Fire;
            }

            DestroyObject(obj.Pos);
            ResetTileColor(obj.Pos);
            yield break;
        }

        // --- 1. ÄÂÅÐÜ (BREAKABLE) ---
        // Ôè÷à: Äâåðü ìîæíî âûáèòü ñèëîé èëè ñæå÷ü
        if (obj.Type == ObjType.Door)
        {
            bool strongWater = element == Element.Water && damage >= 20;
            if (element == Element.Air || element == Element.Fire || element == Element.Force || strongWater)
            {
                Debug.Log("Door BREACHED by Magic!");
                obj.OpenDoor();
                _walls.Remove(obj.Pos);
            }
            yield break;
        }

        // --- 2. ÐÛ×ÀÃ ---
        if (obj.Type == ObjType.Switch)
        {
            // Ðû÷àã ðåàãèðóåò íà ôèçè÷åñêîå âîçäåéñòâèå (Air/Force)
            if (element == Element.Air || element == Element.Force || element == Element.Lightning)
            {
                obj.OpenDoor();

                // Ìàãèÿ ñâÿçåé: Îòêðûâàåì ïðèâÿçàííóþ äâåðü
                if (obj.LinkedObject != null && obj.LinkedObject.Type == ObjType.Door)
                {
                    Debug.Log("Door Unlocked via Switch!");
                    obj.LinkedObject.OpenDoor();
                    _walls.Remove(obj.LinkedObject.Pos);
                }
            }
            yield break;
        }

        // --- 3. ÑÓÍÄÓÊ ---
        if (obj.Type == ObjType.Chest)
        {
            Debug.Log($"LOOT FOUND: {obj.LootModuleID}");
            if (DungeonManager.Instance != null) DungeonManager.Instance.UnlockModule(obj.LootModuleID);
            InitLibrary();
            if (FindFirstObjectByType<GrimoireUI>()) FindFirstObjectByType<GrimoireUI>().RefreshButtons();
            if (UIManager.Instance != null) UIManager.Instance.ShowLootMessage(obj.LootModuleID);

            DestroyObject(obj.Pos);
            yield break;
        }

        if (obj.Type == ObjType.EarthWall || obj.Type == ObjType.Crate || obj.Type == ObjType.Barrel)
        {
            obj.TakeDamage(damage);
            if (obj.CurrentHP <= 0)
            {
                if (obj.Type == ObjType.Barrel && element == Element.Fire)
                {
                    yield return TriggerExplosion(obj.Pos, obj);
                }
                else
                {
                    DestroyObject(obj.Pos);
                }
                yield break;
            }
        }

        // --- 4. ÎÑÒÀËÜÍÎÅ (Áî÷êè, ßùèêè) ---
        if (element == Element.Ice)
        {
            if (!obj.IsFrozen) obj.Freeze();
            yield break;
        }
        if (element == Element.Fire)
        {
            if (obj.IsFrozen) obj.Unfreeze();
            yield break;
        }
        // Òîëêàåì òîëüêî åñëè ýòî íå äâåðü/ðû÷àã/ñóíäóê
        if (element == Element.Air || element == Element.Force) yield return PushObjectRoutine(obj);
    }

    int CountFloorNeighbors(GridPos pos)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                GridPos neighbor = new GridPos(pos.x + dx, pos.y + dy);
                if (_dungeonData.Floors.Contains(neighbor)) count++;
            }
        }
        return count;
    }

    bool IsRoomTile(GridPos pos)
    {
        return CountFloorNeighbors(pos) > 4;
    }

    bool IsModuleUnlocked(string moduleId)
    {
        if (DungeonManager.Instance == null) return false;
        switch (moduleId)
        {
            case "Grenade (Arc)":
            case "Raycast (Inst)":
                return DungeonManager.Instance.IsModuleUnlocked(moduleId);
            case "Cross":
            case "Laser Beam":
                return DungeonManager.Instance.IsModuleUnlocked(moduleId);
            case "Ice":
            case "Air":
                return DungeonManager.Instance.IsModuleUnlocked(moduleId);
            default:
                return true;
        }
    }

    string GetRandomLootModuleId()
    {
        List<string> candidates = new List<string>();
        foreach (string moduleId in _lootPool)
        {
            if (!IsModuleUnlocked(moduleId))
            {
                candidates.Add(moduleId);
            }
        }

        if (candidates.Count == 0)
        {
            candidates.AddRange(_lootPool);
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    IEnumerator PushObjectRoutine(InteractiveObject obj)
    {
        GridPos startPos = obj.Pos;

        // Âû÷èñëÿåì íàïðàâëåíèå îò ãåðîÿ ê îáúåêòó
        Vector3 dirVector = (GetWorldPos(startPos) - GetWorldPos(_heroPos)).normalized;
        int pushX = 0;
        int pushY = 0;
        if (Mathf.Abs(dirVector.x) > Mathf.Abs(dirVector.z))
            pushX = (int)Mathf.Sign(dirVector.x);
        else
            pushY = (int)Mathf.Sign(dirVector.z);

        Debug.Log($"Pushing Object Dir: {pushX}, {pushY}");

        int slideDistance = 4; // Ìàêñ äàëüíîñòü ïîëåòà
        bool hitSomething = false;

        // Óäàëÿåì îáúåêò èç ñòàðîé êëåòêè (ëîãè÷åñêè), ÷òîáû îí ìîã äâèãàòüñÿ
        _interactiveObjects.Remove(startPos);
        _walls.Remove(startPos);

        for (int i = 0; i < slideDistance; i++)
        {
            GridPos nextPos = new GridPos(obj.Pos.x + pushX, obj.Pos.y + pushY);

            // Ïðîâåðêà ñòîëêíîâåíèé
            if (!IsValid(nextPos) || _walls.Contains(nextPos))
            {
                hitSomething = true;
                Debug.Log("Object hit wall!");
                break;
            }

            // Ïîïàäàíèå âî âðàãà
            foreach (var enemy in _enemies)
            {
                if (enemy == null || !enemy.activeSelf) continue;
                if (GetGridPosFromWorld(enemy.transform.position) == nextPos)
                {
                    hitSomething = true;
                    Debug.Log("CRITICAL HIT! Object crushed Enemy!");
                    UnitStats stats = enemy.GetComponent<UnitStats>();
                    if (stats != null) stats.TakeDamage(30);
                    break;
                }
            }
            if (hitSomething) break;

            // Ïîïàäàíèå â ãåðîÿ
            if (nextPos == _heroPos)
            {
                hitSomething = true;
                Debug.Log("Friendly fire from object!");
                _heroStats.TakeDamage(10);
                break;
            }

            // Äâèæåíèå
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

        // Ôèíàë ïîëåòà
        if (hitSomething)
        {
            if (obj.Type == ObjType.Barrel && !obj.IsFrozen)
            {
                // Áî÷êà âðåçàëàñü - âçðûâ!
                yield return TriggerExplosion(obj.Pos, obj);
            }
            else
            {
                // Îáúåêò ðàçáèëñÿ
                Destroy(obj.gameObject);
            }
        }
        else
        {
            // Îñòàíîâèëñÿ íà íîâîé ïîçèöèè
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

    IEnumerator TriggerExplosion(GridPos center, InteractiveObject specificObject = null)
    {
        // Óíè÷òîæàåì ñàìó áî÷êó
        if (specificObject != null)
        {
            Destroy(specificObject.gameObject);
        }
        else if (_interactiveObjects.ContainsKey(center))
        {
            DestroyObject(center);
        }

        // Ñîçäàåì çîíó ïîðàæåíèÿ (3x3)
        List<GridPos> boomZone = new List<GridPos>();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                boomZone.Add(new GridPos(center.x + x, center.y + y));
            }
        }

        // Ýôôåêò âçðûâà
        foreach (var p in boomZone)
        {
            if (vfxExplosion != null)
            {
                GameObject boom = Instantiate(vfxExplosion, GetWorldPos(p) + Vector3.up * 0.5f, Quaternion.identity);
                Destroy(boom, 2f);
            }

            StartCoroutine(FlashTile(p, new Color(1f, 0.5f, 0f)));

            // Óðîí âðàãàì
            foreach (var enemy in _enemies)
            {
                if (enemy == null || !enemy.activeSelf) continue;
                if (GetGridPosFromWorld(enemy.transform.position) == p)
                {
                    UnitStats stats = enemy.GetComponent<UnitStats>();
                    if (stats != null) stats.TakeDamage(40);
                }
            }
            if (p == _heroPos)
            {
                _heroStats.TakeDamage(20);
            }

            // Öåïíàÿ ðåàêöèÿ!
            if (_interactiveObjects.ContainsKey(p))
            {
                yield return new WaitForSeconds(0.1f);
                yield return HitObject(_interactiveObjects[p], Element.Fire, 0);
            }
        }
    }

    // --- ÏÐÎÖÅÑÑÎÐ ÇÀÊËÈÍÀÍÈÉ (ÎÁÍÎÂËÅÍ) ---
    IEnumerator ProcessSpell(SpellBlueprint spell, GridPos targetCenter)
    {
        Color c = spell.VisualColor;
        float projectileSize = 0.3f * spell.PowerLevel;

        bool showVFX = !spell.IsOptimized;

        if (showVFX)
        {
            if (spell.Motion == MotionType.LinearProjectile || spell.Motion == MotionType.ArcingProjectile)
            {
                yield return ShootProjectile(_heroInstance, targetCenter, c, projectileSize);
            }
            else if (spell.Motion == MotionType.InstantRay)
            {
                if (spell.Shape == ShapeType.LineBeam && vfxForceBeam != null && _heroInstance != null)
                {
                    Vector3 start = _heroInstance.transform.position + Vector3.up * 0.8f;
                    Vector3 dir = (GetWorldPos(targetCenter) - GetWorldPos(_heroPos)).normalized;
                    Vector3 end = start + dir * 20f;

                    GameObject beam = Instantiate(vfxForceBeam, Vector3.zero, Quaternion.identity);
                    LaserFade fade = beam.GetComponent<LaserFade>();
                    if (fade != null)
                        fade.SetPositions(start, end);
                    else
                    {
                        LineRenderer lr = beam.GetComponent<LineRenderer>();
                        if (lr != null)
                        {
                            lr.SetPosition(0, start);
                            lr.SetPosition(1, end);
                        }
                    }
                    yield return new WaitForSeconds(0.2f);
                }
                else if (vfxForcePillar != null)
                {
                    GameObject pillar = Instantiate(vfxForcePillar, GetWorldPos(targetCenter), Quaternion.identity);
                    Destroy(pillar, 2.0f);
                }
                else
                {
                    yield return FlashTile(targetCenter, c);
                }
            }
        }
        else
        {
            yield return new WaitForSeconds(0.1f);
        }
        List<GridPos> affectedTiles = CalculateAffectedTiles(spell, targetCenter);

        foreach (GridPos tilePos in affectedTiles)
        {
            if (!IsValid(tilePos)) continue;
            if (showVFX)
                StartCoroutine(FlashTile(tilePos, c));
            else
                StartCoroutine(FlashTile(tilePos, new Color(c.r, c.g, c.b, 0.2f)));
        }

        switch (spell.MainElement)
        {
            case Element.Earth:
                yield return HandleEarth(spell, targetCenter, affectedTiles);
                break;
            case Element.Fire:
                yield return HandleFire(spell, affectedTiles);
                break;
            case Element.Ice:
                yield return HandleIce(spell, affectedTiles);
                break;
            case Element.Air:
                yield return HandleAir(spell, affectedTiles, targetCenter);
                break;
            case Element.Lightning:
                yield return HandleLightning(spell, affectedTiles);
                break;
        }
    }

    List<GridPos> CalculateAffectedTiles(SpellBlueprint spell, GridPos targetCenter)
    {
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
            Vector3 dirVector = (GetWorldPos(targetCenter) - GetWorldPos(_heroPos)).normalized;

            float dist = Mathf.Max(width, height) * 1.5f;
            Vector3 endWorld = GetWorldPos(_heroPos) + dirVector * (dist * tileSize);
            GridPos endGrid = GetGridPosFromWorld(endWorld);

            List<GridPos> rawLine = GetCellsOnLine(_heroPos, endGrid);
            foreach (var cell in rawLine)
            {
                if (cell == _heroPos) continue;
                if (!IsValid(cell)) break;
                if (_walls.Contains(cell) && !_interactiveObjects.ContainsKey(cell)) break;
                affectedTiles.Add(cell);
            }
        }

        return affectedTiles;
    }

    IEnumerator HandleEarth(SpellBlueprint spell, GridPos center, List<GridPos> affectedTiles)
    {
        foreach (GridPos tilePos in affectedTiles)
        {
            if (!IsValid(tilePos)) continue;

            StartCoroutine(FlashTile(tilePos, spell.VisualColor));

            if (spell.Shape == ShapeType.LineBeam)
            {
                ApplyDamageToTile(tilePos, 15 * spell.PowerLevel);
            }
            else if (spell.Shape == ShapeType.Cross)
            {
                if (tilePos == center)
                {
                    ApplyDamageToTile(tilePos, 10 * spell.PowerLevel);
                }
                else if (!IsTileOccupied(tilePos))
                {
                    CastEarthWall(tilePos, spell.PowerLevel);
                }
                else
                {
                    if (_interactiveObjects.ContainsKey(tilePos))
                    {
                        yield return HitObject(_interactiveObjects[tilePos], Element.Earth, 20 * spell.PowerLevel);
                    }
                    ApplyDamageToTile(tilePos, 15 * spell.PowerLevel);
                }
            }
            else
            {
                if (spell.Motion == MotionType.InstantRay)
                {
                    if (!IsTileOccupied(tilePos))
                    {
                        CastEarthWall(tilePos, spell.PowerLevel);
                    }
                    else
                    {
                        if (_interactiveObjects.ContainsKey(tilePos))
                        {
                            yield return HitObject(_interactiveObjects[tilePos], Element.Earth, 20 * spell.PowerLevel);
                        }
                        ApplyDamageToTile(tilePos, 15 * spell.PowerLevel);
                    }
                }
                else
                {
                    ApplyDamageToTile(tilePos, 20 * spell.PowerLevel);
                }
            }
        }
        yield return null;
    }

    IEnumerator HandleFire(SpellBlueprint spell, List<GridPos> affectedTiles)
    {
        foreach (GridPos tilePos in affectedTiles)
        {
            if (!IsValid(tilePos)) continue;

            yield return ApplyObjectHit(tilePos, spell);
            ApplyFireFloorReaction(tilePos);
            ApplyEnemyDamage(tilePos, spell, 1f);
        }
    }

    IEnumerator HandleIce(SpellBlueprint spell, List<GridPos> affectedTiles)
    {
        foreach (GridPos tilePos in affectedTiles)
        {
            if (!IsValid(tilePos)) continue;

            yield return ApplyObjectHit(tilePos, spell);
            ApplyIceFloorReaction(tilePos);
            ApplyEnemyDamage(tilePos, spell, 1f);
        }
    }

    IEnumerator HandleAir(SpellBlueprint spell, List<GridPos> affectedTiles, GridPos targetCenter)
    {
        foreach (GridPos tilePos in affectedTiles)
        {
            if (!IsValid(tilePos)) continue;

            yield return ApplyObjectHit(tilePos, spell);

            foreach (var enemy in _enemies)
            {
                if (enemy == null || !enemy.activeSelf) continue;
                GridPos enPos = GetGridPosFromWorld(enemy.transform.position);
                if (tilePos != enPos) continue;

                int damage = 2;
                UnitStats enemyStats = enemy.GetComponent<UnitStats>();
                if (enemyStats != null) enemyStats.TakeDamage(damage);
                enemy.SetActive(true);

                if (_heroInstance == null) continue;

                Vector3 pushDirVector = Vector3.zero;
                if (spell.Motion == MotionType.ArcingProjectile)
                {
                    pushDirVector = (enemy.transform.position - GetWorldPos(targetCenter)).normalized;
                    if (pushDirVector == Vector3.zero)
                    {
                        pushDirVector = (enemy.transform.position - _heroInstance.transform.position).normalized;
                    }
                }
                else
                {
                    pushDirVector = (enemy.transform.position - _heroInstance.transform.position).normalized;
                }

                int px = 0;
                int py = 0;
                if (Mathf.Abs(pushDirVector.x) > Mathf.Abs(pushDirVector.z)) px = (int)Mathf.Sign(pushDirVector.x);
                else py = (int)Mathf.Sign(pushDirVector.z);

                GridPos pushDest = new GridPos(enPos.x + px, enPos.y + py);

                Debug.Log($"Air Hit! Pushing Enemy to [{pushDest.x}, {pushDest.y}]");

                bool isBlockedByWall = _walls.Contains(pushDest) && !_interactiveObjects.ContainsKey(pushDest);
                bool isBlockedByUnit = (pushDest == _heroPos);

                if (IsValid(pushDest) && !isBlockedByWall && !isBlockedByUnit)
                {
                    yield return MoveUnit(enemy, pushDest);
                }
                else
                {
                    Debug.Log("Push Blocked! Bonus Wall Damage.");
                    if (enemyStats != null)
                    {
                        enemyStats.TakeDamage(15);
                    }
                    StartCoroutine(FlashTile(tilePos, Color.white));
                }
            }
        }
    }

    IEnumerator HandleLightning(SpellBlueprint spell, List<GridPos> affectedTiles)
    {
        foreach (GridPos tilePos in affectedTiles)
        {
            if (!IsValid(tilePos)) continue;

            yield return ApplyObjectHit(tilePos, spell);
            ApplyLightningElectrolysis(tilePos);
            ApplyLightningDamage(tilePos, spell);
        }
    }

    bool IsTileOccupied(GridPos tilePos)
    {
        if (_walls.Contains(tilePos) || _interactiveObjects.ContainsKey(tilePos)) return true;
        foreach (var enemy in _enemies)
        {
            if (enemy != null && enemy.activeSelf && GetGridPosFromWorld(enemy.transform.position) == tilePos)
            {
                return true;
            }
        }
        return false;
    }

    void ApplyDamageToTile(GridPos tilePos, int damage)
    {
        foreach (var enemy in _enemies)
        {
            if (enemy == null || !enemy.activeSelf) continue;
            if (GetGridPosFromWorld(enemy.transform.position) == tilePos)
            {
                UnitStats enemyStats = enemy.GetComponent<UnitStats>();
                if (enemyStats != null) enemyStats.TakeDamage(damage);
                enemy.SetActive(true);
            }
        }
        if (_heroPos == tilePos) _heroStats.TakeDamage(damage);
    }

    IEnumerator ApplyObjectHit(GridPos tilePos, SpellBlueprint spell)
    {
        if (_interactiveObjects.ContainsKey(tilePos))
        {
            yield return HitObject(_interactiveObjects[tilePos], spell.MainElement, 10 * spell.PowerLevel);
        }
    }

    bool TryDamageEnemyAtTile(GridPos tilePos, int damage)
    {
        foreach (var enemy in _enemies)
        {
            if (enemy == null || !enemy.activeSelf) continue;
            if (GetGridPosFromWorld(enemy.transform.position) != tilePos) continue;

            UnitStats enemyStats = enemy.GetComponent<UnitStats>();
            if (enemyStats != null) enemyStats.TakeDamage(damage);
            enemy.SetActive(true);
            return true;
        }
        return false;
    }

    void ApplyEnemyDamage(GridPos tilePos, SpellBlueprint spell, float baseScale)
    {
        float mult = spell.PowerLevel == 2 ? 1.5f : (spell.PowerLevel == 3 ? 2.5f : 1f);
        int damage = Mathf.RoundToInt(10 * mult * baseScale);
        if (spell.MainElement == Element.Air) damage = 2;
        TryDamageEnemyAtTile(tilePos, damage);
    }

    void ApplyFireFloorReaction(GridPos tilePos)
    {
        if (_tileDataMap.ContainsKey(tilePos))
        {
            TileData tile = _tileDataMap[tilePos];
            if (tile.CurrentElement == Element.Ice)
            {
                tile.CurrentElement = Element.Water;
                _walls.Remove(tilePos);
                ResetTileColor(tilePos);
            }
        }
    }

    void ApplyIceFloorReaction(GridPos tilePos)
    {
        if (_tileDataMap.ContainsKey(tilePos))
        {
            TileData tile = _tileDataMap[tilePos];
            if (tile.CurrentElement == Element.Water)
            {
                tile.CurrentElement = Element.Ice;
                _walls.Add(tilePos);
                ResetTileColor(tilePos);
                CastIceWall(tilePos);
            }
            else if (tile.CurrentElement == Element.Fire)
            {
                tile.CurrentElement = Element.None;
                ResetTileColor(tilePos);
            }
        }
    }

    void ApplyLightningElectrolysis(GridPos tilePos)
    {
        if (_tileDataMap.ContainsKey(tilePos) && _tileDataMap[tilePos].CurrentElement == Element.Water)
        {
            List<GridPos> wetTiles = GetConnectedWater(tilePos);
            Debug.Log($"Electrocuted {wetTiles.Count} water tiles!");

            foreach (var wetTile in wetTiles)
            {
                StartCoroutine(FlashTile(wetTile, Color.yellow));

                foreach (var enemy in _enemies)
                {
                    if (enemy != null && enemy.activeSelf && GetGridPosFromWorld(enemy.transform.position) == wetTile)
                    {
                        enemy.GetComponent<UnitStats>().TakeDamage(20);
                    }
                }
                if (_heroPos == wetTile) _heroStats.TakeDamage(10);
            }
        }
    }

    void ApplyLightningDamage(GridPos tilePos, SpellBlueprint spell)
    {
        foreach (var enemy in _enemies)
        {
            if (enemy == null || !enemy.activeSelf) continue;
            if (GetGridPosFromWorld(enemy.transform.position) != tilePos) continue;

            float mult = spell.PowerLevel == 2 ? 1.5f : (spell.PowerLevel == 3 ? 2.5f : 1f);
            int damage = Mathf.RoundToInt(10 * mult);
            UnitStats enemyStats = enemy.GetComponent<UnitStats>();
            if (enemyStats != null) enemyStats.TakeDamage(damage);
            enemy.SetActive(true);
            StartCoroutine(ChainLightningRoutine(enemy, spell.PowerLevel));
        }
    }

    List<GridPos> GetCellsOnLine(GridPos start, GridPos end)
    {
        List<GridPos> line = new List<GridPos>();

        int x0 = start.x; int y0 = start.y;
        int x1 = end.x; int y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            line.Add(new GridPos(x0, y0));

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        return line;
    }

    void CompileSpellToSlot(int slotIndex)
    {
        if (WorkMotion == null || WorkShape == null || WorkElement == null) return;
        string spellName = $"{WorkElement.Name} {WorkShape.Name}";
        if (WorkPowerLevel == 3) spellName = $"MAX {WorkElement.Name}";
        if (WorkIsOptimized) spellName = "Invisible " + spellName;
        int baseMana = WorkMotion.ManaCost + WorkShape.ManaCost;
        int baseHeat = WorkMotion.HeatCost + WorkShape.HeatCost;
        int finalMana = baseMana * WorkPowerLevel;
        int finalHeat = (int)(baseHeat * Mathf.Pow(WorkPowerLevel, 1.5f));
        if (WorkIsOptimized) finalMana = Mathf.RoundToInt(finalMana * 0.7f);
        SpellBlueprint newSpell = new SpellBlueprint(
            spellName,
            WorkElement.ElementData,
            WorkMotion.MotionData,
            WorkShape.ShapeData,
            WorkPowerLevel,
            WorkIsOptimized,
            finalMana,
            finalHeat,
            WorkElement.VisualColor
        );
        while (Spellbook.Count <= slotIndex) Spellbook.Add(null);
        Spellbook[slotIndex] = newSpell;

        if (DungeonManager.Instance != null)
        {
            while (DungeonManager.Instance.SavedSpellbook.Count <= slotIndex)
            {
                DungeonManager.Instance.SavedSpellbook.Add(null);
            }
            DungeonManager.Instance.SavedSpellbook[slotIndex] = newSpell;
            Debug.Log("Grimoire Saved to DungeonManager.");
        }
    }

    // --- PLANNING & INPUT ---
    void TryPlanCommand(GridPos targetPos)
    {
        GridPos currentPlanEnd = _heroPos;
        if (CommandQueue.Count > 0)
        {
            for (int i = CommandQueue.Count - 1; i >= 0; i--)
            {
                if (CommandQueue[i].Type == "MOVE")
                {
                    currentPlanEnd = CommandQueue[i].TargetPos;
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
                CommandQueue.Add(new ActionCommand("MOVE", targetPos, path));
                foreach (GridPos step in path) _gridVisuals[step].GetComponent<Renderer>().material.color = Color.green;
                SpawnMarker(targetPos, Color.cyan);
            }
        }
        else
        {
            if (_selectedSpellIndex >= Spellbook.Count || Spellbook[_selectedSpellIndex] == null) return;
            SpellBlueprint spell = Spellbook[_selectedSpellIndex];
            if (targetPos == currentPlanEnd && spell.Motion != MotionType.SelfBuff) return;
            if (spell.Motion != MotionType.ArcingProjectile && spell.Shape != ShapeType.LineBeam)
            {
                if (!HasLineOfSight(currentPlanEnd, targetPos))
                {
                    StartCoroutine(FlashTile(targetPos, Color.gray));
                    return;
                }
            }
            CommandQueue.Add(new ActionCommand("SPELL", targetPos, null, spell));
            SpawnMarker(targetPos, spell.VisualColor);
        }
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0) && !Input.GetMouseButton(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray);
            foreach (var hit in hits)
            {
                TileData tile = hit.collider.GetComponent<TileData>();
                if (tile != null)
                {
                    TryPlanCommand(tile.Pos);
                    break;
                }
            }
        }
    }

    // --- EXECUTION ---
    IEnumerator ExecutePlayerTurn()
    {
        _currentState = BattleState.PlayerExecuting;
        foreach (ActionCommand cmd in CommandQueue)
        {
            if (cmd.Type == "WAIT")
            {
                yield return new WaitForSeconds(0.5f);
                if (_tileDataMap.ContainsKey(_heroPos) && _tileDataMap[_heroPos].CurrentElement == Element.Fire)
                {
                    _heroStats.TakeDamage(10);
                }
                _heroStats.CoolDown(5);
            }
            else if (cmd.Type == "MOVE")
            {
                foreach (GridPos step in cmd.Path)
                {
                    yield return MoveUnit(_heroInstance, step);
                    _heroPos = step;
                    ResetTileColor(step);
                    CheckEnemyProximity();
                    if (fogOfWar != null) fogOfWar.UpdateFog(_heroPos);
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
        bool enemiesRemain = false;
        foreach (var enemy in _enemies)
        {
            if (enemy == null) continue;
            UnitStats stats = enemy.GetComponent<UnitStats>();
            if (stats != null && stats.currentHP > 0)
            {
                enemiesRemain = true;
                break;
            }
        }

        if (!enemiesRemain)
        {
            _currentState = BattleState.Won;
            Debug.Log("VICTORY STATE REACHED!");
        }
        else
        {
            StartCoroutine(ExecuteEnemyTurn());
        }
    }

    IEnumerator ExecuteEnemyTurn()
    {
        _currentState = BattleState.EnemyExecuting;
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < _enemies.Count; i++)
        {
            if (!_enemies[i].activeSelf || _enemyStatsList[i].currentHP <= 0) continue;
            GridPos myPos = GetGridPosFromWorld(_enemies[i].transform.position);
            List<ActionCommand> aiMoves = _enemyAIList[i].PlanTurn(myPos, _heroPos, _walls);
            foreach (ActionCommand cmd in aiMoves)
            {
                if (cmd.Type == "MOVE")
                {
                    foreach (GridPos step in cmd.Path)
                    {
                        yield return MoveUnit(_enemies[i], step);
                    }
                }
                else if (cmd.Type == "ATTACK")
                {
                    _enemies[i].transform.LookAt(GetWorldPos(cmd.TargetPos));
                    _heroStats.TakeDamage(_enemyAIList[i].damage);
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
        GameObject prefab = GetProjectileVfxPrefab(color);
        if (prefab == null)
        {
            Debug.LogWarning("Projectile VFX prefab is not assigned.");
            yield break;
        }
        GameObject projectile = Instantiate(prefab);
        projectile.transform.localScale = projectile.transform.localScale * size;
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

    GameObject GetProjectileVfxPrefab(Color color)
    {
        if (color == Color.cyan) return vfxIceWall;
        if (color == Color.white) return vfxForceBeam;
        return vfxFireball;
    }

    void CastIceWall(GridPos targetPos)
    {
        if (vfxIceWall == null)
        {
            Debug.LogWarning("Ice wall VFX prefab is not assigned.");
            return;
        }
        Vector3 spawnPos = GetWorldPos(targetPos);
        GameObject wall = Instantiate(vfxIceWall, spawnPos, Quaternion.identity);
        _dynamicWalls.Add(wall);
    }

    void CastEarthWall(GridPos targetPos, int powerLevel)
    {
        if (earthWallPrefab == null)
        {
            Debug.LogWarning("Earth wall prefab is not assigned.");
            return;
        }
        bool isOccupied = _walls.Contains(targetPos) || _interactiveObjects.ContainsKey(targetPos);
        foreach (var enemy in _enemies)
        {
            if (enemy != null && enemy.activeSelf && GetGridPosFromWorld(enemy.transform.position) == targetPos)
            {
                isOccupied = true;
                break;
            }
        }
        if (targetPos == _heroPos) isOccupied = true;

        if (isOccupied) return;

        GameObject obj = Instantiate(earthWallPrefab, GetWorldPos(targetPos) + Vector3.up * 0.5f, Quaternion.identity);
        InteractiveObject interact = obj.GetComponent<InteractiveObject>();
        if (interact == null) interact = obj.AddComponent<InteractiveObject>();

        interact.Type = ObjType.EarthWall;
        interact.Pos = targetPos;
        interact.InitHealth(30 * powerLevel);
        interact.UpdateColor();

        _interactiveObjects.Add(targetPos, interact);
        _walls.Add(targetPos);

        if (fogOfWar != null) fogOfWar.UpdateFog(_heroPos);
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

        if (_tileDataMap.ContainsKey(targetStep) && unit != null)
        {
            Element floor = _tileDataMap[targetStep].CurrentElement;
            if (floor == Element.Fire)
            {
                UnitStats stats = unit.GetComponent<UnitStats>();
                if (stats != null) stats.TakeDamage(10);
                Debug.Log($"{unit.name} burned by Magma!");
            }
        }
    }

    public void UpdateEnemyVisibility(List<GridPos> visibleTiles)
    {
        if (visibleTiles == null) return;

        foreach (var enemy in _enemies)
        {
            if (enemy == null) continue;

            UnitStats stats = enemy.GetComponent<UnitStats>();
            if (stats != null && stats.currentHP <= 0)
            {
                enemy.SetActive(false);
                continue;
            }

            GridPos enemyPos = GetGridPosFromWorld(enemy.transform.position);
            enemy.SetActive(visibleTiles.Contains(enemyPos));
        }
    }

    public void RevealAllEnemies()
    {
        foreach (var enemy in _enemies)
        {
            if (enemy == null) continue;

            UnitStats stats = enemy.GetComponent<UnitStats>();
            if (stats != null && stats.currentHP <= 0) continue;

            enemy.SetActive(true);
        }
    }

    public bool HasLineOfSightLogic(GridPos start, GridPos end)
    {
        return HasLineOfSight(start, end);
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
    public bool IsValidForMove(GridPos p, HashSet<GridPos> currentWalls)
    {
        if (!IsValid(p)) return false; // За пределами карты
        if (currentWalls.Contains(p)) return false; // Стена
        if (_interactiveObjects.ContainsKey(p)) return false; // Ящик/Дверь
        // Проверка: не занято ли другим врагом?
        foreach (var e in _enemies)
        {
            if (e.activeSelf && GetGridPosFromWorld(e.transform.position) == p) return false;
        }
        if (_heroPos == p) return false; // Не вставать на героя
        return true;
    }
    void CheckEnemyProximity()
    {
        foreach (var enemy in _enemies)
        {
            if (enemy == null) continue;
            if (Vector3.Distance(GetWorldPos(_heroPos), enemy.transform.position) < tileSize * 3)
                enemy.SetActive(true);
        }
    }

    GridPos GetGridPosFromWorld(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / tileSize);
        int y = Mathf.RoundToInt(worldPos.z / tileSize); // Use Z for grid Y coordinate.
        return new GridPos(x, y);
    }

    Vector3 GetWorldPos(GridPos pos) => new Vector3(pos.x * tileSize, 0, pos.y * tileSize);

    GridPos FindValidSpawnPos()
    {
        int attempts = 100;
        while (attempts > 0)
        {
            GridPos p = new GridPos(Random.Range(0, width), Random.Range(0, height));
            if (!_walls.Contains(p)) return p;
            attempts--;
        }
        return new GridPos(width - 1, height - 1);
    }

    GridPos FindValidSpawnPosFromList()
    {
        int attempts = 50;
        while (attempts > 0)
        {
            if (_validFloors.Count == 0) return new GridPos(-1, -1);

            GridPos candidate = _validFloors[Random.Range(0, _validFloors.Count)];
            bool isOccupied = candidate == _heroPos
                              || _walls.Contains(candidate)
                              || _interactiveObjects.ContainsKey(candidate);
            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;
                if (GetGridPosFromWorld(enemy.transform.position) == candidate) isOccupied = true;
            }

            if (!isOccupied) return candidate;
            attempts--;
        }

        return new GridPos(-1, -1);
    }

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
        CommandQueue.Clear();
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

        if (_walls.Contains(pos) && !_interactiveObjects.ContainsKey(pos) && el != Element.Ice)
        {
            rend.material.color = Color.black;
            return;
        }
        if (el == Element.Water)
            rend.material.color = Color.blue;
        else if (el == Element.Fire)
            rend.material.color = new Color(1f, 0.5f, 0f);
        else if (el == Element.Ice)
            rend.material.color = Color.cyan;
        else
        {
            bool isDark = (pos.x + pos.y) % 2 == 0;
            rend.material.color = isDark ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
        }
    }

    void UpdateUI()
    {
        if (UIManager.Instance == null) return;

        if (_heroStats != null)
        {
            UIManager.Instance.UpdateStats(
                _heroStats.currentHP,
                _heroStats.currentMana,
                _heroStats.currentHeat,
                _heroStats.maxHeat,
                _heroStats.isOverheated
            );
        }

        UIManager.Instance.UpdatePipeline(CommandQueue);
        UIManager.Instance.UpdateSpellDeck(Spellbook, _selectedSpellIndex);
        UIManager.Instance.ToggleGrimoire(_isGrimoireOpen);

        string motionName = WorkMotion != null ? WorkMotion.Name : "-";
        string shapeName = WorkShape != null ? WorkShape.Name : "-";
        string elementName = WorkElement != null ? WorkElement.Name : "-";
        int previewMana = 0;
        int previewHeat = 0;
        if (WorkMotion != null && WorkShape != null)
        {
            int baseMana = WorkMotion.ManaCost + WorkShape.ManaCost;
            int baseHeat = WorkMotion.HeatCost + WorkShape.HeatCost;
            previewMana = baseMana * WorkPowerLevel;
            previewHeat = (int)(baseHeat * Mathf.Pow(WorkPowerLevel, 1.5f));
        }
        if (WorkIsOptimized)
        {
            previewMana = Mathf.RoundToInt(previewMana * 0.7f);
            if (elementName != "-") elementName += " (OPT)";
        }

        UIManager.Instance.UpdateGrimoirePreview(
            motionName,
            shapeName,
            elementName,
            previewMana,
            previewHeat
        );

    }

    // --- ЛОГИКА МОЛНИИ ---
    List<GridPos> GetConnectedWater(GridPos start)
    {
        List<GridPos> connected = new List<GridPos>();
        if (!_tileDataMap.ContainsKey(start) || _tileDataMap[start].CurrentElement != Element.Water)
            return connected;

        Queue<GridPos> queue = new Queue<GridPos>();
        queue.Enqueue(start);
        HashSet<GridPos> visited = new HashSet<GridPos> { start };

        while (queue.Count > 0)
        {
            GridPos current = queue.Dequeue();
            connected.Add(current);

            GridPos[] neighbors =
            {
                new GridPos(current.x + 1, current.y), new GridPos(current.x - 1, current.y),
                new GridPos(current.x, current.y + 1), new GridPos(current.x, current.y - 1)
            };

            foreach (var n in neighbors)
            {
                if (IsValid(n) && !visited.Contains(n)
                    && _tileDataMap.ContainsKey(n) && _tileDataMap[n].CurrentElement == Element.Water)
                {
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
        }
        return connected;
    }

    GameObject FindNextChainTarget(Vector3 currentPos, List<GameObject> hitTargets, float range)
    {
        GameObject bestTarget = null;
        float closestDist = range;

        foreach (var enemy in _enemies)
        {
            if (enemy == null || !enemy.activeSelf || hitTargets.Contains(enemy)) continue;

            float d = Vector3.Distance(currentPos, enemy.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                bestTarget = enemy;
            }
        }
        return bestTarget;
    }

    IEnumerator ChainLightningRoutine(GameObject startTarget, int bounces)
    {
        List<GameObject> hitTargets = new List<GameObject> { startTarget };

        GameObject current = startTarget;

        for (int i = 0; i < bounces; i++)
        {
            yield return new WaitForSeconds(0.1f);

            GameObject next = FindNextChainTarget(current.transform.position, hitTargets, tileSize * 4);

            if (next != null)
            {
                if (vfxForceBeam != null)
                {
                    GameObject beam = Instantiate(vfxForceBeam, Vector3.zero, Quaternion.identity);
                    LineRenderer lr = beam.GetComponent<LineRenderer>();
                    if (lr)
                    {
                        lr.startColor = Color.yellow;
                        lr.endColor = Color.yellow;
                        lr.SetPosition(0, current.transform.position + Vector3.up);
                        lr.SetPosition(1, next.transform.position + Vector3.up);
                    }
                    Destroy(beam, 0.3f);
                }

                next.GetComponent<UnitStats>().TakeDamage(10);
                hitTargets.Add(next);
                current = next;
            }
            else
            {
                break;
            }
        }
    }
}
