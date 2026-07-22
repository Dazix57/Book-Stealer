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
    }

    void Update()
    {
        PlayerSeen();
        FollowPlayer();
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
}
