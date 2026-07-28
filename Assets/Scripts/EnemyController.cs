using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    private enum EnemyState
    {
        Patrolling,
        Windup,
        Chasing,
        Searching,
        Confused,
        Stunned
    }

    // Atributos personalizables
    [SerializeField]
    private float chaseMultiplier;
    [SerializeField]
    private float fieldOfView;
    [SerializeField]
    private float viewDistance;
    [SerializeField]
    private Transform[] points;

    // Imagen estática mostrada a pantalla completa cuando este enemigo mata al jugador
    [SerializeField]
    private Texture jumpscareImage;

    [SerializeField]
    private float patrolPauseMinDuration = 0.5f; // Espera al llegar a un punto antes de ir al siguiente
    [SerializeField]
    private float patrolPauseMaxDuration = 5f;

    // Si el jugador está dentro de este rango, el enemigo nunca lo pierde de vista
    // (evita falsos negativos del raycast/FOV cuando el jugador está pegado al enemigo)
    [SerializeField]
    private float closeRangeDistance = 15f;

    // Windup: gira hacia el jugador antes de empezar la persecución
    [SerializeField]
    private float windupDuration = 0.5f;
    [SerializeField]
    private float windupTurnSpeed = 720f; // grados por segundo

    // Searching: intenta predecir hacia donde se fue el jugador
    [SerializeField]
    private float searchMinDuration = 2.5f;
    [SerializeField]
    private float searchMaxDuration = 4f;
    [SerializeField]
    private float searchProjectionDistance = 30f;

    // Confused: mira en direcciones aleatorias buscando al jugador
    [SerializeField]
    private float confusedDuration = 2.8f;
    [SerializeField]
    private float confusedSnapInterval = 0.4f;

    // Stunned: aturdido tras ser parriado por el jugado r
    [SerializeField]
    private float stunDuration = 5f;
    [SerializeField]
    private float stunKnockbackDistance = 5f;
    [SerializeField]
    private float stunKnockbackDuration = 0.3f; // Duración del empujón suave (no es un teletransporte)
    [SerializeField]
    private float stunColorTransitionDuration = 0.5f; // Duración del fundido de luz al entrar/salir del aturdimiento
    [SerializeField]
    [Range(0f, 1f)]
    private float stunSearchResetChance = 0.1f; // Probabilidad de reiniciar la persecución al patrullaje en vez de buscar

    // Atributos de control
    private GameObject player;
    private PlayerHandler playerHandler;
    private NavMeshAgent enemyAgent;
    private EnemyState state;
    private float stateTimer;
    private float confusedSnapTimer;
    private float baseSpeed;
    private Vector3 lastKnownPlayerPosition;
    private Vector3 playerMoveDirection;
    private float stateDurationTotal;
    private Coroutine knockbackRoutine;

    // Atributos de patrullaje
    private int destPoint;
    private int repeatCount;
    private bool isPatrolPaused;
    private float patrolPauseTimer;

    private Light EnemyLight;
    private Color InitialColor = new Color(48f / 255f, 165f / 255f, 215f / 255f); // Color de luz cuando está patrullando
    private Color EngageColor = Color.red;
    private Color SearchColor = Color.gray; // Color al que se apaga la luz durante Searching
    private Color ConfusedColor = new Color(48f / 255f, 165f / 255f, 215f / 255f); // Color al que vira la luz durante Confused
    private Color windupStartColor; // Color de luz al entrar en Windup (varía según de dónde venga)
    private Color stunStartColor; // Color de luz al entrar en Stunned (varía según de dónde venga)

    // Estado público (consultado por otros scripts, ej. HideOut)
    public bool InChase
    {
        get { return state != EnemyState.Patrolling; }
    }

    public bool IsStunned
    {
        get { return state == EnemyState.Stunned; }
    }

    // Consultado por PlayerHandler al morir, para saber qué jumpscare mostrar
    public Texture JumpscareImage
    {
        get { return jumpscareImage; }
    }

    //private CapsuleCollider collider;

    void Awake()
    {
        // Valores predeterminados
        destPoint = 0;
        repeatCount = 0;
        state = EnemyState.Patrolling;

        // Referencia al jugador
        player = GameObject.FindGameObjectWithTag("Player");
        playerHandler = player.GetComponent<PlayerHandler>();

        // Referencia al agente de IA
        enemyAgent = GetComponent<NavMeshAgent>();

        // Disabling auto-braking allows for continuous movement
        // between points (i.e. the agent doesn't slow down as it
        // approaches a destination point).
        enemyAgent.autoBraking = false;
        enemyAgent.angularSpeed = 1000f;
        enemyAgent.speed = 5.33f; //Accounts for acceleration
        enemyAgent.acceleration = 100f;

        //collider = GetComponent<CapsuleCollider>();

        // Set values

        viewDistance = 11f;
        fieldOfView = 75.0f;
        chaseMultiplier = 2.25f;

        baseSpeed = enemyAgent.speed;

        EnemyLight = GetComponent<Light>();

        GotoNextPoint();
    }

    void Update()
    {
        switch (state)
        {
            case EnemyState.Patrolling:
                UpdatePatrolling();
                break;
            case EnemyState.Windup:
                UpdateWindup();
                break;
            case EnemyState.Chasing:
                UpdateChasing();
                break;
            case EnemyState.Searching:
                UpdateSearching();
                break;
            case EnemyState.Confused:
                UpdateConfused();
                break;
            case EnemyState.Stunned:
                UpdateStunned();
                break;
        }
    }

    // Revisa si el jugador esta a la vista, dentro de la distancia y angulo dados
    bool CanSeePlayer(float checkDistance, float checkAngle, out Vector3 seenPosition)
    {
        Vector3 directionToPlayer = player.transform.position - transform.position;
        Vector3 rayDir = directionToPlayer.normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        float sneakMultiplier = playerHandler.SneakFOVMultiplier;
        float effectiveDistance = checkDistance * sneakMultiplier;
        float effectiveAngle = checkAngle * sneakMultiplier;

        // DEBUG: Revisa la trayectoria del ray.
        Debug.DrawRay(transform.position, rayDir * effectiveDistance, Color.red);

        seenPosition = player.transform.position;

        return Physics.Raycast(transform.position, rayDir, out RaycastHit hit, effectiveDistance) // Revisa si hay una colisión con un collider
            && hit.collider.gameObject.CompareTag("Player") // Revisa que el collider del gameObject sea de el jugador
            && angle <= effectiveAngle; // Revisa que esta en el rango de visión
    }

    // Revisa si el jugador está a la vista, o si está tan cerca que de todas
    // formas no debería perderse (evita falsos negativos del raycast/FOV
    // cuando el jugador se solapa con el collider del enemigo). El rango
    // cercano ignora el ángulo de visión, pero sigue exigiendo línea de vista
    // real: nunca debe "ver" al jugador a través de una pared.
    bool CanSeePlayerWhileEngaged(float checkDistance, float checkAngle, out Vector3 seenPosition)
    {
        if (CanSeePlayer(checkDistance, checkAngle, out seenPosition))
        {
            return true;
        }

        Vector3 directionToPlayer = seenPosition - transform.position;
        if (directionToPlayer.sqrMagnitude > closeRangeDistance * closeRangeDistance)
        {
            return false;
        }

        return Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, closeRangeDistance)
            && hit.collider.gameObject.CompareTag("Player");
    }

    void UpdatePatrolling()
    {
        EnemyLight.color = InitialColor;

        if (CanSeePlayer(viewDistance, fieldOfView, out _))
        {
            EnterWindup();
            return;
        }

        Patrol();
    }

    void EnterWindup()
    {
        state = EnemyState.Windup;
        stateTimer = windupDuration;
        windupStartColor = EnemyLight.color;

        enemyAgent.isStopped = true;
        enemyAgent.velocity = Vector3.zero;
        enemyAgent.updateRotation = false;
    }

    void UpdateWindup()
    {
        // La luz vira gradualmente hacia rojo, partiendo del color que tuviera al entrar
        float colorProgress = windupDuration > 0f ? Mathf.Clamp01(1f - stateTimer / windupDuration) : 1f;
        EnemyLight.color = Color.Lerp(windupStartColor, EngageColor, colorProgress);

        // Gira hacia el jugador
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, windupTurnSpeed * Time.deltaTime);
        }

        // Si el jugador sale de la vista antes de terminar el windup, pasa a Confused
        // directamente (con la mitad de duración, ya que apenas lo perdió de vista)
        if (!CanSeePlayerWhileEngaged(viewDistance, fieldOfView, out _))
        {
            EnterConfused(confusedDuration * 0.5f);
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            EnterChasing();
        }
    }

    void EnterChasing()
    {
        state = EnemyState.Chasing;

        enemyAgent.isStopped = false;
        enemyAgent.updateRotation = true;
        enemyAgent.speed = baseSpeed * chaseMultiplier; // Aumenta velocidad en persecución

        lastKnownPlayerPosition = player.transform.position;
        playerMoveDirection = transform.forward;
    }

    void UpdateChasing()
    {
        EnemyLight.color = EngageColor;

        float alertViewDistance = viewDistance * chaseMultiplier; // Aumenta la distancia de detección

        if (CanSeePlayerWhileEngaged(alertViewDistance, fieldOfView, out Vector3 seenPosition))
        {
            UpdatePlayerMoveDirection(seenPosition);
            FollowPlayer();
        }
        else
        {
            EnterSearching();
        }
    }

    void UpdatePlayerMoveDirection(Vector3 seenPosition)
    {
        Vector3 delta = seenPosition - lastKnownPlayerPosition;
        delta.y = 0f;
        if (delta.sqrMagnitude > 0.0001f)
        {
            playerMoveDirection = delta.normalized;
        }
        lastKnownPlayerPosition = seenPosition;
    }

    void EnterSearching()
    {
        EnterSearching(lastKnownPlayerPosition + playerMoveDirection * searchProjectionDistance);
    }

    void EnterSearching(Vector3 destination)
    {
        state = EnemyState.Searching;
        stateTimer = Random.Range(searchMinDuration, searchMaxDuration);
        stateDurationTotal = stateTimer;

        enemyAgent.isStopped = false;
        enemyAgent.updateRotation = true;
        enemyAgent.SetDestination(destination);
    }

    void UpdateSearching()
    {
        float alertViewDistance = viewDistance * chaseMultiplier;

        if (CanSeePlayerWhileEngaged(alertViewDistance, fieldOfView, out Vector3 seenPosition))
        {
            EnemyLight.color = EngageColor;
            EnterChasing();
            lastKnownPlayerPosition = seenPosition;
            return;
        }

        // La luz se apaga lentamente hacia gris a medida que se pierde la esperanza de encontrar al jugador
        float colorProgress = stateDurationTotal > 0f ? Mathf.Clamp01(1f - stateTimer / stateDurationTotal) : 1f;
        EnemyLight.color = Color.Lerp(EngageColor, SearchColor, colorProgress);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            EnterConfused();
        }
    }

    void EnterConfused()
    {
        EnterConfused(confusedDuration);
    }

    void EnterConfused(float duration)
    {
        state = EnemyState.Confused;
        stateTimer = duration;
        stateDurationTotal = stateTimer;
        confusedSnapTimer = 0f;

        enemyAgent.isStopped = true;
        enemyAgent.velocity = Vector3.zero;
        enemyAgent.updateRotation = false;
    }

    void UpdateConfused()
    {
        
        float alertViewDistance = viewDistance * chaseMultiplier;

        confusedSnapTimer -= Time.deltaTime;
        if (confusedSnapTimer <= 0f)
        {
            // Gira (casi instantáneamente) hacia una dirección aleatoria buscando al jugador
            float randomYaw = Random.Range(0f, 360f);
            transform.rotation = Quaternion.Euler(0f, randomYaw, 0f);
            
            confusedSnapTimer = Random.Range(confusedSnapInterval,1.2f);
        }

        if (CanSeePlayerWhileEngaged(alertViewDistance, fieldOfView, out Vector3 seenPosition))
        {
            // El windup se encarga de virar la luz rápidamente a rojo
            lastKnownPlayerPosition = seenPosition;
            EnterWindup();
            return;
        }

        // La luz pasa de gris a azul mientras el enemigo sigue confundido
        float colorProgress = stateDurationTotal > 0f ? Mathf.Clamp01(1f - stateTimer / stateDurationTotal) : 1f;
        EnemyLight.color = Color.Lerp(SearchColor, ConfusedColor, colorProgress);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            EnterPatrol();
        }
    }

    void EnterPatrol()
    {
        state = EnemyState.Patrolling;
        isPatrolPaused = false;

        enemyAgent.isStopped = false;
        enemyAgent.updateRotation = true;
        enemyAgent.speed = baseSpeed;

        EnemyLight.color = InitialColor;

        GotoNextPoint();
    }

    // Llamado por PlayerHandler cuando el jugador parry-ea al enemigo: lo aleja
    // y lo deja completamente inmóvil unos segundos antes de retomar la búsqueda.
    public void Parry(Vector3 knockbackDirection)
    {
        state = EnemyState.Stunned;
        stateTimer = stunDuration;
        stunStartColor = EnemyLight.color;

        enemyAgent.isStopped = true;
        enemyAgent.velocity = Vector3.zero;
        enemyAgent.updateRotation = false;

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }

        knockbackDirection.y = 0f;
        if (knockbackDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 knockbackTarget = transform.position + knockbackDirection.normalized * stunKnockbackDistance;
            if (NavMesh.SamplePosition(knockbackTarget, out NavMeshHit hit, stunKnockbackDistance, NavMesh.AllAreas))
            {
                knockbackRoutine = StartCoroutine(KnockbackRoutine(hit.position));
            }
        }
    }

    // Empuja al enemigo suavemente hacia targetPosition (sin teletransportarlo),
    // desacelerando hacia el final para que se sienta como un empujón.
    IEnumerator KnockbackRoutine(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < stunKnockbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / stunKnockbackDuration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out cuadrático

            Vector3 desiredPosition = Vector3.Lerp(startPosition, targetPosition, eased);
            enemyAgent.Move(desiredPosition - transform.position);

            yield return null;
        }

        knockbackRoutine = null;
    }

    void UpdateStunned()
    {
        // La luz funde a negro al entrar y funde de vuelta a rojo al salir,
        // sin solaparse aunque el aturdimiento dure poco.
        float transitionDuration = Mathf.Min(stunColorTransitionDuration, stunDuration * 0.5f);
        float elapsed = stunDuration - stateTimer;

        if (transitionDuration <= 0f)
        {
            EnemyLight.color = Color.black;
        }
        else if (elapsed < transitionDuration)
        {
            EnemyLight.color = Color.Lerp(stunStartColor, Color.black, elapsed / transitionDuration);
        }
        else if (stateTimer < transitionDuration)
        {
            EnemyLight.color = Color.Lerp(Color.black, EngageColor, 1f - stateTimer / transitionDuration);
        }
        else
        {
            EnemyLight.color = Color.black;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            if (Random.value < stunSearchResetChance)
            {
                EnterPatrol();
            }
            else
            {
                lastKnownPlayerPosition = player.transform.position;
                EnterSearching();
            }
        }
    }

    // Llamado por HideOut cuando el jugador se esconde estando lo bastante lejos
    // del enemigo: este pierde el rastro y va a revisar el último punto visto.
    public void LosePlayerAt(Vector3 lastSeenPosition)
    {
        lastKnownPlayerPosition = lastSeenPosition;
        EnterSearching(lastSeenPosition);
    }

    // Llamado por HideOut cuando el jugador se esconde estando demasiado cerca
    // del enemigo: este alcanza a notar hacia dónde se metió y va directo ahí.
    public void InvestigateHideout(Vector3 hideoutPosition)
    {
        lastKnownPlayerPosition = hideoutPosition;
        EnterSearching(hideoutPosition);
    }

    void FollowPlayer()
    {
        //Vector3 dir = (player.transform.position - transform.position).normalized;
        //enemyAgent.velocity = dir * enemyAgent.speed;
        enemyAgent.SetDestination(player.transform.position);
    }

    void GotoNextPoint()
    {
        if (points.Length == 0)
            return;

        enemyAgent.SetDestination(points[destPoint].position);

        int newDestPoint = destPoint;

        if (points.Length > 1)
        {
            // Si el mismo punto ya se repitió 1 vez,
            // se prohíbe elegirlo de nuevo (forzamos un punto distinto).
            bool forceDifferent = repeatCount == 1;

            do
            {
                newDestPoint = Random.Range(0, points.Length);
            }
            while (forceDifferent && newDestPoint == destPoint);
        }

        // Actualiza el contador de repeticiones consecutivas
        if (newDestPoint == destPoint)
        {
            repeatCount++;
        }
        else
        {
            repeatCount = 0;
        }
        destPoint = newDestPoint;
    }

    void Patrol()
    {
        // Ya llegó a un punto y está esperando antes de ir al siguiente
        if (isPatrolPaused)
        {
            patrolPauseTimer -= Time.deltaTime;
            if (patrolPauseTimer <= 0f)
            {
                isPatrolPaused = false;
                enemyAgent.isStopped = false;
                GotoNextPoint();
            }
            return;
        }

        // Choose the next destination point when the agent gets
        // close to the current one.
        if (!enemyAgent.pathPending && enemyAgent.remainingDistance < 0.5f)
        {
            isPatrolPaused = true;
            patrolPauseTimer = Random.Range(patrolPauseMinDuration, patrolPauseMaxDuration);
            enemyAgent.isStopped = true;
            enemyAgent.velocity = Vector3.zero;
        }
    }
}
