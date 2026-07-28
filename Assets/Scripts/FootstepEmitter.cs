using UnityEngine;

// Base para emitir un sonido de paso a un ritmo que depende de la velocidad actual del
// personaje: a más velocidad, menor el intervalo entre pasos.
public abstract class FootstepEmitter : MonoBehaviour
{
    [SerializeField] private AudioClipName footstepClip;
    [SerializeField] private AudioChannel channel = AudioChannel.Game;
    [SerializeField] private float minSpeedToStep = 0.2f; // por debajo de esto se considera quieto, sin pasos
    [SerializeField] private float referenceSpeed = 5f; // velocidad a la que aplica stepIntervalAtReferenceSpeed
    [SerializeField] private float stepIntervalAtReferenceSpeed = 0.4f;

    [Header("Spatial audio")]
    [SerializeField] private AudioSource audioSource; // local a este GameObject, no el AudioSource compartido de AudioManager
    [SerializeField] private float minDistance = 1f; // distancia dentro de la cual se oye a volumen máximo
    [SerializeField] private float maxDistance = 15f; // distancia a partir de la cual deja de oírse

    private float stepTimer;

    protected abstract float CurrentSpeed { get; }
    protected virtual float VolumeScale => 1f;

    protected virtual void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f; // 3D: el sonido se percibe local a este GameObject, con caída por distancia
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
    }

    void Update()
    {
        float speed = CurrentSpeed;

        if (speed < minSpeedToStep)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer += Time.deltaTime;

        float stepInterval = stepIntervalAtReferenceSpeed * (referenceSpeed / speed);
        if (stepTimer >= stepInterval)
        {
            stepTimer -= stepInterval;
            float volume = AudioManager.GetChannelVolume(channel) * VolumeScale;
            audioSource.PlayOneShot(AudioManager.GetClip(footstepClip), volume);
            OnFootstep(speed);
        }
    }

    // Punto de extensión para lo que deba pasar además de reproducir el sonido (ej. avisar a los enemigos).
    protected virtual void OnFootstep(float speed) { }
}
