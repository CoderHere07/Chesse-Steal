using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to the Cheese GameObject.
/// – Detects player by distance (works for 2D sprites in 3D).
/// – Hides only the renderer (keeps the script alive).
/// – Freezes the game with Time.timeScale = 0.
/// – Shows a full-screen win panel instantly.
/// </summary>
public class CheeseCollectible : MonoBehaviour
{
    [Tooltip("Tag that the Player GameObject must have.")]
    public string playerTag = "Player";

    [Tooltip("Collect distance in world units.")]
    public float collectRadius = 1.5f;

    private Transform _playerTransform;
    private GameObject _canvasGO;
    private bool _won = false;

    // ─────────────────────────────────────────────
    // Init
    // ─────────────────────────────────────────────
    private void Start()
    {
        // Cache player
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
            _playerTransform = p.transform;
        else
            Debug.LogWarning($"CheeseCollectible: No GameObject tagged '{playerTag}' found!");

        // Make any existing collider a trigger
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // If it has a Rigidbody, make it kinematic so it doesn't fall through the floor as a trigger
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // Build the win panel (hidden until triggered)
        BuildWinPanel();
    }

    // ─────────────────────────────────────────────
    // Detection
    // ─────────────────────────────────────────────
    private void Update()
    {
        if (_won) return;
        if (_playerTransform == null) return;

        if (Vector3.Distance(transform.position, _playerTransform.position) <= collectRadius)
            TriggerWin();
    }

    // Trigger / Collision fallbacks
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) TriggerWin();
    }
    private void OnCollisionEnter(Collision c)
    {
        if (c.gameObject.CompareTag(playerTag)) TriggerWin();
    }

    // ─────────────────────────────────────────────
    // Win
    // ─────────────────────────────────────────────
    private void TriggerWin()
    {
        if (_won) return;
        _won = true;
        Debug.Log("YOU WIN!");

        // Stop background music
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.StopMusic();
        }

        // Stop game timer
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StopGameTimer();
        }

        // Update win text if subtitle text exists
        if (_canvasGO != null)
        {
            Text subText = _canvasGO.GetComponentInChildren<Text>();
            // Search specifically for the Sub text component
            Text[] texts = _canvasGO.GetComponentsInChildren<Text>();
            foreach (Text t in texts)
            {
                if (t.gameObject.name == "Sub")
                {
                    t.text = "You found the Cheese!";
                    break;
                }
            }
        }

        // Hide only the renderer — keeps the GameObject (and this script) alive
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;

        // Freeze everything (player, physics, animations)
        Time.timeScale = 0f;

        // Unlock cursor for button click
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Show panel immediately at full opacity
        if (_canvasGO != null)
        {
            _canvasGO.SetActive(true);
            CanvasGroup cg = _canvasGO.GetComponentInChildren<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha           = 1f;
                cg.interactable    = true;
                cg.blocksRaycasts  = true;
            }
        }
    }

    // ─────────────────────────────────────────────
    // Play Again / Main Menu
    // ─────────────────────────────────────────────
    private void PlayAgain()
    {
        Time.timeScale = 1f;   // restore time before reload
        MenuManager.startInGameplay = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;   // restore time before reload
        MenuManager.startInGameplay = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ─────────────────────────────────────────────
    // Gizmo
    // ─────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectRadius);
    }

    // ─────────────────────────────────────────────
    // Build Win Panel
    // ─────────────────────────────────────────────
    private void BuildWinPanel()
    {
        // ── EventSystem (required for button clicks) ─────────────
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // ── Canvas ──────────────────────────────────────────────
        _canvasGO = new GameObject("WinCanvas");

        Canvas canvas        = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder  = 999;

        CanvasScaler cs             = _canvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode              = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution      = new Vector2(1920, 1080);
        cs.screenMatchMode          = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight       = 0.5f;

        _canvasGO.AddComponent<GraphicRaycaster>();

        // ── Full-screen dark overlay ─────────────────────────────
        GameObject overlayGO = CreateChild("Overlay", _canvasGO.transform);
        overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
        StretchFull(overlayGO);

        // ── Centre card ──────────────────────────────────────────
        GameObject cardGO = CreateChild("Card", _canvasGO.transform);
        cardGO.AddComponent<Image>().color = new Color(0.09f, 0.09f, 0.09f, 1f);
        SetAnchored(cardGO, new Vector2(0.5f, 0.5f), new Vector2(600f, 320f), Vector2.zero);

        // CanvasGroup sits on the card — controlled in TriggerWin()
        CanvasGroup cg       = cardGO.AddComponent<CanvasGroup>();
        cg.alpha             = 0f;
        cg.interactable      = false;
        cg.blocksRaycasts    = false;

        // ── "YOU WIN!" ───────────────────────────────────────────
        Font font = GetFont();
        AddText(cardGO.transform, "Title",
            "YOU WIN!", font, 64, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
            new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.98f));

        // ── Subtitle ─────────────────────────────────────────────
        AddText(cardGO.transform, "Sub",
            "You found the Cheese!", font, 26, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f),
            new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.57f));

        // ── Play Again button ─────────────────────────────────────
        GameObject btnGO = CreateChild("PlayAgainBtn", cardGO.transform);
        Image btnBg      = btnGO.AddComponent<Image>();
        btnBg.color      = new Color(0.18f, 0.68f, 0.28f);

        Button btn       = btnGO.AddComponent<Button>();
        ColorBlock cb    = btn.colors;
        cb.normalColor      = new Color(0.18f, 0.68f, 0.28f);
        cb.highlightedColor = new Color(0.25f, 0.85f, 0.38f);
        cb.pressedColor     = new Color(0.12f, 0.48f, 0.20f);
        btn.colors = cb;
        btn.onClick.AddListener(PlayAgain);

        RectTransform btnRT    = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin        = new Vector2(0.5f, 0f);
        btnRT.anchorMax        = new Vector2(0.5f, 0f);
        btnRT.pivot            = new Vector2(0.5f, 0.5f);
        btnRT.sizeDelta        = new Vector2(230f, 62f);
        btnRT.anchoredPosition = new Vector2(-130f, 50f);

        AddText(btnGO.transform, "BtnLabel",
            "Play Again", font, 28, FontStyle.Bold, Color.white,
            Vector2.zero, Vector2.one);

        // ── Main Menu button ─────────────────────────────────────
        GameObject mmBtnGO = CreateChild("MainMenuBtn", cardGO.transform);
        Image mmBtnBg      = mmBtnGO.AddComponent<Image>();
        mmBtnBg.color      = new Color(0.2f, 0.45f, 0.7f);

        Button mmBtn       = mmBtnGO.AddComponent<Button>();
        ColorBlock mmCb    = mmBtn.colors;
        mmCb.normalColor      = new Color(0.2f, 0.45f, 0.7f);
        mmCb.highlightedColor = new Color(0.25f, 0.55f, 0.85f);
        mmCb.pressedColor     = new Color(0.15f, 0.35f, 0.55f);
        mmBtn.colors = mmCb;
        mmBtn.onClick.AddListener(GoToMainMenu);

        RectTransform mmBtnRT    = mmBtnGO.GetComponent<RectTransform>();
        mmBtnRT.anchorMin        = new Vector2(0.5f, 0f);
        mmBtnRT.anchorMax        = new Vector2(0.5f, 0f);
        mmBtnRT.pivot            = new Vector2(0.5f, 0.5f);
        mmBtnRT.sizeDelta        = new Vector2(230f, 62f);
        mmBtnRT.anchoredPosition = new Vector2(130f, 50f);

        AddText(mmBtnGO.transform, "BtnLabel",
            "Main Menu", font, 28, FontStyle.Bold, Color.white,
            Vector2.zero, Vector2.one);

        // Hidden until cheese collected
        _canvasGO.SetActive(false);
    }

    // ─────────────────────────────────────────────
    // UI Helpers
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
        RectTransform rt    = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
    }

    private static void AddText(Transform parent, string name,
        string text, Font font, int size, FontStyle style, Color color,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go  = CreateChild(name, parent);
        Text t         = go.AddComponent<Text>();
        t.text         = text;
        t.font         = font;
        t.fontSize     = size;
        t.fontStyle    = style;
        t.alignment    = TextAnchor.MiddleCenter;
        t.color        = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
