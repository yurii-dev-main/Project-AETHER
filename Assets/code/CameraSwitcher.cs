using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public TacticalSystem tacticalSystem;

    [Header("General Controls")]
    public KeyCode switchModeKey = KeyCode.Space;
    public KeyCode freeFlyKey = KeyCode.F;
    public float transitionSpeed = 5f;

    [Header("Tactical Settings")]
    public float panSpeed = 20f; // Увеличил скорость, чтобы было удобнее
    public float zoomSpeed = 5f;
    public float rotateSpeed = 5f;
    public Vector2 heightLimits = new Vector2(5f, 25f);
    public Vector2 angleLimits = new Vector2(30f, 85f);

    [Header("Immersive Settings")]
    public Vector3 shoulderOffset = new Vector3(0, 2.5f, -3.5f);
    public float flySpeed = 10f;
    public float mouseSensitivity = 2f;

    // --- STATE ---
    private bool _isTacticalMode = true;
    private bool _isFreeFly = false;
    private bool _hasSnappedToHero = false; // Флаг: прыгнули ли мы к герою на старте?

    // Тактические переменные
    private Vector3 _pivotPoint;
    private float _currentYaw = 0f;
    private float _currentPitch = 60f;
    private float _currentDistance = 15f;

    // Иммерсивные переменные
    private float _freeYaw = 0f;
    private float _freePitch = 0f;

    void Start()
    {
        if (tacticalSystem == null)
            tacticalSystem = FindFirstObjectByType<TacticalSystem>();

        // Не инициализируем позицию здесь, ждем героя в Update
    }

    void Update()
    {
        // 0. ПРОВЕРКА ИНИЦИАЛИЗАЦИИ
        // Если мы еще не прыгнули к герою, и герой наконец-то появился
        if (!_hasSnappedToHero && tacticalSystem != null && tacticalSystem.HeroTransform != null)
        {
            SnapToHero();
            _hasSnappedToHero = true;
        }

        // 1. Переключение режимов
        if (Input.GetKeyDown(switchModeKey))
        {
            _isTacticalMode = !_isTacticalMode;
            if (!_isTacticalMode && !_isFreeFly && tacticalSystem.HeroTransform)
            {
                _freeYaw = tacticalSystem.HeroTransform.eulerAngles.y;
                _freePitch = 15f;
            }
        }

        // 2. Логика
        if (_isTacticalMode) UpdateTactical();
        else UpdateImmersive();
    }

    void SnapToHero()
    {
        if (tacticalSystem.HeroTransform)
        {
            _pivotPoint = tacticalSystem.HeroTransform.position;
            // Сбрасываем поворот камеры, чтобы смотреть "на север" или как удобно
            _currentYaw = 0f;
        }
    }

    // --- ТАКТИЧЕСКИЙ РЕЖИМ ---
    void UpdateTactical()
    {
        // ZOOM
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _currentDistance -= scroll * zoomSpeed;
        _currentDistance = Mathf.Clamp(_currentDistance, heightLimits.x, heightLimits.y);

        // ORBIT (Вращение - ПКМ)
        if (Input.GetMouseButton(1))
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            _currentYaw += dx * rotateSpeed;
            _currentPitch -= dy * rotateSpeed;
            _currentPitch = Mathf.Clamp(_currentPitch, angleLimits.x, angleLimits.y);
        }

        // PAN (Перемещение - СКМ)
        if (Input.GetMouseButton(2))
        {
            float dx = Input.GetAxis("Mouse X") * panSpeed * Time.deltaTime; // Добавил Time.deltaTime для плавности
            float dy = Input.GetAxis("Mouse Y") * panSpeed * Time.deltaTime;

            Vector3 right = transform.right;
            right.y = 0; right.Normalize();
            Vector3 forward = transform.forward;
            forward.y = 0; forward.Normalize(); // Двигаем в плоскости пола

            _pivotPoint -= (right * dx) + (forward * dy);

            // --- ОГРАНИЧЕНИЕ КАМЕРЫ (CLAMP) ---
            if (tacticalSystem != null)
            {
                // Добавляем небольшой отступ (padding), чтобы можно было видеть крайние стены
                float padding = 2.0f;
                float mapW = tacticalSystem.width * tacticalSystem.tileSize;
                float mapH = tacticalSystem.height * tacticalSystem.tileSize;

                _pivotPoint.x = Mathf.Clamp(_pivotPoint.x, -padding, mapW + padding);
                _pivotPoint.z = Mathf.Clamp(_pivotPoint.z, -padding, mapH + padding);
            }
        }

        // F - Фокус
        if (Input.GetKeyDown(KeyCode.F) && tacticalSystem.HeroTransform)
        {
            _pivotPoint = tacticalSystem.HeroTransform.position;
        }

        // Позиционирование
        Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0);
        Vector3 targetPos = _pivotPoint - (rotation * Vector3.forward * _currentDistance);

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * transitionSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * transitionSpeed);
    }

    // --- ИММЕРСИВНЫЙ РЕЖИМ ---
    void UpdateImmersive()
    {
        if (Input.GetKeyDown(freeFlyKey))
        {
            _isFreeFly = !_isFreeFly;
            if (_isFreeFly)
            {
                _freeYaw = transform.eulerAngles.y;
                _freePitch = transform.eulerAngles.x;
            }
        }

        if (_isFreeFly)
        {
            // Free Fly
            if (Input.GetMouseButton(1) || Cursor.lockState == CursorLockMode.Locked)
            {
                float dx = Input.GetAxis("Mouse X") * mouseSensitivity;
                float dy = Input.GetAxis("Mouse Y") * mouseSensitivity;
                _freeYaw += dx;
                _freePitch -= dy;
                _freePitch = Mathf.Clamp(_freePitch, -89f, 89f);
            }
            Quaternion rot = Quaternion.Euler(_freePitch, _freeYaw, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * transitionSpeed * 2);

            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            float y = 0;
            if (Input.GetKey(KeyCode.E)) y = 1;
            if (Input.GetKey(KeyCode.Q)) y = -1;

            Vector3 moveDir = (transform.right * x) + (transform.forward * z) + (Vector3.up * y);
            transform.position += moveDir * flySpeed * Time.deltaTime;
        }
        else
        {
            // За плечом
            Transform hero = tacticalSystem.HeroTransform;
            if (hero == null) return;

            if (Input.GetMouseButton(1))
            {
                _freeYaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            }

            Quaternion rot = Quaternion.Euler(0, _freeYaw, 0);
            Vector3 targetPos = hero.position + rot * shoulderOffset;

            // Смотрим чуть выше
            Vector3 lookAtPos = hero.position + Vector3.up * 1.5f + (rot * Vector3.forward * 5f);
            // ^ Небольшой хак: смотрим вперед по курсу взгляда, а не строго в затылок

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * transitionSpeed);
            transform.LookAt(hero.position + Vector3.up * 1.5f);
        }
    }
}