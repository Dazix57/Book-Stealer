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

    // Cada entrada es el tag del área cuya llave recogió el jugador (ej. "Area01").
    // Permite consultar cuántas llaves lleva actualmente (Count) y de qué áreas.
    private static readonly List<string> collectedKeys = new List<string>();

    // Nombres (gameObject.name) de las puertas ya abiertas. Las puertas no tienen tag propio
    // (todas comparten el mismo prefab sin tag), así que a diferencia de las áreas de objetivos
    // se identifican por nombre: Unity le asigna uno estable y único por instancia dentro de la
    // escena (ej. "Door (9)"), que se conserva igual en cada recarga de esa misma escena.
    private static readonly HashSet<string> openedDoors = new HashSet<string>();

    public static void MarkDoorOpened(string doorName)
    {
        openedDoors.Add(doorName);
    }

    public static void AddKey(string areaTag)
    {
        collectedKeys.Add(areaTag);
    }

    public static int KeyCount => collectedKeys.Count;

    public static IReadOnlyList<string> CollectedKeys => collectedKeys;

    // Retira 'count' llaves de la lista (ej. al abrir una puerta). No distingue de qué área
    // vino cada llave: todas cuentan igual para cualquier puerta.
    public static void SpendKeys(int count)
    {
        int amountToRemove = Mathf.Min(count, collectedKeys.Count);
        collectedKeys.RemoveRange(collectedKeys.Count - amountToRemove, amountToRemove);
    }

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

    // Usado por el menú de muerte para decidir si un Restart puede reanudar desde el
    // último checkpoint (ver GameOverManager.RestartGame) en vez de tirar todo el progreso.
    public static bool HasCompletedObjectiveAreas => completedObjectiveAreasOrder.Count > 0;

    // Se pone en true justo antes de cargar la escena de juego desde la escena de introducción
    // (ver IntroSceneController), y se consume (lee + resetea) una única vez desde ahí. Restart
    // y LoadCheckpoint recargan la escena de juego directamente, sin pasar por la introducción,
    // así que nunca la vuelven a poner en true: el panel tutorial solo aparece la primera vez.
    private static bool pendingTutorial = false;

    public static void RequestTutorial()
    {
        pendingTutorial = true;
    }

    public static bool ConsumePendingTutorial()
    {
        bool value = pendingTutorial;
        pendingTutorial = false;
        return value;
    }

    // Borra todo el progreso acumulado (áreas completadas, orden, llaves, puertas abiertas).
    // Hay que llamarlo antes de arrancar una partida realmente nueva (Restart, o Play desde el
    // menú principal) -- si no, un área completada en una sesión anterior queda marcada como
    // completa para siempre en Awake() de su CheckObjectives, aunque la escena recién cargada
    // resetee los libros y la llave a su estado inicial: como esa rama nunca llama a ShowKey(),
    // la llave no vuelve a aparecer y las puertas que la piden quedan cerradas para siempre.
    // NO llamar desde LoadCheckpoint()/OnCheckpointSceneLoaded(): esos SÍ dependen de que
    // este estado sobreviva la recarga, para restaurar el progreso guardado.
    public static void ResetProgress()
    {
        completedObjectiveAreas.Clear();
        completedObjectiveAreasOrder.Clear();
        collectedKeys.Clear();
        openedDoors.Clear();
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

        // Restaura las puertas ya abiertas antes que nada: no dependen de que haya
        // algún área completada (una puerta puede pedir 0 llaves).
        foreach (string doorName in openedDoors)
        {
            GameObject.Find(doorName).GetComponent<Door>().RestoreOpenState();
        }

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
