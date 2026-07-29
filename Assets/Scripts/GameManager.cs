using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static readonly Dictionary<string, bool> completedObjectiveAreas = new Dictionary<string, bool>();
    // Guarda el orden en que se completaron las áreas, para poder ubicar al
    // jugador en la última al restaurar un checkpoint (Dictionary no tiene orden garantizado).
    private static readonly List<string> completedObjectiveAreasOrder = new List<string>();

    /// <summary>
    /// Marca como completada un área de objetivos (identificada por el tag de su GameObject).
    /// Solo se guarda en memoria; no persiste entre sesiones de juego.
    /// </summary>
    public static void MarkObjectiveAreaCompleted(string areaTag)
    {
        if (!completedObjectiveAreas.ContainsKey(areaTag))
        {
            completedObjectiveAreas.Add(areaTag, true);
            completedObjectiveAreasOrder.Add(areaTag);
        }
    }

    public static bool IsObjectiveAreaCompleted(string areaTag)
    {
        // CheckObjectives solo llama a MarkObjectiveAreaCompleted una vez que los
        // objetivos fueron completados Y la llave que aparece fue recogida, asi que
        // el diccionario ya refleja ambas condiciones a la vez.
        return completedObjectiveAreas.ContainsKey(areaTag) && completedObjectiveAreas[areaTag];
    }

    public static Dictionary<string, int> GetChildrensTags(GameObject parent, int childrenSize)
    {
        Dictionary<string, int> childrens = new Dictionary<string, int>();

        for (int i = 0; i < childrenSize; i++)
        {
            string childrenTag = parent.transform.GetChild(i).tag;
            // Indexador en vez de Add: varios hijos suelen compartir el tag "Untagged",
            // y Add lanzaria una excepcion al encontrar una clave repetida.
            childrens[childrenTag] = i;
        }

        return childrens;
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

        foreach (string tag in completedObjectiveAreas.Keys)
        {
            // Valida las áreas completadas
            GameObject area = GameObject.FindGameObjectWithTag(tag);
            area.GetComponent<CheckObjectives>().SetObjectivesCompleted();
        }

        // Reubica al jugador en la última área completada
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject lastAreaCompleted = GameObject.FindGameObjectWithTag(completedObjectiveAreasOrder[^1]);
        player.transform.position = lastAreaCompleted.transform.position;
    }
}
