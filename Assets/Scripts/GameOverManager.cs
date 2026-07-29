using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;

    // Duración del jumpscare estático antes de mostrar el menú de muerte
    [SerializeField] private float jumpscareDuration = 1.5f;

    // Imagen a pantalla completa para el jumpscare; se crea una única vez en tiempo de ejecución
    private RawImage jumpscareDisplay;

    private void OnEnable()
    {
        EventManager.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        EventManager.OnPlayerDeath -= HandlePlayerDeath;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameOverPanel.SetActive(false);
    }

    private void HandlePlayerDeath(Texture jumpscareImage)
    {
        StartCoroutine(PlayJumpscareThenGameOver(jumpscareImage));
    }

    private IEnumerator PlayJumpscareThenGameOver(Texture jumpscareImage)
    {
        if (jumpscareImage != null)
        {
            RawImage display = GetJumpscareDisplay();
            display.texture = jumpscareImage;
            display.gameObject.SetActive(true);

            yield return new WaitForSecondsRealtime(jumpscareDuration);

            display.gameObject.SetActive(false);
        }

        TriggerGameOver();
    }

    // Crea, la primera vez que se necesita, una imagen estática a pantalla completa
    // (sin zoom ni animación) sobre este mismo Canvas para mostrar el jumpscare.
    private RawImage GetJumpscareDisplay()
    {
        if (jumpscareDisplay != null) return jumpscareDisplay;

        GameObject displayObject = new GameObject("JumpscareDisplay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        displayObject.transform.SetParent(transform, false);
        displayObject.transform.SetAsLastSibling();

        RectTransform rect = (RectTransform)displayObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        jumpscareDisplay = displayObject.GetComponent<RawImage>();
        jumpscareDisplay.gameObject.SetActive(false);

        return jumpscareDisplay;
    }

    // Update is called once per frame
    public void TriggerGameOver()
    {
        gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; // IMPORTANTE: DEFINIR TIEMPO DE REINICIO PARA LAS ESCENAS
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); 
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f; // IMPORTANTE: DEFINIR TIEMPO DE REINICIO PARA LAS ESCENAS
        SceneManager.LoadScene("MainMenuScene"); // Ajustar nombre a la escena principal
    }
}
