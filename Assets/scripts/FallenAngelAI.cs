using UnityEngine;
using UnityEngine.AI;

public class FallenAngelAI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera playerCamera;
    public NavMeshAgent agent;

    [Header("Movement")]
    public float chaseSpeed = 3.5f;
    public float patrolSpeed = 1.5f;
    public float attackDistance = 1.2f;

    [Header("Vision")]
    [Range(0f, 1f)]
    public float lookThreshold = 0.7f;

    public float detectionRange = 15f;

    [Range(0f, 360f)]
    public float fieldOfView = 120f;

    [Header("Search")]
    public float searchTime = 5f;

    [Header("Idle / Patrol")]
    public float patrolRadius = 10f;
    public float patrolWaitTime = 2f;

    [Header("Floor Control")]
    public bool isActiveOnThisFloor = false;

    [Header("Angel Avoidance")]
    public float separationRadius = 2.0f;
    public float separationStrength = 1.5f;

    [Tooltip("How strongly Angels avoid each other while chasing.")]
    public float chaseSeparationStrength = 2.5f;

    [Tooltip("Agent radius. Increase this if Angels still overlap.")]
    public float agentRadius = 0.45f;

    // Last position where the Angel saw the player
    private Vector3 lastSeenPosition;

    // Search countdown
    private float searchTimer;

    // Patrol countdown
    private float patrolWaitTimer;

    private Vector3 patrolTarget;

    private enum State
    {
        Patrol,
        Chase,
        Search
    }

    private State currentState = State.Patrol;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent == null)
        {
            Debug.LogError("FallenAngelAI needs a NavMeshAgent!");
            enabled = false;
            return;
        }

        // -----------------------------------------------------
        // NAVMESH AGENT SETTINGS
        // -----------------------------------------------------

        agent.speed = patrolSpeed;

        // Give the Angel some physical space
        agent.radius = agentRadius;

        // This helps agents avoid each other
        agent.obstacleAvoidanceType =
            ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        // Each Angel gets a slightly different priority.
        // This prevents every Angel from making exactly
        // the same avoidance decision.
        agent.avoidancePriority = Random.Range(20, 80);

        // Automatically recalculate paths when necessary
        agent.autoRepath = true;

        // -----------------------------------------------------
        // START STATE
        // -----------------------------------------------------

        if (isActiveOnThisFloor)
        {
            SetNewPatrolPoint();
        }
        else
        {
            agent.isStopped = true;
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // =====================================================
        // FLOOR CONTROL
        // =====================================================

        if (!isActiveOnThisFloor)
        {
            agent.isStopped = true;
            return;
        }

        if (player == null)
        {
            agent.isStopped = true;
            return;
        }


        // =====================================================
        // CHECK VISION
        // =====================================================

        bool playerIsLookingAtAngel = IsPlayerLookingAtTurkey();

        bool angelCanSeePlayer = CanSeePlayer();


        // =====================================================
        // CHASE
        // =====================================================

        if (currentState == State.Chase)
        {
            // Angel freezes ONLY while:
            // 1. It is chasing
            // 2. Player is directly looking at it

            if (playerIsLookingAtAngel)
            {
                agent.isStopped = true;
            }
            else
            {
                agent.isStopped = false;
                agent.speed = chaseSpeed;

                if (angelCanSeePlayer)
                {
                    lastSeenPosition = player.position;

                    searchTimer = searchTime;

                    // Instead of simply:
                    // agent.SetDestination(player.position);
                    //
                    // create a slightly separated destination
                    // so multiple Angels don't all converge
                    // on exactly the same point.

                    Vector3 chasePosition =
                        GetSeparatedChasePosition();

                    agent.SetDestination(chasePosition);
                }
                else
                {
                    currentState = State.Search;

                    agent.SetDestination(lastSeenPosition);
                }
            }
        }


        // =====================================================
        // PATROL
        // =====================================================

        else if (currentState == State.Patrol)
        {
            // Looking at Angel during patrol does NOT stop it.

            if (angelCanSeePlayer)
            {
                currentState = State.Chase;

                lastSeenPosition = player.position;
                searchTimer = searchTime;

                agent.isStopped = false;
                agent.speed = chaseSpeed;

                agent.SetDestination(
                    GetSeparatedChasePosition()
                );

                Debug.Log(
                    "Fallen Angel spotted the player!"
                );
            }
            else
            {
                Patrol();
            }
        }


        // =====================================================
        // SEARCH
        // =====================================================

        else if (currentState == State.Search)
        {
            if (angelCanSeePlayer)
            {
                currentState = State.Chase;

                lastSeenPosition = player.position;
                searchTimer = searchTime;

                agent.isStopped = false;
                agent.speed = chaseSpeed;

                agent.SetDestination(
                    GetSeparatedChasePosition()
                );

                Debug.Log(
                    "Fallen Angel found the player again!"
                );
            }
            else
            {
                SearchForPlayer();
            }
        }


        // =====================================================
        // SEPARATION
        // =====================================================

        // Apply additional separation between Angels.
        ApplyAngelSeparation();


        // =====================================================
        // ATTACK
        // =====================================================

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance <= attackDistance)
        {
            AttackPlayer();
        }
    }


    // =========================================================
    // PATROL
    // =========================================================

    void Patrol()
    {
        agent.isStopped = false;
        agent.speed = patrolSpeed;

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance)
        {
            patrolWaitTimer -= Time.deltaTime;

            if (patrolWaitTimer <= 0f)
            {
                SetNewPatrolPoint();
            }
        }
    }


    // =========================================================
    // CHOOSE NEW PATROL POINT
    // =========================================================

    void SetNewPatrolPoint()
    {
        Vector3 randomDirection =
            Random.insideUnitSphere * patrolRadius;

        randomDirection += transform.position;

        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            randomDirection,
            out hit,
            patrolRadius,
            NavMesh.AllAreas))
        {
            patrolTarget = hit.position;

            agent.isStopped = false;
            agent.speed = patrolSpeed;

            agent.SetDestination(patrolTarget);

            patrolWaitTimer = patrolWaitTime;

            Debug.Log(
                "Fallen Angel patrol target: "
                + patrolTarget
            );
        }
        else
        {
            patrolWaitTimer = 1f;
        }
    }


    // =========================================================
    // SEARCH LAST SEEN POSITION
    // =========================================================

    void SearchForPlayer()
    {
        agent.isStopped = false;
        agent.speed = patrolSpeed;

        agent.SetDestination(lastSeenPosition);

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance)
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer <= 0f)
            {
                currentState = State.Patrol;

                SetNewPatrolPoint();

                Debug.Log(
                    "Fallen Angel couldn't find player. " +
                    "Returning to patrol."
                );
            }
        }
    }


    // =========================================================
    // GET SEPARATED CHASE POSITION
    // =========================================================

    Vector3 GetSeparatedChasePosition()
    {
        if (player == null)
            return transform.position;

        Vector3 targetPosition = player.position;

        Vector3 separation = Vector3.zero;

        // Find nearby Angels
        Collider[] nearbyObjects = Physics.OverlapSphere(
            transform.position,
            separationRadius
        );

        int angelCount = 0;

        foreach (Collider other in nearbyObjects)
        {
            if (other.gameObject == gameObject)
                continue;

            FallenAngelAI otherAngel =
                other.GetComponentInParent<FallenAngelAI>();

            if (otherAngel == null)
                continue;

            if (!otherAngel.isActiveOnThisFloor)
                continue;

            Vector3 awayFromOther =
                transform.position - otherAngel.transform.position;

            float distance = awayFromOther.magnitude;

            if (distance < 0.01f)
                continue;

            float strength =
                1f - Mathf.Clamp01(
                    distance / separationRadius
                );

            separation +=
                awayFromOther.normalized * strength;

            angelCount++;
        }

        if (angelCount > 0)
        {
            separation /= angelCount;

            targetPosition +=
                separation * chaseSeparationStrength;
        }

        // Make sure the final position is on NavMesh
        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            targetPosition,
            out hit,
            2f,
            NavMesh.AllAreas))
        {
            return hit.position;
        }

        return targetPosition;
    }


    // =========================================================
    // ANGEL SEPARATION
    // =========================================================

    void ApplyAngelSeparation()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(
            transform.position,
            separationRadius
        );

        Vector3 separation = Vector3.zero;

        int count = 0;

        foreach (Collider other in nearbyObjects)
        {
            if (other.gameObject == gameObject)
                continue;

            FallenAngelAI otherAngel =
                other.GetComponentInParent<FallenAngelAI>();

            if (otherAngel == null)
                continue;

            if (!otherAngel.isActiveOnThisFloor)
                continue;

            Vector3 away =
                transform.position -
                otherAngel.transform.position;

            float distance = away.magnitude;

            if (distance < 0.01f)
                continue;

            // Stronger separation when Angels are closer
            float strength =
                1f - Mathf.Clamp01(
                    distance / separationRadius
                );

            separation +=
                away.normalized * strength;

            count++;
        }

        if (count == 0)
            return;

        separation /= count;

        // Use a different variable name here
        // to avoid the C# scope conflict.
        float separationForce = separationStrength;

        if (currentState == State.Chase)
        {
            separationForce = chaseSeparationStrength;
        }

        // Only modify destination while moving
        if (!agent.isStopped &&
            agent.hasPath)
        {
            Vector3 newDestination =
                agent.destination +
                separation * separationForce * Time.deltaTime;

            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                newDestination,
                out hit,
                1.5f,
                NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }


    // =========================================================
    // CAN FALLEN ANGEL SEE PLAYER?
    // =========================================================

    bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 directionToPlayer =
            player.position - transform.position;

        float distance =
            directionToPlayer.magnitude;

        // Distance check
        if (distance > detectionRange)
        {
            return false;
        }

        // FOV check
        float angle = Vector3.Angle(
            transform.forward,
            directionToPlayer
        );

        if (angle > fieldOfView / 2f)
        {
            return false;
        }

        // Wall check
        Vector3 eyePosition =
            transform.position +
            Vector3.up * 1.5f;

        RaycastHit hit;

        if (Physics.Raycast(
            eyePosition,
            directionToPlayer.normalized,
            out hit,
            detectionRange))
        {
            if (hit.transform.CompareTag("Player"))
            {
                return true;
            }

            return false;
        }

        return false;
    }


    // =========================================================
    // PLAYER LOOKING AT ANGEL
    // =========================================================

    bool IsPlayerLookingAtTurkey()
    {
        if (playerCamera == null)
            return false;

        Vector3 directionToTurkey =
            (
                transform.position -
                playerCamera.transform.position
            ).normalized;

        float dotProduct = Vector3.Dot(
            playerCamera.transform.forward,
            directionToTurkey
        );

        if (dotProduct < lookThreshold)
        {
            return false;
        }

        RaycastHit hit;

        if (Physics.Raycast(
            playerCamera.transform.position,
            directionToTurkey,
            out hit,
            100f))
        {
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }


    // =========================================================
    // ATTACK
    // =========================================================

    void AttackPlayer()
    {
        Debug.Log(
            "The Fallen Angel caught the player!"
        );

        // Add HP damage / jumpscare here
    }


    // =========================================================
    // DEBUG VISION
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        // Separation range
        Gizmos.DrawWireSphere(
            transform.position,
            separationRadius
        );

        // Forward direction
        Gizmos.DrawRay(
            transform.position,
            transform.forward * detectionRange
        );
    }
}