using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Central game-state controller.
/// Handles: countdown timer, score, HUD, death screen, time-up screen.
/// Persists across scene loads. Works alongside MenuManager and CheeseCollectible.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    private static GameManager _instance;
    public static GameManager Instance => _instance;

    // ── State ──────────────────────────────────────────────────────────────
    private float _timeRemaining;
    private bool  _gameActive   = false;
    private bool  _gameOver     = false;

    // ── HUD ───────────────────────────────────────────────────────────────
    private GameObject _hudCanvas;
    private Text       _timerLabel;
    private Text       _difficultyLabel;

    // ── Death / Time-up Panel ─────────────────────────────────────────────
    private GameObject _overlayCanvas;
    private Text       _overlayTitle;
    private Text       _overlaySubtitle;



    // ─────────────────────────────────────────────────────────────────────
    // Bootstrap
    // ─────────────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<GameManager>() == null)
        {
            GameObject go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        BuildHUD();
        BuildOverlayPanel();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Reset state when a new scene is loaded
        _gameOver = false;
        _gameActive = false;
        HideOverlay();
        HideHUD();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Called by MenuManager / MazeGenerator when gameplay actually begins.</summary>
    public void StartGame()
    {
        DifficultyManager dm = DifficultyManager.Instance;
        _timeRemaining = dm != null ? dm.TimeLimit : 180f;
        _gameOver  = false;
        _gameActive = true;

        if (_difficultyLabel != null && dm != null)
            _difficultyLabel.text = dm.DifficultyLabel().ToUpper();

        ShowHUD();
        HideOverlay();
    }

    /// <summary>Called by dynamic trap scripts when the player is killed.</summary>
    public void TriggerDeath(string causeMessage = "You were crushed!")
    {
        if (_gameOver) return;
        _gameOver   = true;
        _gameActive = false;

        // Freeze player input and physics immediately
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) 
        {
            fpc.playerCanMove = false;
            Rigidbody rb = fpc.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        // Freeze all traps so they don't clip through the player's camera
        foreach (var crush in FindObjectsByType<DynamicCrushTrap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            crush.enabled = false;
        }
        foreach (var upDown in FindObjectsByType<DynamicUpDownTrap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            upDown.enabled = false;
        }

        StartCoroutine(ShowDeathAfterDelay(causeMessage));
    }

    /// <summary>Called when the timer hits zero.</summary>
    private void TriggerTimeUp()
    {
        if (_gameOver) return;
        _gameOver   = true;
        _gameActive = false;

        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) fpc.playerCanMove = false;

        ShowOverlay("TIME'S UP!", "The maze was too large...", Color.red);
    }

    public bool IsGameActive() => _gameActive;
    public float TimeRemaining => _timeRemaining;

    // ─────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!_gameActive) return;

        _timeRemaining -= Time.deltaTime;
        _timeRemaining  = Mathf.Max(0f, _timeRemaining);

        UpdateTimerDisplay();

        if (_timeRemaining <= 0f)
            TriggerTimeUp();
    }



    public void StopGameTimer()
    {
        _gameActive = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Coroutines
    // ─────────────────────────────────────────────────────────────────────
    private IEnumerator ShowDeathAfterDelay(string cause)
    {
        // Show HUD stays visible briefly, then swap to overlay
        yield return new WaitForSecondsRealtime(1.5f);

        // Freeze game
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        HideHUD();
        ShowOverlay("YOU DIED!", cause, new Color(0.85f, 0.1f, 0.1f));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Button Actions
    // ─────────────────────────────────────────────────────────────────────
    private void OnTryAgain()
    {
        Time.timeScale = 1f;
        MenuManager.startInGameplay = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnMainMenu()
    {
        Time.timeScale = 1f;
        MenuManager.startInGameplay = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ─────────────────────────────────────────────────────────────────────
    // HUD
    // ─────────────────────────────────────────────────────────────────────
    private void ShowHUD()    { if (_hudCanvas != null) _hudCanvas.SetActive(true); }
    private void HideHUD()    { if (_hudCanvas != null) _hudCanvas.SetActive(false); }

    private void UpdateTimerDisplay()
    {
        if (_timerLabel == null) return;
        int minutes = Mathf.FloorToInt(_timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(_timeRemaining % 60f);
        _timerLabel.text = $"{minutes:00}:{seconds:00}";

        // Turn red when under 30 seconds
        _timerLabel.color = _timeRemaining <= 30f
            ? new Color(1f, 0.25f, 0.25f)
            : Color.white;
    }

    private void BuildHUD()
    {
        _hudCanvas = new GameObject("GameHUDCanvas");
        _hudCanvas.transform.SetParent(transform);

        Canvas c = _hudCanvas.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;

        CanvasScaler cs = _hudCanvas.AddComponent<CanvasScaler>();
        cs.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution  = new Vector2(1920, 1080);
        cs.matchWidthOrHeight   = 0.5f;

        _hudCanvas.AddComponent<GraphicRaycaster>();

        Font font = GetFont();

        // ── Timer (top-right) ──────────────────────────────────────────
        GameObject timerGO = CreateChild("TimerContainer", _hudCanvas.transform);
        SetAnchored(timerGO, new Vector2(1f, 1f), new Vector2(200f, 70f), new Vector2(-110f, -45f));

        Image timerBg = timerGO.AddComponent<Image>();
        timerBg.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject timerTxt = CreateChild("TimerText", timerGO.transform);
        _timerLabel = timerTxt.AddComponent<Text>();
        _timerLabel.font       = font;
        _timerLabel.fontSize   = 36;
        _timerLabel.fontStyle  = FontStyle.Bold;
        _timerLabel.alignment  = TextAnchor.MiddleCenter;
        _timerLabel.color      = Color.white;
        _timerLabel.text       = "03:00";
        StretchFull(timerTxt);

        // ── Difficulty badge (top-left) ────────────────────────────────
        GameObject diffGO = CreateChild("DiffBadge", _hudCanvas.transform);
        SetAnchored(diffGO, new Vector2(0f, 1f), new Vector2(180f, 50f), new Vector2(100f, -35f));
        diffGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        GameObject diffTxt = CreateChild("DiffText", diffGO.transform);
        _difficultyLabel = diffTxt.AddComponent<Text>();
        _difficultyLabel.font      = font;
        _difficultyLabel.fontSize  = 22;
        _difficultyLabel.alignment = TextAnchor.MiddleCenter;
        _difficultyLabel.color     = new Color(1f, 0.85f, 0.2f);
        _difficultyLabel.text      = "MEDIUM";
        StretchFull(diffTxt);

        _hudCanvas.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Overlay Panel (Death / Time-up)
    // ─────────────────────────────────────────────────────────────────────
    private void ShowOverlay(string title, string subtitle, Color titleColor)
    {
        if (_overlayCanvas == null) return;
        _overlayTitle.text    = title;
        _overlayTitle.color   = titleColor;
        _overlaySubtitle.text = subtitle;
        _overlayCanvas.SetActive(true);
    }

    private void HideOverlay()
    {
        if (_overlayCanvas != null) _overlayCanvas.SetActive(false);
    }

    private void BuildOverlayPanel()
    {
        EnsureEventSystem();

        _overlayCanvas = new GameObject("GameOverCanvas");
        _overlayCanvas.transform.SetParent(transform);

        Canvas c = _overlayCanvas.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 996;

        CanvasScaler cs = _overlayCanvas.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight  = 0.5f;

        _overlayCanvas.AddComponent<GraphicRaycaster>();

        Font font = GetFont();

        // Dark full-screen overlay
        GameObject bg = CreateChild("Overlay", _overlayCanvas.transform);
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
        StretchFull(bg);

        // Card
        GameObject card = CreateChild("Card", _overlayCanvas.transform);
        card.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 1f);
        SetAnchored(card, new Vector2(0.5f, 0.5f), new Vector2(680f, 380f), Vector2.zero);

        // Title
        GameObject titleGO = CreateChild("Title", card.transform);
        _overlayTitle = titleGO.AddComponent<Text>();
        _overlayTitle.font       = font;
        _overlayTitle.fontSize   = 58;
        _overlayTitle.fontStyle  = FontStyle.Bold;
        _overlayTitle.alignment  = TextAnchor.MiddleCenter;
        _overlayTitle.color      = new Color(0.85f, 0.1f, 0.1f);
        _overlayTitle.text       = "YOU DIED!";
        _overlayTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        _overlayTitle.verticalOverflow   = VerticalWrapMode.Overflow;
        SetAnchored(titleGO, new Vector2(0.5f, 0.5f), new Vector2(620f, 80f), new Vector2(0f, 110f));

        // Subtitle / cause message
        GameObject subGO = CreateChild("Subtitle", card.transform);
        _overlaySubtitle = subGO.AddComponent<Text>();
        _overlaySubtitle.font       = font;
        _overlaySubtitle.fontSize   = 26;
        _overlaySubtitle.alignment  = TextAnchor.MiddleCenter;
        _overlaySubtitle.color      = new Color(0.85f, 0.85f, 0.85f);
        _overlaySubtitle.text       = "";
        _overlaySubtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        _overlaySubtitle.verticalOverflow   = VerticalWrapMode.Overflow;
        SetAnchored(subGO, new Vector2(0.5f, 0.5f), new Vector2(620f, 80f), new Vector2(0f, 20f));

        // Try Again button
        CreateButton(card.transform, "TryAgainBtn", "Try Again", font, 26,
            new Color(0.18f, 0.68f, 0.28f), new Color(0.25f, 0.85f, 0.38f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(260f, 60f), new Vector2(-145f, -90f), OnTryAgain);

        // Main Menu button
        CreateButton(card.transform, "MainMenuBtn", "Main Menu", font, 26,
            new Color(0.2f, 0.45f, 0.7f), new Color(0.25f, 0.55f, 0.85f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(260f, 60f), new Vector2(145f, -90f), OnMainMenu);

        _overlayCanvas.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    // UI Helpers
    // ─────────────────────────────────────────────────────────────────────
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
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
    }

    private static void CreateButton(Transform parent, string name, string label, Font font, int fontSize,
        Color normalColor, Color highlightedColor, Color textColor,
        Vector2 anchor, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGO = CreateChild(name, parent);
        btnGO.AddComponent<Image>().color = normalColor;

        Button btn  = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = normalColor;
        cb.highlightedColor = highlightedColor;
        cb.pressedColor     = normalColor * 0.7f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
        SetAnchored(btnGO, anchor, size, pos);

        GameObject labelGO = CreateChild("Label", btnGO.transform);
        Text t = labelGO.AddComponent<Text>();
        t.text      = label;
        t.font      = font;
        t.fontSize  = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color     = textColor;
        StretchFull(labelGO);
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
