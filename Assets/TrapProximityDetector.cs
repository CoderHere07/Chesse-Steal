using UnityEngine;

/// <summary>
/// Detects when the player is close to any dynamic trap and plays
/// a danger audio cue through MusicManager. Attach to the Player GameObject
/// or to any persistent GameObject (it searches for traps each frame at a throttled rate).
/// </summary>
public class TrapProximityDetector : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Distance at which the danger sound starts playing.")]
    public float dangerRadius = 8f;

    [Tooltip("Minimum seconds between danger sound pulses.")]
    public float pulseCooldown = 1.5f;

    // ── State ──────────────────────────────────────────────────────────────
    private float _lastPulseTime = -99f;
    private Transform _playerTransform;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────
    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────
    private void Update()
    {
        // Don't check if GameManager says game isn't active
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive()) return;
        if (_playerTransform == null) return;

        // Throttle check by cooldown
        if (Time.time - _lastPulseTime < pulseCooldown) return;

        if (IsNearAnyTrap())
        {
            _lastPulseTime = Time.time;
            MusicManager.Instance?.PlayDangerPulse();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Proximity Checks
    // ─────────────────────────────────────────────────────────────────────
    private bool IsNearAnyTrap()
    {
        Vector3 playerPos = _playerTransform.position;

        // Check DynamicUpDownTrap
        DynamicUpDownTrap[] upDownTraps = Object.FindObjectsByType<DynamicUpDownTrap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var trap in upDownTraps)
        {
            if (Vector3.Distance(playerPos, trap.transform.position) < dangerRadius)
                return true;
        }

        // Check DynamicCrushTrap
        DynamicCrushTrap[] crushTraps = Object.FindObjectsByType<DynamicCrushTrap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var trap in crushTraps)
        {
            if (Vector3.Distance(playerPos, trap.transform.position) < dangerRadius)
                return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gizmo
    // ─────────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (_playerTransform != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(_playerTransform.position, dangerRadius);
        }
    }
}
