using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static readonly List<string> completedObjectiveAreas = new List<string>();

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

    public static void LoadCheckpoint()
    {
        // SceneManager.LoadScene no cambia de escena en el acto: el código que
        // sigue después de llamarlo (dentro de este mismo método) todavía ve la
        // escena VIEJA, a punto de destruirse. Hay que esperar a sceneLoaded,
        // que se dispara cuando la escena nueva ya cargó y sus objetos ya
        // corrieron Awake/OnEnable, para reaplicar el estado sobre los objetos
        // correctos (los de la escena recién cargada).
        SceneManager.sceneLoaded += OnCheckpointSceneLoaded;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private static void OnCheckpointSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnCheckpointSceneLoaded;

        if (completedObjectiveAreas.Count == 0) return;

        foreach (string tag in completedObjectiveAreas)
        {
            // Valida las áreas completadas
            GameObject area = GameObject.FindGameObjectWithTag(tag);
            area.GetComponent<CheckObjectives>().SetObjectivesCompleted();
        }

        // Reubica al jugador en la última área completada
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject lastAreaCompleted = GameObject.FindGameObjectWithTag(completedObjectiveAreas[^1]);
        player.transform.position = lastAreaCompleted.transform.position;
    }
}
