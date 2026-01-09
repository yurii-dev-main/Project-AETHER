using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public TacticalSystem tacticalSystem; // Ссылка на систему (перетащи в инспекторе)

    [Header("Controls")]
    public KeyCode switchKey = KeyCode.Space;
    public float moveSpeed = 5f;
    public float rotateSpeed = 100f; // Чувствительность мыши

    [Header("Tactical View Settings")]
    public float tacticalHeightMultiplier = 1.0f; // Множитель высоты (больше = выше)
    public float tacticalAngle = 60f; // Угол наклона

    [Header("Immersive View Settings")]
    public Vector3 immersiveOffset = new Vector3(0, 2.5f, -3.5f); // Позиция за спиной

    private bool _isTacticalMode = true;
    private float _currentYRotation = 0f; // Для вращения вокруг героя

    void Start()
    {
        // Если забыл перетащить ссылку, попробуем найти сами
        if (tacticalSystem == null)
            tacticalSystem = FindFirstObjectByType<TacticalSystem>();
    }

    void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            _isTacticalMode = !_isTacticalMode;
            // Сбрасываем вращение при входе в иммерсивный режим, чтобы смотреть в спину
            if (!_isTacticalMode && tacticalSystem.HeroTransform != null)
                _currentYRotation = tacticalSystem.HeroTransform.eulerAngles.y;
        }

        if (_isTacticalMode)
        {
            HandleTacticalMode();
        }
        else
        {
            HandleImmersiveMode();
        }
    }

    void HandleTacticalMode()
    {
        if (tacticalSystem == null) return;

        // 1. Находим центр карты
        Vector3 center = tacticalSystem.GetGridCenter();

        // 2. Рассчитываем высоту на основе ширины карты (чтобы всё влезло)
        // Берем большую сторону (width или height) и умножаем
        float maxSize = Mathf.Max(tacticalSystem.width, tacticalSystem.height);
        float height = maxSize * 1.2f * tacticalSystem.tileSize * tacticalHeightMultiplier;

        // Немного отодвигаем назад (Z), чтобы центр был в центре экрана с учетом угла
        float zOffset = -height * 0.5f;

        Vector3 targetPos = new Vector3(center.x, height, center.z + zOffset);
        Quaternion targetRot = Quaternion.Euler(tacticalAngle, 0, 0);

        // Плавное движение
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * moveSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * moveSpeed);
    }

    void HandleImmersiveMode()
    {
        Transform hero = tacticalSystem.HeroTransform;

        // Если героя еще нет (игра не началась), висим на месте
        if (hero == null) return;

        // 1. Вращение камерой (Зажми ПКМ)
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            _currentYRotation += mouseX * rotateSpeed * Time.deltaTime;
        }

        // 2. Рассчитываем позицию
        // Поворот камеры (только по Y)
        Quaternion rotation = Quaternion.Euler(0, _currentYRotation, 0);

        // Позиция = Позиция героя + (Повернутый отступ)
        Vector3 targetPos = hero.position + rotation * immersiveOffset;

        // Камера всегда смотрит на героя (чуть выше ног, в район головы)
        Vector3 lookAtPoint = hero.position + Vector3.up * 1.5f;
        Quaternion targetLookRot = Quaternion.LookRotation(lookAtPoint - transform.position);

        // Движение
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * moveSpeed * 2); // Быстрее в иммерсиве
        transform.rotation = Quaternion.Slerp(transform.rotation, targetLookRot, Time.deltaTime * moveSpeed * 3);
    }
}