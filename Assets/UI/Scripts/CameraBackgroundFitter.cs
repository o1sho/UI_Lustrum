using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CameraBackgroundFitter : MonoBehaviour
{
    [Tooltip("Перетащи сюда объект фона MainMenuBG со сцены")]
    [SerializeField] private SpriteRenderer backgroundSprite;

    [Tooltip("Стандартный Size камеры для 16:9")]
    [SerializeField] private float baseOrthographicSize = 5f;

    private Camera _camera;

    // Вызывается один раз при добавлении скрипта на камеру — сам подтянет твой текущий Size
    private void Reset()
    {
        var cam = GetComponent<Camera>();
        if (cam != null)
            baseOrthographicSize = cam.orthographicSize;
    }

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        FitCamera();
    }

    private void Update()
    {
        FitCamera();
    }

    private void FitCamera()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        if (_camera == null || backgroundSprite == null || _camera.aspect <= 0f)
            return;

        // Ширина спрайта в мировых юнитах (с учётом его Scale)
        float spriteWidth = backgroundSprite.bounds.size.x;

        // Максимальный Size, при котором края камеры не вылезут за фон
        float maxSizeToFitWidth = spriteWidth / (2f * _camera.aspect);

        float targetSize = Mathf.Min(baseOrthographicSize, maxSizeToFitWidth);

        if (!Mathf.Approximately(_camera.orthographicSize, targetSize))
            _camera.orthographicSize = targetSize;
    }
}
