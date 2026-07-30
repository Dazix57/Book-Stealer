using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CheckObjectives : MonoBehaviour
{

    [SerializeField]
    private GameObject[] objectives;

    [SerializeField]
    private GameObject objectivesPanelPrefab;

    private bool showObjectives = false;
    private bool areaCompleted = false;
    private bool checkPoint = false;
    private bool keyPicked = false;
    private int itemsPicked = 0;

    private GameObject objectivesPanel;
    private TextMeshProUGUI objectivesText;

    void Awake()
    {
        // Si el área ya se completó en una sesión anterior, no hace falta volver a revisar los objetos.
        areaCompleted = GameManager.IsObjectiveAreaCompleted(gameObject.tag);
        // Awake() corre una única vez por instancia de ObjectiveArea, así que cada
        // área crea exactamente un panel propio (con sus propios objetivos y texto),
        // sin compartirlo con otras áreas.
        if (objectivesPanel == null)
        {
            GameObject objectivesPanelInstance = Instantiate(objectivesPanelPrefab);
            objectivesPanel = objectivesPanelInstance.transform.Find("Panel").gameObject;
            objectivesText = objectivesPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // La llave (hijo con tag "Key") arranca oculta: solo debe aparecer cuando
        // se recolectan todos los libros del área.
        GameObject key = FindChildByTag("Key");
        if (key != null)
        {
            key.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (showObjectives || checkPoint)
        {
            EnableObjectivePanel();
        }

    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            CalculateObjectivesLeft();
            showObjectives = true;
            objectivesPanel.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            showObjectives = false;
            itemsPicked = 0;
            objectivesPanel.SetActive(false);
        }
    }

    void EnableObjectivePanel()
    {
        if (areaCompleted)
        {
            objectivesText.text = $"Objectives Completed! {objectives.Length} / {objectives.Length}";
        }
        else if (itemsPicked == objectives.Length)
        {
            // Todos los libros ya se recolectaron: la llave aparece y el área
            // solo se marca como completada de verdad cuando el jugador la recoge
            // (ItemHandler la destruye al recogerla, igual que con los libros).
            ShowKey();

            if (!keyPicked && FindChildByTag("Key") == null)
            {
                keyPicked = true;
                GameManager.AddKey(gameObject.tag);
            }

            if (keyPicked)
            {
                areaCompleted = true;
                GameManager.MarkObjectiveAreaCompleted(gameObject.tag);
                RemoveMark();

                objectivesText.text = $"Objectives Completed! {itemsPicked} / {objectives.Length}";
            }
            else
            {
                objectivesText.text = $"Objectives Completed! Pick up the key to open doors.";
            }
        }
        else
        {
            CalculateObjectivesLeft();
            objectivesText.text = $"Books Remaining: {itemsPicked} / {objectives.Length}";
        }
    }

    void CalculateObjectivesLeft()
    {
        itemsPicked = 0;
        foreach(GameObject objective in objectives)
        {
            if (objective == null)
            {
                itemsPicked += 1;
            }
        }
    }

    public void SetObjectivesCompleted()
    {
        itemsPicked = objectives.Length;
        keyPicked = true;
        checkPoint = true;
        foreach(GameObject item in objectives)
        {
            Destroy(item);
        }
        RemoveMark();
        // El área ya estaba completa (incluida la llave) al guardar el checkpoint:
        // si la llave sigue en la escena, se limpia para no dejarla disponible de nuevo.
        RemoveKey();
    }

    // Busca entre los hijos directos del área uno con el tag indicado.
    // Devuelve null si no hay ninguno (por ejemplo, ya fue recogido/destruido).
    GameObject FindChildByTag(string tag)
    {
        Dictionary<string, int> childrens = GameManager.GetChildrensTags(gameObject, gameObject.transform.childCount);
        if (childrens.ContainsKey(tag))
        {
            return gameObject.transform.GetChild(childrens[tag]).gameObject;
        }
        return null;
    }

    // Activa la llave (hijo 'Key') para que quede visible y recogible en el área.
    // El pickup en si lo maneja ItemHandler, igual que con los libros.
    void ShowKey()
    {
        GameObject key = FindChildByTag("Key");
        if (key != null && !key.activeSelf)
        {
            key.SetActive(true);
        }
    }

    // Quita el marcador (hijo 'Mark') del área: se usa tanto al completarla en vivo
    // como al recargar un checkpoint donde el área ya estaba completada, para que
    // ambos casos dejen el área en el mismo estado (sin marcador).
    void RemoveMark()
    {
        RemoveChildByTag("Mark");
    }

    void RemoveKey()
    {
        RemoveChildByTag("Key");
    }

    void RemoveChildByTag(string tag)
    {
        GameObject child = FindChildByTag(tag);
        if (child != null)
        {
            child.tag = "Untagged";
            Destroy(child);
        }
    }
}
