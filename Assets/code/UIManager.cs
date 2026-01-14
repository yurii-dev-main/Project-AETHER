using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Panels")]
    public GameObject grimoirePanel;
    public GameObject victoryPanel;
    public GameObject gameOverPanel;
    public GameObject lootPanel;

    [Header("Text Elements")]
    public TextMeshProUGUI statsText;
    public TextMeshProUGUI pipelineText;
    public TextMeshProUGUI spellDeckText;

    [Header("Grimoire Elements")]
    public TextMeshProUGUI grimoireDebugText;
    public TextMeshProUGUI lootText;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (grimoirePanel) grimoirePanel.SetActive(false);
        if (victoryPanel) victoryPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (lootPanel) lootPanel.SetActive(false);
    }

    public void UpdateStats(int hp, int mana, int heat, int maxHeat, bool isOverheated)
    {
        if (statsText == null) return;

        string status = isOverheated ? "<color=red>[OVERHEAT]</color>" : "OPERATIONAL";
        statsText.text = $"HP: {hp}\nMANA: {mana}\nHEAT: {heat}/{maxHeat}\nSTATUS: {status}";
    }

    public void UpdatePipeline(List<ActionCommand> queue)
    {
        if (pipelineText == null) return;

        string t = "PIPELINE:\n";
        if (queue != null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                ActionCommand cmd = queue[i];
                string cmdName = cmd.Type == "MOVE" ? "MOVE" : (cmd.SpellData != null ? cmd.SpellData.Name : cmd.Type);
                t += $"{i + 1}. {cmdName} >> [{cmd.TargetPos.x}, {cmd.TargetPos.y}]\n";
            }
        }
        pipelineText.text = t;
    }

    public void UpdateSpellDeck(List<SpellBlueprint> spells, int selectedIndex)
    {
        if (spellDeckText == null) return;

        string t = "SPELL DECK (1-9):\n";

        string moveMarker = selectedIndex == -1 ? "> " : "  ";
        t += $"{moveMarker}[ESC] MOVE\n";

        if (spells != null)
        {
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i] == null) continue;
                string marker = selectedIndex == i ? "> " : "  ";
                t += $"{marker}[{i + 1}] {spells[i].Name} ({spells[i].ManaCost} MP)\n";
            }
        }

        spellDeckText.text = t;
    }

    public void ToggleGrimoire(bool isOpen)
    {
        if (grimoirePanel) grimoirePanel.SetActive(isOpen);
    }

    public void UpdateGrimoirePreview(string motion, string shape, string element, int cost, int heat)
    {
        if (grimoireDebugText == null) return;

        grimoireDebugText.text = $"<b>ASSEMBLY:</b>\n" +
                                 $"Motion: {motion}\n" +
                                 $"Shape: {shape}\n" +
                                 $"Element: {element}\n\n" +
                                 $"COST: {cost} MP | {heat} Heat";
    }

    public void ShowVictory(bool isVictory)
    {
        if (victoryPanel) victoryPanel.SetActive(isVictory);
        if (gameOverPanel) gameOverPanel.SetActive(!isVictory);
    }

    public void ShowLootMessage(string moduleName)
    {
        if (lootPanel)
        {
            lootPanel.SetActive(true);
            if (lootText) lootText.text = $"SYSTEM UPDATE:\nMODULE <color=yellow>[{moduleName}]</color> INSTALLED";

            CancelInvoke(nameof(HideLootPanel));
            Invoke(nameof(HideLootPanel), 3.0f);
        }
    }

    void HideLootPanel()
    {
        if (lootPanel) lootPanel.SetActive(false);
    }

    // Привяжи эту функцию к кнопке на панели победы
    public void OnNextLevelClicked()
    {
        if (DungeonManager.Instance != null && FindFirstObjectByType<TacticalSystem>() != null)
        {
            GameObject hero = GameObject.FindWithTag("Player");
            if (hero != null)
            {
                var stats = hero.GetComponent<UnitStats>();
                DungeonManager.Instance.CompleteLevel(stats.currentHP, stats.currentMana, stats.currentHeat);
            }
            else
            {
                DungeonManager.Instance.CompleteLevel(100, 50, 0);
            }
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }

    // Привяжи эту функцию к кнопке на панели поражения
    public void OnRestartClicked()
    {
        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.RestartGame();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}
