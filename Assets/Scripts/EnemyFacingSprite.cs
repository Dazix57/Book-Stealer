using System.Collections.Generic;
using UnityEngine;

// Presentación visual del enemigo: alterna su cara visible, aplica pixelado por distancia y
// limita la velocidad de sus animaciones. La calidad cambia por niveles con histéresis para que
// un pequeño movimiento de cámara alrededor de un límite no haga saltar de nivel continuamente.
public class EnemyFacingSprite : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private GameObject forwardSprite;
    [SerializeField] private GameObject backwardSprite;

    [Header("Facing")]
    [SerializeField] private float facingHysteresis = 0.35f;
    [SerializeField] private float minFacingSwitchInterval = 0.2f;

    [Header("Distance pixelation")]
    [Tooltip("Por encima de esta distancia, el sprite se renderiza en 16 x 16.")]
    [SerializeField] private float farDistance = 50f;
    [Tooltip("Entre esta distancia y Far Distance, el sprite se renderiza en 32 x 32.")]
    [SerializeField] private float mediumDistance = 30f;
    [Tooltip("Entre esta distancia y Medium Distance, el sprite se renderiza en 64 x 64.")]
    [SerializeField] private float nearDistance = 15f;
    [Tooltip("Margen para evitar que la calidad oscile al cruzar una distancia límite.")]
    [SerializeField] private float distanceHysteresis = 1f;
    [SerializeField] private int farPixelGrid = 16;
    [SerializeField] private int mediumPixelGrid = 32;
    [SerializeField] private int nearPixelGrid = 64;

    [Header("Distance darkness")]
    [Tooltip("Tintes multiplicativos: valores más bajos hacen al enemigo más difícil de distinguir.")]
    [SerializeField] private Color farDistanceTint = new Color(0.22f, 0.25f, 0.32f, 1f);
    [SerializeField] private Color mediumDistanceTint = new Color(0.38f, 0.42f, 0.50f, 1f);
    [SerializeField] private Color nearDistanceTint = new Color(0.58f, 0.62f, 0.70f, 1f);
    [SerializeField] private Color fullResolutionTint = new Color(0.78f, 0.80f, 0.86f, 1f);

    [Header("Animation frame rate")]
    [Tooltip("Los clips actuales del enemigo están muestreados a 60 FPS.")]
    [SerializeField] private float authoredAnimationFps = 60f;
    [SerializeField] private float farAnimationFps = 4f;
    [SerializeField] private float mediumAnimationFps = 8f;
    [SerializeField] private float nearAnimationFps = 12f;
    [Tooltip("A esta distancia o menos, la animación alcanza su velocidad máxima de cerca.")]
    [SerializeField] private float fullSpeedDistance = 4f;
    [SerializeField] private float closestAnimationFps = 24f;
    [SerializeField] private float animationFpsChangePerSecond = 24f;

    [Header("Crouch highlight")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f, 0.6f);
    [SerializeField] private float highlightScale = 1.15f;

    private enum DetailLevel { Far, Medium, Near, Full }

    private Transform cameraTransform;
    private SpriteRenderer forwardRenderer;
    private SpriteRenderer backwardRenderer;
    private SpriteRenderer forwardHighlight;
    private SpriteRenderer backwardHighlight;
    private Material forwardMaterial;
    private Material backwardMaterial;
    private Animator[] spriteAnimators;
    private Sprite forwardLastSprite;
    private Sprite backwardLastSprite;
    private DetailLevel currentDetailLevel;
    private bool isInitialized;
    private bool showingForward;
    private float lastFacingSwitchTime = float.NegativeInfinity;
    private float currentAnimationFps;
    private readonly Dictionary<Sprite, Vector4> spriteUvRectCache = new Dictionary<Sprite, Vector4>();

    private const string PixelationShaderName = "Custom/EnemyDistancePixelation";
    private static readonly int PixelGridSizeId = Shader.PropertyToID("_PixelGridSize");
    private static readonly int SpriteUVRectId = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int DistanceTintId = Shader.PropertyToID("_DistanceTint");

    private void Start()
    {
        cameraTransform = Camera.main != null ? Camera.main.transform : null;
        forwardRenderer = forwardSprite != null ? forwardSprite.GetComponent<SpriteRenderer>() : null;
        backwardRenderer = backwardSprite != null ? backwardSprite.GetComponent<SpriteRenderer>() : null;

        if (forwardRenderer == null || backwardRenderer == null)
        {
            Debug.LogError($"{nameof(EnemyFacingSprite)} on {name} requires forward and backward SpriteRenderers.", this);
            enabled = false;
            return;
        }

        Shader pixelationShader = Shader.Find(PixelationShaderName);
        if (pixelationShader == null)
        {
            Debug.LogError($"Could not find shader {PixelationShaderName}.", this);
            enabled = false;
            return;
        }

        forwardMaterial = new Material(pixelationShader);
        backwardMaterial = new Material(pixelationShader);
        forwardRenderer.material = forwardMaterial;
        backwardRenderer.material = backwardMaterial;

        forwardHighlight = CreateHighlightRenderer(forwardRenderer);
        backwardHighlight = CreateHighlightRenderer(backwardRenderer);
        spriteAnimators = GetComponentsInChildren<Animator>(true);

        currentDetailLevel = DetailLevelForDistance(GetDistanceToCamera());
        currentAnimationFps = AnimationFpsForDistance(GetDistanceToCamera(), currentDetailLevel);
        ApplyDetailLevel(currentDetailLevel);
        ApplyAnimationSpeed(currentAnimationFps);
        SetFacing(true);
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized) return;

        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;
            cameraTransform = mainCamera.transform;
        }

        float distance = GetDistanceToCamera();
        UpdateDetailLevel(distance);
        UpdateAnimationSpeed(distance);
        UpdateSpriteUVRect(forwardRenderer, forwardHighlight, forwardMaterial, ref forwardLastSprite);
        UpdateSpriteUVRect(backwardRenderer, backwardHighlight, backwardMaterial, ref backwardLastSprite);

        UpdateFacing();
    }

    private float GetDistanceToCamera()
    {
        return cameraTransform == null ? 0f : Vector3.Distance(transform.position, cameraTransform.position);
    }

    private DetailLevel DetailLevelForDistance(float distance)
    {
        if (distance > farDistance) return DetailLevel.Far;
        if (distance > mediumDistance) return DetailLevel.Medium;
        if (distance > nearDistance) return DetailLevel.Near;
        return DetailLevel.Full;
    }

    private void UpdateDetailLevel(float distance)
    {
        DetailLevel nextDetailLevel = currentDetailLevel;

        switch (currentDetailLevel)
        {
            case DetailLevel.Far:
                if (distance < farDistance - distanceHysteresis) nextDetailLevel = DetailLevel.Medium;
                break;
            case DetailLevel.Medium:
                if (distance > farDistance + distanceHysteresis) nextDetailLevel = DetailLevel.Far;
                else if (distance < mediumDistance - distanceHysteresis) nextDetailLevel = DetailLevel.Near;
                break;
            case DetailLevel.Near:
                if (distance > mediumDistance + distanceHysteresis) nextDetailLevel = DetailLevel.Medium;
                else if (distance < nearDistance - distanceHysteresis) nextDetailLevel = DetailLevel.Full;
                break;
            case DetailLevel.Full:
                if (distance > nearDistance + distanceHysteresis) nextDetailLevel = DetailLevel.Near;
                break;
        }

        if (nextDetailLevel == currentDetailLevel) return;

        currentDetailLevel = nextDetailLevel;
        ApplyDetailLevel(currentDetailLevel);
    }

    private void ApplyDetailLevel(DetailLevel detailLevel)
    {
        int pixelGrid = detailLevel switch
        {
            DetailLevel.Far => farPixelGrid,
            DetailLevel.Medium => mediumPixelGrid,
            DetailLevel.Near => nearPixelGrid,
            _ => 0,
        };
        Color distanceTint = detailLevel switch
        {
            DetailLevel.Far => farDistanceTint,
            DetailLevel.Medium => mediumDistanceTint,
            DetailLevel.Near => nearDistanceTint,
            _ => fullResolutionTint,
        };

        forwardMaterial.SetFloat(PixelGridSizeId, pixelGrid);
        backwardMaterial.SetFloat(PixelGridSizeId, pixelGrid);
        forwardMaterial.SetColor(DistanceTintId, distanceTint);
        backwardMaterial.SetColor(DistanceTintId, distanceTint);
        UpdateSpriteUVRect(forwardRenderer, forwardHighlight, forwardMaterial, ref forwardLastSprite, true);
        UpdateSpriteUVRect(backwardRenderer, backwardHighlight, backwardMaterial, ref backwardLastSprite, true);
    }

    private void UpdateAnimationSpeed(float distance)
    {
        float targetAnimationFps = AnimationFpsForDistance(distance, currentDetailLevel);
        currentAnimationFps = Mathf.MoveTowards(
            currentAnimationFps,
            targetAnimationFps,
            animationFpsChangePerSecond * Time.deltaTime);
        ApplyAnimationSpeed(currentAnimationFps);
    }

    private float AnimationFpsForDistance(float distance, DetailLevel detailLevel)
    {
        switch (detailLevel)
        {
            case DetailLevel.Far:
                return farAnimationFps;
            case DetailLevel.Medium:
                return mediumAnimationFps;
            case DetailLevel.Near:
                return nearAnimationFps;
            default:
                float clampedFullSpeedDistance = Mathf.Clamp(fullSpeedDistance, 0f, nearDistance);
                if (nearDistance <= clampedFullSpeedDistance) return closestAnimationFps;

                float closeness = 1f - Mathf.InverseLerp(clampedFullSpeedDistance, nearDistance, distance);
                return Mathf.Lerp(nearAnimationFps, closestAnimationFps, closeness);
        }
    }

    private void ApplyAnimationSpeed(float animationFps)
    {
        if (spriteAnimators == null) return;

        float speed = authoredAnimationFps > 0f ? animationFps / authoredAnimationFps : 1f;
        foreach (Animator spriteAnimator in spriteAnimators)
        {
            if (spriteAnimator != null) spriteAnimator.speed = speed;
        }
    }

    private void UpdateSpriteUVRect(SpriteRenderer spriteRenderer, SpriteRenderer highlightRenderer, Material material, ref Sprite lastSprite, bool force = false)
    {
        if (spriteRenderer == null || material == null) return;

        Sprite sprite = spriteRenderer.sprite;
        if (!force && sprite == lastSprite) return;

        lastSprite = sprite;
        if (highlightRenderer != null) highlightRenderer.sprite = sprite;
        if (sprite == null || sprite.texture == null)
        {
            material.SetVector(SpriteUVRectId, new Vector4(0f, 0f, 1f, 1f));
            return;
        }

        if (!spriteUvRectCache.TryGetValue(sprite, out Vector4 uvRect))
        {
            // Sprite.uv funciona tanto para sprites sueltos como para sprites empacados en atlas;
            // textureRect puede no estar disponible para ciertos sprites empacados.
            Vector2[] uv = sprite.uv;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            foreach (Vector2 coordinate in uv)
            {
                minX = Mathf.Min(minX, coordinate.x);
                minY = Mathf.Min(minY, coordinate.y);
                maxX = Mathf.Max(maxX, coordinate.x);
                maxY = Mathf.Max(maxY, coordinate.y);
            }

            uvRect = new Vector4(
                minX,
                minY,
                Mathf.Max(0.0001f, maxX - minX),
                Mathf.Max(0.0001f, maxY - minY));
            spriteUvRectCache.Add(sprite, uvRect);
        }

        material.SetVector(SpriteUVRectId, uvRect);
    }

    private void UpdateFacing()
    {
        if (Time.time - lastFacingSwitchTime < minFacingSwitchInterval) return;

        Vector3 toCamera = (cameraTransform.position - transform.position).normalized;
        float facingDot = Vector3.Dot(transform.forward, toCamera);

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

    private void SetFacing(bool front)
    {
        showingForward = front;
        forwardSprite.SetActive(front);
        backwardSprite.SetActive(!front);
    }

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

    // Llamado por EnemyController mientras el jugador está agachado.
    public void SetHighlighted(bool highlighted)
    {
        if (!isInitialized) return;

        forwardHighlight.enabled = highlighted;
        backwardHighlight.enabled = highlighted;
    }

    private void OnDestroy()
    {
        if (forwardMaterial != null) Destroy(forwardMaterial);
        if (backwardMaterial != null) Destroy(backwardMaterial);
    }

    private void OnValidate()
    {
        nearDistance = Mathf.Max(0f, nearDistance);
        farDistance = Mathf.Max(nearDistance, farDistance);
        mediumDistance = Mathf.Clamp(mediumDistance, nearDistance, farDistance);
        distanceHysteresis = Mathf.Max(0f, distanceHysteresis);
        fullSpeedDistance = Mathf.Clamp(fullSpeedDistance, 0f, nearDistance);
        farPixelGrid = Mathf.Max(1, farPixelGrid);
        mediumPixelGrid = Mathf.Max(1, mediumPixelGrid);
        nearPixelGrid = Mathf.Max(1, nearPixelGrid);
        authoredAnimationFps = Mathf.Max(0.01f, authoredAnimationFps);
    }
}
