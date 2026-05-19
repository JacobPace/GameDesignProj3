using UnityEngine;

public class DistanceToggle : MonoBehaviour
{
    [Tooltip("The object(s) to enable/disable based on distance.")]
    public GameObject[] targets;

    [Tooltip("Drag the player here, or leave empty to auto-find by tag.")]
    public Transform player;

    [Tooltip("Distance at which the targets turn off.")]
    public float maxDistance = 20f;

    [Tooltip("How often (in seconds) to check distance. 0 = every frame.")]
    public float checkInterval = 0.1f;

    [Tooltip("Measure distance from the renderer's visual center instead of the pivot. Fixes offset pivots.")]
    public bool useRendererBounds = true;

    [Tooltip("If true, targets are ON when far away and OFF when close.")]
    public bool invert = false;

    private float timer;
    private Renderer[] cachedRenderers;
    private Vector3[] cachedCenters;   // last known visual center for each target
    private bool[] centersInitialized;

    void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (targets != null)
        {
            cachedRenderers = new Renderer[targets.Length];
            cachedCenters = new Vector3[targets.Length];
            centersInitialized = new bool[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;

                cachedRenderers[i] = targets[i].GetComponentInChildren<Renderer>(true);

                // Seed the cached center while the object is still active
                if (useRendererBounds && cachedRenderers[i] != null && targets[i].activeInHierarchy)
                {
                    cachedCenters[i] = cachedRenderers[i].bounds.center;
                    centersInitialized[i] = true;
                }
                else
                {
                    cachedCenters[i] = targets[i].transform.position;
                    centersInitialized[i] = true;
                }
            }
        }
    }

    Vector3 GetTargetPosition(int i)
    {
        // If the target is currently active, refresh the cached center (handles moving objects)
        if (useRendererBounds
            && cachedRenderers != null
            && cachedRenderers[i] != null
            && targets[i].activeInHierarchy)
        {
            cachedCenters[i] = cachedRenderers[i].bounds.center;
            centersInitialized[i] = true;
            return cachedCenters[i];
        }

        // Otherwise fall back to the last known good center, or transform.position
        if (centersInitialized[i]) return cachedCenters[i];
        return targets[i].transform.position;
    }

    void Update()
    {
        if (player == null || targets == null || targets.Length == 0) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkInterval;

        float sqrMax = maxDistance * maxDistance;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;

            Vector3 targetPos = GetTargetPosition(i);
            float sqrDist = (player.position - targetPos).sqrMagnitude;
            bool withinRange = sqrDist <= sqrMax;
            bool shouldBeOn = invert ? !withinRange : withinRange;

            if (targets[i].activeSelf != shouldBeOn)
                targets[i].SetActive(shouldBeOn);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (targets == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;

            Vector3 pos;
            if (Application.isPlaying && centersInitialized != null && i < centersInitialized.Length && centersInitialized[i])
            {
                pos = cachedCenters[i]; // use the cached value at runtime so the sphere stays put when off
            }
            else
            {
                // Editor preview: try to get bounds, fall back to transform
                var r = targets[i].GetComponentInChildren<Renderer>(true);
                pos = (useRendererBounds && r != null) ? r.bounds.center : targets[i].transform.position;
            }
            Gizmos.DrawWireSphere(pos, maxDistance);
        }
    }
}