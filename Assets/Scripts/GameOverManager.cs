using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private string mainMenuSceneName = "MenuPuyo";

    // Duración del fundido de blanco a negro que se dispara al morir, antes de mostrar el panel
    [SerializeField] private float deathFadeToBlackDuration = 0.5f;

    // Overlay a pantalla completa para el flash de muerte; se crea una única vez en tiempo de ejecución
    private Image deathFlash;

    private void OnEnable()
    {
        EventManager.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        EventManager.OnPlayerDeath -= HandlePlayerDeath;
    }

    void Start()
    {
        gameOverPanel.SetActive(false);

        gameOverPanel.transform.Find("RestartButton").GetComponent<Button>().onClick.AddListener(RestartGame);
        gameOverPanel.transform.Find("MenuButton").GetComponent<Button>().onClick.AddListener(LoadMainMenu);
    }

    private void HandlePlayerDeath()
    {
        StartCoroutine(PlayDeathFlashThenGameOver());
    }

    private IEnumerator PlayDeathFlashThenGameOver()
    {
        Image flash = GetDeathFlash();
        flash.color = Color.white;
        flash.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < deathFadeToBlackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            flash.color = Color.Lerp(Color.white, Color.black, elapsed / deathFadeToBlackDuration);
            yield return null;
        }
        flash.color = Color.black;

        TriggerGameOver();
    }

    // Crea, la primera vez que se necesita, un overlay a pantalla completa (detrás del
    // gameOverPanel, ver SetAsFirstSibling) para el flash blanco -> negro sobre este mismo Canvas.
    private Image GetDeathFlash()
    {
        if (deathFlash != null) return deathFlash;

        GameObject flashObject = new GameObject("DeathFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flashObject.transform.SetParent(transform, false);
        flashObject.transform.SetAsFirstSibling();

        RectTransform rect = (RectTransform)flashObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        deathFlash = flashObject.GetComponent<Image>();
        deathFlash.raycastTarget = false;
        deathFlash.gameObject.SetActive(false);

        return deathFlash;
    }

    public void TriggerGameOver()
    {
        gameOverPanel.SetActive(true);
        Time.timeScale = 0f;

        // El cursor está bloqueado y oculto durante el gameplay; hay que liberarlo para poder
        // clickear Restart/Menu.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;

        // El jugador (y el pauseMenu que trae consigo) están en DontDestroyOnLoad; sin destruirlo
        // acá, sobreviviría a la recarga con su posición/salud de la muerte en vez de dejar que la
        // escena recién cargada traiga uno fresco en su posición de partida.
        InitializePauseMenu.DestroyPersistentPlayer();

        // Restart es una partida nueva de verdad (a diferencia de LoadCheckpoint): sin esto,
        // las áreas completadas/llaves de la partida anterior quedarían pegadas para siempre.
        GameManager.ResetProgress();

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        InitializePauseMenu.DestroyPersistentPlayer();
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
