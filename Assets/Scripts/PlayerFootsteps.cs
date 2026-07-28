using System;
using UnityEngine;

public class PlayerFootsteps : FootstepEmitter
{
    [SerializeField] private PlayerHandler playerHandler;
    [SerializeField] private Rigidbody rb;

    // Volumen "natural" de los pasos al oído del jugador; es una elección de mezcla de audio y
    // no debe afectar qué tan lejos los oyen los enemigos (ver HearingLoudness más abajo).
    [SerializeField] private float baseVolumeMultiplier = 0.7f;

    // Qué tan más flojos suenan los pasos (para el jugador) y qué tan lejos se pueden
    // oír (para los enemigos) mientras se está agachado. 1 = normal.
    [SerializeField] private float crouchLoudnessMultiplier = 0.4f;

    // posición del paso, "loudness" (1 = normal, menor mientras se está agachado)
    public static event Action<Vector3, float> FootstepHeard;

    protected override void Awake()
    {
        base.Awake();
        if (playerHandler == null) playerHandler = GetComponent<PlayerHandler>();
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    protected override float CurrentSpeed
    {
        get
        {
            Vector3 horizontalVelocity = rb.linearVelocity;
            horizontalVelocity.y = 0f;
            return horizontalVelocity.magnitude;
        }
    }

    // Solo entra el agachado: así el alcance auditivo del enemigo queda compensado y no se ve
    // reducido por baseVolumeMultiplier, que es puramente una atenuación de audio.
    private float HearingLoudness => playerHandler.Crouching ? crouchLoudnessMultiplier : 1f;

    protected override float VolumeScale => baseVolumeMultiplier * HearingLoudness;

    protected override void OnFootstep(float speed)
    {
        FootstepHeard?.Invoke(transform.position, HearingLoudness);
    }
}
