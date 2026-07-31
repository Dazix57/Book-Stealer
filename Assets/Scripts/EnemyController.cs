using System.Collections;
using System.Collections.Generic;
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
        Stunned,
        ForceApproach
    }

    // Atributos personalizables
    [SerializeField]
    private float chaseMultiplier;
    [SerializeField]
    private float fieldOfView;
    [SerializeField]
    private float viewDistance;

    // Subconjunto de PatrolWaypoint de la escena que le toca patrullar a este enemigo,
    // calculado una vez en Awake() (ver AssignedWaypoints): ya no hace falta asignarle a mano
    // el recorrido a cada enemigo por separado, y cada uno cubre su propia zona sin superponerse.
    private Transform[] points;

    // Imagen estática mostrada a pantalla completa cuando este enemigo mata al jugador
    [SerializeField]
    private Texture jumpscareImage;

    // Opacidad máxima del jumpscare (0-1). Algunas imágenes (ej. fondos claros) se perciben
    // mucho más intensas/duraderas que otras a la misma opacidad, aunque el fundido dure lo
    // mismo para todos los enemigos (ver ChaseJumpscareManager) -- este valor permite atenuar
    // esas imágenes puntualmente sin tocar el arte.
    [SerializeField]
    [Range(0f, 1f)]
    private float jumpscareMaxAlpha = 1f;

    [SerializeField]
    private float patrolPauseMinDuration = 0.5f; // Espera al llegar a un punto antes de ir al siguiente
    [SerializeField]
    private float patrolPauseMaxDuration = 5f;

    // Si el jugador está dentro de este rango, el enemigo nunca lo pierde de vista
    // (evita falsos negativos del raycast/FOV cuando el jugador está pegado al enemigo)
    [SerializeField]
    private float closeRangeDistance = 32f;

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
    [Range(0f, 1f)]
    private float stunSearchResetChance = 0.1f; // Probabilidad de reiniciar la persecución al patrullaje en vez de buscar

    // Detección por oído: pasos del jugador dentro de este alcance (a volumen normal) pueden
    // hacer que el enemigo entre en Searching o Confused. Solo aplica mientras patrulla: el
    // property InChase ya cubre windup/persecución/búsqueda/confundido/aturdido, evitando que
    // el oído interrumpa un compromiso visual en curso o uno recién terminado.
    [SerializeField]
    private float hearingRange = 17.5f;
    [SerializeField]
    [Range(0f, 1f)]
    private float minSearchChanceOnHear = 0.3f; // probabilidad de Searching si el paso se oye justo en el borde del alcance
    [SerializeField]
    [Range(0f, 1f)]
    private float maxSearchChanceOnHear = 0.95f; // probabilidad de Searching si el paso ocurre justo encima del enemigo

    [Header("Sonidos")]
    [SerializeField]
    private AudioSource enemyAudioSource; // local a este GameObject; se crea sola si no se asigna
    [SerializeField]
    private float audioMinDistance = 5f;
    [SerializeField]
    private float audioMaxDistance = 40f;

    // Zumbido de proximidad: suena todo el tiempo (en cualquier estado), en loop, y es Unity
    // quien sube/baja su volumen según la distancia real al AudioListener (la cámara del jugador)
    // vía minDistance/maxDistance — no hace falta medir la distancia a mano cuadro a cuadro.
    // minDistance actúa como "piso": dentro de ese radio el volumen ya no sigue subiendo, para
    // que nunca se reviente por más cerca que el jugador se ponga.
    [Header("Sonido de proximidad")]
    [SerializeField]
    private AudioSource proximityAudioSource; // siempre se crea de cero: no debe compartirse con enemyAudioSource/idleaudioSource
    [SerializeField]
    private float proximityMinDistance = 3f;
    [SerializeField]
    private float proximityMaxDistance = 18f;

    // Sonido de ambiente mientras patrulla, a intervalos aleatorios entre estos dos valores
    [SerializeField]
    private float idleSoundMinInterval = 15f;
    [SerializeField]
    private float idleSoundMaxInterval = 45f;
    private float idleSoundTimer;
    [SerializeField] private AudioSource idleaudioSource;
    [SerializeField] private AudioClipName idleClip;
    // Una de estas tres se elige al azar cada vez que el enemigo nota al jugador (ver EnterWindup)
    private static readonly AudioClipName[] reactSounds =
    {
        AudioClipName.React1,
        AudioClipName.React2,
        AudioClipName.React3,
    };

    // Escalada por parry: cada vez que este enemigo es parriado (ver Parry() y parryCount más
    // abajo) se vuelve más agresivo. Todos estos incrementos son acumulativos (parryCount veces).
    [Header("Escalada por parry")]
    [SerializeField]
    private float searchChanceOnLoseSightBase = 0.5f; // probabilidad base de Searching (vs Confused) al perder de vista en Chasing
    [SerializeField]
    private float searchChanceIncreasePerParry = 0.1f; // se suma a esa base, y también a la de la detección por oído, por cada parry
    [SerializeField]
    private float searchDurationMultiplierPerParry = 0.5f; // +50% de duración de Searching por parry
    [SerializeField]
    private float confusedDurationMultiplierPerParry = 0.5f; // -50% de duración de Confused por parry (con piso en 0)
    [SerializeField]
    private float rangeIncreasePerParry = 0.25f; // +25% al closeRangeDistance y al hearingRange, por parry
    private int parryCount = 0;

    // Consultado por HideOut para escalar su propio rango de extracción forzada por parry
    public int ParryCount => parryCount;

    // Aumenta con cada parry recibido; usado en CanSeePlayerWhileEngaged
    float EffectiveCloseRangeDistance => closeRangeDistance * (1f + rangeIncreasePerParry * parryCount);

    [SerializeField]
    private float baseSpeedIncreasePerParry = 0.05f; // +5% de velocidad base por parry (patrullaje Y persecución, ya que esta última se deriva de la base)

    // Velocidad base ya escalada por parries; se usa en vez de baseSpeed en todos lados
    float EffectiveBaseSpeed => baseSpeed * (1f + baseSpeedIncreasePerParry * parryCount);
    protected virtual float VolumeScale => 1f;

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
    private bool chaseTrackingActive; // evita registrar/desregistrar la persecución más de una vez (AudioManager + ChaseStarted/Ended)

    // Compartido entre todas las instancias: cuenta cuántos enemigos están persiguiendo
    // activamente (Chasing o ForceApproach) a la vez, para saber cuándo el jugador
    // deja de estar perseguido por completo (ver StartChaseTracking/StopChaseAudio).
    private static int globalChaseCount;
    public static event System.Action ChaseStarted;
    public static event System.Action ChaseEnded;

    // Disparado junto con ChaseStarted (mismo gate de globalChaseCount == 1), pero lleva la imagen
    // del enemigo que inició la persecución (y su opacidad máxima), para el jumpscare rápido que
    // la muestra en pantalla.
    public static event System.Action<Texture, float> ChaseJumpscare;

    // Llamado por InitializeGameplayAudio al cargar la escena de gameplay: globalChaseCount
    // es estático, así que sin esto podría arrastrar un valor viejo de la escena anterior.
    public static void ResetChaseState()
    {
        globalChaseCount = 0;
    }

    // ForceApproach: usado por HideOut cuando el jugador se esconde demasiado cerca del enemigo.
    // El destino (ej. el interior de un mueble) suele estar fuera del NavMesh -- el agente solo
    // puede acercarse hasta el punto transitable más próximo, así que la llegada se mide por
    // distancia física real a forceApproachTarget, no por NavMeshAgent.remainingDistance (que mide
    // contra el destino ya recortado al NavMesh y puede no bajar nunca de un umbral chico).
    [SerializeField]
    private float forceApproachArrivalDistance = 2.5f; // qué tan cerca del destino cuenta como "llegó"
    private Vector3 forceApproachTarget;
    private System.Action onForceApproachArrived;

    // Atributos de patrullaje
    private int destPoint;
    private int repeatCount;
    private bool isPatrolPaused;
    private float patrolPauseTimer;

    // Salvavidas anti-atasco: si el punto asignado queda inalcanzable (ej. en otro piso, sin
    // conexión de NavMesh hasta ahí), el agente nunca baja de remainingDistance y se queda
    // "caminando" para siempre sin llegar a ningún lado. Pasado este tiempo sin progreso real,
    // se fuerza a elegir otro punto en vez de quedar congelado.
    private const float patrolStuckTimeout = 6f;
    private float patrolStuckTimer;
    private float lastRemainingDistance;

    // El sprite (forward/backward) y su silueta de resalte mientras el jugador está agachado
    // viven en EnemyFacingSprite, que también es quien maneja el halo emissive de niebla por
    // distancia (ver EnemyFacingSprite.UpdateGlow), reemplazando a la luz real que tenía antes
    // (delataba al enemigo desde lejos al iluminar el entorno a su alrededor).
    private EnemyFacingSprite facingSprite;

    // Estado público (consultado por otros scripts, ej. HideOut)
    public bool InChase
    {
        get { return state != EnemyState.Patrolling; }
    }

    public bool IsStunned
    {
        get { return state == EnemyState.Stunned; }
    }

    // Consultado por HideOut: a diferencia de InChase, no incluye Windup/Searching/Confused/Stunned,
    // solo la persecución activa propiamente dicha.
    public bool IsChasing
    {
        get { return state == EnemyState.Chasing; }
    }

    //private CapsuleCollider collider;

    void OnEnable()
    {
        PlayerFootsteps.FootstepHeard += OnPlayerFootstepHeard;
    }

    void OnDisable()
    {
        PlayerFootsteps.FootstepHeard -= OnPlayerFootstepHeard;
    }

    // Detección por oído: puede hacer que el enemigo entre en Searching (hacia la posición del
    // paso) o en Confused, con más probabilidad de Searching cuanto más cerca se oyó el paso.
    // Si el paso sonó mientras el jugador sprintaba, entra en Searching directo, sin roll.
    // Se ignora por completo mientras el enemigo esté en cualquier estado que no sea Patrolling
    // (la detección visual manda: no debe interrumpir una persecución en curso ni reactivar
    // Searching/Confused justo después de haber perdido al jugador por vista).
    void OnPlayerFootstepHeard(Vector3 position, float loudness, bool wasSprinting)
    {
        if (InChase) return;

        float effectiveHearingRange = hearingRange * (1f + rangeIncreasePerParry * parryCount);
        float effectiveRange = effectiveHearingRange * loudness;
        float distance = Vector3.Distance(transform.position, position);
        if (distance > effectiveRange) return;

        bool goesToSearch = wasSprinting;
        if (!goesToSearch)
        {
            float proximity = effectiveRange > 0f ? 1f - Mathf.Clamp01(distance / effectiveRange) : 1f;
            float searchChance = Mathf.Lerp(minSearchChanceOnHear, maxSearchChanceOnHear, proximity);
            searchChance = Mathf.Clamp01(searchChance + searchChanceIncreasePerParry * parryCount);
            goesToSearch = Random.value < searchChance;
        }

        if (goesToSearch)
        {
            lastKnownPlayerPosition = position;
            EnterSearching(position);
        }
        else
        {
            EnterConfused();
        }
    }

    void Awake()
    {
        // Valores predeterminados
        destPoint = 0;
        repeatCount = 0;
        state = EnemyState.Patrolling;

        // Recorrido de patrullaje: solo los PatrolWaypoint más cercanos a este enemigo que a
        // cualquier otro (ver AssignedWaypoints), para que cada uno cubra su propia zona.
        points = AssignedWaypoints();

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

        // El Rigidbody nunca quedó protegido de la física (a diferencia del jugador, ver
        // PlayerHandler.Awake): sin esto, el contacto con el NavMesh o con el jugador puede
        // aplicarle un torque mínimo que hace vibrar transform.rotation. Toda la rotación real
        // ya la maneja el NavMeshAgent (updateRotation) o asignaciones directas a transform.
        // rotation (Windup/Confused), nunca física — así que congelarla del todo es seguro y
        // elimina esa fuente de ruido angular (que hacía parpadear el sprite forward/backward).
        Rigidbody enemyRigidbody = GetComponent<Rigidbody>();
        if (enemyRigidbody != null)
        {
            enemyRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        //collider = GetComponent<CapsuleCollider>();

        // Set values

        viewDistance = 13f;
        fieldOfView = 75.0f;
        chaseMultiplier = 2.6f; // Alcance de detección en alerta (viewDistance*chaseMultiplier): más ancho para que cueste más perderlo de vista una vez enganchado

        baseSpeed = enemyAgent.speed;

        facingSprite = GetComponent<EnemyFacingSprite>();

        if (enemyAudioSource == null) enemyAudioSource = GetComponent<AudioSource>();
        if (enemyAudioSource == null) enemyAudioSource = gameObject.AddComponent<AudioSource>();
        enemyAudioSource.playOnAwake = false;
        enemyAudioSource.loop = false;
        enemyAudioSource.spatialBlend = 1f;
        enemyAudioSource.rolloffMode = AudioRolloffMode.Linear;
        enemyAudioSource.minDistance = audioMinDistance;
        enemyAudioSource.maxDistance = audioMaxDistance;

        idleSoundTimer = Random.Range(idleSoundMinInterval, idleSoundMaxInterval);

        if (idleaudioSource == null)
        {
            idleaudioSource = GetComponent<AudioSource>();
        }
        if (idleaudioSource == null)
        {
            idleaudioSource = gameObject.AddComponent<AudioSource>();
        }

        idleaudioSource.playOnAwake = false;
        idleaudioSource.loop = false;
        idleaudioSource.spatialBlend = 1f; // 3D: el sonido se percibe local a este GameObject, con caída por distancia
        idleaudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        idleaudioSource.minDistance = 1f;
        idleaudioSource.maxDistance = 35f;

        // Siempre se crea un AudioSource nuevo (nunca GetComponent primero): a diferencia de
        // enemyAudioSource/idleaudioSource, este necesita su propio minDistance/maxDistance/rolloff
        // tunados para el efecto de proximidad, y no debe terminar compartiendo el mismo AudioSource
        // que esos otros dos (los pisaría con su propia configuración, y viceversa).
        if (proximityAudioSource == null) proximityAudioSource = gameObject.AddComponent<AudioSource>();
        proximityAudioSource.clip = AudioManager.GetClip(AudioClipName.ProximitySound);
        proximityAudioSource.playOnAwake = false;
        proximityAudioSource.loop = true;
        proximityAudioSource.spatialBlend = 1f;
        proximityAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        proximityAudioSource.minDistance = proximityMinDistance;
        proximityAudioSource.maxDistance = proximityMaxDistance;
        proximityAudioSource.volume = AudioManager.GetMixedVolume(AudioClipName.ProximitySound, AudioChannel.Game);
        proximityAudioSource.Play();

        GotoNextPoint();
    }

    void PlaySound(AudioClipName clip)
    {
        enemyAudioSource.PlayOneShot(AudioManager.GetClip(clip), AudioManager.GetMixedVolume(clip, AudioChannel.Game, chaseBoost: InChase));
    }

    // Reparte todos los PatrolWaypoint de la escena entre los enemigos por cercanía: cada
    // waypoint le toca al enemigo más cercano a él. Como esto se recalcula igual (y de forma
    // determinística) en el Awake() de cada enemigo, todos llegan al mismo reparto sin
    // necesitar coordinarse, y ningún waypoint termina asignado a más de uno.
    Transform[] AssignedWaypoints()
    {
        
        GameObject[] waypoints = GameObject.FindGameObjectsWithTag("WayPoint");
        EnemyController[] allEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);

        List<Transform> assigned = new List<Transform>();
        foreach (GameObject waypoint in waypoints)
        {
            EnemyController closestEnemy = null;
            float closestDistance = float.PositiveInfinity;

            foreach (EnemyController candidate in allEnemies)
            {
                float distance = Vector3.Distance(candidate.transform.position, waypoint.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = candidate;
                }
            }

            if (closestEnemy == this)
            {
                assigned.Add(waypoint.transform);
            }
        }

        return assigned.ToArray();
    }

    // Mientras el jugador está agachado, le pide a EnemyFacingSprite que muestre la silueta
    // (visible a través de paredes) del sprite forward/backward que esté activo en ese momento.
    void UpdateHighlight()
    {
        facingSprite.SetHighlighted(playerHandler.Crouching);
    }

    void Update()
    {
        UpdateHighlight();

        // Vive fuera del switch: suena en cualquier estado, no solo en chase (ver Header
        // "Sonido de proximidad"). Solo se refresca el volumen "base"; la caída real por
        // distancia la aplica Unity solo (minDistance/maxDistance, ver Awake()).
        proximityAudioSource.volume = AudioManager.GetMixedVolume(AudioClipName.ProximitySound, AudioChannel.Game);

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
            case EnemyState.ForceApproach:
                UpdateForceApproach();
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

        float effectiveCloseRangeDistance = EffectiveCloseRangeDistance;

        Vector3 directionToPlayer = seenPosition - transform.position;
        if (directionToPlayer.sqrMagnitude > effectiveCloseRangeDistance * effectiveCloseRangeDistance)
        {
            return false;
        }

        return Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, effectiveCloseRangeDistance)
            && hit.collider.gameObject.CompareTag("Player");
    }

    void UpdatePatrolling()
    {
        if (CanSeePlayer(viewDistance, fieldOfView, out _))
        {
            EnterWindup();
            return;
        }

        UpdateIdleSound();
        Patrol();
    }

    // Sonido de ambiente a intervalos aleatorios mientras patrulla, para dar la sensación de
    // que el enemigo sigue ahí incluso lejos de la vista del jugador.
    void UpdateIdleSound()
    {
        idleSoundTimer -= Time.deltaTime;
        if (idleSoundTimer <= 0f)
        {
            float volume = AudioManager.GetMixedVolume(idleClip, AudioChannel.Game) * VolumeScale;
            idleaudioSource.PlayOneShot(AudioManager.GetClip(idleClip), volume);
            //PlaySound(AudioClipName.BS_Enemy1Idle);
            idleSoundTimer = Random.Range(idleSoundMinInterval, idleSoundMaxInterval);
        }
    }

    void EnterWindup()
    {
        state = EnemyState.Windup;
        stateTimer = windupDuration;

        enemyAgent.isStopped = true;
        enemyAgent.velocity = Vector3.zero;
        enemyAgent.updateRotation = false;

        PlaySound(reactSounds[Random.Range(0, reactSounds.Length)]);
    }

    void UpdateWindup()
    {
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
        enemyAgent.speed = EffectiveBaseSpeed * chaseMultiplier; // Aumenta velocidad en persecución

        lastKnownPlayerPosition = player.transform.position;
        playerMoveDirection = transform.forward;

        StartChaseTracking();
    }

    // Activa el override de BS_Chase / camera shake, si este enemigo no lo tenía ya activo
    // (se usa tanto al entrar en Chasing como en ForceApproach: para el jugador, ambos son
    // "me está persiguiendo activamente").
    void StartChaseTracking()
    {
        if (chaseTrackingActive) return;

        chaseTrackingActive = true;
        AudioManager.EnemyStartedChasing();

        globalChaseCount++;
        if (globalChaseCount == 1)
        {
            ChaseStarted?.Invoke();
            ChaseJumpscare?.Invoke(jumpscareImage, jumpscareMaxAlpha);
        }
    }

    void UpdateChasing()
    {
        float alertViewDistance = viewDistance * chaseMultiplier; // Aumenta la distancia de detección

        if (CanSeePlayerWhileEngaged(alertViewDistance, fieldOfView, out Vector3 seenPosition))
        {
            UpdatePlayerMoveDirection(seenPosition);
            FollowPlayer();
        }
        else
        {
            // Cuanto más lo hayan parriado, más probable que pase a Searching en vez de Confused
            float searchChance = Mathf.Clamp01(searchChanceOnLoseSightBase + searchChanceIncreasePerParry * parryCount);
            if (Random.value < searchChance)
            {
                EnterSearching();
            }
            else
            {
                EnterConfused();
            }
        }
    }

    // Detiene el override de BS_Chase en AudioManager, si este enemigo lo tenía activo.
    void StopChaseAudio()
    {
        if (!chaseTrackingActive) return;

        chaseTrackingActive = false;
        AudioManager.EnemyStoppedChasing();

        globalChaseCount--;
        if (globalChaseCount == 0)
        {
            ChaseEnded?.Invoke();
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
        float baseDuration = Random.Range(searchMinDuration, searchMaxDuration);
        stateTimer = baseDuration * (1f + searchDurationMultiplierPerParry * parryCount);
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
            EnterChasing();
            lastKnownPlayerPosition = seenPosition;
            return;
        }

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
        float multiplier = Mathf.Max(0f, 1f - confusedDurationMultiplierPerParry * parryCount);
        stateTimer = duration * multiplier;
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
            lastKnownPlayerPosition = seenPosition;
            EnterWindup();
            return;
        }

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
        enemyAgent.speed = EffectiveBaseSpeed;

        StopChaseAudio();

        GotoNextPoint();
    }

    // Llamado por PlayerHandler cuando el jugador parry-ea al enemigo: lo aleja
    // y lo deja completamente inmóvil unos segundos antes de retomar la búsqueda.
    public void Parry(Vector3 knockbackDirection)
    {
        parryCount++;

        state = EnemyState.Stunned;
        stateTimer = stunDuration;

        AudioManager.RegisterParry();
        PlaySound(AudioClipName.Parry);

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
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            PlaySound(AudioClipName.Comeback);

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

    // Llamado por HideOut cuando el jugador se esconde estando demasiado cerca del enemigo:
    // en vez de perder el rastro o pasar a Searching/Confused, sigue directo hacia el escondite
    // (ignorando si lo ve o no) y, al llegar, ejecuta onArrived (ej. sacarlo a la fuerza) antes
    // de retomar la persecución normal.
    public void ForceApproach(Vector3 target, System.Action onArrived)
    {
        state = EnemyState.ForceApproach;
        forceApproachTarget = target;
        onForceApproachArrived = onArrived;

        enemyAgent.isStopped = false;
        enemyAgent.updateRotation = true;
        enemyAgent.speed = EffectiveBaseSpeed * chaseMultiplier;
        enemyAgent.SetDestination(target);

        StartChaseTracking();
    }

    void UpdateForceApproach()
    {
        float distance = Vector3.Distance(transform.position, forceApproachTarget);
        if (distance <= forceApproachArrivalDistance)
        {
            System.Action callback = onForceApproachArrived;
            onForceApproachArrived = null;
            callback?.Invoke();

            EnterChasing();
        }
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
        patrolStuckTimer = 0f;
        lastRemainingDistance = float.PositiveInfinity;

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

        if (enemyAgent.pathPending) return;

        // El punto quedó completamente inalcanzable (sin ruta posible, ej. otro piso sin
        // conexión de NavMesh): no tiene sentido esperar a que "llegue" a remainingDistance < 0.5,
        // porque nunca va a pasar. Se prueba con otro punto de inmediato.
        if (!enemyAgent.hasPath || enemyAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            GotoNextPoint();
            return;
        }

        // Choose the next destination point when the agent gets
        // close to the current one.
        if (enemyAgent.remainingDistance < 0.5f)
        {
            isPatrolPaused = true;
            patrolPauseTimer = Random.Range(patrolPauseMinDuration, patrolPauseMaxDuration);
            enemyAgent.isStopped = true;
            enemyAgent.velocity = Vector3.zero;
            return;
        }

        // Salvavidas: ruta parcial (o agente empujado/trabado) que jamás baja de 0.5 de
        // remainingDistance. Si hace patrolStuckTimeout segundos que no achica la distancia
        // real, se lo da por atascado y se elige otro punto en vez de quedar congelado ahí.
        if (enemyAgent.remainingDistance < lastRemainingDistance - 0.1f)
        {
            lastRemainingDistance = enemyAgent.remainingDistance;
            patrolStuckTimer = 0f;
        }
        else
        {
            patrolStuckTimer += Time.deltaTime;
            if (patrolStuckTimer >= patrolStuckTimeout)
            {
                GotoNextPoint();
            }
        }
    }
}
