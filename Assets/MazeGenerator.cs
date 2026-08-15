using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally generates a perfect maze at runtime using the
/// Recursive Backtracker (Depth-First Search) algorithm.
///
/// HOW IT WORKS:
/// - The maze grid is (MazeWidth × MazeHeight) cells.
/// - Each cell is cellSize units wide and cellSize units deep.
/// - Walls are placed between cells. A "carved" passage = no wall between those two cells.
/// - After generation, every dead-end cell (only 1 open neighbour) is recorded
///   so TrapSpawner can place traps and cheese there.
///
/// PREFAB SETUP (assign in Inspector on the MazeGenerator GameObject):
///   wallPrefab  – A thin box (e.g., scale 0.3 × wallHeight × cellSize).
///                 Should have BoxCollider. Will have medieval material applied.
///   floorPrefab – A flat box (e.g., scale cellSize × 0.3 × cellSize).
///   ceilingPrefab (optional) – same as floor, placed at wallHeight.
/// </summary>
public enum WallDirection { North, East, South, West }

[RequireComponent(typeof(TrapSpawner))]
public class MazeGenerator : MonoBehaviour
{
    // ── Configuration ──────────────────────────────────────────────────────
    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject ceilingPrefab; // Optional

    [Header("Maze Dimensions (overridden by DifficultyManager at runtime)")]
    public int  mazeWidth  = 13;
    public int  mazeHeight = 13;
    public float cellSize   = 6f;
    public float wallHeight = 35f; // Extra tall 35-unit walls
    public float wallThickness = 0.5f;

    [Header("Player Reference (auto-found if null)")]
    public Transform playerTransform;

    // ── Events ─────────────────────────────────────────────────────────────
    /// <summary>Fired after the maze geometry is fully built.</summary>
    public static event Action<MazeGenerator> OnMazeReady;

    // ── Internal Grid Data ─────────────────────────────────────────────────
    private bool[,] _passagesE; // East passages  (mazeWidth-1 × mazeHeight)
    private bool[,] _passagesN; // North passages (mazeWidth   × mazeHeight-1)
    private bool[,] _visited;

    private List<Vector2Int>       _deadEnds    = new List<Vector2Int>();
    private List<(Vector2Int, Vector2Int)> _longCorridors = new List<(Vector2Int, Vector2Int)>();

    private List<GameObject> _generatedObjects = new List<GameObject>();
    private Dictionary<string, GameObject> _wallRegistry = new Dictionary<string, GameObject>();

    // Public accessors for TrapSpawner and MiniMapController
    public bool[,] PassagesE => _passagesE;
    public bool[,] PassagesN => _passagesN;
    public List<Vector2Int>               DeadEnds     => _deadEnds;
    public List<(Vector2Int, Vector2Int)> LongCorridors => _longCorridors;
    public int Width  => mazeWidth;
    public int Height => mazeHeight;
    public float CellSize => cellSize;

    public GameObject GetWallAt(Vector2Int cell, WallDirection dir)
    {
        if (dir == WallDirection.South) { cell.y -= 1; dir = WallDirection.North; }
        else if (dir == WallDirection.West) { cell.x -= 1; dir = WallDirection.East; }
        
        string key = $"{dir}_{cell.x}_{cell.y}";
        return _wallRegistry.TryGetValue(key, out GameObject w) ? w : null;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Start()
    {
        // Disable old static maze objects to prevent overlap
        HideStaticMazeObjects();

        // Let DifficultyManager override the grid size
        if (DifficultyManager.Instance != null)
        {
            mazeWidth  = DifficultyManager.Instance.MazeWidth;
            mazeHeight = DifficultyManager.Instance.MazeHeight;
        }

        // Auto-find player
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        GenerateMaze();
        BuildGeometry();
        AnalyseMaze();
        TeleportPlayerToStart();

        OnMazeReady?.Invoke(this);
    }

    private void HideStaticMazeObjects()
    {
        GameObject[] allObjs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjs)
        {
            if (go.CompareTag("Generated")) continue;
            string n = go.name.ToLower();
            if (n == "walls" || n == "ground" || n.StartsWith("trap trigger") || (n == "cheese" && go.transform.parent == null))
            {
                go.SetActive(false);
                Debug.Log($"[MazeGenerator] Disabled static maze object '{go.name}'.");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step 1 – Generate the passage map
    // ─────────────────────────────────────────────────────────────────────
    private void GenerateMaze()
    {
        _passagesE = new bool[mazeWidth - 1, mazeHeight];
        _passagesN = new bool[mazeWidth,     mazeHeight - 1];
        _visited   = new bool[mazeWidth,     mazeHeight];

        CarveFrom(new Vector2Int(0, 0));
        Debug.Log($"[MazeGenerator] Maze generated: {mazeWidth}×{mazeHeight}");
    }

    private void CarveFrom(Vector2Int current)
    {
        _visited[current.x, current.y] = true;

        List<Vector2Int> neighbours = GetUnvisitedNeighbours(current);
        ShuffleList(neighbours);

        foreach (var next in neighbours)
        {
            if (!_visited[next.x, next.y])
            {
                CarvePassage(current, next);
                CarveFrom(next);
            }
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private List<Vector2Int> GetUnvisitedNeighbours(Vector2Int cell)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        if (cell.x > 0 && !_visited[cell.x - 1, cell.y]) result.Add(new Vector2Int(cell.x - 1, cell.y));
        if (cell.x < mazeWidth - 1 && !_visited[cell.x + 1, cell.y]) result.Add(new Vector2Int(cell.x + 1, cell.y));
        if (cell.y > 0 && !_visited[cell.x, cell.y - 1]) result.Add(new Vector2Int(cell.x, cell.y - 1));
        if (cell.y < mazeHeight - 1 && !_visited[cell.x, cell.y + 1]) result.Add(new Vector2Int(cell.x, cell.y + 1));
        return result;
    }

    private void CarvePassage(Vector2Int a, Vector2Int b)
    {
        Vector2Int diff = b - a;
        if (diff == Vector2Int.right)  _passagesE[a.x,     a.y] = true;
        if (diff == Vector2Int.left)   _passagesE[b.x,     b.y] = true;
        if (diff == Vector2Int.up)     _passagesN[a.x,     a.y] = true;
        if (diff == Vector2Int.down)   _passagesN[a.x,     b.y] = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step 2 – Build geometry
    // ─────────────────────────────────────────────────────────────────────
    private void BuildGeometry()
    {
        if (wallPrefab == null || floorPrefab == null)
        {
            Debug.LogError("[MazeGenerator] wallPrefab or floorPrefab is not assigned! Assign them in the Inspector.");
            return;
        }

        // Parent container so the Hierarchy stays tidy
        GameObject mazeParent = new GameObject("=== MAZE ===");
        mazeParent.tag = "Generated";
        _wallRegistry.Clear();

        float halfCell = cellSize * 0.5f;
        float halfThick = wallThickness * 0.5f;

        for (int x = 0; x < mazeWidth; x++)
        {
            for (int y = 0; y < mazeHeight; y++)
            {
                Vector3 cellOrigin = new Vector3(x * cellSize, 0f, y * cellSize);

                // ── Floor ──────────────────────────────────────────────
                SpawnFloor(cellOrigin + new Vector3(halfCell, 0f, halfCell), mazeParent.transform);

                // ── Optional Ceiling ───────────────────────────────────
                if (ceilingPrefab != null)
                    SpawnCeiling(cellOrigin + new Vector3(halfCell, wallHeight, halfCell), mazeParent.transform);

                // ── South wall (y==0 boundary or no south passage) ─────
                if (y == 0)
                {
                    GameObject w = SpawnWall(cellOrigin + new Vector3(halfCell, wallHeight * 0.5f, 0f), Quaternion.identity, new Vector3(cellSize, wallHeight, wallThickness), mazeParent.transform);
                    _wallRegistry[$"North_{x}_{-1}"] = w;
                }

                // ── West wall (x==0 boundary or no west passage) ───────
                if (x == 0)
                {
                    GameObject w = SpawnWall(cellOrigin + new Vector3(0f, wallHeight * 0.5f, halfCell), Quaternion.Euler(0f, 90f, 0f), new Vector3(cellSize, wallHeight, wallThickness), mazeParent.transform);
                    _wallRegistry[$"East_{-1}_{y}"] = w;
                }

                // ── East wall (between cell and x+1 neighbour) ─────────
                if (x < mazeWidth - 1 && !_passagesE[x, y])
                {
                    GameObject w = SpawnWall(cellOrigin + new Vector3(cellSize, wallHeight * 0.5f, halfCell), Quaternion.Euler(0f, 90f, 0f), new Vector3(cellSize, wallHeight, wallThickness), mazeParent.transform);
                    _wallRegistry[$"East_{x}_{y}"] = w;
                }
                // East boundary
                if (x == mazeWidth - 1)
                {
                    GameObject w = SpawnWall(cellOrigin + new Vector3(cellSize, wallHeight * 0.5f, halfCell), Quaternion.Euler(0f, 90f, 0f), new Vector3(cellSize, wallHeight, wallThickness), mazeParent.transform);
                    _wallRegistry[$"East_{x}_{y}"] = w;
                }

                // ── North wall (between cell and y+1 neighbour) ────────
                if (y < mazeHeight - 1 && !_passagesN[x, y])
                {
                    GameObject w = SpawnWall(cellOrigin + new Vector3(halfCell, wallHeight * 0.5f, cellSize), Quaternion.identity, new Vector3(cellSize, wallHeight, wallThickness), mazeParent.transform);
                    _wallRegistry[$"North_{x}_{y}"] = w;
                }
                // North boundary
                if (y == mazeHeight - 1)
                {
                    GameObject w = SpawnWall(cellOrigin + new Vector3(halfCell, wallHeight * 0.5f, cellSize), Quaternion.identity, new Vector3(cellSize, wallHeight, wallThickness), mazeParent.transform);
                    _wallRegistry[$"North_{x}_{y}"] = w;
                }
            }
        }

        _generatedObjects.Add(mazeParent);
    }

    private Material _customFloorMat;

    private void SpawnFloor(Vector3 pos, Transform parent)
    {
        GameObject go = Instantiate(floorPrefab, pos, Quaternion.identity, parent);
        go.transform.localScale = new Vector3(cellSize, 0.3f, cellSize);
        go.tag = "Generated";

#if UNITY_EDITOR
        if (_customFloorMat == null)
        {
            _customFloorMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Tiles074_1K-JPG/CheckerFloorMaterial.mat");
            if (_customFloorMat == null)
            {
                _customFloorMat = new Material(Shader.Find("Standard"));
                var albedo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Tiles074_1K-JPG/Tiles074_1K-JPG_Color.jpg");
                if (albedo != null) _customFloorMat.SetTexture("_MainTex", albedo);
                var normal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Tiles074_1K-JPG/Tiles074_1K-JPG_NormalGL.jpg");
                if (normal != null)
                {
                    _customFloorMat.SetTexture("_BumpMap", normal);
                    _customFloorMat.EnableKeyword("_NORMALMAP");
                }
                UnityEditor.AssetDatabase.CreateAsset(_customFloorMat, "Assets/Tiles074_1K-JPG/CheckerFloorMaterial.mat");
            }
        }
#endif

        if (_customFloorMat != null)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = _customFloorMat;
        }
    }

    private void SpawnCeiling(Vector3 pos, Transform parent)
    {
        GameObject go = Instantiate(ceilingPrefab, pos, Quaternion.identity, parent);
        go.transform.localScale = new Vector3(cellSize, 0.3f, cellSize);
        go.tag = "Generated";
    }

    private Material _customWallMat;

    private GameObject SpawnWall(Vector3 pos, Quaternion rot, Vector3 scale, Transform parent)
    {
        GameObject go = Instantiate(wallPrefab, pos, rot, parent);
        go.transform.localScale = scale;
        go.tag = "Generated";

#if UNITY_EDITOR
        if (_customWallMat == null)
        {
            _customWallMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/cheese-pattern-background/CheeseWallMaterial.mat");
            if (_customWallMat == null)
            {
                _customWallMat = new Material(Shader.Find("Standard"));
                var albedo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/cheese-pattern-background/OPDETC0.jpg");
                if (albedo != null) _customWallMat.SetTexture("_MainTex", albedo);
                
                // Adjust tiling so it looks like wallpaper rather than a single stretched image
                _customWallMat.mainTextureScale = new Vector2(3f, 3f);
                
                UnityEditor.AssetDatabase.CreateAsset(_customWallMat, "Assets/cheese-pattern-background/CheeseWallMaterial.mat");
            }
        }
#endif

        if (_customWallMat != null)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = _customWallMat;
            else
            {
                // In case the renderer is on a child object
                var renderers = go.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.sharedMaterial = _customWallMat;
            }
        }

        return go;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step 3 – Analyse maze topology (dead ends + long corridors)
    // ─────────────────────────────────────────────────────────────────────
    private void AnalyseMaze()
    {
        _deadEnds.Clear();
        _longCorridors.Clear();

        for (int x = 0; x < mazeWidth; x++)
        {
            for (int y = 0; y < mazeHeight; y++)
            {
                int openCount = CountOpenPassages(x, y);
                if (openCount == 1)
                    _deadEnds.Add(new Vector2Int(x, y));
            }
        }

        // Detect long straight horizontal corridors (length >= 3)
        for (int y = 0; y < mazeHeight; y++)
        {
            int runStart = 0;
            for (int x = 0; x < mazeWidth - 1; x++)
            {
                if (!_passagesE[x, y])
                {
                    if (x - runStart >= 2)
                        _longCorridors.Add((new Vector2Int(runStart, y), new Vector2Int(x, y)));
                    runStart = x + 1;
                }
            }
        }

        // Detect long straight vertical corridors (length >= 3)
        for (int x = 0; x < mazeWidth; x++)
        {
            int runStart = 0;
            for (int y = 0; y < mazeHeight - 1; y++)
            {
                if (!_passagesN[x, y])
                {
                    if (y - runStart >= 2)
                        _longCorridors.Add((new Vector2Int(x, runStart), new Vector2Int(x, y)));
                    runStart = y + 1;
                }
            }
        }

        Debug.Log($"[MazeGenerator] Dead ends: {_deadEnds.Count}, Long corridors: {_longCorridors.Count}");
    }

    private int CountOpenPassages(int x, int y)
    {
        int count = 0;
        if (x > 0           && _passagesE[x - 1, y]) count++;  // West
        if (x < mazeWidth-1 && _passagesE[x,     y]) count++;  // East
        if (y > 0           && _passagesN[x, y - 1]) count++;  // South
        if (y < mazeHeight-1&& _passagesN[x,     y]) count++;  // North
        return count;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step 4 – Teleport player cleanly onto the ground at cell (0,0)
    // ─────────────────────────────────────────────────────────────────────
    private void TeleportPlayerToStart()
    {
        if (playerTransform == null) return;

        // Cell (0,0) center position
        Vector3 targetPos = new Vector3(cellSize * 0.5f, 0f, cellSize * 0.5f);

        // Raycast down from above to align perfectly on floor
        Vector3 rayStart = new Vector3(cellSize * 0.5f, 10f, cellSize * 0.5f);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f))
        {
            targetPos.y = hit.point.y;
        }
        else
        {
            targetPos.y = 0.5f; // Fallback floor surface level
        }

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Add the extent of the collider so the bottom rests exactly on the floor
        float yOffset = 1.0f; // Default for 2m tall capsule
        if (cc != null) 
        {
            yOffset = (cc.height * 0.5f) + 0.1f;
        }
        else 
        {
            Collider col = playerTransform.GetComponent<Collider>();
            if (col != null) yOffset = col.bounds.extents.y + 0.1f;
        }
        targetPos.y += yOffset;

        playerTransform.position = targetPos;
        playerTransform.rotation = Quaternion.Euler(0f, 0f, 0f);

        if (rb != null)
        {
            rb.position = targetPos;
            rb.rotation = Quaternion.Euler(0f, 0f, 0f);
        }

        if (cc != null) cc.enabled = true;

        Debug.Log($"[MazeGenerator] Player positioned cleanly on ground at {targetPos}.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // BFS Pathfinding Helpers (for TrapSpawner path placement)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the exact BFS shortest path of cells from startCell to endCell.
    /// </summary>
    public List<Vector2Int> FindPath(Vector2Int startCell, Vector2Int endCell)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(startCell);
        visited.Add(startCell);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == endCell) break;

            foreach (var nb in GetConnectedNeighbours(current))
            {
                if (!visited.Contains(nb))
                {
                    visited.Add(nb);
                    parent[nb] = current;
                    queue.Enqueue(nb);
                }
            }
        }

        List<Vector2Int> path = new List<Vector2Int>();
        if (!visited.Contains(endCell)) return path;

        Vector2Int curr = endCell;
        while (curr != startCell)
        {
            path.Add(curr);
            curr = parent[curr];
        }
        path.Add(startCell);
        path.Reverse();
        return path;
    }

    public List<Vector2Int> GetConnectedNeighbours(Vector2Int cell)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        int x = cell.x;
        int y = cell.y;
        if (x > 0           && _passagesE[x - 1, y]) list.Add(new Vector2Int(x - 1, y)); // West
        if (x < mazeWidth-1 && _passagesE[x,     y]) list.Add(new Vector2Int(x + 1, y)); // East
        if (y > 0           && _passagesN[x, y - 1]) list.Add(new Vector2Int(x, y - 1)); // South
        if (y < mazeHeight-1&& _passagesN[x,     y]) list.Add(new Vector2Int(x, y + 1)); // North
        return list;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public Helpers for TrapSpawner
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Converts a grid cell coordinate to a world-space centre position.</summary>
    public Vector3 CellToWorld(Vector2Int cell, float heightOffset = 1f)
    {
        return new Vector3(cell.x * cellSize + cellSize * 0.5f, heightOffset, cell.y * cellSize + cellSize * 0.5f);
    }

    /// <summary>Returns true if there is an open passage going East from (x,y).</summary>
    public bool HasPassageEast(int x, int y)
        => x < mazeWidth - 1 && _passagesE[x, y];

    /// <summary>Returns true if there is an open passage going North from (x,y).</summary>
    public bool HasPassageNorth(int x, int y)
        => y < mazeHeight - 1 && _passagesN[x, y];

    /// <summary>Destroys all generated maze objects (called on scene reload).</summary>
    public void ClearMaze()
    {
        foreach (var go in _generatedObjects)
            if (go != null) Destroy(go);
        _generatedObjects.Clear();
    }

    private void OnDestroy() => ClearMaze();
}
