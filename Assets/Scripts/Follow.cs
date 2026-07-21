using UnityEngine;

public class Follow : MonoBehaviour
{

    // Atributos personalizables
    [SerializeField]
    private float speed;
    [SerializeField]
    private float fieldOfView;
    [SerializeField]
    private float viewDistance;

    // Atributos de control
    private bool isFollowed;
    private GameObject player;

    void Start()
    {
        // Valores predeterminados
        speed = 5.0f;
        fieldOfView = 60.0f;
        viewDistance = 3.0f;
        isFollowed = false;

        // Referencia al jugador
        player = GameObject.FindGameObjectWithTag("MainPlayer");
    }

    void Update()
    {
        PlayerSeen();
        FollowPlayer();
    }

    void PlayerSeen()
    {
        Vector3 directionToPlayer = (player.transform.position - transform.position).normalized;
        Vector3 rayEnd = transform.position + directionToPlayer * viewDistance;

        // DEBUG: Revisa la trayectoria del ray.
        Debug.DrawRay(transform.position, rayEnd, Color.red);

        Vector3 npcToPlayer = player.transform.position - transform.position;
        float angle = Vector3.Angle(transform.right, npcToPlayer);

        // DEBUG: Revisa el angulo formado con respecto al eje rojo del gameObject y el jugador.
        Debug.Log("Angle: " + angle);

        if (Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, viewDistance) 
        && hit.collider.CompareTag("MainPlayer") && angle > fieldOfView && angle < fieldOfView * 2)
        {
            isFollowed = true;
        }
    }

    void FollowPlayer()
    {
        if(isFollowed)
        {
            transform.position = Vector3.MoveTowards(transform.position, player.transform.position, speed * Time.deltaTime);
        }
    }
}
