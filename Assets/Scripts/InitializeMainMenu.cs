using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class InitializeMainMenu : MonoBehaviour
{
    [SerializeField]
    private string gameSceneName;

    private MainMenuOptionsEnum[] menuOptions =
    (MainMenuOptionsEnum[]) Enum.GetValues(typeof(MainMenuOptionsEnum));

    private GameObject buttonsPanel;
    private int currentIndex = 0;

    void Awake()
    {
        buttonsPanel = transform.Find("Buttons").gameObject;

        EnsureInputModule();
        AddHoverEvents();

        GetButtonText(menuOptions[currentIndex]).color = Color.yellow;
    }

    void Update()
    {
        Selection();
    }

    void Selection()
    {
        TextMeshProUGUI currentButton = GetButtonText(menuOptions[currentIndex]);

        if (Keyboard.current.wKey.wasPressedThisFrame && currentIndex > 0)
        {
            // Pone blanco culquier opción que no sea la primera (options[i] : i > 0)
            currentButton.color = Color.white;
            currentIndex--;
            // Pone amarillo la opción anterior (options[i - 1])
            GetButtonText(menuOptions[currentIndex]).color = Color.yellow;
        }
        else if (Keyboard.current.sKey.wasPressedThisFrame && currentIndex < menuOptions.Length - 1)
        {
            // Pone blanco cualquier opción que no sea la última (options[i] : i < options.Length)
            currentButton.color = Color.white;
            currentIndex++;
            // Pone amarillo la opción siguiente (options[i - 1])
            GetButtonText(menuOptions[currentIndex]).color = Color.yellow;
        }
        else if (currentIndex == 0)
        {
            currentButton.color = Color.yellow;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            ReadInput(menuOptions[currentIndex]);
        }
    }

    TextMeshProUGUI GetButtonText(MainMenuOptionsEnum option)
    {
        // A diferencia de PauseMenu, los botones del prefab Menu no renombran su hijo
        // de texto para que calce con la opción, así que se busca por componente.
        return buttonsPanel.transform.Find($"{option}Button").GetComponentInChildren<TextMeshProUGUI>();
    }

    void EnsureInputModule()
    {
        // La escena del menú no trae ningún Input Module asignado a su EventSystem;
        // sin uno, ningún evento de puntero (PointerEnter, etc.) se dispara jamás.
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();

        if (eventSystem != null && eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            InputSystemUIInputModule inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
    }

    void AddHoverEvents()
    {
        for (int i = 0; i < menuOptions.Length; i++)
        {
            int index = i; // copia local: evita que el closure capture la misma "i" en todas las iteraciones

            GameObject buttonObj = buttonsPanel.transform.Find($"{menuOptions[i]}Button").gameObject;
            EventTrigger trigger = buttonObj.AddComponent<EventTrigger>();

            EventTrigger.Entry hoverEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hoverEntry.callback.AddListener((_) => OnHoverOption(index));
            trigger.triggers.Add(hoverEntry);

            // El click solo llega aquí si el puntero está realmente sobre este botón
            // (lo resuelve el GraphicRaycaster), a diferencia de leer Mouse.current.leftButton
            // en Update(), que dispara sin importar dónde esté el mouse.
            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((_) => OnClickOption(index));
            trigger.triggers.Add(clickEntry);
        }
    }

    void OnHoverOption(int hoverIndex)
    {
        if (hoverIndex == currentIndex) return;

        GetButtonText(menuOptions[currentIndex]).color = Color.white;
        currentIndex = hoverIndex;
        GetButtonText(menuOptions[currentIndex]).color = Color.yellow;
    }

    void OnClickOption(int index)
    {
        // Sincroniza currentIndex/resaltado con el botón clickeado antes de confirmarlo
        OnHoverOption(index);
        ReadInput(menuOptions[index]);
    }

    void ReadInput(MainMenuOptionsEnum currentSelection)
    {
        switch (currentSelection)
        {
            case MainMenuOptionsEnum.Play:
                SceneManager.LoadScene(gameSceneName);
                break;

            case MainMenuOptionsEnum.Settings:
                Debug.Log("En proceso ...");
                break;

            case MainMenuOptionsEnum.Credits:
                Debug.Log("En proceso ...");
                break;

            case MainMenuOptionsEnum.Exit:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }
}
