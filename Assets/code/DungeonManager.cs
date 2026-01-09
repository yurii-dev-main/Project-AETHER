using UnityEngine;
using System.Collections.Generic;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance;

    [Header("Session Data")]
    public int CurrentLevel = 1;

    public List<SpellBlueprint> SavedSpellbook = new List<SpellBlueprint>();
    public int SavedHP = 100;
    public int SavedMana = 50;
    public int SavedHeat = 0;

    // Ñîõðàíÿåì ñîáðàííûå ñïåëëû (ïîêà óïðîùåííî - ïðîñòî ñïèñîê èíäåêñîâ èëè òèïîâ)
    // Â ïîëíîé âåðñèè òóò áóäåò List<SpellBlueprint>

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Æèâåò âå÷íî
            if (SavedSpellbook.Count == 0)
            {
                InitializeDefaultSpells();
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
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    void InitializeDefaultSpells()
    {
        SavedSpellbook.Clear();
        SavedSpellbook.Add(new SpellBlueprint("Fireball", Element.Fire, MotionType.LinearProjectile, ShapeType.SingleTile, 1, 10, 5, Color.red));
        SavedSpellbook.Add(new SpellBlueprint("Ice", Element.Ice, MotionType.ArcingProjectile, ShapeType.Cross, 1, 20, 10, Color.cyan));
        SavedSpellbook.Add(new SpellBlueprint("Force", Element.Force, MotionType.InstantRay, ShapeType.SingleTile, 1, 5, 2, Color.magenta));
    }
}
