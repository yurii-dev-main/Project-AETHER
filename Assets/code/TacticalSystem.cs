using UnityEngine; 
using System.Collections;
using System.Collections.Generic;

// --- ТИПЫ ДАННЫХ (Enum, Classes) ---
public enum Element { None, Fire, Water, Ice, Earth, Air } 
public enum MotionType { LinearProjectile, ArcingProjectile, InstantRay, SelfBuff }
public enum ShapeType { SingleTile, Cross, LineBeam } 

[System.Serializable]
public class SpellBlueprint {
    public string Name; public Element MainElement; public MotionType Motion; public ShapeType Shape;
    public int PowerLevel; public bool IsOptimized;
    public int ManaCost; public int HeatCost; public Color VisualColor;

    public SpellBlueprint(string name, Element el, MotionType mot, ShapeType sh, int power, bool optimized, int mana, int heat, Color col) {
        Name = name; MainElement = el; Motion = mot; Shape = sh; PowerLevel = power; IsOptimized = optimized;
        ManaCost = mana; HeatCost = heat; VisualColor = col;
    }
}

[System.Serializable]
public class SpellModule {
    public string Name; public string Type; public int ManaCost; public int HeatCost;
    public MotionType MotionData; public ShapeType ShapeData; public Element ElementData; public Color VisualColor;

    public static SpellModule CreateMotion(string name, MotionType m, int mana, int heat) { return new SpellModule { Name = name, Type = "MOTION", MotionData = m, ManaCost = mana, HeatCost = heat }; }
    public static SpellModule CreateShape(string name, ShapeType s, int mana, int heat) { return new SpellModule { Name = name, Type = "SHAPE", ShapeData = s, ManaCost = mana, HeatCost = heat }; }
    public static SpellModule CreateElement(string name, Element e, Color c) { return new SpellModule { Name = name, Type = "ELEMENT", ElementData = e, VisualColor = c }; }
}

public class ActionCommand {
    public string Type; public GridPos TargetPos; public List<GridPos> Path; public SpellBlueprint SpellData; 
    public ActionCommand(string type, GridPos target, List<GridPos> path = null, SpellBlueprint spell = null) { Type = type; TargetPos = target; Path = path; SpellData = spell; }
}

public enum BattleState { PlayerPlanning, PlayerExecuting, EnemyExecuting, Won, Lost }
public class TileData : MonoBehaviour { public GridPos Pos; public Element CurrentElement = Element.None; }

// --- ГЛАВНЫЙ КЛАСС ---

public class TacticalSystem : MonoBehaviour {
    [Header("Map Settings")]
    public int width = 20; public int height = 15; public float tileSize = 1.1f;
    [Range(0, 100)] public int wallChance = 5; 
    [Range(0, 100)] public int puddleChance = 10; 
    [Range(0, 100)] public int objectChance = 15; 

    [Header("VFX")]
    public GameObject vfxFireball; public GameObject vfxIceWall; public GameObject vfxForceBeam; public GameObject vfxForcePillar; public GameObject vfxExplosion;

    [Header("Prefabs")]
    public GameObject tilePrefab; public GameObject heroPrefab; public GameObject enemyPrefab; public GameObject markerPrefab; 
    public GameObject cratePrefab; public GameObject barrelPrefab; public GameObject doorPrefab;

    public Transform HeroTransform => _heroInstance != null ? _heroInstance.transform : null;
    public Vector3 GetGridCenter() => new Vector3(width * tileSize / 2f - tileSize/2, 0, height * tileSize / 2f - tileSize/2);

    // Данные
    private Dictionary<GridPos, GameObject> _gridVisuals = new Dictionary<GridPos, GameObject>();
    private Dictionary<GridPos, TileData> _tileDataMap = new Dictionary<GridPos, TileData>();
    private HashSet<GridPos> _walls = new HashSet<GridPos>();
    private Dictionary<GridPos, InteractiveObject> _interactiveObjects = new Dictionary<GridPos, InteractiveObject>();
    private List<GameObject> _dynamicWalls = new List<GameObject>();

    private GridPos _heroPos = new GridPos(1, 1);
    
    private List<GameObject> _enemies = new List<GameObject>();
    private List<UnitStats> _enemyStatsList = new List<UnitStats>();
    private List<EnemyAI> _enemyAIList = new List<EnemyAI>();
    
    private GameObject _heroInstance;
    private UnitStats _heroStats;

    public List<ActionCommand> CommandQueue = new List<ActionCommand>();
    public List<SpellBlueprint> Spellbook = new List<SpellBlueprint>();
    
    public SpellModule WorkMotion; public SpellModule WorkShape; public SpellModule WorkElement;
    public int WorkPowerLevel = 1; public bool WorkIsOptimized = false;

    private List<SpellModule> _libraryMotion = new List<SpellModule>();
    private List<SpellModule> _libraryShape = new List<SpellModule>();
    private List<SpellModule> _libraryElement = new List<SpellModule>();

    public List<SpellModule> GetMotionLibrary() => _libraryMotion;
    public List<SpellModule> GetShapeLibrary() => _libraryShape;
    public List<SpellModule> GetElementLibrary() => _libraryElement;

    private List<GameObject> _spawnedMarkers = new List<GameObject>();
    private BattleState _currentState = BattleState.PlayerPlanning;
    
    // Флаг, чтобы показать победу только один раз
    private bool _victoryShown = false;

    private int _selectedSpellIndex = -1;
    private bool _isGrimoireOpen = false; 
    private int _currentLevel = 1;
    private string[] _lootPool = { "Grenade (Arc)", "Raycast (Inst)", "Cross", "Laser Beam", "Ice", "Air" };

    public MapGenerator mapGenerator;
    public FogOfWar fogOfWar;

    void Start() {
        if (DungeonManager.Instance != null) {
            _currentLevel = DungeonManager.Instance.CurrentLevel;
            Spellbook = DungeonManager.Instance.SavedSpellbook;
        } else {
            Spellbook.Add(new SpellBlueprint("Temp Fire", Element.Fire, MotionType.LinearProjectile, ShapeType.SingleTile, 1, false, 10, 5, Color.red));
        }

        InitLibrary();
        WorkMotion = _libraryMotion[0]; WorkShape = _libraryShape[0]; WorkElement = _libraryElement[0]; WorkPowerLevel = 1;

        GenerateGrid();
        SpawnUnits();
    }

    void InitLibrary() {
        _libraryMotion.Clear(); _libraryShape.Clear(); _libraryElement.Clear();
        bool IsUnlocked(string name) { if (DungeonManager.Instance == null) return false; return DungeonManager.Instance.IsModuleUnlocked(name); }

        if(IsUnlocked("Projectile")) _libraryMotion.Add(SpellModule.CreateMotion("Projectile", MotionType.LinearProjectile, 5, 2));
        if(IsUnlocked("Grenade (Arc)")) _libraryMotion.Add(SpellModule.CreateMotion("Grenade (Arc)", MotionType.ArcingProjectile, 10, 5));
        if(IsUnlocked("Raycast (Inst)")) _libraryMotion.Add(SpellModule.CreateMotion("Raycast (Inst)", MotionType.InstantRay, 10, 5));

        if(IsUnlocked("Point")) _libraryShape.Add(SpellModule.CreateShape("Point", ShapeType.SingleTile, 0, 0));
        if(IsUnlocked("Cross")) _libraryShape.Add(SpellModule.CreateShape("Cross", ShapeType.Cross, 10, 5));
        if(IsUnlocked("Laser Beam")) _libraryShape.Add(SpellModule.CreateShape("Laser Beam", ShapeType.LineBeam, 15, 10));

        if(IsUnlocked("Fire")) _libraryElement.Add(SpellModule.CreateElement("Fire", Element.Fire, Color.red));
        if(IsUnlocked("Ice")) _libraryElement.Add(SpellModule.CreateElement("Ice", Element.Ice, Color.cyan));
        if(IsUnlocked("Air")) _libraryElement.Add(SpellModule.CreateElement("Air", Element.Air, Color.white));
    }

    void Update() {
        // UI SYNC - каждый кадр обновляем статы и деку
        if (UIManager.Instance != null && _heroStats != null) {
            UIManager.Instance.UpdateStats(_heroStats.currentHP, _heroStats.currentMana, _heroStats.currentHeat, _heroStats.maxHeat, _heroStats.isOverheated);
            UIManager.Instance.UpdatePipeline(CommandQueue);
            UIManager.Instance.UpdateSpellDeck(Spellbook, _selectedSpellIndex);
        }

        // БЛОКИРОВКА ПРИ ПОБЕДЕ/ПОРАЖЕНИИ
        if (_currentState == BattleState.Won || _currentState == BattleState.Lost) {
            // ИСПРАВЛЕНИЕ: Вызываем ShowVictory только ОДИН раз
            if (!_victoryShown && UIManager.Instance != null) {
                UIManager.Instance.ShowVictory(_currentState == BattleState.Won);
                _victoryShown = true;
            }
            
            // Если игрок нажал R в конце игры - тоже обрабатываем
            if(Input.GetKeyDown(KeyCode.R)) {
                if (DungeonManager.Instance != null) {
                    if (_currentState == BattleState.Won) DungeonManager.Instance.CompleteLevel(_heroStats.currentHP, _heroStats.currentMana, _heroStats.currentHeat);
                    else DungeonManager.Instance.RestartGame();
                } else UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab)) {
            _isGrimoireOpen = !_isGrimoireOpen;
            if (UIManager.Instance != null) UIManager.Instance.ToggleGrimoire(_isGrimoireOpen);
        }

        if (_isGrimoireOpen) return; 
        if (_currentState != BattleState.PlayerPlanning) return;

        if (Input.GetKeyDown(KeyCode.Escape)) _selectedSpellIndex = -1;
        for (int i = 0; i < 9; i++) { if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { if (i < Spellbook.Count) _selectedSpellIndex = i; } }

        HandleMouseInput();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
            if (CommandQueue.Count == 0) CommandQueue.Add(new ActionCommand("WAIT", _heroPos));
            StartCoroutine(ExecutePlayerTurn());
        }
        if (Input.GetMouseButtonDown(1)) ClearQueue();
    }

    // --- UI METHODS ---
    public void UI_SelectModule(int typeIndex, int index) {
        if (typeIndex == 0 && index < _libraryMotion.Count) WorkMotion = _libraryMotion[index];
        if (typeIndex == 1 && index < _libraryShape.Count) WorkShape = _libraryShape[index];
        if (typeIndex == 2 && index < _libraryElement.Count) WorkElement = _libraryElement[index];
    }
    public void UI_SetPower(float val) { WorkPowerLevel = Mathf.RoundToInt(val); }
    public void UI_Compile(int slot) { CompileSpellToSlot(slot); }
    public void UI_ToggleOptimization(bool isOpt) { WorkIsOptimized = isOpt; }

    void CompileSpellToSlot(int slotIndex) {
        if (WorkMotion == null || WorkShape == null || WorkElement == null) return;
        string spellName = $"{WorkElement.Name} {WorkShape.Name}"; 
        if (WorkPowerLevel == 3) spellName = $"MAX {WorkElement.Name}"; 
        if (WorkIsOptimized) spellName = "Invisible " + spellName;

        int baseMana = WorkMotion.ManaCost + WorkShape.ManaCost;
        int baseHeat = WorkMotion.HeatCost + WorkShape.HeatCost;
        int finalMana = baseMana * WorkPowerLevel;
        int finalHeat = (int)(baseHeat * Mathf.Pow(WorkPowerLevel, 1.5f));
        if (WorkIsOptimized) finalMana = Mathf.RoundToInt(finalMana * 0.7f);

        SpellBlueprint newSpell = new SpellBlueprint(spellName, WorkElement.ElementData, WorkMotion.MotionData, WorkShape.ShapeData, WorkPowerLevel, WorkIsOptimized, finalMana, finalHeat, WorkElement.VisualColor);

        while (Spellbook.Count <= slotIndex) Spellbook.Add(null);
        Spellbook[slotIndex] = newSpell;

        if (DungeonManager.Instance != null) {
            while (DungeonManager.Instance.SavedSpellbook.Count <= slotIndex) DungeonManager.Instance.SavedSpellbook.Add(null);
            DungeonManager.Instance.SavedSpellbook[slotIndex] = newSpell;
        }
    }

    // --- ЛОГИКА ---
    void GenerateGrid() {
        _gridVisuals.Clear(); _walls.Clear(); _tileDataMap.Clear();
        foreach(var kvp in _interactiveObjects) Destroy(kvp.Value.gameObject); _interactiveObjects.Clear();
        foreach(var obj in _dynamicWalls) Destroy(obj); _dynamicWalls.Clear();
        Transform oldBoard = transform.Find("Board"); if(oldBoard != null) Destroy(oldBoard.gameObject);
        
        // Генератор подземелья
        var data = mapGenerator.Generate(width, height);
        
        GameObject boardHolder = new GameObject("Board"); boardHolder.transform.parent = transform;

        // Создаем пол и стены на основе данных генератора
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                GridPos p = new GridPos(x, y);
                if (data.Floors.Contains(p) || data.Walls.Contains(p)) {
                    Vector3 worldPos = GetWorldPos(p);
                    GameObject tile = Instantiate(tilePrefab, worldPos, Quaternion.identity);
                    tile.name = $"Tile_{x}_{y}"; tile.transform.parent = boardHolder.transform;
                    _gridVisuals.Add(p, tile);
                    TileData tileData = tile.AddComponent<TileData>(); tileData.Pos = p; _tileDataMap.Add(p, tileData);

                    if (data.Walls.Contains(p)) {
                        _walls.Add(p);
                        tile.GetComponent<Renderer>().material.color = Color.black;
                        tile.transform.localScale += Vector3.up * 1.5f; tile.name = "WALL";
                    } else {
                        // Шанс спавна объектов ТОЛЬКО в комнатах (для простоты - на любом полу пока)
                        // Но если хотим сундуки:
                        if (Random.value < 0.05f) { // 5% шанс на клетку пола
                            if (Random.value < 0.3f) SpawnObject(p, ObjType.Chest);
                            else SpawnObject(p, (Random.value < 0.5f) ? ObjType.Crate : ObjType.Barrel);
                        } else {
                            ResetTileColor(p);
                        }
                    }
                }
            }
        }
        
        _heroPos = data.StartPos;
        fogOfWar.InitFog(width, height, tileSize, this);
        fogOfWar.UpdateFog(_heroPos);
    }

    void SpawnObject(GridPos pos, ObjType type) {
        GameObject prefab = (type == ObjType.Crate) ? cratePrefab : (type == ObjType.Barrel ? barrelPrefab : cratePrefab);
        if(prefab == null) return;
        GameObject obj = Instantiate(prefab, GetWorldPos(pos) + Vector3.up * 0.5f, Quaternion.identity);
        InteractiveObject interact = obj.GetComponent<InteractiveObject>();
        if(interact == null) interact = obj.AddComponent<InteractiveObject>();
        interact.Type = type; interact.Pos = pos;
        if(type == ObjType.Chest) { interact.UpdateColor(); interact.LootModuleID = _lootPool[Random.Range(0, _lootPool.Length)]; }
        _interactiveObjects.Add(pos, interact); _walls.Add(pos);
    }

    void SpawnUnits() {
        if(_heroInstance != null) Destroy(_heroInstance); foreach(var e in _enemies) if(e != null) Destroy(e); _enemies.Clear(); _enemyStatsList.Clear(); _enemyAIList.Clear();
        _heroInstance = Instantiate(heroPrefab, GetWorldPos(_heroPos) + Vector3.up * 0.5f, Quaternion.identity);
        _heroStats = _heroInstance.GetComponent<UnitStats>();
        if (DungeonManager.Instance != null) { _heroStats.currentHP = DungeonManager.Instance.SavedHP; _heroStats.currentMana = DungeonManager.Instance.SavedMana; _heroStats.currentHeat = DungeonManager.Instance.SavedHeat; }
        
        // Враги
        int enemyCount = 1 + (_currentLevel / 2);
        for(int i=0; i<enemyCount; i++) {
            GridPos spawnPos = FindValidSpawnPos();
            GameObject newEnemy = Instantiate(enemyPrefab, GetWorldPos(spawnPos) + Vector3.up * 0.5f, Quaternion.identity);
            UnitStats stats = newEnemy.GetComponent<UnitStats>(); stats.maxHP += (_currentLevel - 1) * 20; stats.currentHP = stats.maxHP;
            EnemyAI ai = newEnemy.AddComponent<EnemyAI>(); ai.Init(this, stats); newEnemy.SetActive(false);
            _enemies.Add(newEnemy); _enemyStatsList.Add(stats); _enemyAIList.Add(ai);
        }
    }

    // --- PHYSICS & LOGIC ---
    IEnumerator HitObject(InteractiveObject obj, Element element, int damage) {
        obj.Shake(); yield return new WaitForSeconds(0.2f);
        
        // ОТКРЫТИЕ СУНДУКА
        if (obj.Type == ObjType.Chest) {
            Debug.Log($"LOOT FOUND: {obj.LootModuleID}");
            if (DungeonManager.Instance != null) DungeonManager.Instance.UnlockModule(obj.LootModuleID);
            
            // ОБНОВЛЯЕМ БИБЛИОТЕКУ И UI
            InitLibrary(); 
            if(FindFirstObjectByType<GrimoireUI>()) FindFirstObjectByType<GrimoireUI>().RefreshButtons();
            
            if (UIManager.Instance != null) UIManager.Instance.ShowLootMessage(obj.LootModuleID);
            DestroyObject(obj.Pos);
            yield break;
        }

        if (element == Element.Ice) { if (!obj.IsFrozen) obj.Freeze(); yield break; }
        if (element == Element.Fire) { if (obj.IsFrozen) obj.Unfreeze(); else { if (obj.Type == ObjType.Barrel) yield return TriggerExplosion(obj.Pos, obj); else if (obj.Type == ObjType.Crate) DestroyObject(obj.Pos); } yield break; }
        if (element == Element.Air) yield return PushObjectRoutine(obj);
    }

    // ... (Остальные методы: PushObjectRoutine, DestroyObject, TriggerExplosion, ProcessSpell, ShootProjectile, MoveUnit, HasLineOfSight, IsValid, GetWorldPos, SpawnMarker, ClearQueue, FlashTile, ResetTileColor, DrawUnitLabel, CheckEnemyProximity, GetGridPosFromWorld, GetCellsOnLine, HandleMouseInput, TryPlanCommand) ...
    // ВСТАВЬ ИХ СЮДА (Код идентичен прошлому шагу)
    // НЕ ЗАБУДЬ ВСТАВИТЬ FindValidSpawnPos, ExecutePlayerTurn, ExecuteEnemyTurn
    
    // (Для компиляции вставляю критические куски)
    IEnumerator ProcessSpell(SpellBlueprint spell, GridPos targetCenter) {
        Color c = spell.VisualColor; float projectileSize = 0.3f * spell.PowerLevel; 
        bool showVFX = !spell.IsOptimized; 
        if (showVFX) {
            if (spell.Motion == MotionType.LinearProjectile || spell.Motion == MotionType.ArcingProjectile) yield return ShootProjectile(_heroInstance, targetCenter, c, projectileSize);
            else if (spell.Motion == MotionType.InstantRay) {
                 if (spell.Shape == ShapeType.LineBeam && vfxForceBeam != null) {
                     Vector3 start = _heroInstance.transform.position + Vector3.up * 0.8f; 
                     Vector3 dir = (GetWorldPos(targetCenter) - GetWorldPos(_heroPos)).normalized;
                     Vector3 end = start + dir * 20f; 
                     GameObject beam = Instantiate(vfxForceBeam, Vector3.zero, Quaternion.identity);
                     LaserFade fade = beam.GetComponent<LaserFade>(); if(fade) fade.SetPositions(start, end); else { LineRenderer lr = beam.GetComponent<LineRenderer>(); if(lr) { lr.SetPosition(0, start); lr.SetPosition(1, end); } }
                     yield return new WaitForSeconds(0.2f);
                 } else { if (vfxForcePillar != null) { GameObject pillar = Instantiate(vfxForcePillar, GetWorldPos(targetCenter), Quaternion.identity); Destroy(pillar, 2f); } else yield return FlashTile(targetCenter, c); }
            }
        } else yield return new WaitForSeconds(0.1f);

        List<GridPos> affectedTiles = new List<GridPos>();
        if (spell.Shape == ShapeType.SingleTile) affectedTiles.Add(targetCenter);
        else if (spell.Shape == ShapeType.Cross) { affectedTiles.Add(targetCenter); affectedTiles.Add(new GridPos(targetCenter.x+1, targetCenter.y)); affectedTiles.Add(new GridPos(targetCenter.x-1, targetCenter.y)); affectedTiles.Add(new GridPos(targetCenter.x, targetCenter.y+1)); affectedTiles.Add(new GridPos(targetCenter.x, targetCenter.y-1)); }
        else if (spell.Shape == ShapeType.LineBeam) {
            Vector3 dirVector = (GetWorldPos(targetCenter) - GetWorldPos(_heroPos)).normalized;
            float dist = Mathf.Max(width, height) * 1.5f; Vector3 endWorld = GetWorldPos(_heroPos) + dirVector * (dist * tileSize);
            List<GridPos> rawLine = GetCellsOnLine(_heroPos, GetGridPosFromWorld(endWorld));
            foreach(var cell in rawLine) { if (cell == _heroPos) continue; if (!IsValid(cell)) break; if (_walls.Contains(cell) && !_interactiveObjects.ContainsKey(cell)) break; affectedTiles.Add(cell); }
        }

        foreach (GridPos tilePos in affectedTiles) {
            if (!IsValid(tilePos)) continue;
            if(showVFX) StartCoroutine(FlashTile(tilePos, c)); else StartCoroutine(FlashTile(tilePos, new Color(c.r, c.g, c.b, 0.2f)));
            if (_interactiveObjects.ContainsKey(tilePos)) yield return HitObject(_interactiveObjects[tilePos], spell.MainElement, 10 * spell.PowerLevel);
            if (_tileDataMap.ContainsKey(tilePos)) { TileData tile = _tileDataMap[tilePos]; if (spell.MainElement == Element.Fire && tile.CurrentElement == Element.Ice) { tile.CurrentElement = Element.Water; _walls.Remove(tilePos); ResetTileColor(tilePos); } else if (spell.MainElement == Element.Ice && tile.CurrentElement == Element.Water) { tile.CurrentElement = Element.Ice; _walls.Add(tilePos); ResetTileColor(tilePos); } }
            foreach(var enemy in _enemies) {
                if(!enemy.activeSelf) continue; GridPos enPos = GetGridPosFromWorld(enemy.transform.position); 
                if (tilePos == enPos) { 
                    float mult = spell.PowerLevel == 2 ? 1.5f : (spell.PowerLevel == 3 ? 2.5f : 1f); int damage = Mathf.RoundToInt(10 * mult); if (spell.MainElement == Element.Air) damage = 2; 
                    enemy.GetComponent<UnitStats>().TakeDamage(damage); enemy.SetActive(true); 
                    if (spell.MainElement == Element.Air) {
                        Vector3 pushDirVector = (enemy.transform.position - _heroInstance.transform.position).normalized;
                        int px = 0; int py = 0; if(Mathf.Abs(pushDirVector.x) > Mathf.Abs(pushDirVector.z)) px = (int)Mathf.Sign(pushDirVector.x); else py = (int)Mathf.Sign(pushDirVector.z); 
                        GridPos pushDest = new GridPos(enPos.x + px, enPos.y + py); 
                        bool isBlocked = _walls.Contains(pushDest) && !_interactiveObjects.ContainsKey(pushDest);
                        if (IsValid(pushDest) && !isBlocked && pushDest != _heroPos) yield return MoveUnit(enemy, pushDest); else { enemy.GetComponent<UnitStats>().TakeDamage(15); StartCoroutine(FlashTile(tilePos, Color.white)); }
                    } 
                } 
            }
        }
    }
    
    IEnumerator ExecutePlayerTurn() { _currentState = BattleState.PlayerExecuting; foreach (ActionCommand cmd in CommandQueue) { if (cmd.Type == "WAIT") { yield return new WaitForSeconds(0.5f); _heroStats.CoolDown(5); } else if (cmd.Type == "MOVE") { foreach(GridPos step in cmd.Path) { yield return MoveUnit(_heroInstance, step); _heroPos = step; ResetTileColor(step); CheckEnemyProximity(); fogOfWar.UpdateFog(_heroPos); } } else if (cmd.Type == "SPELL") { if (_heroStats.ConsumeMana(cmd.SpellData.ManaCost)) { _heroStats.AddHeat(cmd.SpellData.HeatCost); _heroInstance.transform.LookAt(GetWorldPos(cmd.TargetPos)); yield return ProcessSpell(cmd.SpellData, cmd.TargetPos); } } GameObject usedMarker = _spawnedMarkers.Find(m => Vector3.Distance(m.transform.position, GetWorldPos(cmd.TargetPos)) < 0.5f); if(usedMarker != null) { _spawnedMarkers.Remove(usedMarker); Destroy(usedMarker); } yield return new WaitForSeconds(0.2f); } ClearQueue(); bool enemiesRemain = false; foreach(var enemy in _enemies) { if (enemy != null && enemy.GetComponent<UnitStats>().currentHP > 0) { enemiesRemain = true; break; } } if (!enemiesRemain) { _currentState = BattleState.Won; } else { StartCoroutine(ExecuteEnemyTurn()); } }
    IEnumerator ExecuteEnemyTurn() { _currentState = BattleState.EnemyExecuting; yield return new WaitForSeconds(0.5f); for(int i=0; i<_enemies.Count; i++) { if (_enemies[i].activeSelf && _enemyStatsList[i].currentHP > 0) { GridPos myPos = GetGridPosFromWorld(_enemies[i].transform.position); List<ActionCommand> aiMoves = _enemyAIList[i].PlanTurn(myPos, _heroPos, _walls); foreach (ActionCommand cmd in aiMoves) { if (cmd.Type == "MOVE") { foreach(GridPos step in cmd.Path) { yield return MoveUnit(_enemies[i], step); } } else if (cmd.Type == "ATTACK") { _enemies[i].transform.LookAt(GetWorldPos(cmd.TargetPos)); _heroStats.TakeDamage(_enemyAIList[i].damage); } yield return new WaitForSeconds(0.2f); } } } if (_heroStats.currentHP <= 0) _currentState = BattleState.Lost; else { _heroStats.CoolDown(2); _currentState = BattleState.PlayerPlanning; } }
    // Вставь остальные хелперы (ShootProjectile, MoveUnit, GetCellsOnLine, и т.д.) из старого файла
    // ДЛЯ КОМПИЛЯЦИИ:
    IEnumerator ShootProjectile(GameObject shooter, GridPos target, Color color, float size) { GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere); projectile.transform.localScale = Vector3.one * size; projectile.GetComponent<Renderer>().material.color = color; Vector3 startPos = shooter.transform.position + Vector3.up * 0.5f; projectile.transform.position = startPos; Vector3 targetWorld = GetWorldPos(target) + Vector3.up * 0.5f; float t = 0; while(t < 1f) { t += Time.deltaTime * 10f; projectile.transform.position = Vector3.Lerp(startPos, targetWorld, t); yield return null; } Destroy(projectile); }
    IEnumerator MoveUnit(GameObject unit, GridPos targetStep) { Vector3 targetWorld = GetWorldPos(targetStep); unit.transform.LookAt(targetWorld); Vector3 startPos = unit.transform.position; Vector3 endPos = targetWorld + Vector3.up * 0.5f; float t = 0; while(t < 1f) { t += Time.deltaTime * 8f; unit.transform.position = Vector3.Lerp(startPos, endPos, t); yield return null; } unit.transform.position = endPos; }
    bool HasLineOfSight(GridPos start, GridPos end) { Vector3 startWorld = GetWorldPos(start) + Vector3.up * 0.5f; Vector3 endWorld = GetWorldPos(end) + Vector3.up * 0.5f; Vector3 direction = (endWorld - startWorld).normalized; float distance = Vector3.Distance(startWorld, endWorld); if (Physics.Raycast(startWorld, direction, out RaycastHit hit, distance)) { if (hit.collider.gameObject.name == "WALL" || hit.collider.gameObject.name == "ICE_WALL") return false; } return true; }
    bool IsValid(GridPos p) => p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;
    Vector3 GetWorldPos(GridPos pos) => new Vector3(pos.x * tileSize, 0, pos.y * tileSize);
    void SpawnMarker(GridPos pos, Color color) { if (markerPrefab == null) return; Vector3 spawnPos = GetWorldPos(pos) + Vector3.up * 0.5f; GameObject marker = Instantiate(markerPrefab, spawnPos, Quaternion.identity); marker.GetComponent<Renderer>().material.color = color; _spawnedMarkers.Add(marker); }
    void ClearQueue() { CommandQueue.Clear(); foreach(var m in _spawnedMarkers) Destroy(m); _spawnedMarkers.Clear(); foreach(var kvp in _gridVisuals) ResetTileColor(kvp.Key); }
    IEnumerator FlashTile(GridPos pos, Color flashColor) { if (_gridVisuals.ContainsKey(pos)) { Renderer rend = _gridVisuals[pos].GetComponent<Renderer>(); rend.material.color = flashColor; yield return new WaitForSeconds(0.2f); ResetTileColor(pos); } }
    void ResetTileColor(GridPos pos) { if (!_gridVisuals.ContainsKey(pos)) return; if (!_tileDataMap.ContainsKey(pos)) return; Renderer rend = _gridVisuals[pos].GetComponent<Renderer>(); Element el = _tileDataMap[pos].CurrentElement; if (_walls.Contains(pos)) { rend.material.color = (el == Element.Ice) ? Color.cyan : Color.black; return; } if (el == Element.Water) rend.material.color = Color.blue; else if (el == Element.Fire) rend.material.color = new Color(0.5f, 0, 0); else { bool isDark = (pos.x + pos.y) % 2 == 0; rend.material.color = isDark ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.6f, 0.6f, 0.6f); } }
    void DrawUnitLabel(GameObject unit, UnitStats stats) { if (unit == null || stats == null || !unit.activeSelf) return; Vector3 screenPos = Camera.main.WorldToScreenPoint(unit.transform.position + Vector3.up * 1.5f); if (screenPos.z > 0) GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y, 100, 30), $"HP: {stats.currentHP}"); }
    void CheckEnemyProximity() { foreach(var enemy in _enemies) if (Vector3.Distance(GetWorldPos(_heroPos), enemy.transform.position) < tileSize * 3) enemy.SetActive(true); }
    GridPos GetGridPosFromWorld(Vector3 worldPos) { return new GridPos(Mathf.RoundToInt(worldPos.x / tileSize), Mathf.RoundToInt(worldPos.z / tileSize)); }
    List<GridPos> GetCellsOnLine(GridPos start, GridPos end) { List<GridPos> line = new List<GridPos>(); int x0 = start.x; int y0 = start.y; int x1 = end.x; int y1 = end.y; int dx = Mathf.Abs(x1 - x0); int dy = Mathf.Abs(y1 - y0); int sx = x0 < x1 ? 1 : -1; int sy = y0 < y1 ? 1 : -1; int err = dx - dy; while (true) { line.Add(new GridPos(x0, y0)); if (x0 == x1 && y0 == y1) break; int e2 = 2 * err; if (e2 > -dy) { err -= dy; x0 += sx; } if (e2 < dx) { err += dx; y0 += sy; } } return line; }
    GridPos FindValidSpawnPos() { int attempts = 100; while(attempts > 0) { GridPos p = new GridPos(Random.Range(0, width), Random.Range(0, height)); if(!_walls.Contains(p)) return p; attempts--; } return new GridPos(width-1, height-1); }
    IEnumerator PushObjectRoutine(InteractiveObject obj) { GridPos startPos = obj.Pos; Vector3 dirVector = (GetWorldPos(startPos) - GetWorldPos(_heroPos)).normalized; int pushX = 0; int pushY = 0; if (Mathf.Abs(dirVector.x) > Mathf.Abs(dirVector.z)) pushX = (int)Mathf.Sign(dirVector.x); else pushY = (int)Mathf.Sign(dirVector.z); int slideDistance = 4; bool hitSomething = false; _interactiveObjects.Remove(startPos); _walls.Remove(startPos); for (int i = 0; i < slideDistance; i++) { GridPos nextPos = new GridPos(obj.Pos.x + pushX, obj.Pos.y + pushY); if (!IsValid(nextPos) || _walls.Contains(nextPos)) { hitSomething = true; break; } bool hitEnemy = false; foreach(var enemy in _enemies) { if (GetGridPosFromWorld(enemy.transform.position) == nextPos && enemy.activeSelf) { enemy.GetComponent<UnitStats>().TakeDamage(30); hitEnemy = true; break; } } if (hitEnemy) { hitSomething = true; break; } if (nextPos == _heroPos) { hitSomething = true; _heroStats.TakeDamage(10); break; } Vector3 startWorld = obj.transform.position; Vector3 endWorld = GetWorldPos(nextPos) + Vector3.up * 0.5f; float t = 0; while(t < 1f) { t += Time.deltaTime * 15f; obj.transform.position = Vector3.Lerp(startWorld, endWorld, t); yield return null; } obj.Pos = nextPos; } if (hitSomething) { if (obj.Type == ObjType.Barrel && !obj.IsFrozen) yield return TriggerExplosion(obj.Pos, obj); else Destroy(obj.gameObject); } else { _interactiveObjects.Add(obj.Pos, obj); _walls.Add(obj.Pos); } }
    void DestroyObject(GridPos pos) { if (_interactiveObjects.ContainsKey(pos)) { InteractiveObject obj = _interactiveObjects[pos]; _interactiveObjects.Remove(pos); _walls.Remove(pos); Destroy(obj.gameObject); } }
    IEnumerator TriggerExplosion(GridPos center, InteractiveObject specificObject = null) { if (specificObject != null) Destroy(specificObject.gameObject); else if (_interactiveObjects.ContainsKey(center)) DestroyObject(center); List<GridPos> boomZone = new List<GridPos>(); for(int x=-1; x<=1; x++) for(int y=-1; y<=1; y++) boomZone.Add(new GridPos(center.x+x, center.y+y)); foreach(var p in boomZone) { if (vfxExplosion != null) { GameObject boom = Instantiate(vfxExplosion, GetWorldPos(p) + Vector3.up * 0.5f, Quaternion.identity); Destroy(boom, 2f); } StartCoroutine(FlashTile(p, new Color(1f, 0.5f, 0f))); foreach(var enemy in _enemies) if (GetGridPosFromWorld(enemy.transform.position) == p && enemy.activeSelf) enemy.GetComponent<UnitStats>().TakeDamage(40); if (p == _heroPos) _heroStats.TakeDamage(20); if (_interactiveObjects.ContainsKey(p)) { yield return new WaitForSeconds(0.1f); yield return HitObject(_interactiveObjects[p], Element.Fire, 0); } } }
    void TryPlanCommand(GridPos targetPos) { GridPos currentPlanEnd = _heroPos; if (CommandQueue.Count > 0) { for(int i=CommandQueue.Count-1; i>=0; i--) { if(CommandQueue[i].Type == "MOVE") { currentPlanEnd = CommandQueue[i].TargetPos; break; } } } if (_selectedSpellIndex == -1) { if (_walls.Contains(targetPos) || targetPos == currentPlanEnd) return; List<GridPos> path = Pathfinding.FindPath(currentPlanEnd, targetPos, _walls, width, height); if (path != null && path.Count > 0) { CommandQueue.Add(new ActionCommand("MOVE", targetPos, path)); foreach (GridPos step in path) _gridVisuals[step].GetComponent<Renderer>().material.color = Color.green; SpawnMarker(targetPos, Color.cyan); } } else { if (_selectedSpellIndex >= Spellbook.Count || Spellbook[_selectedSpellIndex] == null) return; SpellBlueprint spell = Spellbook[_selectedSpellIndex]; if (targetPos == currentPlanEnd && spell.Motion != MotionType.SelfBuff) return; if (spell.Motion != MotionType.ArcingProjectile && spell.Shape != ShapeType.LineBeam) { if (!HasLineOfSight(currentPlanEnd, targetPos)) { StartCoroutine(FlashTile(targetPos, Color.gray)); return; } } CommandQueue.Add(new ActionCommand("SPELL", targetPos, null, spell)); SpawnMarker(targetPos, spell.VisualColor); } }
    void HandleMouseInput() { if (Input.GetMouseButtonDown(0) && !Input.GetMouseButton(1)) { Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); if (Physics.Raycast(ray, out RaycastHit hit)) { foreach(var kvp in _gridVisuals) { if(kvp.Value == hit.collider.gameObject) { TryPlanCommand(kvp.Key); break; } } } } }
}
