using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserFade : MonoBehaviour
{
    public float duration = 0.5f; // Как долго виден луч
    private LineRenderer _line;
    private float _timer;
    private Color _startColor;

    void Start()
    {
        _line = GetComponent<LineRenderer>();
        _startColor = _line.startColor; // Берем цвет из настроек компонента
        _timer = 0;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float progress = _timer / duration;

        if (progress >= 1f)
        {
            Destroy(gameObject); // Удаляем луч
        }
        else
        {
            // Растворяем (Alpha от 1 до 0)
            float alpha = 1f - progress;
            Color fade = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);
            _line.startColor = fade;
            _line.endColor = fade;
        }
    }

    // Метод для установки точек из кода
    public void SetPositions(Vector3 start, Vector3 end)
    {
        LineRenderer lr = GetComponent<LineRenderer>(); // Получаем заново, т.к. Start может еще не сработать
        if (lr == null) lr = gameObject.AddComponent<LineRenderer>();

        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }
}