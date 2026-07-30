using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Door : MonoBehaviour
{
    // Cantidad de llaves que pide ESTA puerta para abrirse. Es un campo por instancia:
    // cada puerta colocada en la escena (o cada variante del prefab) puede pedir un número distinto.
    [Tooltip("Cantidad de llaves necesarias para abrir esta puerta en particular.")]
    [Min(0)]
    [SerializeField]
    private int keysRequired = 1;

    [SerializeField]
    private GameObject promptPanelPreFab;

    // Hijo que rota para simular la bisagra al abrirse. Si se deja vacío, la puerta
    // igual se "abre" (deja de bloquear el paso) pero sin animación de giro.
    [SerializeField]
    private Transform doorPivot;

    [SerializeField]
    private float openAngle = 100f;

    [SerializeField]
    private float openDuration = 1f;

    private GameObject promptPanel;
    private TextMeshProUGUI promptText;
    private GameObject player = null;
    private bool inRange = false;
    private bool isOpen = false;

    void Awake()
    {
        promptPanel = Instantiate(promptPanelPreFab);
        promptText = promptPanel.GetComponentInChildren<TextMeshProUGUI>();
        promptPanel.SetActive(false);
    }

    void Update()
    {
        // Una vez abierta, la puerta queda así por el resto de la partida: no hay más que revisar.
        if (isOpen || !inRange) return;

        UpdatePrompt();
        CheckOpenInput();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            inRange = true;
            player = other.gameObject;

            if (!isOpen)
            {
                promptPanel.SetActive(true);
                UpdatePrompt();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            inRange = false;
            promptPanel.SetActive(false);
        }
    }

    void UpdatePrompt()
    {
        promptText.text = GameManager.KeyCount >= keysRequired
            ? "Press E to open"
            : $"Keys to open door {GameManager.KeyCount} / {keysRequired}";
    }

    void CheckOpenInput()
    {
        if (GameManager.KeyCount < keysRequired) return;
        if (!Keyboard.current[player.GetComponent<PlayerHandler>().PickUpKey].wasPressedThisFrame) return;

        GameManager.SpendKeys(keysRequired);
        Open();
    }

    void Open()
    {
        isOpen = true;
        promptPanel.SetActive(false);
        AudioManager.Play(AudioClipName.DoorOpenSound, AudioChannel.Game);

        if (doorPivot != null)
        {
            StartCoroutine(SwingOpen());
        }
    }

    IEnumerator SwingOpen()
    {
        Quaternion startRotation = doorPivot.localRotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0f, openAngle, 0f);
        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            doorPivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, elapsed / openDuration);
            yield return null;
        }

        doorPivot.localRotation = targetRotation;
    }
}
