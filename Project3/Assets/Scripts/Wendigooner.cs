using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class Wendigooner : MonoBehaviour
{
    private static readonly WaitForSeconds _tick = new(0.1f);
    public enum MonsterState { Idle, Stalking, Hiding, Charging, Fleeing, Vanished }

    [Header("References")]
    public Transform player;
    public Camera playerCamera;
    public NavMeshObstacle playerObstacle;
    private NavMeshAgent agent;
    private Renderer[] _allRenderers;
    private Light[] _allChildLights;

    [Header("Distance")]
    public float maxStalkDistance = 30f;
    public float minStalkDistance = 10f;
    private float currentTargetDistance;

    [Header("Detection")]
    public LayerMask obstacleMask;
    public LayerMask lightLayerMask;
    public LayerMask flashlightLayerMask;
    public MonsterState currentState = MonsterState.Idle;
    private MonsterState _previousState = MonsterState.Idle;

    [Header("Gameplay Mechanics")]
    public float flashVanishDuration = 10f;
    public float heightTolerance = 3f;
    public float attackRange = 2f;
    public float attackCooldown = 3f;
    private int playerStrikes = 0;
    private float _nextAttackTime = 0f;

    [Header("Spawn Grace Settings")]
    public float spawnGraceDuration = 3.5f;
    private float _spawnGraceTimer = 0f;

    [Header("AI Master Switch")]
    private bool isAIActive = true;
    private Vector3 savedVelocity;

    private float _stuckTimer = 0f;
    private float _vanishTimer = 0f;
    private float _logTimer = 0f;

    private readonly HashSet<Collider> _activeLightColliders = new();

    void Awake()
    {
        // CORE FIX: Decouple from scaled environment asset hierarchies instantly
        if (transform.parent != null)
        {
            Vector3 worldPos = transform.position;
            Quaternion worldRot = transform.rotation;
            transform.SetParent(null);
            transform.position = worldPos;
            transform.rotation = worldRot;
        }

        agent = GetComponent<NavMeshAgent>();
        _allRenderers = GetComponentsInChildren<Renderer>(true);
        _allChildLights = GetComponentsInChildren<Light>(true);
        if (playerObstacle != null) playerObstacle.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Start()
    {
        if (player == null && Player.Instance != null)
        {
            player = Player.Instance.transform;
        }
        StartCoroutine(AIController());
    }

    IEnumerator AIController()
    {
        while (true)
        {
            if (!isAIActive)
            {
                yield return _tick;
                continue;
            }

            if (player == null)
            {
                if (Player.Instance != null) player = Player.Instance.transform;
                yield return _tick;
                continue;
            }

            if (currentState == MonsterState.Vanished)
            {
                HandleVanish();
                yield return _tick;
                continue;
            }

            if (_spawnGraceTimer > 0f)
            {
                _spawnGraceTimer -= 0.1f;
            }

            MatchPlayerFloor();

            float dist = Vector3.Distance(transform.position, player.position);
            HandleLogging(dist);

            float sanityPercent = 1f - (Player.Instance.sanity / 100f);
            UpdateStats(sanityPercent);

            // 1. SYSTEM ABSOLUTE PRIORITY: Flashlight safety intersection check
            if (IsHitByFlashlight())
            {
                Vanish(flashVanishDuration);
                yield return _tick;
                continue;
            }

            DetermineState(dist);
            ExecuteState();
            CheckIfStuck();
            CheckAttack(dist);

            yield return _tick;
        }
    }

    public void DisableAI()
    {
        isAIActive = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            savedVelocity = agent.velocity;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    public void EnableAI()
    {
        isAIActive = true;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.velocity = savedVelocity;
        }
    }

    void HandleLogging(float flat3DDist)
    {
        _logTimer += 0.1f;
        if (_logTimer >= 3f)
        {
            float targetDisplayDistance = flat3DDist;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.pathPending)
                {
                    Vector3 monsterPlane = Vector3.ProjectOnPlane(transform.position, Vector3.up);
                    Vector3 playerPlane = Vector3.ProjectOnPlane(player.position, Vector3.up);
                    targetDisplayDistance = Vector3.Distance(monsterPlane, playerPlane);
                }
                else
                {
                    NavMeshPath currentPath = agent.path;
                    if (currentPath != null && currentPath.corners.Length >= 2)
                    {
                        float calculatedLength = 0f;
                        Vector3[] corners = currentPath.corners;
                        for (int i = 0; i < corners.Length - 1; i++)
                        {
                            calculatedLength += Vector3.Distance(corners[i], corners[i + 1]);
                        }
                        targetDisplayDistance = calculatedLength;
                    }
                }
            }

            Debug.Log($"<color=yellow>AI True Distance: {targetDisplayDistance:F1}m</color> (Direct Line: {flat3DDist:F1}m) | State: {currentState} | Sanity: {Player.Instance.sanity:F0}%");
            _logTimer = 0f;
        }
    }

    void UpdateStats(float sanityPercent)
    {
        if (currentState == MonsterState.Fleeing)
            agent.speed = 15f;
        else if (currentState == MonsterState.Charging)
            agent.speed = Mathf.Lerp(6f, 13f, sanityPercent);
        else
            agent.speed = Mathf.Lerp(4f, 8f, sanityPercent);

        agent.acceleration = 120f;
        agent.angularSpeed = 360f;
        currentTargetDistance = Mathf.Lerp(maxStalkDistance, minStalkDistance, sanityPercent);
        agent.stoppingDistance = (currentState == MonsterState.Charging) ? attackRange - 0.2f : 1.5f;
    }

    void DetermineState(float dist)
    {
        _previousState = currentState;

        if (CheckIfInLight())
        {
            currentState = MonsterState.Fleeing;
            return;
        }

        bool canBeSeenByPlayerEyes = (_spawnGraceTimer <= 0f) && IsInPlayerView();
        if (canBeSeenByPlayerEyes)
        {
            currentState = MonsterState.Fleeing;
            return;
        }

        // SANITY GAURD: charging state isolated exclusively to sanity evaluation
        if (Player.Instance.sanity < 45f && dist <= (currentTargetDistance + 6f))
        {
            currentState = MonsterState.Charging;
            return;
        }

        if (dist < (currentTargetDistance - 4f))
        {
            currentState = MonsterState.Hiding;
        }
        else
        {
            currentState = MonsterState.Stalking;
        }
    }

    void ExecuteState()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (currentState != _previousState)
        {
            agent.ResetPath();
        }

        if (agent.pathPending)
        {
            if (agent.isStopped) agent.isStopped = false;
            return;
        }

        if (currentState == MonsterState.Charging)
        {
            if (agent.isStopped) agent.isStopped = false;
            Vector3 localTargetPos = player.position;
            localTargetPos.y = transform.position.y;
            agent.SetDestination(localTargetPos);
            return;
        }

        if (agent.hasPath && agent.remainingDistance <= agent.stoppingDistance)
        {
            agent.ResetPath();
        }

        bool hasActiveRouting = agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathPartial;
        if (hasActiveRouting && agent.remainingDistance > agent.stoppingDistance)
        {
            if (agent.isStopped) agent.isStopped = false;
            return;
        }

        if (agent.isStopped) agent.isStopped = false;
        if (playerObstacle != null) playerObstacle.enabled = (currentState == MonsterState.Stalking);

        switch (currentState)
        {
            case MonsterState.Stalking:
                FindPosition(false);
                break;
            case MonsterState.Hiding:
            case MonsterState.Fleeing:
                FindPosition(true);
                break;
        }
    }

    void MatchPlayerFloor()
    {
        if (currentState == MonsterState.Vanished || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        float yDiff = Mathf.Abs(transform.position.y - player.position.y);
        if (yDiff > (heightTolerance * 1.5f))
        {
            Vector3 targetFloorPos = new Vector3(transform.position.x, player.position.y, transform.position.z);
            if (NavMesh.SamplePosition(targetFloorPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }

    bool IsHitByFlashlight()
    {
        if (Flashlight.Instance == null || Flashlight.Instance.lightSource == null) return false;

        Light flashComp = Flashlight.Instance.lightSource;
        if (!flashComp.enabled || flashComp.intensity <= 0) return false;

        // Define three distinct vertical target tracking offsets across the monster's model body
        Vector3[] targetPoints = new Vector3[3];
        targetPoints[0] = transform.position + (Vector3.up * 1.8f); // Head / Horns level
        targetPoints[1] = transform.position + (Vector3.up * 1.1f); // Center Chest level
        targetPoints[2] = transform.position + (Vector3.up * 0.2f); // Feet / Footing level

        Vector3 cameraPos = playerCamera.transform.position;
        float maxRange = flashComp.range;
        float halfConeAngle = flashComp.spotAngle / 1.8f;

        // MULTI-POINT OCCLUSION SWEEP: Loop through all 3 targets. If any single point passes, trigger the vanish!
        for (int i = 0; i < targetPoints.Length; i++)
        {
            Vector3 targetPos = targetPoints[i];
            float distanceToTarget = Vector3.Distance(cameraPos, targetPos);

            if (distanceToTarget <= maxRange)
            {
                Vector3 directionToTarget = (targetPos - cameraPos).normalized;
                float angleToTarget = Vector3.Angle(playerCamera.transform.forward, directionToTarget);

                if (angleToTarget < halfConeAngle)
                {
                    // Proximity Safety Buffer: Force vanish if standing directly on top of the player
                    if (distanceToTarget < 2.5f) return true;

                    // Fire an open-universe linecast pass to test for wall obstructions
                    if (Physics.Linecast(cameraPos, targetPos, out RaycastHit hit, ~0))
                    {
                        // Connection confirmed if the ray strikes any part of the monster hierarchy or the player
                        if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.transform == player)
                        {
                            Debug.Log($"<color=green><b>[Wendigooner]</b></color> Flashlight contact confirmed on target node index: {i}");
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    void CheckAttack(float dist)
    {
        if (currentState != MonsterState.Charging || dist > attackRange || Time.time < _nextAttackTime) return;

        _nextAttackTime = Time.time + attackCooldown;
        playerStrikes++;

        if (playerStrikes >= 3)
            TriggerGameOver();
        else
            Vanish(5f);
    }

    void TriggerGameOver()
    {
        Debug.LogError("Game Over! Caught 3 times.");
    }

    void FindPosition(bool hide)
    {
        Vector3 pForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        float targetRadiusDist = hide ? maxStalkDistance : currentTargetDistance;

        Vector3 bestPoint = Vector3.zero;
        float bestScore = -1f;
        bool foundValidPoint = false;

        bool isSanityLow = (Player.Instance.sanity < 45f);

        for (int i = 0; i < 40; i++)
        {
            Vector3 randomDir = Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward;
            Vector3 initialCalculatedTarget = player.position + (randomDir * targetRadiusDist);

            if (NavMesh.SamplePosition(initialCalculatedTarget, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                if (IsPositionLit(hit.position)) continue;

                if (ValidatePath(hit.position, pForward))
                {
                    Vector3 dir = (hit.position - player.position).normalized;
                    float dot = Vector3.Dot(pForward, dir);
                    bool hasLOS = Physics.Linecast(playerCamera.transform.position, hit.position + Vector3.up * 1.2f, out RaycastHit sightHit, ~0) &&
                                  (sightHit.transform == transform || sightHit.transform.IsChildOf(transform) || sightHit.transform == player);

                    bool criteriaMatched = false;

                    if (hide)
                    {
                        if (dot < 0.2f || !hasLOS) criteriaMatched = true;
                    }
                    else
                    {
                        if (isSanityLow)
                        {
                            if (dot < -0.3f) criteriaMatched = true; // Front approach
                        }
                        else
                        {
                            if (dot > 0.45f) criteriaMatched = true; // Stalk behind player's back
                        }
                    }

                    if (criteriaMatched)
                    {
                        float currentScore = 0f;
                        NavMeshPath testPath = new();

                        if (agent.CalculatePath(hit.position, testPath))
                        {
                            if (testPath.status == NavMeshPathStatus.PathComplete) currentScore += 100f;
                            else if (testPath.status == NavMeshPathStatus.PathPartial) currentScore += 50f;
                            else continue;
                        }

                        float distanceToPlayer = Vector3.Distance(hit.position, player.position);
                        currentScore += (targetRadiusDist - Mathf.Abs(distanceToPlayer - targetRadiusDist)) * 2.0f;

                        if (currentScore > bestScore)
                        {
                            bestScore = currentScore;
                            bestPoint = hit.position;
                            foundValidPoint = true;
                        }
                    }
                }
            }
        }

        if (foundValidPoint)
        {
            agent.SetDestination(bestPoint);
        }
        else
        {
            Vector3 fallbackTarget = player.position - (pForward * targetRadiusDist);
            if (NavMesh.SamplePosition(fallbackTarget, out NavMeshHit fallbackHit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(fallbackHit.position);
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
                if (IsPositionLit(corner)) return false;

                Vector3 dir = Vector3.ProjectOnPlane((corner - playerCamera.transform.position), Vector3.up).normalized;
                if (Vector3.Dot(pForward, dir) > 0.45f)
                {
                    if (Physics.Linecast(playerCamera.transform.position, corner, out RaycastHit hit, ~0))
                    {
                        if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.transform == player)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        return false;
    }

    private bool IsPositionLit(Vector3 position)
    {
        Collider[] hitLights = Physics.OverlapSphere(position, 0.5f, lightLayerMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitLights.Length; i++)
        {
            if (!Physics.Linecast(hitLights[i].bounds.center, position, obstacleMask))
            {
                return true;
            }
        }
        return false;
    }

    void CheckIfStuck()
    {
        if (currentState == MonsterState.Vanished) return;

        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit localHit, 4.0f, NavMesh.AllAreas))
            {
                transform.position = localHit.position;
                agent.Warp(localHit.position);
                agent.ResetPath();
                return;
            }
        }

        bool isTryingToEscape = (currentState == MonsterState.Fleeing || currentState == MonsterState.Hiding);
        bool isTrappedInPlayerSight = isTryingToEscape && IsInPlayerView();
        bool isPhysicallyStopped = (agent != null && agent.isOnNavMesh && agent.velocity.sqrMagnitude < 0.05f);

        if (isTrappedInPlayerSight && isPhysicallyStopped)
        {
            _stuckTimer += 0.1f;
            if (_stuckTimer >= 0.8f)
            {
                Vanish(flashVanishDuration);
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
    }

    void Vanish(float duration)
    {
        _stuckTimer = 0f;
        _vanishTimer = duration;
        currentState = MonsterState.Vanished;

        ToggleVisible(false);

        if (agent != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            agent.enabled = false;
        }

        if (TryGetComponent<Collider>(out var myCollider))
        {
            myCollider.enabled = false;
        }

        _activeLightColliders.Clear();
        Debug.Log($"Vanish cleanly executed via absolute system level clear pass.");
    }

    void HandleVanish()
    {
        _vanishTimer -= 0.1f;
        if (_vanishTimer <= 0)
        {
            Vector3 forwardProj = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
            if (forwardProj.sqrMagnitude < 0.01f)
            {
                forwardProj = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            }

            int executionAttempts = 20;
            for (int i = 0; i < executionAttempts; i++)
            {
                float progress = (float)i / executionAttempts;
                float currentSearchRadius = Mathf.Lerp(maxStalkDistance, 5f, progress);
                float navMeshSearchRadius = Mathf.Lerp(2.0f, 10.0f, progress);

                Vector3 randomDirection = Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward;
                Vector3 prospectivePoint = player.position + (randomDirection * currentSearchRadius);

                if (NavMesh.SamplePosition(prospectivePoint, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
                {
                    if (!IsPositionLit(hit.position))
                    {
                        Vector3 targetChestHeight = hit.position + (Vector3.up * 1.2f);
                        Vector3 screenPos = playerCamera.WorldToScreenPoint(targetChestHeight);

                        bool pointIsOnScreen = screenPos.z > 0 &&
                                               screenPos.x >= 0 && screenPos.x <= Screen.width &&
                                               screenPos.y >= 0 && screenPos.y <= Screen.height;

                        bool isObscuredByWall = Physics.Linecast(playerCamera.transform.position, targetChestHeight, out RaycastHit sightHit, ~0) &&
                                                sightHit.transform != transform && !sightHit.transform.IsChildOf(transform) && sightHit.transform != player;

                        if (!pointIsOnScreen || isObscuredByWall)
                        {
                            MaterializeMonster(hit.position);
                            return;
                        }
                    }
                }
            }

            if (NavMesh.SamplePosition(player.position, out NavMeshHit finalHit, 3.0f, NavMesh.AllAreas))
            {
                Vector3 safetyBackstep = player.position - (forwardProj * 5f);
                if (NavMesh.SamplePosition(safetyBackstep, out NavMeshHit safetyHit, 6.0f, NavMesh.AllAreas))
                {
                    MaterializeMonster(safetyHit.position);
                    return;
                }
                MaterializeMonster(finalHit.position);
                return;
            }

            _vanishTimer = 0.2f;
        }
    }

    void MaterializeMonster(Vector3 targetPosition)
    {
        transform.position = targetPosition;

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(targetPosition);
            agent.isStopped = false;
            agent.ResetPath();
            agent.SetDestination(targetPosition);
            agent.velocity = Vector3.zero;
        }

        if (TryGetComponent<Collider>(out var myCollider))
        {
            myCollider.enabled = true;
        }

        ToggleVisible(true);
        currentState = MonsterState.Idle;
        _stuckTimer = 0f;
        _spawnGraceTimer = spawnGraceDuration;
    }

    void ToggleVisible(bool v)
    {
        if (_allRenderers == null || _allRenderers.Length == 0) _allRenderers = GetComponentsInChildren<Renderer>(true);
        if (_allChildLights == null || _allChildLights.Length == 0) _allChildLights = GetComponentsInChildren<Light>(true);

        foreach (var r in _allRenderers) if (r != null) r.enabled = v;
        foreach (var l in _allChildLights) if (l != null) l.enabled = v;
    }

    bool IsInPlayerView()
    {
        if (playerCamera == null) return false;

        Vector3 targetChestLevel = transform.position + (Vector3.up * 1.2f);
        Vector3 screenPoint = playerCamera.WorldToScreenPoint(targetChestLevel);

        bool isOnScreen = screenPoint.z > 0 &&
                          screenPoint.x >= 0 && screenPoint.x <= Screen.width &&
                          screenPoint.y >= 0 && screenPoint.y <= Screen.height;

        if (!isOnScreen) return false;

        Vector3 eyeLevel = playerCamera.transform.position;
        if (Physics.Linecast(eyeLevel, targetChestLevel, out RaycastHit hit, ~0))
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        int otherLayer = other.gameObject.layer;
        if (((1 << otherLayer) & lightLayerMask.value) != 0)
        {
            if (!_activeLightColliders.Contains(other)) _activeLightColliders.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        int otherLayer = other.gameObject.layer;
        if (((1 << otherLayer) & lightLayerMask.value) != 0)
        {
            if (_activeLightColliders.Contains(other)) _activeLightColliders.Remove(other);
        }
    }

    public bool CheckIfInLight()
    {
        // Clean out any missing or disabled scene light volumes from memory first
        _activeLightColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);

        // Returns true if the monster is currently inside a light zone
        return _activeLightColliders.Count > 0;
    }
}
