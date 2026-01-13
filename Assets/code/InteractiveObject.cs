using UnityEngine;

public enum ObjType { Crate, Barrel, Door } // Добавили Door

public class InteractiveObject : MonoBehaviour
{
    public ObjType Type;
    public GridPos Pos;

    public bool IsFrozen = false;
    public bool IsOpen = false; // Для двери

    public void Shake()
    {
        StartCoroutine(ShakeRoutine());
    }

    public void Freeze()
    {
        if (IsFrozen || Type == ObjType.Door) return; // Двери пока не морозим
        IsFrozen = true;
        GetComponent<Renderer>().material.color = Color.cyan;
    }

    public void Unfreeze()
    {
        if (!IsFrozen) return;
        IsFrozen = false;
        UpdateColor();
    }

    // НОВОЕ: Открытие двери
    public void OpenDoor()
    {
        if (Type != ObjType.Door || IsOpen) return;

        IsOpen = true;
        // Визуально "открываем" (уменьшаем или поворачиваем)
        transform.localScale = new Vector3(0.2f, 1f, 0.2f);
        // Меняем цвет на зеленый (проход)
        GetComponent<Renderer>().material.color = Color.green;

        // Важно: Логику удаления из стен (_walls.Remove) должен делать TacticalSystem
    }

    public void UpdateColor()
    {
        Renderer r = GetComponent<Renderer>();
        if (Type == ObjType.Barrel) r.material.color = Color.red;
        else if (Type == ObjType.Crate) r.material.color = new Color(0.6f, 0.4f, 0.2f); // Brown
        else if (Type == ObjType.Door) r.material.color = new Color(0.4f, 0.2f, 0.1f); // Dark Wood
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