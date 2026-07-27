using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static readonly List<string> completedObjectiveAreas = new List<string>();

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

    /// <summary>
    /// Marca como completada un área de objetivos (identificada por el tag de su GameObject).
    /// Solo se guarda en memoria; no persiste entre sesiones de juego.
    /// </summary>
    public static void MarkObjectiveAreaCompleted(string areaTag)
    {
        if (!completedObjectiveAreas.Contains(areaTag))
        {
            completedObjectiveAreas.Add(areaTag);
        }
    }

    public static bool IsObjectiveAreaCompleted(string areaTag)
    {
        return completedObjectiveAreas.Contains(areaTag);
    }
}
