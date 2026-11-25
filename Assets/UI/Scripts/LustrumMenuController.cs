using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class LustrumMenuController : MonoBehaviour
{
    public enum MenuState { Main, Settings, LoadGame }

    [Header("Camera Movement")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float mainMenuY = 0f;
    [SerializeField] private float settingsY = 26.5f;
    [SerializeField] private float loadGameY = -25f;
    [SerializeField] private float cameraSmoothTime = 0.25f;

    private UIDocument _uiDocument;
    private VisualElement _screenMain;
    private VisualElement _screenSettings;
    private VisualElement _screenLoadGame;

    // Память фокуса для главного меню
    private Button _btnResume;
    private Button _btnSettings;
    private Button _btnLoadGame;
    private Button _lastMainMenuButton;

    // Контролы экрана Settings
    private DropdownField _ddLanguage;
    private Slider _sldTextSpeed;
    private Label _lblTextSpeed;
    private Slider _sldAutoSpeed;
    private Label _lblAutoSpeed;
    private Button _btnSizeNormal;
    private Button _btnSizeLarge;

    private MenuState _currentState = MenuState.Main;
    private float _targetCameraY;
    private float _velocityY;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        var root = _uiDocument.rootVisualElement;

        // 1. Находим корневые экраны (screen-load-game пока может быть null — это безопасно)
        _screenMain = root.Q<VisualElement>("screen-main");
        _screenSettings = root.Q<VisualElement>("screen-settings");
        _screenLoadGame = root.Q<VisualElement>("screen-load-game");

        // 2. Включаем Focus Follows Hover для всех кнопок и будущих карточек сохранений
        SetupHoverFocus(root);

        // 3. Кнопки главного меню (запоминаем, откуда ушли, для красивого возврата фокуса)
        _btnResume = root.Q<Button>("btn-resume");
        _btnSettings = root.Q<Button>("btn-settings");
        _btnLoadGame = root.Q<Button>("btn-load-game");
        _lastMainMenuButton = _btnResume;

        _btnSettings?.RegisterCallback<ClickEvent>(_ =>
        {
            _lastMainMenuButton = _btnSettings;
            SwitchState(MenuState.Settings);
        });

        _btnLoadGame?.RegisterCallback<ClickEvent>(_ =>
        {
            _lastMainMenuButton = _btnLoadGame;
            SwitchState(MenuState.LoadGame);
        });

        root.Q<Button>("btn-quit")?.RegisterCallback<ClickEvent>(_ => Debug.Log("Quit clicked"));
        root.Q<Button>("btn-back-hint")?.RegisterCallback<ClickEvent>(_ => SwitchState(MenuState.Main));

        // 4. Вкладки и контролы экрана Settings
        SetupTabs(root);

        _ddLanguage = root.Q<DropdownField>("dd-language");

        _sldTextSpeed = root.Q<Slider>("sld-text-speed");
        _lblTextSpeed = root.Q<Label>("lbl-text-speed");
        BindSlider(_sldTextSpeed, _lblTextSpeed);

        _sldAutoSpeed = root.Q<Slider>("sld-auto-speed");
        _lblAutoSpeed = root.Q<Label>("lbl-auto-speed");
        BindSlider(_sldAutoSpeed, _lblAutoSpeed);

        _btnSizeNormal = root.Q<Button>("btn-size-normal");
        _btnSizeLarge = root.Q<Button>("btn-size-large");
        _btnSizeNormal?.RegisterCallback<ClickEvent>(_ => SetTextSize(true));
        _btnSizeLarge?.RegisterCallback<ClickEvent>(_ => SetTextSize(false));

        root.Q<Button>("btn-reset")?.RegisterCallback<ClickEvent>(_ => ResetSettings());
        root.Q<Button>("btn-apply")?.RegisterCallback<ClickEvent>(_ =>
        {
            Debug.Log($"Applied! Lang: {_ddLanguage?.value}, TextSpeed: {_sldTextSpeed?.value:0.0}");
            SwitchState(MenuState.Main);
        });

        // 5. Стартовое состояние без анимации + фокус на RESUME
        _currentState = MenuState.Main;
        _targetCameraY = mainMenuY;
        SetScreenVisible(_screenMain, true, "screen--hidden-bottom", instant: true);
        SetScreenVisible(_screenSettings, false, "screen--hidden-top", instant: true);
        SetScreenVisible(_screenLoadGame, false, "screen--hidden-bottom", instant: true);
        FocusDelayed(_btnResume);
    }

    private void Update()
    {
        if (targetCamera != null)
        {
            Vector3 pos = targetCamera.transform.position;
            pos.y = Mathf.SmoothDamp(pos.y, _targetCameraY, ref _velocityY, cameraSmoothTime);
            targetCamera.transform.position = pos;
        }

        if (_currentState != MenuState.Main &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SwitchState(MenuState.Main);
        }
    }

    public void SwitchState(MenuState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;

        switch (newState)
        {
            case MenuState.Main:
                _targetCameraY = mainMenuY;
                SetScreenVisible(_screenMain, true, "screen--hidden-bottom");
                SetScreenVisible(_screenSettings, false, "screen--hidden-top");
                SetScreenVisible(_screenLoadGame, false, "screen--hidden-bottom");
                FocusDelayed(_lastMainMenuButton ?? _btnResume);
                break;

            case MenuState.Settings:
                _targetCameraY = settingsY;
                SetScreenVisible(_screenMain, false, "screen--hidden-bottom");
                SetScreenVisible(_screenSettings, true, "screen--hidden-top");
                SetScreenVisible(_screenLoadGame, false, "screen--hidden-bottom");

                // Ищем именно текущую активную вкладку по классу
                var activeTab = _screenSettings?.Q<Button>(className: "tab-btn--active");
                FocusDelayed(activeTab);
                break;

            case MenuState.LoadGame:
                _targetCameraY = loadGameY;
                SetScreenVisible(_screenMain, false, "screen--hidden-top");
                SetScreenVisible(_screenSettings, false, "screen--hidden-top");
                SetScreenVisible(_screenLoadGame, true, "screen--hidden-bottom");

                // Q() всегда берёт первый подходящий элемент — это и будет первая карточка
                var firstSaveCard = _screenLoadGame?.Q<VisualElement>(className: "save-card");
                FocusDelayed(firstSaveCard);
                break;
        }
    }

    private void SetupHoverFocus(VisualElement root)
    {
        root.Query<Button>().ForEach(RegisterHoverFocus);
        root.Query<VisualElement>(className: "save-card").ForEach(RegisterHoverFocus);
    }

    private void RegisterHoverFocus(VisualElement el)
    {
        if (el == null) return;
        el.RegisterCallback<PointerEnterEvent>(_ =>
        {
            // Проверяем, что элемент активен и может принимать фокус
            if (el.focusable && el.enabledInHierarchy)
                el.Focus();
        });
    }

    private void FocusDelayed(VisualElement target)
    {
        if (target == null) return;

        // Задержка 30мс даёт экрану выйти из display: none перед установкой фокуса.
        // Повторная проверка resolvedStyle защищает от быстрого спама кнопками.
        target.schedule.Execute(() =>
        {
            if (target.focusable && target.resolvedStyle.display != DisplayStyle.None)
                target.Focus();
        }).StartingIn(30);
    }

    private void SetScreenVisible(VisualElement screen, bool visible, string hiddenClass, bool instant = false)
    {
        if (screen == null) return;

        if (visible)
        {
            screen.style.display = DisplayStyle.Flex;
            if (instant)
            {
                screen.RemoveFromClassList("screen--hidden-top");
                screen.RemoveFromClassList("screen--hidden-bottom");
            }
            else
            {
                screen.schedule.Execute(() =>
                {
                    screen.RemoveFromClassList("screen--hidden-top");
                    screen.RemoveFromClassList("screen--hidden-bottom");
                }).StartingIn(10);
            }
        }
        else
        {
            screen.AddToClassList(hiddenClass);
            if (instant)
            {
                screen.style.display = DisplayStyle.None;
            }
            else
            {
                screen.schedule.Execute(() =>
                {
                    if (screen.ClassListContains(hiddenClass))
                        screen.style.display = DisplayStyle.None;
                }).StartingIn(360);
            }
        }
    }

    private void SetupTabs(VisualElement root)
    {
        string[] tabs = { "game", "audio", "display", "controls" };

        foreach (string tab in tabs)
        {
            var btn = root.Q<Button>($"tab-btn-{tab}");
            if (btn == null) continue;

            string currentTab = tab;
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                foreach (string t in tabs)
                {
                    root.Q<Button>($"tab-btn-{t}")?.EnableInClassList("tab-btn--active", t == currentTab);
                    root.Q<VisualElement>($"page-{t}")?.EnableInClassList("hidden", t != currentTab);
                }
            });
        }
    }

    private void BindSlider(Slider slider, Label valueLabel)
    {
        if (slider == null || valueLabel == null) return;

        valueLabel.text = slider.value.ToString("0.0", CultureInfo.InvariantCulture);
        slider.RegisterValueChangedCallback(evt =>
        {
            valueLabel.text = evt.newValue.ToString("0.0", CultureInfo.InvariantCulture);
        });
    }

    private void SetTextSize(bool isNormal)
    {
        _btnSizeNormal?.EnableInClassList("seg-btn--active", isNormal);
        _btnSizeLarge?.EnableInClassList("seg-btn--active", !isNormal);
    }

    private void ResetSettings()
    {
        if (_ddLanguage != null) _ddLanguage.index = 0;
        if (_sldTextSpeed != null) _sldTextSpeed.value = 1.0f;
        if (_sldAutoSpeed != null) _sldAutoSpeed.value = 1.0f;
        SetTextSize(true);
    }
}
