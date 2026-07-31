using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Muestra un jumpscare rápido (imagen del enemigo, con shake y fundido) apenas el jugador
// empieza a ser perseguido por primera vez (ver EnemyController.ChaseJumpscare).
public class ChaseJumpscareManager : MonoBehaviour
{
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeStrength = 15f; // píxeles de offset máximo mientras tiembla
    [SerializeField] private float fadeDuration = 0.4f;
    [Tooltip("Orden propio del Canvas del jumpscare. Debe estar por encima de los Canvas de gameplay y pausa.")]
    [SerializeField] private int jumpscareSortingOrder = 100;

    private RawImage jumpscareDisplay;
    private Canvas jumpscareCanvas;
    private RectTransform jumpscareRect;
    private Vector2 jumpscareBasePosition;
    private Coroutine jumpscareRoutine;

    private void OnEnable()
    {
        EnemyController.ChaseJumpscare += HandleChaseJumpscare;
    }

    private void OnDisable()
    {
        EnemyController.ChaseJumpscare -= HandleChaseJumpscare;
    }

    private void HandleChaseJumpscare(Texture jumpscareImage, float maxAlpha)
    {
        if (jumpscareImage == null) return;

        if (jumpscareRoutine != null)
        {
            StopCoroutine(jumpscareRoutine);
        }
        jumpscareRoutine = StartCoroutine(PlayJumpscare(jumpscareImage, maxAlpha));
    }

    private IEnumerator PlayJumpscare(Texture jumpscareImage, float maxAlpha)
    {
        RawImage display = GetJumpscareDisplay();
        display.texture = jumpscareImage;
        display.color = new Color(1f, 1f, 1f, maxAlpha);
        jumpscareRect.anchoredPosition = jumpscareBasePosition;
        display.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            jumpscareRect.anchoredPosition = jumpscareBasePosition + Random.insideUnitCircle * shakeStrength;
            yield return null;
        }
        jumpscareRect.anchoredPosition = jumpscareBasePosition;

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Color color = display.color;
            color.a = maxAlpha * (1f - Mathf.Clamp01(elapsed / fadeDuration));
            display.color = color;
            yield return null;
        }

        display.gameObject.SetActive(false);
        jumpscareRoutine = null;
    }

    // Crea una capa de Canvas independiente. SetAsLastSibling solo ordena elementos dentro de
    // un mismo Canvas; no sirve frente a otro Canvas (por ejemplo el de un efecto de pantalla).
    // El override de orden garantiza que esta imagen siempre se dibuje encima de esos efectos.
    private RawImage GetJumpscareDisplay()
    {
        if (jumpscareDisplay != null) return jumpscareDisplay;

        GameObject displayObject = new GameObject(
            "ChaseJumpscareDisplay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasRenderer),
            typeof(RawImage));
        displayObject.transform.SetParent(transform, false);
        displayObject.transform.SetAsLastSibling();

        jumpscareCanvas = displayObject.GetComponent<Canvas>();
        jumpscareCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        jumpscareCanvas.overrideSorting = true;
        jumpscareCanvas.sortingOrder = jumpscareSortingOrder;

        jumpscareRect = (RectTransform)displayObject.transform;
        jumpscareRect.anchorMin = Vector2.zero;
        jumpscareRect.anchorMax = Vector2.one;
        jumpscareRect.offsetMin = Vector2.zero;
        jumpscareRect.offsetMax = Vector2.zero;
        jumpscareBasePosition = jumpscareRect.anchoredPosition;

        jumpscareDisplay = displayObject.GetComponent<RawImage>();
        jumpscareDisplay.raycastTarget = false;
        jumpscareDisplay.maskable = false;
        jumpscareDisplay.gameObject.SetActive(false);

        return jumpscareDisplay;
    }
}
