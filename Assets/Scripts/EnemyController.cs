using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{

    // Atributos personalizables
    [SerializeField]
    private float chaseMultiplier = 4.0f;
    [SerializeField]
    private float timerDuration = 5.0f;
    [SerializeField]
    private float fieldOfView = 60.0f;
    [SerializeField]
    private float viewDistance = 5.0f;
    [SerializeField]
    private Transform[] points;


    // Atributos de control
    private GameObject player;
    private Timer chaseTimer;
    private NavMeshAgent enemyAgent;

    // Atributos de patrullaje
    private int destPoint;
    private int repeatCount;
    private bool inChase;

    void Start()
    {
        // Valores predeterminados
        destPoint = 0;
        repeatCount = 0;
        inChase = false;

        // Componente de temporizador
        chaseTimer = gameObject.AddComponent<Timer>();
        chaseTimer.Duration = timerDuration;

        // Referencia al jugador
        player = GameObject.FindGameObjectWithTag("Player");

        // Referencia al agente de IA
        enemyAgent = GetComponent<NavMeshAgent>();

        // Disabling auto-braking allows for continuous movement
        // between points (i.e. the agent doesn't slow down as it
        // approaches a destination point).
        enemyAgent.autoBraking = false;

        GotoNextPoint();
    }

    void Update()
    {
        PlayerSeen();
    }

    void PlayerSeen()
    {
        Vector3 directionToPlayer = player.transform.position - transform.position;
        Vector3 rayDir = directionToPlayer.normalized;
        
        RaycastHit hit;

        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        // DEBUG: Revisa la trayectoria del ray.
        Debug.DrawRay(transform.position, rayDir * viewDistance, Color.red);

        // DEBUG: Revisa el angulo formado con respecto al eje rojo del gameObject y el jugador.
        Debug.Log("Angle: " + angle);

        bool isVisible = Physics.Raycast(transform.position, rayDir, out hit, viewDistance) // Revisa si hay una colisión con un collider
            && hit.collider.gameObject.CompareTag("Player") // Revisa que el collider del gameObject sea de el jugador
            && angle <= fieldOfView; // Revisa que esta en el rango de visión
            
        if(isVisible && !chaseTimer.Running)
        {
            chaseTimer.Run(); // Inicia el tiempo de persecución
        }

        if(chaseTimer.Running)
        {
            if(!inChase)
            {
                enemyAgent.speed *= chaseMultiplier; // Aumenta Velocidad en persecución
                viewDistance *= chaseMultiplier; // Aumenta la distancia de detección
                inChase = true;
            }
            FollowPlayer();
        }
        else
        {
            if(inChase)
            {
                // Retorna los valores a predeterminado
                enemyAgent.speed /= chaseMultiplier;
                viewDistance /= chaseMultiplier;
                inChase = false;
            }
            Patrol();
        }
    }

    void FollowPlayer()
    {
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
        // Choose the next destination point when the agent gets
        // close to the current one.
        if(!enemyAgent.pathPending && enemyAgent.remainingDistance < 0.5f)
        {
            GotoNextPoint();
        }
    }
}