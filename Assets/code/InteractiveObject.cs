using UnityEngine;

public enum ObjType { Crate, Barrel, Door, Chest, Switch } // Добавили Switch

public class InteractiveObject : MonoBehaviour
{
    public ObjType Type;
    public GridPos Pos;

    [Header("Loot & Logic")]
    public string LootModuleID;
    public InteractiveObject LinkedObject; // Ссылка на то, что мы открываем/роняем

    [Header("State")]
    public bool IsFrozen = false;
    public bool IsOpen = false; // Для двери и рычага (Open = Activated)

    public void Shake()
    {
        StartCoroutine(ShakeRoutine());
    }

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

        // Анимация открытия
        if (Type == ObjType.Door)
        {
            transform.localScale = new Vector3(0.2f, 1f, 0.2f); // Дверь становится тонкой
            GetComponent<Renderer>().material.color = Color.green;
        }
        else if (Type == ObjType.Switch)
        {
            // Рычаг меняет цвет
            GetComponent<Renderer>().material.color = Color.green;
            Debug.Log("Switch Activated!");

            // Активируем связанный объект
            if (LinkedObject != null)
            {
                if (LinkedObject.Type == ObjType.Door) LinkedObject.OpenDoor();
                // Тут можно добавить логику для Люстры (Trap)
            }
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
            case ObjType.Switch: r.material.color = Color.magenta; break; // Рычаг фиолетовый
        }
    }

    System.Collections.IEnumerator ShakeRoutine()
    {
        Vector3 original = transform.position;
        for (int i = 0; i < 5; i++)
        {
            transform.position = original + Random.insideUnitSphere * 0.1f;
            yield return new WaitForSeconds(0.05f);
        }
        transform.position = original;
    }
}