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

    // GRIMOIRE (collected slots)
    public List<SpellBlueprint> SavedSpellbook = new List<SpellBlueprint>();

    // LIBRARY (unlocked parts)
    public List<string> UnlockedMotionIDs = new List<string>();
    public List<string> UnlockedShapeIDs = new List<string>();
    public List<string> UnlockedElementIDs = new List<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Æèâåò âå÷íî
            if (SavedSpellbook.Count == 0)
            {
                InitializeNewGame();
            }
        }
        else
        {
            Destroy(gameObject); // Óáèâàåì äóáëèêàòû ïðè ïåðåçàãðóçêå
        }
    }

    public void CompleteLevel(int hp, int mana, int heat)
    {
        SavedHP = hp;
        SavedMana = mana;
        SavedHeat = heat;
        CurrentLevel++;

        Debug.Log($"LEVEL {CurrentLevel} STARTING...");

        // Ïåðåçàãðóæàåì ñöåíó (íî òàê êàê Manager æèâ, äàííûå îñòàíóòñÿ)
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public void RestartGame()
    {
        CurrentLevel = 1;
        SavedHP = 100;
        SavedMana = 50;
        SavedHeat = 0;
        InitializeNewGame();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    void InitializeNewGame()
    {
        UnlockedMotionIDs.Clear();
        UnlockedShapeIDs.Clear();
        UnlockedElementIDs.Clear();
        SavedSpellbook.Clear();

        UnlockMotion("Projectile");
        UnlockShape("Point");
        UnlockElement("Fire");

        SavedSpellbook.Add(new SpellBlueprint("Fireball", Element.Fire, MotionType.LinearProjectile, ShapeType.SingleTile, 1, false, 10, 5, Color.red));
    }

    public void UnlockMotion(string motionId)
    {
        if (!UnlockedMotionIDs.Contains(motionId))
        {
            UnlockedMotionIDs.Add(motionId);
            Debug.Log($"NEW FIRMWARE DETECTED: {motionId}");
        }
    }

    public void UnlockShape(string shapeId)
    {
        if (!UnlockedShapeIDs.Contains(shapeId))
        {
            UnlockedShapeIDs.Add(shapeId);
            Debug.Log($"NEW FIRMWARE DETECTED: {shapeId}");
        }
    }

    public void UnlockElement(string elementId)
    {
        if (!UnlockedElementIDs.Contains(elementId))
        {
            UnlockedElementIDs.Add(elementId);
            Debug.Log($"NEW FIRMWARE DETECTED: {elementId}");
        }
    }

    public bool IsMotionUnlocked(string motionId)
    {
        return UnlockedMotionIDs.Contains(motionId);
    }

    public bool IsShapeUnlocked(string shapeId)
    {
        return UnlockedShapeIDs.Contains(shapeId);
    }

    public bool IsElementUnlocked(string elementId)
    {
        return UnlockedElementIDs.Contains(elementId);
    }
}
