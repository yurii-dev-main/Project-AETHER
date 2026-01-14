using UnityEngine;
using System.Collections.Generic;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance;

    [Header("Session Data")]
    public int CurrentLevel = 1;
    public int SavedHP = 100;
    public int SavedMana = 50;
    public int SavedHeat = 0;

    // ГРИМУАР (Собранные слоты)
    public List<SpellBlueprint> SavedSpellbook = new List<SpellBlueprint>();

    // БИБЛИОТЕКА (Разблокированные детали)
    // HashSet не виден в Инспекторе Unity и стирается при перезагрузке домена,
    // поэтому мы будем инициализировать его при старте, если он пуст.
    public HashSet<string> UnlockedModules = new HashSet<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // ИСПРАВЛЕНИЕ: Проверяем UnlockedModules. 
            // Если он пуст (даже если есть спеллы) — нужно выдать базу.
            if (SavedSpellbook.Count == 0 || UnlockedModules.Count == 0)
            {
                InitializeNewGame();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeNewGame()
    {
        // 1. Очистка
        SavedSpellbook.Clear();
        UnlockedModules.Clear();

        // 2. СТАРТОВЫЙ НАБОР (Starter Kit)
        // Точковые способности и Огонь, как ты просил
        UnlockModule("Projectile"); // Способ доставки (Motion)
        UnlockModule("Point");      // Форма урона (Shape)
        UnlockModule("Fire");       // Стихия (Element)

        // 3. Собираем первый спелл для игрока (чтобы слот 1 не был пустым)
        SavedSpellbook.Add(new SpellBlueprint("Fireball", Element.Fire, MotionType.LinearProjectile, ShapeType.SingleTile, 1, false, 10, 5, Color.red));

        Debug.Log("NEW GAME INITIALIZED: Starter Kit Unlocked.");
    }

    public void UnlockModule(string moduleName)
    {
        if (!UnlockedModules.Contains(moduleName))
        {
            UnlockedModules.Add(moduleName);
            Debug.Log($"NEW FIRMWARE DETECTED: {moduleName}");
        }
    }

    public bool IsModuleUnlocked(string moduleName)
    {
        return UnlockedModules.Contains(moduleName);
    }

    public void CompleteLevel(int hp, int mana, int heat)
    {
        SavedHP = hp; SavedMana = mana; SavedHeat = heat;
        CurrentLevel++;
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public void RestartGame()
    {
        CurrentLevel = 1; SavedHP = 100; SavedMana = 50; SavedHeat = 0;
        InitializeNewGame();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}