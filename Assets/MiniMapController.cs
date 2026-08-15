using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamically builds a Mini-Map UI in the top-left corner.
/// Generates a Texture2D from the procedural maze data.
/// Tracks only the Player in real-time.
/// </summary>
public class MiniMapController : MonoBehaviour
{
    private MazeGenerator _maze;
    private GameObject _mapCanvas;
    private RectTransform _mapContainer;
    private RawImage _mapImage;

    // Trackers
    private RectTransform _playerTracker;
    private Transform _playerTransform;
    
    private List<(Transform trap, RectTransform marker)> _trapTrackers = new List<(Transform, RectTransform)>();
    
    [Header("Mini-Map Settings")]
    public int pixelsPerCell = 10;
    public float maxMapSizeOnScreen = 300f; // Adjusted to be clearly visible on 1080p reference

    [Header("Colors")]
    public Color wallColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    public Color pathColor = new Color(0.9f, 0.9f, 0.9f, 0.8f);
    public Color playerColor = Color.blue;
    public Color upDownTrapColor = new Color(1f, 0.5f, 0f, 1f); // Orange
    public Color crushTrapColor = Color.red;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<MiniMapController>() == null)
        {
            GameObject go = new GameObject("MiniMapController");
            go.AddComponent<MiniMapController>();
            DontDestroyOnLoad(go);
        }
    }

    private void OnEnable()
    {
        MazeGenerator.OnMazeReady += HandleMazeReady;
    }

    private void OnDisable()
    {
        MazeGenerator.OnMazeReady -= HandleMazeReady;
    }

    private void HandleMazeReady(MazeGenerator maze)
    {
        _maze = maze;

        BuildUI();
        GenerateMapTexture();

        StartCoroutine(FindEntitiesDelayed());
    }

    private System.Collections.IEnumerator FindEntitiesDelayed()
    {
        yield return null; // Wait one frame for TrapSpawner to instantiate traps

        // Find Player
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _playerTransform = p.transform;
            _playerTracker = CreateMarker("PlayerMarker", playerColor, new Vector2(16, 24)); // Increased size for visibility
        }

        // Track traps on the minimap
        _trapTrackers.Clear();
        foreach (var upDownTrap in FindObjectsOfType<DynamicUpDownTrap>())
        {
            var marker = CreateMarker("TrapMarker", upDownTrapColor, new Vector2(12, 12), true);
            _trapTrackers.Add((upDownTrap.transform, marker));
        }
        foreach (var crushTrap in FindObjectsOfType<DynamicCrushTrap>())
        {
            var marker = CreateMarker("TrapMarker", crushTrapColor, new Vector2(12, 12), true);
            _trapTrackers.Add((crushTrap.transform, marker));
        }
    }

    private void BuildUI()
    {
        if (_mapCanvas != null) Destroy(_mapCanvas);

        _mapCanvas = new GameObject("MiniMapCanvas");
        Canvas c = _mapCanvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 10; // Draw on top
        
        CanvasScaler scaler = _mapCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); // Ensure consistent scaling
        scaler.matchWidthOrHeight = 0.5f;

        // Container
        GameObject containerGO = new GameObject("MapContainer");
        containerGO.transform.SetParent(_mapCanvas.transform, false);
        _mapContainer = containerGO.AddComponent<RectTransform>();
        _mapContainer.anchorMin = new Vector2(0, 1);
        _mapContainer.anchorMax = new Vector2(0, 1);
        _mapContainer.pivot = new Vector2(0, 1); // Top-left pivot
        
        // Push it down by 100px so it sits cleanly below the Difficulty Label ("Medium")
        _mapContainer.anchoredPosition = new Vector2(20, -100); 

        // Background / Border
        Image bg = containerGO.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.5f);

        // RawImage for the Texture
        GameObject imgGO = new GameObject("MapTexture");
        imgGO.transform.SetParent(_mapContainer, false);
        RectTransform imgRect = imgGO.AddComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.sizeDelta = Vector2.zero; // Fill parent
        _mapImage = imgGO.AddComponent<RawImage>();

        // Set container size based on maze aspect ratio
        float aspect = (float)_maze.Width / _maze.Height;
        if (aspect >= 1f)
            _mapContainer.sizeDelta = new Vector2(maxMapSizeOnScreen, maxMapSizeOnScreen / aspect);
        else
            _mapContainer.sizeDelta = new Vector2(maxMapSizeOnScreen * aspect, maxMapSizeOnScreen);
    }

    private void GenerateMapTexture()
    {
        int texWidth = _maze.Width * pixelsPerCell;
        int texHeight = _maze.Height * pixelsPerCell;

        Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // Crisp pixels

        // Fill with path color initially
        Color[] pixels = new Color[texWidth * texHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = pathColor;

        int wallThickness = Mathf.Max(1, pixelsPerCell / 3);

        // Draw Walls
        for (int x = 0; x < _maze.Width; x++)
        {
            for (int y = 0; y < _maze.Height; y++)
            {
                int px = x * pixelsPerCell;
                int py = y * pixelsPerCell;

                // South Boundary (y=0)
                if (y == 0) DrawRect(pixels, texWidth, px, py, pixelsPerCell, wallThickness, wallColor);
                // West Boundary (x=0)
                if (x == 0) DrawRect(pixels, texWidth, px, py, wallThickness, pixelsPerCell, wallColor);

                // East Wall
                if (x == _maze.Width - 1 || !_maze.PassagesE[x, y])
                    DrawRect(pixels, texWidth, px + pixelsPerCell - wallThickness, py, wallThickness, pixelsPerCell, wallColor);

                // North Wall
                if (y == _maze.Height - 1 || !_maze.PassagesN[x, y])
                    DrawRect(pixels, texWidth, px, py + pixelsPerCell - wallThickness, pixelsPerCell, wallThickness, wallColor);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        _mapImage.texture = tex;
    }

    private void DrawRect(Color[] pixels, int texWidth, int startX, int startY, int width, int height, Color c)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                pixels[y * texWidth + x] = c;
            }
        }
    }

    private RectTransform CreateMarker(string name, Color color, Vector2 size, bool isCircle = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_mapContainer, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        
        // Anchor to bottom-left so we can map (0,0) to bottom-left of map
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = color;
        
        return rt;
    }

    private void Update()
    {
        if (_maze == null || _mapContainer == null) return;

        float mazeWorldWidth = _maze.Width * _maze.CellSize;
        float mazeWorldHeight = _maze.Height * _maze.CellSize;

        // Update Player
        if (_playerTransform != null && _playerTracker != null)
        {
            Vector2 uiPos = WorldToMapSpace(_playerTransform.position, mazeWorldWidth, mazeWorldHeight);
            _playerTracker.anchoredPosition = uiPos;
            
            // Rotate player marker (assuming player looks along Z axis in 3D, mapped to Y axis in 2D UI)
            float yRot = _playerTransform.eulerAngles.y;
            _playerTracker.localRotation = Quaternion.Euler(0, 0, -yRot);
        }

        // Update Traps
        foreach (var tracker in _trapTrackers)
        {
            if (tracker.trap != null && tracker.marker != null)
            {
                Vector2 uiPos = WorldToMapSpace(tracker.trap.position, mazeWorldWidth, mazeWorldHeight);
                tracker.marker.anchoredPosition = uiPos;
            }
        }
    }

    private Vector2 WorldToMapSpace(Vector3 worldPos, float mazeWorldWidth, float mazeWorldHeight)
    {
        // World 0,0 is the bottom-left corner of the maze.
        // We map world X to UI X, and world Z to UI Y.
        float normalizedX = Mathf.Clamp01(worldPos.x / mazeWorldWidth);
        float normalizedY = Mathf.Clamp01(worldPos.z / mazeWorldHeight);

        return new Vector2(normalizedX * _mapContainer.sizeDelta.x, normalizedY * _mapContainer.sizeDelta.y);
    }
}
