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

    public TextMeshProUGUI previewText; // Текст предпросмотра

    void Start()
    {
        if (tacticalSystem == null) tacticalSystem = FindFirstObjectByType<TacticalSystem>();
        GenerateAllButtons();
    }

    void Update()
    {
        // Обновляем текст превью каждый кадр
        if (tacticalSystem && previewText)
        {
            var m = tacticalSystem.WorkMotion;
            var s = tacticalSystem.WorkShape;
            var e = tacticalSystem.WorkElement;
            int p = tacticalSystem.WorkPowerLevel;

            if (m != null && s != null && e != null)
            {
                int cost = (m.ManaCost + s.ManaCost) * p;
                previewText.text = $"ASSEMBLY:\n{m.Name} + {s.Name} + {e.Name}\nPower: {p}\nCost: {cost} MP";
            }
        }
    }

    void GenerateAllButtons()
    {
        // Motion Buttons
        foreach (var m in tacticalSystem.GetMotionLibrary())
        {
            int index = tacticalSystem.GetMotionLibrary().IndexOf(m);
            CreateButton(motionContainer, $"{m.Name} ({m.ManaCost})", () => tacticalSystem.UI_SelectModule(0, index));
        }

        // Shape Buttons
        foreach (var s in tacticalSystem.GetShapeLibrary())
        {
            int index = tacticalSystem.GetShapeLibrary().IndexOf(s);
            CreateButton(shapeContainer, $"{s.Name} (+{s.ManaCost})", () => tacticalSystem.UI_SelectModule(1, index));
        }

        // Element Buttons
        foreach (var e in tacticalSystem.GetElementLibrary())
        {
            int index = tacticalSystem.GetElementLibrary().IndexOf(e);
            // Красим текст кнопки в цвет элемента
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

    // Эти методы привяжи к кнопкам SAVE в инспекторе
    public void SaveToSlot1() => tacticalSystem.UI_Compile(0);
    public void SaveToSlot2() => tacticalSystem.UI_Compile(1);
    public void SaveToSlot3() => tacticalSystem.UI_Compile(2);

    // Привяжи к слайдеру
    public void OnSliderChanged(float val) => tacticalSystem.UI_SetPower(val);
}