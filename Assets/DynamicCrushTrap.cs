using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unified, data-driven crush trap wall (Native Version).
/// Native structural walls are dynamically converted into traps using this script.
/// The trap moves by a specified moveDistance and then retracts.
/// On player collision → calls GameManager.TriggerDeath() with a message.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DynamicCrushTrap : MonoBehaviour
{
    // ── Set by TrapSpawner ─────────────────────────────────────────────────
    [HideInInspector] public float speed = 2f;
    [HideInInspector] public Vector3 moveDirection = Vector3.right;
    [HideInInspector] public float moveDistance = 5f;

    // ── State ──────────────────────────────────────────────────────────────
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private bool _movingForward  = false;
    private bool _movingBackward = false;
    private Collider _myCollider;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────
    private void Start()
    {
        _startPos   = transform.position;
        _targetPos  = _startPos + moveDirection.normalized * moveDistance;
        _myCollider = GetComponent<Collider>();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Called by DynamicTrapTrigger when the player enters the trigger zone.</summary>
    public void Activate()
    {
        if (!_movingForward && !_movingBackward)
            _movingForward = true;
    }

    /// <summary>Reset to start position (e.g., if the trap should be reusable).</summary>
    public void Reset()
    {
        _movingForward  = false;
        _movingBackward = false;
        transform.position = _startPos;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (_movingForward)
        {
            transform.position = Vector3.MoveTowards(transform.position, _targetPos, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _targetPos) < 0.01f)
            {
                transform.position = _targetPos;
                _movingForward  = false;
                _movingBackward = true;
            }
        }
        else if (_movingBackward)
        {
            transform.position = Vector3.MoveTowards(transform.position, _startPos, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _startPos) < 0.01f)
            {
                transform.position = _startPos;
                _movingBackward    = false;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Collision
    // ─────────────────────────────────────────────────────────────────────
    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // Only kill the player if the wall is moving forward and is very close to closing the gap
        if (_movingForward && Vector3.Distance(transform.position, _targetPos) < 0.6f)
        {
            Debug.Log("[DynamicCrushTrap] Player crushed!");

            if (GameManager.Instance != null)
                GameManager.Instance.TriggerDeath("You were crushed by the walls!");
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
