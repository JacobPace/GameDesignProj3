using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterNavigation : MonoBehaviour
{
    private NavMeshAgent _agent;
    private Vector3 _savedVelocity;

    public bool IsGroundedOnMesh => _agent != null && _agent.enabled && _agent.isOnNavMesh;
    public bool IsPathPending => _agent != null && _agent.enabled && _agent.pathPending;
    public float RemainingDistance => _agent != null ? _agent.remainingDistance : Mathf.Infinity;
    public float StoppingDistance => _agent != null ? _agent.stoppingDistance : 0f;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    public void UpdateAgentProperties(float speed, float acceleration, float stoppingDistance)
    {
        if (!_agent.enabled) return;
        _agent.speed = speed;
        _agent.acceleration = acceleration;
        _agent.stoppingDistance = stoppingDistance;
        _agent.angularSpeed = 360f;
    }

    public void MoveToDestination(Vector3 targetPosition)
    {
        if (!IsGroundedOnMesh) return;
        _agent.SetDestination(targetPosition);
    }

    public void StopMovement()
    {
        if (!IsGroundedOnMesh) return;
        _savedVelocity = _agent.velocity;
        _agent.ResetPath();
        _agent.isStopped = true;
    }

    public void ResumeMovement()
    {
        if (!IsGroundedOnMesh) return;
        _agent.isStopped = false;
        _agent.velocity = _savedVelocity;
    }

    public void SetAgentComponentState(bool isEnabled)
    {
        if (_agent == null) return;
        _agent.enabled = isEnabled;
    }

    public void ForceWarp(Vector3 position)
    {
        if (_agent == null) return;
        _agent.enabled = true;
        _agent.Warp(position);
    }

    public float CalculateTrueWalkDistance(Vector3 targetPosition)
    {
        if (!IsGroundedOnMesh || IsPathPending)
        {
            return Vector3.Distance(Vector3.ProjectOnPlane(transform.position, Vector3.up), Vector3.ProjectOnPlane(targetPosition, Vector3.up));
        }

        NavMeshPath path = _agent.path;
        if (path == null || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
        {
            return Vector3.Distance(Vector3.ProjectOnPlane(transform.position, Vector3.up), Vector3.ProjectOnPlane(targetPosition, Vector3.up));
        }

        float totalLength = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            totalLength += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        }
        return totalLength;
    }

    public void MatchPlayerFloorHeight(Vector3 playerPosition, float heightTolerance)
    {
        if (!IsGroundedOnMesh) return;

        float verticalDifference = Mathf.Abs(transform.position.y - playerPosition.y);
        if (verticalDifference > (heightTolerance * 1.5f))
        {
            Vector3 clampedFloorTarget = new Vector3(transform.position.x, playerPosition.y, transform.position.z);
            if (NavMesh.SamplePosition(clampedFloorTarget, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
        }
    }

    public bool CheckPathValidity(Vector3 destination, out Vector3 samplingPosition)
    {
        samplingPosition = destination;
        NavMeshPath path = new NavMeshPath();
        if (_agent.CalculatePath(destination, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete) return true;
        }
        return false;
    }
}