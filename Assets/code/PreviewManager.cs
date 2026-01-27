using UnityEngine;
using System.Collections.Generic;

public class PreviewManager : MonoBehaviour
{
    public static PreviewManager Instance;

    [Header("Settings")]
    public Material ghostMaterial; // Полупрозрачный материал
    public GameObject tileHighlightPrefab; // Плоский квадрат для подсветки пола

    private GameObject _ghostHero;
    private List<GameObject> _activeHighlights = new List<GameObject>();
    private TacticalSystem _tacticalSystem;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        _tacticalSystem = FindFirstObjectByType<TacticalSystem>();
    }

    // Создает или обновляет Фантома
    public void UpdateGhostHero(GameObject realHeroPrefab, Vector3 pos, Quaternion rot)
    {
        if (_ghostHero == null)
        {
            _ghostHero = Instantiate(realHeroPrefab, pos, rot);
            _ghostHero.name = "Ghost_Hero";

            // Удаляем лишние компоненты (чтобы фантом не имел статов и коллизий)
            Destroy(_ghostHero.GetComponent<UnitStats>());
            Destroy(_ghostHero.GetComponent<UnityEngine.AI.NavMeshAgent>()); // Если есть
            Destroy(_ghostHero.GetComponent<Collider>());

            // Красим в призрака
            foreach (var r in _ghostHero.GetComponentsInChildren<Renderer>())
            {
                r.material = ghostMaterial;
            }
        }

        _ghostHero.transform.position = pos;
        _ghostHero.transform.rotation = rot;
    }

    // Показывает зону поражения
    public void ShowSpellPreview(List<GridPos> affectedTiles, Color color)
    {
        // 1. Очистка старых
        ClearHighlights();

        // 2. Создание новых
        foreach (var p in affectedTiles)
        {
            Vector3 worldPos = _tacticalSystem.GetWorldPosPublic(p) + Vector3.up * 0.1f; // Чуть выше пола

            GameObject hl = Instantiate(tileHighlightPrefab, worldPos, Quaternion.Euler(90, 0, 0));
            hl.GetComponent<Renderer>().material.color = new Color(color.r, color.g, color.b, 0.5f); // Полупрозрачный цвет спелла
            _activeHighlights.Add(hl);
        }
    }

    public void ClearHighlights()
    {
        foreach (var obj in _activeHighlights) Destroy(obj);
        _activeHighlights.Clear();
    }

    // Если нужно скрыть фантома (например, при выполнении хода)
    public void HideGhost()
    {
        if (_ghostHero) _ghostHero.SetActive(false);
        ClearHighlights();
    }

    public void ShowGhost()
    {
        if (_ghostHero) _ghostHero.SetActive(true);
    }
}
