using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dynamic version of UpDownTrap.
/// Speed is randomised by TrapSpawner at spawn time.
/// Uses Raycast detection on Start to find the exact floor surface
/// so it stops right at ground level and NEVER passes through the floor.
/// On player hit → calls GameManager.TriggerDeath() with a descriptive message.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DynamicUpDownTrap : MonoBehaviour
{
    // ── Set by TrapSpawner ─────────────────────────────────────────────────
    [HideInInspector] public float speed        = 5f;
    [HideInInspector] public float dropDistance = 20f; // fallback if raycast misses

    // ── State ──────────────────────────────────────────────────────────────
    private Vector3 _startPos;
    private Vector3 _bottomPos;
    private bool    _movingDown  = true;
    private float   _pauseTimer  = 0f;
    public  float   pauseAtBottom = 0.25f; // pause briefly at ground before going back up

    private Collider _myCollider;
    private Collider _playerCollider;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────
    private void Start()
    {
        _startPos   = transform.position;
        _myCollider = GetComponent<Collider>();

        // Calculate exact bottom position by raycasting down to the floor surface
        float halfHeight = _myCollider != null ? _myCollider.bounds.extents.y : 1f;

        // Temporarily disable collider for raycast check
        bool colWasEnabled = _myCollider.enabled;
        _myCollider.enabled = false;

        if (Physics.Raycast(_startPos, Vector3.down, out RaycastHit hit, 60f))
        {
            // Floor surface is hit.point.y; place trap bottom right above floor
            float floorY = hit.point.y + halfHeight + 0.1f;
            _bottomPos = new Vector3(_startPos.x, floorY, _startPos.z);
        }
        else
        {
            float floorY = Mathf.Max(0.2f + halfHeight, _startPos.y - dropDistance);
            _bottomPos = new Vector3(_startPos.x, floorY, _startPos.z);
        }

        _myCollider.enabled = colWasEnabled;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerCollider = player.GetComponent<Collider>();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (_pauseTimer > 0f)
        {
            _pauseTimer -= Time.deltaTime;
            return;
        }

        if (_movingDown)
        {
            transform.position = Vector3.MoveTowards(transform.position, _bottomPos, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, _bottomPos) < 0.01f)
            {
                transform.position = _bottomPos;
                _movingDown = false;
                _pauseTimer = pauseAtBottom; // pause at ground
            }
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, _startPos, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, _startPos) < 0.01f)
            {
                transform.position = _startPos;
                _movingDown = true;
            }
        }

        // Manual collision check
        if (_myCollider != null && _playerCollider != null)
        {
            if (_myCollider.bounds.Intersects(_playerCollider.bounds))
                KillPlayer();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Collision (backup for physics-based setups)
    // ─────────────────────────────────────────────────────────────────────
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            KillPlayer();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Death
    // ─────────────────────────────────────────────────────────────────────
    private void KillPlayer()
    {
        Debug.Log("[DynamicUpDownTrap] Player squashed!");

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerDeath("You were squashed by a spike trap!");
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
