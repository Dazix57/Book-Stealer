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
            areaCompleted = true;
            GameManager.MarkObjectiveAreaCompleted(gameObject.tag);
            RemoveMark();

            objectivesText.text = $"Objectives Completed! {itemsPicked} / {objectives.Length}";
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
        checkPoint = true;
        foreach(GameObject item in objectives)
        {
            Destroy(item);
        }
        RemoveMark();
    }

    // Quita el marcador (hijo 'Mark') del área: se usa tanto al completarla en vivo
    // como al recargar un checkpoint donde el área ya estaba completada, para que
    // ambos casos dejen el área en el mismo estado (sin marcador).
    void RemoveMark()
    {
        if (gameObject.transform.childCount > 0)
        {
            gameObject.transform.GetChild(0).tag = "Untagged";
            Destroy(gameObject.transform.GetChild(0).gameObject);
        }
    }
}
