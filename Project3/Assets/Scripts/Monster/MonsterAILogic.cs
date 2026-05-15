using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using static Wendigooner;

[RequireComponent(typeof(MonsterNavigation))]
[RequireComponent(typeof(MonsterDetection))]
[RequireComponent(typeof(Rigidbody))]
public class MonsterAILogic : MonoBehaviour
{
    private static readonly WaitForSeconds TICK_RATE = new(0.1f);
    public enum AIState { Idle, Stalking, Hiding, Charging, Fleeing, Vanished }

    [Header("Core Reference Targets")]
    public Transform playerTarget;
    public Camera playerVisualCamera;
    public NavMeshObstacle fallbackObstacle;

    [Header("Distance Range Brackets")]
    public float maxStalkRadius = 30f;
    public float minStalkRadius = 10f;
    private float _dynamicStalkTargetRadius;

    [Header("Detection Layers")]
    [Tooltip("The physics layer containing your custom solid cave box wall colliders")]
    public LayerMask obstacleMask;
    public LayerMask lightLayerMask;
    public LayerMask flashlightLayerMask;

    [Header("Behavior Control Switches")]
    public AIState currentBehaviorState = AIState.Idle;
    public float vanishCooldownSeconds = 5f;
    public float visualGracePeriodSeconds = 3.5f;
    public float physicalMeleeRange = 2f;
    public float structuralAttackCooldown = 3f;
    public float flashVanishRange;

    [Tooltip("Maximum distance the player can spot the monster to trigger a flee response")]
    public float maxSightFleeRange = 20f;

    private MonsterNavigation _navigation;
    private MonsterDetection _detection;
    private Renderer[] _meshHierarchyRenderers;
    private Light[] _eyePointLights;

    private AIState _previousFrameState = AIState.Idle;
    private bool _isGlobalAIActive = true;
    private int _accruedDamageStrikes = 0;
    private float _nextAllowedAttackTimestamp = 0f;
    private float _graceTimer = 0f;
    private float _vanishTimer = 0f;
    private float _telemetryLogTimer = 0f;

    private void Awake()
    {
        if (transform.parent != null)
        {
            Vector3 worldPos = transform.position;
            Quaternion worldRot = transform.rotation;
            transform.SetParent(null);
            transform.position = worldPos;
            transform.rotation = worldRot;
        }

        _navigation = GetComponent<MonsterNavigation>();
        _detection = GetComponent<MonsterDetection>();
        _meshHierarchyRenderers = GetComponentsInChildren<Renderer>(true);
        _eyePointLights = GetComponentsInChildren<Light>(true);

        if (fallbackObstacle != null) fallbackObstacle.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Start()
    {
        if (playerTarget == null && Player.Instance != null) playerTarget = Player.Instance.transform;
        StartCoroutine(ExecuteCoreProcessingLoop());
    }

    private IEnumerator ExecuteCoreProcessingLoop()
    {
        while (true)
        {
            if (!_isGlobalAIActive) { yield return TICK_RATE; continue; }
            if (playerTarget == null) { yield return TICK_RATE; continue; }
            if (currentBehaviorState == AIState.Vanished)
            {
                ProcessVanishSequence();
                yield return TICK_RATE; continue;
            }
            if (_graceTimer > 0f) _graceTimer -= 0.1f;
            _navigation.MatchPlayerFloorHeight(playerTarget.position, 3f);
            float literal3DDistance = Vector3.Distance(transform.position, playerTarget.position);
            ProcessTelemetryLogs(literal3DDistance);
            if (currentBehaviorState != AIState.Charging && literal3DDistance < 5.0f)
            {
                _previousFrameState = currentBehaviorState;
                currentBehaviorState = AIState.Fleeing;
                _navigation.StopMovement(); _navigation.ResumeMovement();
                CompileNextSpatialTrackingDestination(true);
                yield return TICK_RATE; continue;
            }
            if (Flashlight.Instance != null && Flashlight.Instance.lightSource != null)
            {
                bool isFlashlightToggledOn = (bool)typeof(Flashlight)
                    .GetField("isOn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(Flashlight.Instance);

                if (isFlashlightToggledOn && Flashlight.Instance.currentPower > 0.05f)
                {
                    Vector3 chestLevel = transform.position + (Vector3.up * 1.2f);
                    Vector3 cameraPos = playerVisualCamera.transform.position;
                    float distanceToMonster = Vector3.Distance(cameraPos, chestLevel);

                    if (distanceToMonster <= flashVanishRange) // check if monster is within a configurable range to the player while the flashlight is on
                    {
                        Vector3 directionToMonster = (chestLevel - cameraPos).normalized;
                        float angleToMonster = Vector3.Angle(playerVisualCamera.transform.forward, directionToMonster);
                        if (angleToMonster < (Flashlight.Instance.lightSource.spotAngle / 1.8f))
                        {
                            bool clearAirChannel = true;
                            if (Physics.Linecast(cameraPos, chestLevel, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                                if (hit.transform != transform && !hit.transform.IsChildOf(transform) && hit.transform != playerTarget.root)
                                    clearAirChannel = false; // Blocked by wall                                
                            if (clearAirChannel)
                            {
                                Debug.Log("<color=magenta><b>[AI Math Check]</b></color> Flashlight aim confirmed via vectors! Forcing vanish.");
                                TriggerVanishTransition(vanishCooldownSeconds);
                                yield return TICK_RATE;
                                continue;
                            }
                        }
                    }
                }
            }
            float currentSanityRatio = 1f - (Player.Instance.sanity / 100f);
            _dynamicStalkTargetRadius = Mathf.Lerp(maxStalkRadius, minStalkRadius, currentSanityRatio);
            SetMovementStats(currentSanityRatio, literal3DDistance);
            EvaluateTargetBehaviorState(literal3DDistance);
            ExecuteActiveMovementCommands();
            EvaluateCombatAttackStrikes(literal3DDistance);
            yield return TICK_RATE;
        }
    }

    private void SetMovementStats(float sanityPercent, float distanceToPlayer)
    {
        float speed = 4f;
        float acceleration = 120f;
        float stoppingDistance = 1.5f;

        if (currentBehaviorState == AIState.Fleeing)
            speed = 15f;
        else if (currentBehaviorState == AIState.Charging)
        { 
            speed = 4.5f;
            stoppingDistance = physicalMeleeRange - 0.2f;

            // Drops acceleration sharply at close range 
            // to prevent the agent from clipping through the player model capsule.
            if (distanceToPlayer < 4.5f)
            {
                acceleration = 20f;
            }
        }
        else
        {
            speed = Mathf.Lerp(4f, 8f, sanityPercent);
        }

        _navigation.UpdateAgentProperties(speed, acceleration, stoppingDistance);
    }

    private void EvaluateTargetBehaviorState(float distanceToPlayer)
    {
        _previousFrameState = currentBehaviorState;

        if (Player.Instance.sanity < 45f && distanceToPlayer <= (_dynamicStalkTargetRadius + 6f))
        {
            currentBehaviorState = AIState.Charging;
            return;
        }

        bool canBeSeenByPlayerEyes = (_graceTimer <= 0f) && _detection.IsVisibleOnPlayerScreen(playerVisualCamera, obstacleMask, maxSightFleeRange);

        if (_detection.IsInsideAmbientSafeZoneLight() || canBeSeenByPlayerEyes)
        {
            currentBehaviorState = AIState.Fleeing;
            return;
        }

        if (distanceToPlayer < (_dynamicStalkTargetRadius - 4f))
        {
            currentBehaviorState = AIState.Hiding;
        }
        else
        {
            currentBehaviorState = AIState.Stalking;
        }
    }

    private void ExecuteActiveMovementCommands()
    {
        if (!_navigation.IsGroundedOnMesh) return;

        if (currentBehaviorState != _previousFrameState)
        {
            _navigation.StopMovement();
            _navigation.ResumeMovement();
        }

        if (_navigation.IsPathPending) return;

        if (currentBehaviorState == AIState.Charging)
        {
            Vector3 flatPlayerFooting = playerTarget.position;
            flatPlayerFooting.y = transform.position.y;
            _navigation.MoveToDestination(flatPlayerFooting);
            return;
        }

        if (_navigation.RemainingDistance <= _navigation.StoppingDistance)
        {
            _navigation.StopMovement();
            _navigation.ResumeMovement();
        }

        if (_navigation.RemainingDistance > _navigation.StoppingDistance && currentBehaviorState != AIState.Idle) return;

        CompileNextSpatialTrackingDestination(currentBehaviorState == AIState.Hiding || currentBehaviorState == AIState.Fleeing);
    }

    private void CompileNextSpatialTrackingDestination(bool isEscapeProfile)
    {
        Vector3 viewForward = Vector3.ProjectOnPlane(playerVisualCamera.transform.forward, Vector3.up).normalized;
        if (viewForward.sqrMagnitude < 0.01f) viewForward = Vector3.ProjectOnPlane(playerTarget.forward, Vector3.up).normalized;

        float chosenTargetRange = isEscapeProfile ? maxStalkRadius : _dynamicStalkTargetRadius;
        bool isSanityDegraded = Player.Instance.sanity < 45f;

        Vector3 selectedBestNode = Vector3.zero;
        float optimalEvaluationScore = -1f;
        bool coordinateFound = false;

        for (int i = 0; i < 40; i++)
        {
            Vector3 randomOffset = Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward;
            Vector3 targetQuery = playerTarget.position + (randomOffset * chosenTargetRange);
            targetQuery.y = playerTarget.position.y;

            if (UnityEngine.AI.NavMesh.SamplePosition(targetQuery, out UnityEngine.AI.NavMeshHit hit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (Player.Instance != null && _detection.IsInsideAmbientSafeZoneLight()) continue;

                Vector3 targetDirection = (hit.position - playerTarget.position).normalized;
                float alignmentDot = Vector3.Dot(viewForward, targetDirection);

                // Re-fetch screen pixels to verify stealth nodes before choosing them
                Vector3 nodeScreenPos = playerVisualCamera.WorldToScreenPoint(hit.position + Vector3.up * 1.2f);
                bool nodeOnScreen = nodeScreenPos.z > 0 && nodeScreenPos.x >= 0 && nodeScreenPos.x <= Screen.width && nodeScreenPos.y >= 0 && nodeScreenPos.y <= Screen.height;

                bool loopFilterMatch = false;
                if (isEscapeProfile)
                {
                    if (alignmentDot < 0.2f || !nodeOnScreen) loopFilterMatch = true;
                }
                else if (isSanityDegraded)
                {
                    if (alignmentDot < -0.3f) loopFilterMatch = true; // Front ambush
                }
                else
                {
                    if (!nodeOnScreen) loopFilterMatch = true; // Smart stealth circle (Any direction, just out of view)
                }

                if (loopFilterMatch)
                {
                    float prospectiveScore = 0f;
                    if (_navigation.CheckPathValidity(hit.position, out Vector3 verifiedNode))
                    {
                        prospectiveScore += 100f;
                        float distanceToPlayer = Vector3.Distance(verifiedNode, playerTarget.position);
                        prospectiveScore += (chosenTargetRange - Mathf.Abs(distanceToPlayer - chosenTargetRange)) * 2.0f;

                        if (prospectiveScore > optimalEvaluationScore)
                        {
                            optimalEvaluationScore = prospectiveScore;
                            selectedBestNode = verifiedNode;
                            coordinateFound = true;
                        }
                    }
                }
            }
        }

        if (coordinateFound) _navigation.MoveToDestination(selectedBestNode);
        else
        {
            Vector3 rearFallback = playerTarget.position - (viewForward * chosenTargetRange);
            if (UnityEngine.AI.NavMesh.SamplePosition(rearFallback, out UnityEngine.AI.NavMeshHit backupHit, 15f, UnityEngine.AI.NavMesh.AllAreas))
            {
                _navigation.MoveToDestination(backupHit.position);
            }
        }
    }

    private void EvaluateCombatAttackStrikes(float currentDistance)
    {
        if (currentBehaviorState != AIState.Charging || currentDistance > physicalMeleeRange || Time.time < _nextAllowedAttackTimestamp) return;

        _nextAllowedAttackTimestamp = Time.time + structuralAttackCooldown;
        _accruedDamageStrikes++;
        Debug.LogWarning($"Strike registered! Count: {_accruedDamageStrikes}/3");

        if (_accruedDamageStrikes >= 3) ExecuteGameOverSequence();
        else TriggerVanishTransition(5f);
    }

    private void TriggerVanishTransition(float clearWindowDuration)
    {
        _vanishTimer = clearWindowDuration;
        currentBehaviorState = AIState.Vanished;

        ToggleVisualComponents(false);
        _navigation.StopMovement();
        _navigation.SetAgentComponentState(false);

        if (TryGetComponent<Collider>(out var hitBox)) hitBox.enabled = false;
        _detection.FlushSensoryCache();
    }

    private void ProcessVanishSequence()
    {
        _vanishTimer -= 0.1f;
        if (_vanishTimer <= 0)
        {
            Vector3 forwardOrientation = Vector3.ProjectOnPlane(playerVisualCamera.transform.forward, Vector3.up).normalized;
            if (forwardOrientation.sqrMagnitude < 0.01f) forwardOrientation = Vector3.ProjectOnPlane(playerTarget.forward, Vector3.up).normalized;

            Vector3 targetRespawnCoordinates = playerTarget.position - (forwardOrientation * maxStalkRadius);
            targetRespawnCoordinates.y = playerTarget.position.y;

            if (UnityEngine.AI.NavMesh.SamplePosition(targetRespawnCoordinates, out UnityEngine.AI.NavMeshHit spawnHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                StartCoroutine(ExecuteSafeReentrySequence(spawnHit.position));
            }
            else _vanishTimer = 0.5f;
        }
    }

    private IEnumerator ExecuteSafeReentrySequence(Vector3 secureWorldPosition)
    {
        currentBehaviorState = AIState.Vanished;
        transform.position = secureWorldPosition;
        _navigation.SetAgentComponentState(true);

        yield return new WaitForFixedUpdate(); // 1-Frame physics handshake allows agent to safely initialize

        if (_navigation.IsGroundedOnMesh) _navigation.ForceWarp(secureWorldPosition);
        if (TryGetComponent<Collider>(out var hitBox)) hitBox.enabled = true;
        ToggleVisualComponents(true);

        currentBehaviorState = AIState.Idle;
        _graceTimer = visualGracePeriodSeconds;
        _navigation.MoveToDestination(secureWorldPosition);
    }

    private void ToggleVisualComponents(bool isVisible)
    {
        foreach (var r in _meshHierarchyRenderers) if (r != null) r.enabled = isVisible;
        foreach (var l in _eyePointLights) if (l != null) l.enabled = isVisible;
    }

    private void ProcessTelemetryLogs(float flatDistance)
    {
        _telemetryLogTimer += 0.1f;
        if (_telemetryLogTimer >= 3f)
        {
            float totalCalculatedWalkwayLength = _navigation.CalculateTrueWalkDistance(playerTarget.position);
            Debug.Log($"AI Navigation Length: {totalCalculatedWalkwayLength:F1}m (Straight Line: {flatDistance:F1}m) | State: {currentBehaviorState} | Strikes: {_accruedDamageStrikes}/3");
            _telemetryLogTimer = 0f;
        }
    }

    public void ShutdownAIModule()
    {
        _isGlobalAIActive = false;
        _navigation.StopMovement();
    }

    private void ExecuteGameOverSequence()
    {
        ShutdownAIModule();
        if (Player.Instance != null)
            Player.Instance.GameOver(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ensure the player instance configuration script exists on scene boot
        if (Player.Instance == null) return;

        int otherLayer = other.gameObject.layer;

        // Check if the volume we stepped into belongs to the environment light safe-zone mask
        if (((1 << otherLayer) & Player.Instance.lightLayer.value) != 0)
        {
            // Tell the detection module to add this volume to its active hash array list
            if (_detection != null)
            {
                // Use this if your MonsterDetection has a public list tracking system:
                System.Reflection.MethodInfo enterMethod = typeof(MonsterDetection).GetMethod("OnTriggerEnter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enterMethod != null) enterMethod.Invoke(_detection, new object[] { other });
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (Player.Instance == null) return;

        int otherLayer = other.gameObject.layer;

        if (((1 << otherLayer) & Player.Instance.lightLayer.value) != 0)
        {
            if (_detection != null)
            {
                System.Reflection.MethodInfo exitMethod = typeof(MonsterDetection).GetMethod("OnTriggerExit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (exitMethod != null) exitMethod.Invoke(_detection, new object[] { other });
            }
        }
    }
}