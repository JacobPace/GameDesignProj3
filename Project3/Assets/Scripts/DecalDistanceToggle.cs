using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(DecalProjector))]
public class DecalDistanceToggle : MonoBehaviour
{
    [Tooltip("Drag the player capsule here, or leave empty to auto-find by tag.")]
    public Transform player;

    [Tooltip("Distance at which the decal turns off.")]
    public float maxDistance = 20f;

    [Tooltip("How often (in seconds) to check distance. 0 = every frame.")]
    public float checkInterval = 0.1f;

    private DecalProjector decal;
    private float timer;

    void Awake()
    {
        decal = GetComponent<DecalProjector>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (player == null || decal == null) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkInterval;

        float sqrDist = (player.position - transform.position).sqrMagnitude;
        bool shouldBeOn = sqrDist <= maxDistance * maxDistance;

        if (decal.enabled != shouldBeOn)
            decal.enabled = shouldBeOn;
    }
}