using UnityEngine;

public class HideOuts : MonoBehaviour
{
    [SerializeField]
    private GameObject hidePromptPanelPreFab;
    private GameObject promptPanel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            promptPanel = Instantiate<GameObject>(hidePromptPanelPreFab, Camera.main.transform.position, Quaternion.identity);
            promptPanel.SetActive(true);        
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            promptPanel.SetActive(false);
        }
    }
}
