using TMPro;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;
using UnityEngine.InputSystem;

public class ItemHandler : MonoBehaviour
{
    [SerializeField]
    private GameObject promptPanel;
    private GameObject player = null;
    private bool picked = false;
    private bool inRange = false;


    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            inRange = true;
            player = other.gameObject;

            promptPanel.GetComponentInChildren<TextMeshProUGUI>().text = "Press E to pick";
            promptPanel.SetActive(inRange);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            inRange = false;
            promptPanel.SetActive(inRange);
        }
    }

    void CheckItemPicked()
    {
        if (player != null && inRange && Keyboard.current[player.GetComponent<PlayerHandler>().PickUpKey].wasPressedThisFrame)
        {
            picked = true;
            
        }
    }

    public bool Picked
    {
        get {return picked;}
        set {picked = value;}
    } 
}
