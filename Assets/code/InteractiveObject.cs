using UnityEngine;

public enum ObjType { Crate, Barrel, Door, Chest, Switch, EarthWall }

public class InteractiveObject : MonoBehaviour
{
    public ObjType Type;
    public GridPos Pos;

    [Header("Stats")]
    public int MaxHP = 1;
    public int CurrentHP = 1;

    [Header("Loot & Logic")]
    public string LootModuleID;
    public InteractiveObject LinkedObject;

    [Header("State")]
    public bool IsFrozen = false;
    public bool IsOpen = false;

    public void InitHealth(int hp)
    {
        MaxHP = hp;
        CurrentHP = hp;
        UpdateColor();
    }

    public void TakeDamage(int dmg)
    {
        if (this == null) return; // Защита
        CurrentHP -= dmg;
        Shake();
        if (CurrentHP > 0) StartCoroutine(FlashWhite());
    }

    public void Shake() { if (this != null) StartCoroutine(ShakeRoutine()); }

    public void Freeze()
    {
        if (IsFrozen || Type == ObjType.Door || Type == ObjType.Chest || Type == ObjType.Switch) return;
        IsFrozen = true;
        GetComponent<Renderer>().material.color = Color.cyan;
    }

    public void Unfreeze()
    {
        if (!IsFrozen) return;
        IsFrozen = false;
        UpdateColor();
    }

    public void OpenDoor()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (Type == ObjType.Door)
        {
            transform.localScale = new Vector3(0.2f, 1f, 0.2f);
            GetComponent<Renderer>().material.color = Color.green;
        }
        else if (Type == ObjType.Switch)
        {
            GetComponent<Renderer>().material.color = Color.green;
            if (LinkedObject != null && LinkedObject.Type == ObjType.Door) LinkedObject.OpenDoor();
        }
    }

    public void UpdateColor()
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null) return;
        switch (Type)
        {
            case ObjType.Barrel: r.material.color = Color.red; break;
            case ObjType.Crate: r.material.color = new Color(0.6f, 0.4f, 0.2f); break;
            case ObjType.Door: r.material.color = new Color(0.4f, 0.2f, 0.1f); break;
            case ObjType.Chest: r.material.color = Color.yellow; break;
            case ObjType.Switch: r.material.color = Color.magenta; break;
            case ObjType.EarthWall: r.material.color = new Color(0.3f, 0.3f, 0.3f); break;
        }
    }

    System.Collections.IEnumerator ShakeRoutine()
    {
        Vector3 original = transform.position;
        for (int i = 0; i < 5; i++)
        {
            if (this == null) yield break; // Проверка существования
            transform.position = original + Random.insideUnitSphere * 0.1f;
            yield return new WaitForSeconds(0.05f);
        }
        if (this != null) transform.position = original;
    }

    System.Collections.IEnumerator FlashWhite()
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null) yield break;
        Color old = r.material.color;
        r.material.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        if (r != null) r.material.color = old;
    }
}