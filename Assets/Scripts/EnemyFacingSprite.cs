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

        forwardHighlight = CreateHighlightRenderer(forwardRenderer);
        backwardHighlight = CreateHighlightRenderer(backwardRenderer);

        SetFacing(true);
    }

    void FixedUpdate()
    {
        Vector3 toCamera = cameraTransform.position - transform.position;
        bool cameraSeesFront = Vector3.Dot(transform.forward, toCamera) > 0f;

        if (cameraSeesFront != showingForward)
            SetFacing(cameraSeesFront);
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

    // Llamado por EnemyController.Parry(): tiñe el sprite real hacia verde según qué tan
    // parriado esté el enemigo (0 = color normal, 1 = verde completo). No afecta a la
    // silueta de resalte, que tiene su propio color (highlightColor) independiente.
    public void SetParryTint(float progress)
    {
        Color tint = Color.Lerp(Color.white, Color.green, Mathf.Clamp01(progress));
        forwardRenderer.color = tint;
        backwardRenderer.color = tint;
    }
}
