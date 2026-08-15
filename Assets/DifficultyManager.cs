using UnityEngine;

/// <summary>
/// Stores the chosen difficulty and exposes all gameplay parameters
/// derived from it. Created automatically at runtime; persists across
/// scene loads so the menu selection survives restarts.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    private static DifficultyManager _instance;
    public static DifficultyManager Instance => _instance;

    // ── Difficulty Enum ────────────────────────────────────────────────────
    public enum Difficulty { Easy, Medium, Hard }

    [HideInInspector] public Difficulty CurrentDifficulty = Difficulty.Medium;

    // ── Maze Dimensions ────────────────────────────────────────────────────
    /// <summary>Width of the maze in cells (odd numbers produce cleaner mazes).</summary>
    public int MazeWidth
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case Difficulty.Easy:   return 9;
                case Difficulty.Hard:   return 17;
                default:                return 13;  // Medium (13x6 = 78 units wide)
            }
        }
    }

    /// <summary>Height (depth) of the maze in cells.</summary>
    public int MazeHeight => MazeWidth;   // keep square

    // ── Trap Count ─────────────────────────────────────────────────────────
    public int CrushTrapCount
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case Difficulty.Easy:   return 3;
                case Difficulty.Hard:   return 8;
                default:                return 5;
            }
        }
    }

    public int UpDownTrapCount
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case Difficulty.Easy:   return 3;
                case Difficulty.Hard:   return 8;
                default:                return 5;
            }
        }
    }

    // ── Trap Speed Range ───────────────────────────────────────────────────
    public float TrapSpeedMin
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case Difficulty.Easy:   return 0.5f;
                case Difficulty.Hard:   return 1.5f;
                default:                return 1.0f;
            }
        }
    }

    public float TrapSpeedMax
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case Difficulty.Easy:   return 0.5f;
                case Difficulty.Hard:   return 1.5f;
                default:                return 1.0f;
            }
        }
    }

    // ── Up/Down Trap Speed Range ───────────────────────────────────────────
    public float UpDownTrapSpeedMin
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case Difficulty.Easy:   return 0.5f;
                case Difficulty.Hard:   return 3.0f;
                default:                return 1.0f;
            }
        }
    }

    public float UpDownTrapSpeedMax
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case Difficulty.Easy:   return 0.5f;
                case Difficulty.Hard:   return 3.0f;
                default:                return 1.0f;
            }
        }
    }

    // ── Timer ──────────────────────────────────────────────────────────────
    /// <summary>How many seconds the player has to find the cheese.</summary>
    public float TimeLimit
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case Difficulty.Easy:   return 300f;
                case Difficulty.Hard:   return 600f;
                default:                return 480f;
            }
        }
    }

    // ── Initialisation ─────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<DifficultyManager>() == null)
        {
            GameObject go = new GameObject("DifficultyManager");
            go.AddComponent<DifficultyManager>();
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ──────────────────────────────────────────────────────────
    public void SetDifficulty(Difficulty d)
    {
        CurrentDifficulty = d;
        Debug.Log($"[DifficultyManager] Difficulty set to {d}");
    }

    public string DifficultyLabel()
    {
        switch (CurrentDifficulty)
        {
            case Difficulty.Easy: return "Easy";
            case Difficulty.Hard: return "Hard";
            default:              return "Medium";
        }
    }
}
