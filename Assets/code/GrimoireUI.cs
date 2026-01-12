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
    public Toggle optimizationToggle; // НОВОЕ: Ссылка на чекбокс

    public TextMeshProUGUI previewText;

    void Start()
    {
        if (tacticalSystem == null) tacticalSystem = FindFirstObjectByType<TacticalSystem>();
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
            // (Код обновления текста такой же, tacticalSystem сам считает cost)
        }
    }

    void GenerateAllButtons()
    {
        foreach (Transform t in motionContainer) Destroy(t.gameObject);
        foreach (Transform t in shapeContainer) Destroy(t.gameObject);
        foreach (Transform t in elementContainer) Destroy(t.gameObject);

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
