using UnityEngine;
using System.Collections.Generic;

public class MonsterDetection : MonoBehaviour
{
    private readonly HashSet<Collider> _activeLightColliders = new();

    public bool IsInsideAmbientSafeZoneLight()
    {
        _activeLightColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        return _activeLightColliders.Count > 0;
    }

    /// <summary>
    /// Checks if the monster is within a valid proximity range, on the player's screen, and has an un-occluded line of sight.
    /// </summary>
    public bool IsVisibleOnPlayerScreen(Camera playerCamera, LayerMask obstacleMask, float maxRangeCheck)
    {
        if (playerCamera == null) return false;

        Vector3 targetChestLevel = transform.position + (Vector3.up * 1.2f);
        float distanceToPlayer = Vector3.Distance(playerCamera.transform.position, targetChestLevel);

        // If the monster is further away than the maximum sight range, ignore the check
        if (distanceToPlayer > maxRangeCheck) return false;

        // Is it physically within the monitor's rendering borders?
        Vector3 viewportPoint = playerCamera.WorldToScreenPoint(targetChestLevel);
        bool withinScreenMargins = viewportPoint.z > 0 &&
                                   viewportPoint.x >= 0 && viewportPoint.x <= Screen.width &&
                                   viewportPoint.y >= 0 && viewportPoint.y <= Screen.height;

        if (!withinScreenMargins) return false;

        // Ensure no solid cave box geometry blocks the view
        Vector3 eyeLevel = playerCamera.transform.position;
        LayerMask combinedMask = (obstacleMask.value == 0) ? ~0 : obstacleMask;

        if (Physics.Linecast(eyeLevel, targetChestLevel, out RaycastHit hit, combinedMask))
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            {
                return false; // Safely hidden out of view behind something
            }
        }

        // Inside range, visible on monitor frame, and has clear line of sight path
        return true;
    }

    public bool EvaluateNodeLightStatus(Vector3 worldPosition, LayerMask lightLayer, LayerMask wallLayer)
    {
        Collider[] hitFields = Physics.OverlapSphere(worldPosition, 0.5f, lightLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitFields.Length; i++)
        {
            if (!Physics.Linecast(hitFields[i].bounds.center, worldPosition, wallLayer)) return true;
        }
        return false;
    }

    public void FlushSensoryCache()
    {
        _activeLightColliders.Clear();
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
}