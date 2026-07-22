using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{

    // Atributos personalizables
    [SerializeField]
    private float speed;
    [SerializeField]
    private float fieldOfView;
    [SerializeField]
    private float viewDistance;

    // Atributos de control
    private bool isVisible;
    private GameObject player;

    private NavMeshAgent enemyAgent;

    // Atributos de patrulla
    public Transform[] points;
    private int destPoint = 0;
    private int repeatCount = 0;

    void Start()
    {
        // Valores predeterminados
        speed = 0.5f;
        fieldOfView = 60.0f;
        viewDistance = 5.0f;
        isVisible = false;

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
        FollowPlayer();
        Patrol();
    }

    void PlayerSeen()
    {
        Vector3 directionToPlayer = player.transform.position - transform.position;
        Vector3 rayDir = directionToPlayer.normalized * viewDistance;
        
        RaycastHit hit;

        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        // DEBUG: Revisa la trayectoria del ray.
        Debug.DrawRay(transform.position, rayDir, Color.red);

        // DEBUG: Revisa el angulo formado con respecto al eje rojo del gameObject y el jugador.
        Debug.Log("Angle: " + angle);

        if(Physics.Raycast(transform.position, rayDir, out hit, viewDistance) // Revisa si hay una colisión con un collider
            && hit.collider.gameObject.CompareTag("Player") // Revisa que el gameObject sea el jugador
            && angle <= fieldOfView) // Revisa que esta en el rango de visión
        {
            isVisible = true;
        }
    }

    void FollowPlayer()
    {
        if(isVisible)
        {
            enemyAgent.SetDestination(player.transform.position);
            enemyAgent.speed = speed;
        }
    }

    void GotoNextPoint()
    {
        // Returns if no points have been set up
        if (points.Length == 0)
            return;

        // Set the agent to go to the currently selected destination.
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
        // Solo patrulla si no ha visto al jugador
        if (!isVisible)
        {
            // Choose the next destination point when the agent gets
            // close to the current one.
            if (!enemyAgent.pathPending && enemyAgent.remainingDistance < 0.5f)
                GotoNextPoint();
        }
    }
}