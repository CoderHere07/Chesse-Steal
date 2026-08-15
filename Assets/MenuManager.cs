using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MenuManager : MonoBehaviour
{
    private static MenuManager _instance;
    public static MenuManager Instance => _instance;

    // Persists whether we should jump straight to gameplay or show main menu
    public static bool startInGameplay = false;

    private GameObject _mainMenuCanvas;
    private GameObject _pauseMenuCanvas;
    
    private GameObject _mainMenuPanel;
    private GameObject _optionsPanel;

    // Difficulty button references for highlight feedback
    private Button _easyBtn;
    private Button _mediumBtn;
    private Button _hardBtn;

    private bool _isPaused = false;
    private bool _inMainMenu = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Object.FindFirstObjectByType<MenuManager>() == null)
        {
            GameObject go = new GameObject("MenuManager");
            go.AddComponent<MenuManager>();
        }
    }

    private void Awake()
    {
        // Maintain a single instance in the scene
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Persist the MenuManager across scene loads so it functions after restarting
        DontDestroyOnLoad(gameObject);

        // Build the dynamic UI elements
        BuildMainMenuUI();
        BuildPauseMenuUI();

        // Subscribe to scene loaded callback to configure state on restart
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ConfigureSceneState();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureSceneState();
    }

    private void ConfigureSceneState()
    {
        if (startInGameplay)
        {
            StartGameplay();
        }
        else
        {
            ShowMainMenu();
        }
    }

    private void Update()
    {
        // Don't handle pause if we are in the main menu
        if (_inMainMenu) return;

        // Toggle pause on Escape or P keys
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            PauseGame();
        }
        else
        {
            ResumeGame();
        }
    }

    public bool IsPaused() => _isPaused;
    public bool IsInMainMenu() => _inMainMenu;

    private void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;

        // Stop camera movement
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            fpc.cameraCanMove = false;
        }

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_pauseMenuCanvas != null)
        {
            _pauseMenuCanvas.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        _isPaused = false;
        Time.timeScale = 1f;

        // Resume camera movement
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            fpc.cameraCanMove = true;
        }

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_pauseMenuCanvas != null)
        {
            _pauseMenuCanvas.SetActive(false);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        startInGameplay = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        startInGameplay = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ShowMainMenu()
    {
        _inMainMenu = true;
        _isPaused = false;
        Time.timeScale = 0f;

        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            fpc.cameraCanMove = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_mainMenuCanvas != null)
        {
            _mainMenuCanvas.SetActive(true);
            _mainMenuPanel.SetActive(true);
            _optionsPanel.SetActive(false);
        }
        if (_pauseMenuCanvas != null)
        {
            _pauseMenuCanvas.SetActive(false);
        }
    }

    public void StartGameplay()
    {
        _inMainMenu = false;
        _isPaused = false;
        startInGameplay = true;
        Time.timeScale = 1f;

        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            fpc.cameraCanMove = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_mainMenuCanvas != null)
        {
            _mainMenuCanvas.SetActive(false);
        }
        if (_pauseMenuCanvas != null)
        {
            _pauseMenuCanvas.SetActive(false);
        }
    }

    public void ShowOptions()
    {
        if (_mainMenuPanel != null) _mainMenuPanel.SetActive(false);
        if (_optionsPanel != null) _optionsPanel.SetActive(true);
    }

    public void HideOptions()
    {
        if (_mainMenuPanel != null) _mainMenuPanel.SetActive(true);
        if (_optionsPanel != null) _optionsPanel.SetActive(false);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Difficulty helpers
    private void StartGameEasy()
    {
        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.SetDifficulty(DifficultyManager.Difficulty.Easy);
        RestartGame();
    }

    private void StartGameMedium()
    {
        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.SetDifficulty(DifficultyManager.Difficulty.Medium);
        RestartGame();
    }

    private void StartGameHard()
    {
        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.SetDifficulty(DifficultyManager.Difficulty.Hard);
        RestartGame();
    }

    // ─────────────────────────────────────────────
    // Dynamic UI Construction (Pure C# / Canvas)
    // ─────────────────────────────────────────────
    
    private void BuildMainMenuUI()
    {
        // Create Canvas for Main Menu
        _mainMenuCanvas = new GameObject("MainMenuCanvas");
        _mainMenuCanvas.transform.SetParent(transform); // Child of persistent MenuManager

        Canvas canvas = _mainMenuCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998; // Under win canvas

        CanvasScaler cs = _mainMenuCanvas.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;

        _mainMenuCanvas.AddComponent<GraphicRaycaster>();

        // Ensure EventSystem is present
        EnsureEventSystem();

        // Overlay Background
        GameObject overlay = CreateChild("Overlay", _mainMenuCanvas.transform);
        overlay.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        StretchFull(overlay);

        // Center Panel Card for Main Menu Main Panel
        _mainMenuPanel = CreateChild("MainMenuPanel", _mainMenuCanvas.transform);
        _mainMenuPanel.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 1f);
        SetAnchored(_mainMenuPanel, new Vector2(0.5f, 0.5f), new Vector2(700f, 550f), Vector2.zero);

        Font font = GetFont();

        // Menu Title
        AddTextAbsolute(_mainMenuPanel.transform, "Title", "CHEESE STEAL", font, 52, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
            new Vector2(600f, 75f), new Vector2(0f, 190f));

        // Subtitle
        AddTextAbsolute(_mainMenuPanel.transform, "Subtitle", "Help Jerry collect the Cheese and avoid the traps!", font, 22, FontStyle.Italic, new Color(0.8f, 0.8f, 0.8f),
            new Vector2(600f, 60f), new Vector2(0f, 115f));

        // Difficulty Label
        AddTextAbsolute(_mainMenuPanel.transform, "DiffLabel", "SELECT DIFFICULTY", font, 18, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
            new Vector2(600f, 30f), new Vector2(0f, 60f));

        // Difficulty row — Easy / Medium / Hard
        float btnY   = 15f;
        float btnW   = 160f;
        float btnH   = 50f;
        float gap    = 20f;
        float totalW = btnW * 3 + gap * 2;  // 540
        float startX = -totalW / 2f + btnW / 2f;  // left edge of first btn

        _easyBtn   = CreateButtonAndReturn(_mainMenuPanel.transform, "EasyBtn",   "EASY",   font, 22, new Color(0.18f, 0.60f, 0.28f), new Color(0.25f, 0.78f, 0.38f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(btnW, btnH), new Vector2(startX, btnY), StartGameEasy);
        _mediumBtn = CreateButtonAndReturn(_mainMenuPanel.transform, "MediumBtn", "MEDIUM", font, 22, new Color(0.85f, 0.55f, 0.10f), new Color(0.95f, 0.70f, 0.20f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(btnW, btnH), new Vector2(startX + btnW + gap, btnY), StartGameMedium);
        _hardBtn   = CreateButtonAndReturn(_mainMenuPanel.transform, "HardBtn",   "HARD",   font, 22, new Color(0.70f, 0.15f, 0.15f), new Color(0.88f, 0.22f, 0.22f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(btnW, btnH), new Vector2(startX + (btnW + gap) * 2f, btnY), StartGameHard);

        // How to Play Button
        CreateButton(_mainMenuPanel.transform, "OptionsButton", "How to Play", font, 26, new Color(0.85f, 0.6f, 0.15f), new Color(0.95f, 0.7f, 0.2f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(360f, 65f), new Vector2(0f, -55f), ShowOptions);

        // Quit Button
        CreateButton(_mainMenuPanel.transform, "QuitButton", "Quit Game", font, 26, new Color(0.7f, 0.2f, 0.2f), new Color(0.85f, 0.25f, 0.25f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(360f, 65f), new Vector2(0f, -135f), QuitGame);

        // Footer / Version
        AddTextAbsolute(_mainMenuPanel.transform, "Footer", "v2.0.0 • Dynamic Maze", font, 18, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f),
            new Vector2(600f, 40f), new Vector2(0f, -210f));

        // ────────── Options Panel Card (How to Play) ──────────
        _optionsPanel = CreateChild("OptionsPanel", _mainMenuCanvas.transform);
        _optionsPanel.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 1f);
        SetAnchored(_optionsPanel, new Vector2(0.5f, 0.5f), new Vector2(700f, 550f), Vector2.zero);
        _optionsPanel.SetActive(false);

        // Options Title
        AddTextAbsolute(_optionsPanel.transform, "OptionsTitle", "HOW TO PLAY", font, 42, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
            new Vector2(600f, 60f), new Vector2(0f, 190f));

        // Instructions Text
        string optionsDesc = 
            "<b>Controls</b>\n" +
            "• Move: W/A/S/D or Arrow Keys\n" +
            "• Look around: Mouse\n" +
            "• Sprint: Hold Left Shift\n" +
            "• Pause: Press Escape or P key\n\n" +
            "<b>Rules</b>\n" +
            "• Maze changes every run!\n" +
            "• Collect the cheese before the timer runs out\n" +
            "• Avoid the traps & moving walls\n" +
            "• Harder difficulty = bigger maze, faster traps";

        AddTextAbsolute(_optionsPanel.transform, "OptionsText", optionsDesc, font, 22, FontStyle.Normal, Color.white,
            new Vector2(600f, 240f), new Vector2(0f, 25f));

        // Back Button
        CreateButton(_optionsPanel.transform, "BackButton", "Back", font, 24, new Color(0.4f, 0.4f, 0.45f), new Color(0.5f, 0.5f, 0.55f), Color.white,
            new Vector2(0.5f, 0f), new Vector2(220f, 55f), new Vector2(0f, 40f), HideOptions);
    }

    private void BuildPauseMenuUI()
    {
        // Create Canvas for Pause Menu
        _pauseMenuCanvas = new GameObject("PauseMenuCanvas");
        _pauseMenuCanvas.transform.SetParent(transform); // Child of persistent MenuManager

        Canvas canvas = _pauseMenuCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 997; // Under Main Menu and Win panel

        CanvasScaler cs = _pauseMenuCanvas.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;

        _pauseMenuCanvas.AddComponent<GraphicRaycaster>();

        // Overlay Background
        GameObject overlay = CreateChild("Overlay", _pauseMenuCanvas.transform);
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        StretchFull(overlay);

        // Center Panel Card for Pause Menu
        GameObject pausePanel = CreateChild("PausePanel", _pauseMenuCanvas.transform);
        pausePanel.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 1f);
        SetAnchored(pausePanel, new Vector2(0.5f, 0.5f), new Vector2(700f, 530f), Vector2.zero);

        Font font = GetFont();

        // Pause Title
        AddTextAbsolute(pausePanel.transform, "Title", "GAME PAUSED", font, 48, FontStyle.Bold, Color.white,
            new Vector2(600f, 70f), new Vector2(0f, 180f));

        // Resume Button
        CreateButton(pausePanel.transform, "ResumeBtn", "Resume", font, 26, new Color(0.18f, 0.68f, 0.28f), new Color(0.25f, 0.85f, 0.38f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(360f, 60f), new Vector2(0f, 90f), ResumeGame);

        // Restart Button
        CreateButton(pausePanel.transform, "RestartBtn", "Restart Game", font, 26, new Color(0.85f, 0.6f, 0.15f), new Color(0.95f, 0.7f, 0.2f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(360f, 60f), new Vector2(0f, 20f), RestartGame);

        // Main Menu Button
        CreateButton(pausePanel.transform, "MainMenuBtn", "Main Menu", font, 26, new Color(0.2f, 0.45f, 0.7f), new Color(0.25f, 0.55f, 0.85f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(360f, 60f), new Vector2(0f, -50f), GoToMainMenu);

        // Quit Button
        CreateButton(pausePanel.transform, "QuitBtn", "Quit Game", font, 26, new Color(0.7f, 0.2f, 0.2f), new Color(0.85f, 0.25f, 0.25f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(360f, 60f), new Vector2(0f, -120f), QuitGame);

        // Start disabled
        _pauseMenuCanvas.SetActive(false);
    }

    // ─────────────────────────────────────────────
    // UI Helpers (borrowed and enhanced)
    // ─────────────────────────────────────────────
    
    private static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchored(GameObject go, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    private static void AddText(Transform parent, string name,
        string text, Font font, int size, FontStyle style, Color color,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = CreateChild(name, parent);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AddTextAbsolute(Transform parent, string name,
        string text, Font font, int size, FontStyle style, Color color,
        Vector2 sizeDelta, Vector2 pos)
    {
        GameObject go = CreateChild(name, parent);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        SetAnchored(go, new Vector2(0.5f, 0.5f), sizeDelta, pos);
    }

    private static void CreateButton(Transform parent, string name, string labelText, Font font, int fontSize,
        Color normalColor, Color highlightedColor, Color textColor,
        Vector2 anchor, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClickAction)
    {
        CreateButtonAndReturn(parent, name, labelText, font, fontSize, normalColor, highlightedColor, textColor, anchor, size, pos, onClickAction);
    }

    private static Button CreateButtonAndReturn(Transform parent, string name, string labelText, Font font, int fontSize,
        Color normalColor, Color highlightedColor, Color textColor,
        Vector2 anchor, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnGO = CreateChild(name, parent);
        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.color = normalColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = highlightedColor;
        cb.pressedColor = normalColor * 0.7f;
        btn.colors = cb;
        btn.onClick.AddListener(onClickAction);

        SetAnchored(btnGO, anchor, size, pos);

        // Label
        GameObject labelGO = CreateChild("Label", btnGO.transform);
        Text t = labelGO.AddComponent<Text>();
        t.text = labelText;
        t.font = font;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = textColor;
        StretchFull(labelGO);

        return btn;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(esGO);
        }
    }
}
