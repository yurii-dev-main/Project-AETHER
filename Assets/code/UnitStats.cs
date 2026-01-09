using UnityEngine;

public class UnitStats : MonoBehaviour
{
    [Header("Health")]
    public int maxHP = 100;
    public int currentHP;

    [Header("Magic (Hero Only)")]
    public int maxMana = 50;
    public int currentMana;

    [Header("Wand Heat")]
    public int maxHeat = 20;
    public int currentHeat = 0;
    public bool isOverheated = false; // Флаг блокировки

    void Start()
    {
        currentHP = maxHP;
        currentMana = maxMana;
        currentHeat = 0;
    }

    public void TakeDamage(int dmg)
    {
        currentHP -= dmg;
        if (currentHP < 0) currentHP = 0;
        StartCoroutine(ShakeEffect());
        if (currentHP == 0) Die();
    }

    public bool ConsumeMana(int cost)
    {
        // Если перегрев - магия не работает!
        if (isOverheated)
        {
            Debug.Log("WAND IS JAMMED! COOLING DOWN...");
            return false;
        }
        if (currentMana >= cost)
        {
            currentMana -= cost;
            return true;
        }
        return false;
    }

    public void AddHeat(int heat)
    {
        currentHeat += heat;
        // Если достигли максимума - БЛОКИРОВКА
        if (currentHeat >= maxHeat)
        {
            currentHeat = maxHeat;
            isOverheated = true;
            Debug.Log("<color=red>SYSTEM FAILURE! WAND OVERHEATED!</color>");
        }
    }

    public void CoolDown(int amount)
    {
        currentHeat -= amount;
        if (currentHeat < 0) currentHeat = 0;

        // Разблокировка только если остыли ниже 50%
        if (isOverheated && currentHeat < (maxHeat / 2))
        {
            isOverheated = false;
            Debug.Log("<color=green>System Operational.</color>");
        }
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} DIED!");
        transform.rotation = Quaternion.Euler(90, 0, 0);
        GetComponent<Collider>().enabled = false;
    }

    System.Collections.IEnumerator ShakeEffect()
    {
        Vector3 originalPos = transform.position;
        for (int i = 0; i < 5; i++)
        {
            transform.position = originalPos + Random.insideUnitSphere * 0.2f;
            yield return new WaitForSeconds(0.05f);
        }
        transform.position = originalPos;
    }
}