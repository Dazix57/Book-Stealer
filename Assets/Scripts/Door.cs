using System.Collections;
using System.Collections.Generic;
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

    // Otras puertas que dan acceso a la MISMA habitación (ej. una habitación con dos entradas).
    // Al abrir esta puerta, las puertas vinculadas se abren también sin gastar llaves de nuevo.
    // Basta con enlazar en una sola dirección: en Awake se completa el enlace inverso automáticamente.
    [Tooltip("Puertas que dan acceso a la misma habitación. Al abrir esta, esas también se abren.")]
    [SerializeField]
    private List<Door> linkedDoors = new List<Door>();

    // Marcador (debe tener un SpriteRenderer propio) que señala esta puerta cuando es la
    // única que queda cerrada y el jugador ya tiene las llaves necesarias. A diferencia
    // del marcador de las áreas de objetivos, este NO depende de la tecla de "ver objetivo
    // más cercano" de PlayerHandler: por eso NO lleva tag "Mark" (no participa de ese
    // sistema, así ninguna revelación por TAB de otra zona puede apagarlo). Door prende y
    // apaga su SpriteRenderer directamente, y una vez activo queda fijo, sin parpadear,
    // hasta que la puerta se abre.
    [Tooltip("Marcador que queda fijo sobre esta puerta cuando es la única cerrada y el jugador ya tiene las llaves.")]
    [SerializeField]
    private GameObject lastDoorMarker;

    // Todas las puertas de la escena que siguen cerradas. Al quedar una sola, se le
    // activa el marcador para indicarle al jugador hacia dónde ir.
    private static readonly List<Door> closedDoors = new List<Door>();

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

        if (lastDoorMarker != null)
        {
            lastDoorMarker.SetActive(false);
        }

        LinkReciprocally();

        closedDoors.Add(this);
    }

    void OnDestroy()
    {
        // Se saca de la lista estática al destruirse (ej. al recargar la escena desde
        // un checkpoint), para que no queden referencias colgando a una puerta ya destruida.
        closedDoors.Remove(this);
    }

    // Asegura que el enlace sea bidireccional aunque en el Inspector solo se haya
    // arrastrado esta puerta en la lista de la otra (o viceversa), para que abrir
    // cualquiera de las dos siempre abra ambas.
    void LinkReciprocally()
    {
        foreach (Door linkedDoor in linkedDoors)
        {
            if (linkedDoor != null && !linkedDoor.linkedDoors.Contains(this))
            {
                linkedDoor.linkedDoors.Add(this);
            }
        }
    }

    void Update()
    {
        // Una vez abierta, la puerta queda así por el resto de la partida: no hay más que revisar.
        if (isOpen) return;

        // Corre para CUALQUIER puerta sin abrir (no solo la marcada ni solo estando cerca):
        // si esta es la última cerrada y el jugador acaba de juntar en OTRA parte del mapa
        // la llave que le faltaba, el marcador tiene que prenderse igual, sin depender de
        // que el jugador esté parado frente a esta puerta.
        UpdateLastDoorMarker();

        if (!inRange) return;

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
        // Corta la recursión: si una puerta vinculada nos abre de vuelta a nosotros
        // (o si el enlace incluye un ciclo), ya no hay nada que hacer.
        if (isOpen) return;

        isOpen = true;
        promptPanel.SetActive(false);
        AudioManager.Play(AudioClipName.DoorOpenSound, AudioChannel.Game);

        if (doorPivot != null)
        {
            StartCoroutine(SwingOpen());
        }

        closedDoors.Remove(this);
        RemoveDoorMarker();
        UpdateLastDoorMarker();
        GameManager.MarkDoorOpened(gameObject.name);

        foreach (Door linkedDoor in linkedDoors)
        {
            if (linkedDoor != null)
            {
                linkedDoor.Open();
            }
        }
    }

    // Aplica el estado de "abierta" sin sonido ni giro de bisagra: se usa al restaurar un
    // checkpoint (ver GameManager.OnCheckpointSceneLoaded) para puertas que ya se habían
    // abierto en una sesión anterior, así no sorprende al jugador con la animación de apertura.
    // No hace falta propagar a linkedDoors ni volver a reportar a GameManager: cada puerta
    // vinculada que en su momento se abrió quedó guardada con su propio nombre.
    public void RestoreOpenState()
    {
        if (isOpen) return;

        isOpen = true;
        promptPanel.SetActive(false);

        if (doorPivot != null)
        {
            doorPivot.localRotation *= Quaternion.Euler(0f, openAngle, 0f);
        }

        closedDoors.Remove(this);
        RemoveDoorMarker();
        UpdateLastDoorMarker();
    }

    // Se llama siempre que ESTA puerta se abre, tenga o no el marcador activo (si nunca
    // llegó a ser la última cerrada, esto solo limpia un objeto inactivo que ya no hace falta).
    void RemoveDoorMarker()
    {
        if (lastDoorMarker == null) return;

        Destroy(lastDoorMarker);
    }

    // Si solo queda una puerta cerrada en toda la escena Y el jugador ya tiene las llaves
    // que ESA puerta pide, prende el marcador y fuerza su SpriteRenderer a visible: queda
    // fijo sin depender de TAB ni de ningún timer de revelación, hasta que la puerta se
    // abra (RemoveDoorMarker lo destruye en ese momento). Si le siguen faltando llaves, lo
    // mantiene apagado. Como esta puerta es la única cerrada, la única forma de que el
    // conteo de llaves baje mientras tanto sería abriéndola a ella misma (lo que la saca de
    // closedDoors), así que nunca hace falta volver a apagar el marcador por perder llaves.
    static void UpdateLastDoorMarker()
    {
        if (closedDoors.Count != 1) return;

        Door lastDoor = closedDoors[0];
        if (lastDoor.lastDoorMarker == null) return;

        bool hasEnoughKeys = GameManager.KeyCount >= lastDoor.keysRequired;
        lastDoor.lastDoorMarker.SetActive(hasEnoughKeys);

        if (hasEnoughKeys)
        {
            SpriteRenderer markerSprite = lastDoor.lastDoorMarker.GetComponent<SpriteRenderer>();
            if (markerSprite != null)
            {
                markerSprite.enabled = true;
            }
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
