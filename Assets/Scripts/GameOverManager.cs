using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameOverPanel.SetActive(false); 
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
