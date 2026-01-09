using UnityEngine;

public enum ObjType { Crate, Barrel }

public class InteractiveObject : MonoBehaviour
{
    public ObjType Type;
    public GridPos Pos;

    public bool IsFrozen = false; // Состояние заморозки

    public void Shake()
    {
        StartCoroutine(ShakeRoutine());
    }

    public void Freeze()
    {
        if (IsFrozen) return;
        IsFrozen = true;
        GetComponent<Renderer>().material.color = Color.cyan; // Синий
    }

    public void Unfreeze()
    {
        if (!IsFrozen) return;
        IsFrozen = false;
        // Возвращаем цвет в зависимости от типа
        GetComponent<Renderer>().material.color = (Type == ObjType.Barrel) ? Color.red : new Color(0.6f, 0.4f, 0.2f);
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