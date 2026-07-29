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

    private RawImage jumpscareDisplay;
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

    private void HandleChaseJumpscare(Texture jumpscareImage)
    {
        if (jumpscareImage == null) return;

        if (jumpscareRoutine != null)
        {
            StopCoroutine(jumpscareRoutine);
        }
        jumpscareRoutine = StartCoroutine(PlayJumpscare(jumpscareImage));
    }

    private IEnumerator PlayJumpscare(Texture jumpscareImage)
    {
        RawImage display = GetJumpscareDisplay();
        display.texture = jumpscareImage;
        display.color = Color.white;
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
            color.a = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            display.color = color;
            yield return null;
        }

        display.gameObject.SetActive(false);
        jumpscareRoutine = null;
    }

    // Crea, la primera vez que se necesita, una imagen a pantalla completa (delante de
    // todo lo demás en este Canvas) para mostrar el jumpscare.
    private RawImage GetJumpscareDisplay()
    {
        if (jumpscareDisplay != null) return jumpscareDisplay;

        GameObject displayObject = new GameObject("ChaseJumpscareDisplay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        displayObject.transform.SetParent(transform, false);
        displayObject.transform.SetAsLastSibling();

        jumpscareRect = (RectTransform)displayObject.transform;
        jumpscareRect.anchorMin = Vector2.zero;
        jumpscareRect.anchorMax = Vector2.one;
        jumpscareRect.offsetMin = Vector2.zero;
        jumpscareRect.offsetMax = Vector2.zero;
        jumpscareBasePosition = jumpscareRect.anchoredPosition;

        jumpscareDisplay = displayObject.GetComponent<RawImage>();
        jumpscareDisplay.raycastTarget = false;
        jumpscareDisplay.gameObject.SetActive(false);

        return jumpscareDisplay;
    }
}
