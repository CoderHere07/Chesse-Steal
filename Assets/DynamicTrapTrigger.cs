using UnityEngine;

/// <summary>
/// Unified trigger zone for dynamic crush traps.
/// Replaces TrapTrigger and TrapTrigger2 in the dynamic maze system.
/// Holds an array of DynamicCrushTrap walls to activate; set by TrapSpawner at spawn time.
/// One trigger can activate multiple walls simultaneously (e.g., both walls of a pair).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DynamicTrapTrigger : MonoBehaviour
{
    // ── Set by TrapSpawner ─────────────────────────────────────────────────
    [HideInInspector] public DynamicCrushTrap[] wallsToActivate;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────
    private void Start()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Trigger
    // ─────────────────────────────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log("[DynamicTrapTrigger] Player walked into trap trigger!");

        if (wallsToActivate == null) return;
        foreach (var wall in wallsToActivate)
        {
            if (wall != null)
                wall.Activate();
        }
    }
}
