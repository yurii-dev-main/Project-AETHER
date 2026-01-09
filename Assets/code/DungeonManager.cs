using UnityEngine;
using System.Collections.Generic;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance;

    [Header("Session Data")]
    public int CurrentLevel = 1;

    // Сохраняем состояние героя между уровнями
    public int SavedHP = 100;
    public int SavedMana = 50;
    public int SavedHeat = 0;

    // Сохраняем собранные спеллы (пока упрощенно - просто список индексов или типов)
    // В полной версии тут будет List<SpellBlueprint>

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Живет вечно
        }
        else
        {
            Destroy(gameObject); // Убиваем дубликаты при перезагрузке
        }
    }

    public void CompleteLevel(int hp, int mana, int heat)
    {
        SavedHP = hp;
        SavedMana = mana;
        SavedHeat = heat;
        CurrentLevel++;

        Debug.Log($"LEVEL {CurrentLevel} STARTING...");

        // Перезагружаем сцену (но так как Manager жив, данные останутся)
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
}