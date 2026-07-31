using UnityEngine;

public class EnemyFacingSprite : MonoBehaviour
{
    [SerializeField] private GameObject forwardSprite;
    [SerializeField] private GameObject backwardSprite;

    // Silueta que se ve a través de paredes mientras el jugador está agachado (ver
    // EnemyController.UpdateHighlight / SetHighlighted). Es una copia más grande de cada
    // sprite, creada en runtime como hijo de ese mismo GameObject: al quedar bajo un padre
    // inactivo (forwardSprite/backwardSprite se apagan según de qué lado se vea al enemigo),
    // se oculta sola sin necesitar sincronizarla a mano.
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f, 0.6f);
    [SerializeField] private float highlightScale = 1.15f;

    // Reemplaza a la luz real que tenía el enemigo (delataba su posición a distancia porque
    // iluminaba el entorno a su alrededor). El sprite usa Custom/EnemyCamouflageNoise: en vez de
    // brillar, se camufla con ruido tipo estática (mismo principio que el pixelado ya usado en la
    // cámara principal, ver Assets/Materials/PixelCamera.renderTexture) — solo a distancias
    // realmente lejanas empieza a aparecer ruido; dentro de clearDistance se dibuja tal cual es,
    // sin ningún ruido.
    [Header("Camuflaje por distancia")]
    [SerializeField] private float clearDistance = 20f; // dentro de este radio, sprite 100% nítido (sin ruido)
    [SerializeField] private float obscuredDistance = 28f; // más allá de esto, ruido a su tope (ver _MaxNoiseAmount en el shader)

    // Instancia propia por renderer (en vez de una sola compartida): así queda garantizado que
    // ambas caras reciben el mismo _Clarity todos los frames, sin depender de que Unity trate un
    // Material asignado a dos SpriteRenderer distintos como realmente equivalente en todo momento.
    private Material forwardMaterial;
    private Material backwardMaterial;

    private Transform cameraTransform;
    private bool showingForward;

    private SpriteRenderer forwardRenderer;
    private SpriteRenderer backwardRenderer;
    private SpriteRenderer forwardHighlight;
    private SpriteRenderer backwardHighlight;

    void Start()
    {
        cameraTransform = Camera.main.transform;

        forwardRenderer = forwardSprite.GetComponent<SpriteRenderer>();
        backwardRenderer = backwardSprite.GetComponent<SpriteRenderer>();

        Shader noiseShader = Shader.Find("Custom/EnemyCamouflageNoise");
        forwardMaterial = new Material(noiseShader);
        backwardMaterial = new Material(noiseShader);
        forwardRenderer.material = forwardMaterial;
        backwardRenderer.material = backwardMaterial;

        forwardHighlight = CreateHighlightRenderer(forwardRenderer);
        backwardHighlight = CreateHighlightRenderer(backwardRenderer);

        SetFacing(true);
    }

    void Update()
    {
        UpdateCamouflage();
    }

    // Margen alrededor del cruce de 90° (dot == 0) entre mostrar el sprite forward/backward.
    // Cerca del enemigo, un movimiento lateral chico del jugador ya representa un cambio angular
    // grande (el ángulo relativo escala con distancia lateral / distancia al enemigo), así que sin
    // este margen el dot cruza 0 varias veces por segundo y el sprite parpadea entre ambas caras.
    [SerializeField] private float facingHysteresis = 0.35f;

    // Segunda red de seguridad, independiente del margen de arriba: por más ruido angular que
    // haya (steering del NavMeshAgent, etc.), nunca se permite más de un cambio de cara dentro
    // de esta ventana de tiempo, así que un parpadeo rápido queda descartado de raíz.
    [SerializeField] private float minFacingSwitchInterval = 0.2f;
    private float lastFacingSwitchTime = float.NegativeInfinity;

    void FixedUpdate()
    {
        if (Time.time - lastFacingSwitchTime < minFacingSwitchInterval) return;

        Vector3 toCamera = (cameraTransform.position - transform.position).normalized;
        float facingDot = Vector3.Dot(transform.forward, toCamera);

        // Solo cambia de cara al cruzar claramente hacia el otro lado; dentro del margen se
        // mantiene la cara actual, así que el cruce exacto de 90° no causa parpadeo.
        if (showingForward && facingDot < -facingHysteresis)
        {
            SetFacing(false);
            lastFacingSwitchTime = Time.time;
        }
        else if (!showingForward && facingDot > facingHysteresis)
        {
            SetFacing(true);
            lastFacingSwitchTime = Time.time;
        }
    }

    // Camuflaje por distancia: cuanto más lejos esté la cámara, más ruido cubre al sprite (0 =
    // puro ruido, el enemigo se pierde); cerca, el ruido desaparece y se ve nítido (1).
    void UpdateCamouflage()
    {
        float distance = Vector3.Distance(transform.position, cameraTransform.position);
        float range = Mathf.Max(0.01f, obscuredDistance - clearDistance); // evita división por cero si se configuran mal en el Inspector
        float clarity = Mathf.Clamp01(1f - (distance - clearDistance) / range);

        forwardMaterial.SetFloat("_Clarity", clarity);
        backwardMaterial.SetFloat("_Clarity", clarity);
    }

    private void SetFacing(bool front)
    {
        showingForward = front;
        forwardSprite.SetActive(front);
        backwardSprite.SetActive(!front);
    }

    // Crea, en runtime, la silueta ampliada para un sprite (forward o backward) dado. No
    // requiere ningún objeto o material configurado de antemano en el prefab.
    private SpriteRenderer CreateHighlightRenderer(SpriteRenderer spriteRenderer)
    {
        GameObject highlightObject = new GameObject("HighlightOutline");
        Transform highlightTransform = highlightObject.transform;
        highlightTransform.SetParent(spriteRenderer.transform, false);
        highlightTransform.localPosition = Vector3.zero;
        highlightTransform.localRotation = Quaternion.identity;
        highlightTransform.localScale = Vector3.one * highlightScale;

        SpriteRenderer highlight = highlightObject.AddComponent<SpriteRenderer>();
        highlight.sprite = spriteRenderer.sprite;
        highlight.color = highlightColor;
        highlight.sortingOrder = spriteRenderer.sortingOrder - 1;
        highlight.material = new Material(Shader.Find("Custom/SpriteXRay"));
        highlight.enabled = false;
        return highlight;
    }

    // Llamado por EnemyController mientras el jugador está agachado. Solo se termina viendo
    // la silueta del sprite (forward/backward) que esté activo en este momento, ya que la otra
    // queda bajo un GameObject padre inactivo.
    public void SetHighlighted(bool highlighted)
    {
        forwardHighlight.enabled = highlighted;
        backwardHighlight.enabled = highlighted;
    }
}
