using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private void OnEnable()
    {
        EventManager.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        EventManager.OnPlayerDeath -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath()
    {
        Debug.Log("Restarting...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
