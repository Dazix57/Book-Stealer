using System;
using System.Collections;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class InitializeMenu : MonoBehaviour
{
    private static InitializeMenu instance;

    [SerializeField]
    private GameObject pauseMenuPreFab;

    private MenuOptionsEnum[] pauseMenuOptions = 
    (MenuOptionsEnum[]) Enum.GetValues(typeof(MenuOptionsEnum));

    private ConfirmationOptionsEnum[] confirmationMenuOptions = 
    (ConfirmationOptionsEnum[]) Enum.GetValues(typeof(ConfirmationOptionsEnum));


    private GameObject pauseMenu;
    private GameObject mainPanel;
    private GameObject confirmationPanel;

    private bool isPausedMenuActive = false;
    private bool isConfirmationMenuActive = false;
    private int currentIndex = 0;
    private int lastIndex = 0;

    void Awake()
    {
        // Evita duplicados si esta escena se recarga o si el objeto ya existe
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        pauseMenu = Instantiate(pauseMenuPreFab, Camera.main.transform.position, Quaternion.identity);
        DontDestroyOnLoad(pauseMenu);

        mainPanel = pauseMenu.transform.Find("PauseCanvas/PausePanel").gameObject;
        confirmationPanel = pauseMenu.transform.Find("PauseCanvas/ConfirmationPanel").gameObject;

        EnsureInputModule();
        AddHoverEvents(mainPanel, pauseMenuOptions);
        AddHoverEvents(confirmationPanel, confirmationMenuOptions);
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPausedMenuActive && !isConfirmationMenuActive)
            {
                EnablePauseMenu(false);
                ResetSelection(mainPanel, pauseMenuOptions);
            }
            else
            {
                EnablePauseMenu(true);
            }
        }

        if (isConfirmationMenuActive)
        {
            Selection(confirmationPanel, confirmationMenuOptions);
        }
        else if (isPausedMenuActive)
        {
            Selection(mainPanel, pauseMenuOptions);
        }
    }

    void Selection<T>(GameObject panel, T[] options)
    {
    TextMeshProUGUI currentButton = GetButtonText(panel, options[currentIndex]);

    if (Keyboard.current.wKey.wasPressedThisFrame && currentIndex > 0)
    {
        // Pone blanco culquier opción que no sea la primera (options[i] : i > 0) 
        currentButton.color = Color.white;
        currentIndex--;
        // Pone amarillo la opción anterior (options[i - 1])
        GetButtonText(panel, options[currentIndex]).color = Color.yellow;
    }
    else if (Keyboard.current.sKey.wasPressedThisFrame && currentIndex < options.Length - 1)
    {
        // Pone blanco cualquier opción que no sea la última (options[i] : i < options.Length)
        currentButton.color = Color.white;
        currentIndex++;
        // Pone amarillo la opción siguiente (options[i - 1])
        GetButtonText(panel, options[currentIndex]).color = Color.yellow;
    }
    else if (currentIndex == 0)
        {
            currentButton.color = Color.yellow;
        }

    if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            ReadInput(options[currentIndex]);
        }
    }

    void ResetSelection<T>(GameObject panel, T[] options, int resetToIndex = 0)
    {
        // Pone en blanco el botón que estaba resaltado antes de resetear
        GetButtonText(panel, options[currentIndex]).color = Color.white;

        // Reinicia el índice al valor indicado (por defecto 0, salvo que se especifique otro)
        currentIndex = resetToIndex;

        // Apaga únicamente la bandera correspondiente al panel que se está reseteando,
        // sin afectar el estado del otro menú (ej. confirmación se cierra sin cerrar pausa)
        if (panel == confirmationPanel)
        {
            isConfirmationMenuActive = false;
        }
        else if (panel == mainPanel)
        {
            isPausedMenuActive = false;
        }
    }

    TextMeshProUGUI GetButtonText<T>(GameObject panel, T option)
    {
        // Devuelve el boton del canvas perteneciente al panel respectivo
        return panel.transform.Find($"{option}Button/{option}").gameObject.GetComponent<TextMeshProUGUI>();
    }

    void EnsureInputModule()
    {
        // El EventSystem del prefab no trae ningún Input Module asignado;
        // sin uno, ningún evento de puntero (PointerEnter, etc.) se dispara jamás.
        GameObject eventSystemObj = pauseMenu.transform.Find("PauseEventSystem").gameObject;

        if (eventSystemObj.GetComponent<InputSystemUIInputModule>() == null)
        {
            InputSystemUIInputModule inputModule = eventSystemObj.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
    }

    void AddHoverEvents<T>(GameObject panel, T[] options)
    {
        for (int i = 0; i < options.Length; i++)
        {
            int index = i; // copia local: evita que el closure capture la misma "i" en todas las iteraciones

            GameObject buttonObj = panel.transform.Find($"{options[i]}Button").gameObject;
            EventTrigger trigger = buttonObj.AddComponent<EventTrigger>();

            EventTrigger.Entry hoverEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hoverEntry.callback.AddListener((_) => OnHoverOption(panel, options, index));
            trigger.triggers.Add(hoverEntry);

            // El click solo llega aquí si el puntero está realmente sobre este botón
            // (lo resuelve el GraphicRaycaster), a diferencia de leer Mouse.current.leftButton
            // en Update(), que dispara sin importar dónde esté el mouse.
            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((_) => OnClickOption(panel, options, index));
            trigger.triggers.Add(clickEntry);
        }
    }

    bool IsPanelActive(GameObject panel)
    {
        // ConfirmationPanel no desactiva PausePanel al abrirse, así que ambos
        // quedan activos a la vez con distinta cantidad de opciones.
        return (panel == confirmationPanel && isConfirmationMenuActive) ||
               (panel == mainPanel && isPausedMenuActive && !isConfirmationMenuActive);
    }

    void OnHoverOption<T>(GameObject panel, T[] options, int hoverIndex)
    {
        if (!IsPanelActive(panel) || hoverIndex == currentIndex) return;

        GetButtonText(panel, options[currentIndex]).color = Color.white;
        currentIndex = hoverIndex;
        GetButtonText(panel, options[currentIndex]).color = Color.yellow;
    }

    void OnClickOption<T>(GameObject panel, T[] options, int index)
    {
        if (!IsPanelActive(panel)) return;

        // Sincroniza currentIndex/resaltado con el botón clickeado antes de confirmarlo
        OnHoverOption(panel, options, index);
        ReadInput(options[index]);
    }

    void ReadInput<T>(T currentSelection)
    {
        switch(currentSelection)
        {
            case MenuOptionsEnum.Continue:
            EnablePauseMenu(false);
            ResetSelection(mainPanel, pauseMenuOptions);
            break;

            case MenuOptionsEnum.Options:
            Debug.Log("En proceso ...");
            break;

            case MenuOptionsEnum.LastCheckPoint:
            case MenuOptionsEnum.MainMenu:
            EnableConfirmationMenu(true);
            lastIndex = currentIndex;
            currentIndex = 0;
            isConfirmationMenuActive = true;
            break;

            case ConfirmationOptionsEnum.Yes:
            Debug.Log("En proceso ... (Yes)");
            break;

            case ConfirmationOptionsEnum.No:
            EnableConfirmationMenu(false);
            ResetSelection(confirmationPanel, confirmationMenuOptions, lastIndex);
            Debug.Log("En proceso ... (No)");
            break;
        }
    }

    void EnablePauseMenu(bool enable)
    {
        isPausedMenuActive = enable;

        // Revisa si el menu de pausa esta activo
        if(isPausedMenuActive)
        {
            // Activa el menu de pausa
            mainPanel.SetActive(enable);
            Time.timeScale = 0;

            // Libera el cursor para poder usar el mouse en el menú
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
        else
        {
            // Desactiva el menu de pausa
            mainPanel.SetActive(enable);
            Time.timeScale = 1;

            // Vuelve a bloquear y ocultar el cursor para el control de cámara
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    void EnableConfirmationMenu(bool enable)
    {
        // Activa la ventana de confirmación
        confirmationPanel.SetActive(enable);
        Time.timeScale = 0;
    }
}