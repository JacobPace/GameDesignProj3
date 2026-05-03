using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class Wendigooner : MonoBehaviour
{
    private static readonly WaitForSeconds _tick = new(0.1f);
    public enum MonsterState { Idle, Stalking, Hiding, Charging, Fleeing, Vanished }

    [Header("References")]
    public Transform player;
    public Camera playerCamera;
    public NavMeshObstacle playerObstacle;
    private NavMeshAgent agent;
    private MeshRenderer[] renderers;

    [Header("Sanity & Distance")]
    [Range(0, 100)] public float playerSanity = 100f; // hook up to actual sanity system
    public float maxStalkDistance = 30f;
    public float minStalkDistance = 10f;
    private float currentTargetDistance;

    [Header("Detection")]
    public LayerMask obstacleMask;
    public LayerMask lightLayerMask;
    public MonsterState currentState = MonsterState.Idle;

    private float _stuckTimer = 0f;
    private float _vanishTimer = 0f;
    private float _logTimer = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        renderers = GetComponentsInChildren<MeshRenderer>();
        if (playerObstacle != null) playerObstacle.enabled = false;
    }

    void Start() => StartCoroutine(AIController());

    IEnumerator AIController()
    {
        while (true)
        {
            if (player == null) { yield return _tick; continue; }
            float dist = Vector3.Distance(transform.position, player.position);
            HandleLogging(dist);

            float sanityPercent = 1f - (playerSanity / 100f);
            UpdateStats(sanityPercent);

            if (currentState != MonsterState.Vanished)
            {
                DetermineState(dist);
                ExecuteState();
                CheckIfStuck();
            }
            else
            {
                HandleVanish();
            }
            yield return _tick;
        }
    }

    void HandleLogging(float dist)
    {
        _logTimer += 0.1f;
        if (_logTimer >= 3f)
        {
            Debug.Log($"<color=yellow>AI Distance: {dist:F1}m</color> | State: {currentState}");
            _logTimer = 0f;
        }
    }

    void UpdateStats(float sanityPercent)
    {
        agent.speed = (currentState == MonsterState.Fleeing) ? 15f : Mathf.Lerp(4f, 10f, sanityPercent);
        agent.acceleration = 120f;
        currentTargetDistance = Mathf.Lerp(maxStalkDistance, minStalkDistance, sanityPercent);
        agent.stoppingDistance = 1.5f;
    }

    void DetermineState(float dist)
    {
        if (CheckIfInLight() || IsInPlayerView()) { currentState = MonsterState.Fleeing; return; }
        if (playerSanity > 70f && dist < (currentTargetDistance - 5f)) { currentState = MonsterState.Hiding; return; }
        currentState = (playerSanity < 30f && dist < 12f) ? MonsterState.Charging : MonsterState.Stalking;
    }

    void ExecuteState()
    {
        agent.isStopped = false;
        if (playerObstacle != null) playerObstacle.enabled = (currentState == MonsterState.Stalking);

        switch (currentState)
        {
            case MonsterState.Stalking: FindPosition(false); break;
            case MonsterState.Hiding:
            case MonsterState.Fleeing: FindPosition(true); break;
            case MonsterState.Charging: agent.SetDestination(player.position); break;
        }
    }

    void FindPosition(bool hide)
    {
        Vector3 pForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        for (int i = 0; i < 50; i++)
        {
            Vector3 target = player.position + (Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward * currentTargetDistance);
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            {
                if (ValidatePath(hit.position, pForward))
                {
                    Vector3 dir = (hit.position - player.position).normalized;
                    float dot = Vector3.Dot(pForward, dir);
                    bool hasLOS = !Physics.Linecast(hit.position + Vector3.up, playerCamera.transform.position, obstacleMask);

                    if (hide && (dot < 0.2f || !hasLOS)) { agent.SetDestination(hit.position); return; }
                    if (!hide && dot < -0.4f && hasLOS) { agent.SetDestination(hit.position); return; }
                }
            }
        }
    }

    bool ValidatePath(Vector3 dest, Vector3 pForward)
    {
        NavMeshPath path = new();
        if (agent.CalculatePath(dest, path))
        {
            int cornersToCheck = Mathf.Min(path.corners.Length, 4);
            for (int i = 0; i < cornersToCheck; i++)
            {
                Vector3 corner = path.corners[i];
                Vector3 dir = Vector3.ProjectOnPlane((corner - playerCamera.transform.position), Vector3.up).normalized;
                if (Vector3.Dot(pForward, dir) > 0.45f && !Physics.Linecast(playerCamera.transform.position, corner, obstacleMask))
                    return false;
            }
            return true;
        }
        return false;
    }

    void CheckIfStuck()
    {
        if ((currentState == MonsterState.Fleeing || currentState == MonsterState.Hiding) && IsInPlayerView())
        {
            _stuckTimer += 0.1f;
        }
        else { _stuckTimer = 0f; }

        if (_stuckTimer > 2.0f) { Vanish(); }
    }

    void Vanish()
    {
        _stuckTimer = 0f;
        _vanishTimer = 8f;
        currentState = MonsterState.Vanished;
        ToggleVisible(false);
        agent.isStopped = true;
        agent.Warp(new Vector3(0, -500, 0)); // Move to a "limbo" position
        // limbo... limbo... limbo....
    }

    void HandleVanish()
    {
        _vanishTimer -= 0.1f;
        if (_vanishTimer <= 0)
        {
            Vector3 spawnPos = player.position - (Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized * maxStalkDistance);
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 40f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                ToggleVisible(true);
                currentState = MonsterState.Idle;
            }
        }
    }

    void ToggleVisible(bool v) { foreach (var r in renderers) r.enabled = v; }

    bool IsInPlayerView()
    {
        Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        Vector3 dir = Vector3.ProjectOnPlane((transform.position - playerCamera.transform.position), Vector3.up).normalized;
        return Vector3.Dot(forward, dir) > 0.45f && !Physics.Linecast(playerCamera.transform.position, transform.position, obstacleMask);
    }

    bool CheckIfInLight()
    {
        Light flash = playerCamera.GetComponentInChildren<Light>();
        if (flash != null && flash.enabled)
        {
            Vector3 dir = (transform.position - flash.transform.position).normalized;
            if (Vector3.Angle(flash.transform.forward, dir) < flash.spotAngle / 2 && Vector3.Distance(transform.position, flash.transform.position) < flash.range)
                if (!Physics.Linecast(flash.transform.position, transform.position, obstacleMask)) return true;
        }
        return Physics.CheckSphere(transform.position, 2.5f, lightLayerMask);
    }
}