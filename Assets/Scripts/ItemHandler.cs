using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ItemHandler : MonoBehaviour
{
    [SerializeField]
    private GameObject itemPanelPreFab;
    private GameObject itemPanel;
    private GameObject player = null;
    private bool inRange = false;

    void Awake()
    {
        // 'promptPanel' es el prefab, no una instancia de escena: hay que instanciarlo
        // antes de poder activarlo/mostrarlo (SetActive falla sobre el asset directo).
        itemPanel = Instantiate(itemPanelPreFab);
        itemPanel.SetActive(inRange);
    }

    // Update is called once per frame
    void Update()
    {
        CheckItemPicked();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            inRange = true;
            player = other.gameObject;

            itemPanel.GetComponentInChildren<TextMeshProUGUI>().text = "Press E to pick up";
            itemPanel.SetActive(inRange);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            inRange = false;
            itemPanel.SetActive(inRange);
        }
    }

    void CheckItemPicked()
    {
        if (player != null && inRange && Keyboard.current[player.GetComponent<PlayerHandler>().PickUpKey].wasPressedThisFrame)
        {
            AudioClipName pickUpSound = gameObject.CompareTag("Key") ? AudioClipName.KeyPickUpSound : AudioClipName.PickUpSound;
            AudioManager.Play(pickUpSound, AudioChannel.Game);
            Destroy(itemPanel);
            Destroy(gameObject);
        }
    }
}
