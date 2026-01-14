using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GrimoireUI : MonoBehaviour
{
    public TacticalSystem tacticalSystem;
    public GameObject buttonPrefab;

    public Transform motionContainer;
    public Transform shapeContainer;
    public Transform elementContainer;
    public Toggle optimizationToggle;

    public TextMeshProUGUI previewText;

    void Start()
    {
        if (tacticalSystem == null) tacticalSystem = FindFirstObjectByType<TacticalSystem>();

        // Генерируем кнопки при старте
        GenerateAllButtons();

        if (optimizationToggle)
        {
            optimizationToggle.onValueChanged.AddListener(OnOptimizeToggled);
        }
    }

    void Update()
    {
        if (tacticalSystem && previewText)
        {
            // Обновляем текст превью
            var m = tacticalSystem.WorkMotion;
            var s = tacticalSystem.WorkShape;
            var e = tacticalSystem.WorkElement;
            int p = tacticalSystem.WorkPowerLevel;
            bool opt = tacticalSystem.WorkIsOptimized;

            if (m != null && s != null && e != null)
            {
                int cost = (m.ManaCost + s.ManaCost);
                int heat = (m.HeatCost + s.HeatCost);
                cost *= p;
                heat = (int)(heat * Mathf.Pow(p, 1.5f));
                if (opt) cost = Mathf.RoundToInt(cost * 0.7f);

                string optText = opt ? " (INVISIBLE)" : "";
                previewText.text = $"ASSEMBLY:\n{m.Name} + {s.Name} + {e.Name}\nPower: {p}{optText}\nCost: {cost} MP | {heat} Heat";
            }
        }
    }

    // НОВЫЙ МЕТОД: Публичный, чтобы вызывать извне
    public void RefreshButtons()
    {
        GenerateAllButtons();
    }

    void GenerateAllButtons()
    {
        // 1. Очистка старых кнопок
        ClearContainer(motionContainer);
        ClearContainer(shapeContainer);
        ClearContainer(elementContainer);

        if (tacticalSystem == null) return;

        // 2. Генерация новых
        foreach (var m in tacticalSystem.GetMotionLibrary())
        {
            int index = tacticalSystem.GetMotionLibrary().IndexOf(m);
            CreateButton(motionContainer, $"{m.Name} ({m.ManaCost})", () => tacticalSystem.UI_SelectModule(0, index));
        }
        foreach (var s in tacticalSystem.GetShapeLibrary())
        {
            int index = tacticalSystem.GetShapeLibrary().IndexOf(s);
            CreateButton(shapeContainer, $"{s.Name} (+{s.ManaCost})", () => tacticalSystem.UI_SelectModule(1, index));
        }
        foreach (var e in tacticalSystem.GetElementLibrary())
        {
            int index = tacticalSystem.GetElementLibrary().IndexOf(e);
            CreateButton(elementContainer, e.Name, () => tacticalSystem.UI_SelectModule(2, index), e.VisualColor);
        }
    }

    void ClearContainer(Transform container)
    {
        foreach (Transform child in container) Destroy(child.gameObject);
    }

    void CreateButton(Transform container, string text, UnityEngine.Events.UnityAction action, Color? color = null)
    {
        GameObject btnObj = Instantiate(buttonPrefab, container);
        btnObj.GetComponentInChildren<TextMeshProUGUI>().text = text;
        if (color.HasValue) btnObj.GetComponentInChildren<TextMeshProUGUI>().color = color.Value;
        btnObj.GetComponent<Button>().onClick.AddListener(action);
    }

    public void SaveToSlot1() => tacticalSystem.UI_Compile(0);
    public void SaveToSlot2() => tacticalSystem.UI_Compile(1);
    public void SaveToSlot3() => tacticalSystem.UI_Compile(2);
    public void OnSliderChanged(float val) => tacticalSystem.UI_SetPower(val);
    public void OnOptimizeToggled(bool isOpt) => tacticalSystem.UI_ToggleOptimization(isOpt);
}