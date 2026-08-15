using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens to MazeGenerator.OnMazeReady, then:
///  1. Finds the farthest dead end from the start cell → places Cheese there.
///  2. Randomly places DynamicUpDownTrap prefabs at dead ends.
///  3. Randomly places DynamicCrushTrap pairs + trigger zones in long corridors.
///
/// PREFAB SETUP (assign in Inspector on the TrapSpawner GameObject):
///   cheesePrefab        – your existing Cheese GameObject (with CheeseCollectible component)
///   upDownTrapPrefab    – prefab with DynamicUpDownTrap component and Collider
///   crushWallPrefab     – prefab with DynamicCrushTrap component, Collider, Rigidbody (isKinematic)
/// </summary>
public class TrapSpawner : MonoBehaviour
{
    // ── Inspector Fields ───────────────────────────────────────────────────
    [Header("Prefabs (assign in Inspector)")]
    public GameObject cheesePrefab;
    public GameObject upDownTrapPrefab;
    public GameObject crushWallPrefab;

    [Header("Trap Count (overridden by DifficultyManager at runtime)")]
    public int upDownTrapCount = 3;
    public int crushTrapCount  = 3;

    [Header("Trap Y Position (height above floor for UpDown traps)")]
    public float upDownTrapStartHeight = 37f;

    [Header("Crush Wall Height (centre of wall)")]
    public float crushWallHeight = 17.5f;

    // ── Internal ───────────────────────────────────────────────────────────
    private MazeGenerator _maze;
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private HashSet<GameObject> _usedWalls = new HashSet<GameObject>();
    private HashSet<Vector2Int> _occupiedTrapCells = new HashSet<Vector2Int>();

    private Material _trapMaterial;

    private void ApplyTrapMaterial(GameObject trapGO)
    {
#if UNITY_EDITOR
        if (_trapMaterial == null)
        {
            _trapMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Texture/Materials/laminate_floor_02_diff_4k.mat");
        }
#endif
        if (_trapMaterial != null)
        {
            var renderers = trapGO.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.sharedMaterial = _trapMaterial;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────
    private void OnEnable()  => MazeGenerator.OnMazeReady += HandleMazeReady;
    private void OnDisable() => MazeGenerator.OnMazeReady -= HandleMazeReady;

    private Vector2Int _cheeseCell;

    // ─────────────────────────────────────────────────────────────────────
    // Main Entry Point
    // ─────────────────────────────────────────────────────────────────────
    private void HandleMazeReady(MazeGenerator maze)
    {
        _maze = maze;
        _usedWalls.Clear();
        _occupiedTrapCells.Clear();

        // Apply difficulty settings
        if (DifficultyManager.Instance != null)
        {
            upDownTrapCount = DifficultyManager.Instance.UpDownTrapCount;
            crushTrapCount  = DifficultyManager.Instance.CrushTrapCount;
        }

        // Make a shuffled copy of dead ends
        List<Vector2Int> deadEnds = new List<Vector2Int>(maze.DeadEnds);
        ShuffleList(deadEnds);

        _cheeseCell = PlaceCheese(deadEnds);

        // Find solution path from Start cell (0,0) to Cheese cell
        List<Vector2Int> path = _maze.FindPath(new Vector2Int(0, 0), _cheeseCell);

        PlaceUpDownTraps(path, deadEnds);
        PlaceCrushTraps(maze.LongCorridors);

        // Tell GameManager the game can start (timer begins now)
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cheese Placement — always at the farthest dead end from (0,0)
    // ─────────────────────────────────────────────────────────────────────
    private Vector2Int PlaceCheese(List<Vector2Int> deadEnds)
    {
        if (deadEnds.Count == 0) { Debug.LogWarning("[TrapSpawner] No dead ends found for cheese!"); return Vector2Int.zero; }

        // Find the dead end with the highest Manhattan distance from (0,0)
        Vector2Int farthest = deadEnds[0];
        int maxDist = 0;
        foreach (var cell in deadEnds)
        {
            int dist = cell.x + cell.y;  // Manhattan from origin
            if (dist > maxDist) { maxDist = dist; farthest = cell; }
        }

        Vector3 worldPos = _maze.CellToWorld(farthest, 1.25f);
        GameObject cheese = null;

        if (cheesePrefab != null)
        {
            cheese = Instantiate(cheesePrefab, worldPos, Quaternion.identity);
        }
        else
        {
#if UNITY_EDITOR
            GameObject fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AddOns/MG3D_Food/Cheese.fbx");
            if (fbx != null)
            {
                cheese = Instantiate(fbx, worldPos, Quaternion.identity);
            }
#endif
            if (cheese == null)
            {
                cheese = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cheese.transform.position = worldPos;
                cheese.GetComponent<Renderer>().material.color = Color.yellow;
            }
        }

        // Ensure it has required components
        if (cheese.GetComponent<Collider>() == null)
        {
            var box = cheese.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 1f);
        }
        if (cheese.GetComponent<CheeseCollectible>() == null)
        {
            cheese.AddComponent<CheeseCollectible>();
        }

        // Make the cheese a little bit bigger
        cheese.transform.localScale = Vector3.one * 1.5f;

        cheese.tag = "Generated";
        _spawnedObjects.Add(cheese);

        // Remove from pool so traps don't land on the cheese cell
        deadEnds.Remove(farthest);
        _occupiedTrapCells.Add(farthest);

        Debug.Log($"[TrapSpawner] Cheese placed at cell {farthest} (world: {worldPos})");
        return farthest;
    }

    // ─────────────────────────────────────────────────────────────────────
    // UpDown Traps — placed along open path corridors with room to move
    // ─────────────────────────────────────────────────────────────────────
    private void PlaceUpDownTraps(List<Vector2Int> path, List<Vector2Int> deadEnds)
    {
        if (upDownTrapPrefab == null) { Debug.LogWarning("[TrapSpawner] upDownTrapPrefab not assigned!"); return; }

        float speedMin = DifficultyManager.Instance != null ? DifficultyManager.Instance.UpDownTrapSpeedMin : 2f;
        float speedMax = DifficultyManager.Instance != null ? DifficultyManager.Instance.UpDownTrapSpeedMax : 5f;

        // Build list of valid candidate cells from the main solution path (excluding start & cheese)
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int i = 2; i < path.Count - 2; i++)
        {
            Vector2Int c = path[i];
            // Only use open corridor cells (not 1-cell dead ends)
            // AND ensure it's not at or adjacent to the spawn area (0,0)
            if (!deadEnds.Contains(c) && (c.x > 1 || c.y > 1))
                candidates.Add(c);
        }

        // Shuffle candidates for variety across runs
        ShuffleList(candidates);

        int placed = 0;

        foreach (var cell in candidates)
        {
            if (placed >= upDownTrapCount) break;

            // Ensure spacing of at least 2 cells between traps (and cheese)
            bool tooClose = false;
            foreach (var existing in _occupiedTrapCells)
            {
                if (Vector2Int.Distance(cell, existing) < 2f) { tooClose = true; break; }
            }
            if (tooClose) continue;

            Vector3 worldPos = _maze.CellToWorld(cell, upDownTrapStartHeight);
            GameObject trapGO = Instantiate(upDownTrapPrefab, worldPos, Quaternion.identity);
            trapGO.tag = "Generated";
            _spawnedObjects.Add(trapGO);
            ApplyTrapMaterial(trapGO);

            DynamicUpDownTrap trap = trapGO.GetComponent<DynamicUpDownTrap>();
            if (trap != null)
            {
                trap.speed        = Random.Range(speedMin, speedMax);
                trap.dropDistance = Random.Range(18f, 22f); // Matched to 24-unit wall height
            }

            _occupiedTrapCells.Add(cell);
            placed++;
            Debug.Log($"[TrapSpawner] UpDown trap placed at corridor cell {cell}  speed={trap?.speed:F1}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Crush Traps — dynamically convert existing native walls in long corridors
    // ─────────────────────────────────────────────────────────────────────
    public void PlaceCrushTraps(List<(Vector2Int, Vector2Int)> corridors)
    {
        if (corridors.Count == 0)    { Debug.LogWarning("[TrapSpawner] No long corridors found for crush traps!"); return; }

        // Shuffle corridors for randomness
        List<(Vector2Int, Vector2Int)> shuffled = new List<(Vector2Int, Vector2Int)>(corridors);
        ShuffleListTuple(shuffled);

        float speedMin = DifficultyManager.Instance != null ? DifficultyManager.Instance.TrapSpeedMin : 2f;
        float speedMax = DifficultyManager.Instance != null ? DifficultyManager.Instance.TrapSpeedMax : 5f;

        int placed = 0;
        foreach (var (start, end) in shuffled)
        {
            if (placed >= crushTrapCount) break;

            // PREVENT SPAWNING ON TOP OF OR ADJACENT TO START POSITION (0,0)
            if (start.x <= 1 && start.y <= 1) continue;
            if (end.x <= 1 && end.y <= 1) continue;

            bool isHorizontal = (start.y == end.y);
            bool foundValidWalls = false;
            GameObject wallA = null;
            GameObject wallB = null;
            Vector2Int chosenCell = start;
            Vector3 dirA = Vector3.zero;
            Vector3 dirB = Vector3.zero;

            // Find a cell in this corridor that has both opposing solid walls
            if (isHorizontal)
            {
                for (int x = start.x; x <= end.x; x++)
                {
                    Vector2Int cell = new Vector2Int(x, start.y);
                    
                    bool tooClose = false;
                    foreach (var existing in _occupiedTrapCells)
                    {
                        if (Vector2Int.Distance(cell, existing) < 2f) { tooClose = true; break; }
                    }
                    if (tooClose) continue;

                    GameObject nWall = _maze.GetWallAt(cell, WallDirection.North);
                    GameObject sWall = _maze.GetWallAt(cell, WallDirection.South);

                    if (nWall != null && sWall != null && !_usedWalls.Contains(nWall) && !_usedWalls.Contains(sWall))
                    {
                        wallA = nWall; wallB = sWall;
                        dirA = Vector3.back;    // North wall moves South
                        dirB = Vector3.forward; // South wall moves North
                        chosenCell = cell;
                        foundValidWalls = true;
                        break;
                    }
                }
            }
            else
            {
                for (int y = start.y; y <= end.y; y++)
                {
                    Vector2Int cell = new Vector2Int(start.x, y);
                    
                    bool tooClose = false;
                    foreach (var existing in _occupiedTrapCells)
                    {
                        if (Vector2Int.Distance(cell, existing) < 2f) { tooClose = true; break; }
                    }
                    if (tooClose) continue;

                    GameObject eWall = _maze.GetWallAt(cell, WallDirection.East);
                    GameObject wWall = _maze.GetWallAt(cell, WallDirection.West);

                    if (eWall != null && wWall != null && !_usedWalls.Contains(eWall) && !_usedWalls.Contains(wWall))
                    {
                        wallA = eWall; wallB = wWall;
                        dirA = Vector3.left;  // East wall moves West
                        dirB = Vector3.right; // West wall moves East
                        chosenCell = cell;
                        foundValidWalls = true;
                        break;
                    }
                }
            }

            if (!foundValidWalls) continue;
            
            _occupiedTrapCells.Add(chosenCell);

            _usedWalls.Add(wallA);
            _usedWalls.Add(wallB);

            float speed = Random.Range(speedMin, speedMax);
            float moveDist = (_maze.CellSize * 0.5f) - 0.25f; // Move halfway minus small gap to avoid overlapping at center

            DynamicCrushTrap trapA = ConvertWallToTrap(wallA, dirA, moveDist, speed);
            DynamicCrushTrap trapB = ConvertWallToTrap(wallB, dirB, moveDist, speed);

            // Create Trigger Zone at the cell center (ground level)
            Vector3 cellCenter = _maze.CellToWorld(chosenCell, 1.0f);
            CreateTriggerZone(cellCenter, new[] { trapA, trapB }, isHorizontal);

            placed++;
            Debug.Log($"[TrapSpawner] Native Crush trap created at cell {chosenCell}");
        }
    }

    private DynamicCrushTrap ConvertWallToTrap(GameObject wallGO, Vector3 dir, float dist, float speed)
    {
        Rigidbody rb = wallGO.GetComponent<Rigidbody>();
        if (rb == null) rb = wallGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        DynamicCrushTrap trap = wallGO.GetComponent<DynamicCrushTrap>();
        if (trap == null) trap = wallGO.AddComponent<DynamicCrushTrap>();

        trap.moveDirection = dir;
        trap.moveDistance = dist;
        trap.speed = speed;
        
        ApplyTrapMaterial(wallGO);
        
        return trap;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Trigger Zone Creation
    // ─────────────────────────────────────────────────────────────────────
    private void CreateTriggerZone(Vector3 position, DynamicCrushTrap[] walls, bool isHorizontal)
    {
        GameObject trigGO = new GameObject("CrushTrapTrigger");
        trigGO.transform.position = position;
        trigGO.tag = "Generated";
        _spawnedObjects.Add(trigGO);

        BoxCollider col = trigGO.AddComponent<BoxCollider>();
        col.isTrigger = true;

        float cellSize = _maze.CellSize;
        if (isHorizontal)
            col.size = new Vector3(cellSize, cellSize, cellSize);
        else
            col.size = new Vector3(cellSize, cellSize, cellSize);

        DynamicTrapTrigger trigger = trigGO.AddComponent<DynamicTrapTrigger>();
        trigger.wallsToActivate = walls;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────────────────
    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private static void ShuffleListTuple(List<(Vector2Int, Vector2Int)> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private void OnDestroy()
    {
        foreach (var go in _spawnedObjects)
            if (go != null) Destroy(go);
    }
}
