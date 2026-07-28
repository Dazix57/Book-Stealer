using UnityEngine;
using UnityEngine.AI;

public class EnemyFootsteps : FootstepEmitter
{
    [SerializeField] private NavMeshAgent agent;

    // Los pasos del enemigo suenan un poco más fuerte que los del jugador.
    [SerializeField] private float loudnessMultiplier = 1.3f;

    protected override void Awake()
    {
        base.Awake();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    protected override float CurrentSpeed => agent.velocity.magnitude;

    protected override float VolumeScale => loudnessMultiplier;
}
