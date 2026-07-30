using UnityEngine;
using UnityEngine.AI;

public class EnemyFootsteps : FootstepEmitter
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyController enemyController;

    // Los pasos del enemigo suenan un poco más fuerte que los del jugador.
    [SerializeField] private float loudnessMultiplier = 1.2f;

    protected override void Awake()
    {
        base.Awake();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (enemyController == null) enemyController = GetComponent<EnemyController>();
    }

    protected override float CurrentSpeed => agent.velocity.magnitude;

    protected override float VolumeScale => loudnessMultiplier;

    // Pasos más fuertes mientras este enemigo está en chase (ver EnemyController.InChase);
    // el boost real (con su tope, para no reventar) lo aplica AudioManager.GetMixedVolume.
    protected override bool ChaseBoost => enemyController != null && enemyController.InChase;
}
