using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MonsterDetection : MonoBehaviour
{
    private MonsterAILogic _aiLogic;
    private readonly HashSet<Collider> _activeLightColliders = new();
    private bool _isCurrentlyFlashed = false;

    public bool WasHitByFlashlight => _isCurrentlyFlashed;

    private void Awake()
    {
        _aiLogic = GetComponent<MonsterAILogic>();
    }

    private void FixedUpdate()
    {
        // Reset every physics tick. If OnTriggerStay doesn't keep setting it, it drops to false.
        _isCurrentlyFlashed = false;
    }

    public bool IsInsideAmbientSafeZoneLight()
    {
        _activeLightColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        return _activeLightColliders.Count > 0;
    }

    public bool IsVisibleOnPlayerScreen(Camera playerCamera)
    {
        if (playerCamera == null) return false;
        Vector3 targetChestLevel = transform.position + (Vector3.up * 1.2f);
        Vector3 viewportPoint = playerCamera.WorldToScreenPoint(targetChestLevel);
        return viewportPoint.z > 0 && viewportPoint.x >= 0 && viewportPoint.x <= Screen.width && viewportPoint.y >= 0 && viewportPoint.y <= Screen.height;
    }

    // =========================================================================
    // NATIVE TRIGGER ENGINE: CHECKS IF FLASHLIGHT IS ON AND HAS LINE OF SIGHT
    // =========================================================================
    private void OnTriggerStay(Collider other)
    {
        if (Flashlight.Instance == null || other != Flashlight.Instance.flashlightTriggerCollider) return;

        // Line-of-sight wall occlusion check using open layer sweep (~0)
        Vector3 chestLevel = transform.position + (Vector3.up * 1.2f);
        Vector3 flashOrigin = Flashlight.Instance.transform.position;

        if (Physics.Linecast(flashOrigin, chestLevel, out RaycastHit hit, ~0))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.transform == _aiLogic.playerTarget)
            {
                _isCurrentlyFlashed = true;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Player.Instance != null && ((1 << other.gameObject.layer) & Player.Instance.lightLayer.value) != 0)
        {
            if (!_activeLightColliders.Contains(other)) _activeLightColliders.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (Player.Instance != null && ((1 << other.gameObject.layer) & Player.Instance.lightLayer.value) != 0)
        {
            if (_activeLightColliders.Contains(other)) _activeLightColliders.Remove(other);
        }
    }

    public void FlushSensoryCache()
    {
        _activeLightColliders.Clear();
        _isCurrentlyFlashed = false;
    }
}
