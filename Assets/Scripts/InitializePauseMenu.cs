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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class InitializePauseMenu : MonoBehaviour
{
    private static InitializePauseMenu instance;

    [SerializeField]
    private GameObject pauseMenuPreFab;

    [SerializeField]
    private string mainMenuSceneName;

    private MenuOptionsEnum[] pauseMenuOptions = 
    (MenuOptionsEnum[]) Enum.GetValues(typeof(MenuOptionsEnum));

    private ConfirmationOptionsEnum[] confirmationMenuOptions = 
    (ConfirmationOptionsEnum[]) Enum.GetValues(typeof(ConfirmationOptionsEnum));


    private GameObject pauseMenu;
    private GameObject mainPanel;
    private GameObject confirmationPanel;
    private GameObject settingsPanel;
    private Volume pauseBlur;

    private bool isPausedMenuActive = false;
    private bool isConfirmationMenuActive = false;
    private bool isSettingsMenuActive = false;
    private int currentIndex = 0;
    private int lastIndex = 0;

    // Recuerda cuál de las dos opciones (LastCheckPoint o MainMenu) abrió el
    // panel de confirmación, ya que ambas comparten el mismo Yes/No.
    private MenuOptionsEnum pendingConfirmation;

    // Navegación por teclado dentro de Settings: fila 0 = volumen UI, 1 = volumen juego, 2 = Back.
    private UnityEngine.UI.Slider uiVolumeSlider;
    private UnityEngine.UI.Slider gameVolumeSlider;
    private TextMeshProUGUI[] settingsTexts;
    private int settingsIndex = 0;
    private const float settingsVolumeStep = 0.1f;

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
        settingsPanel = pauseMenu.transform.Find("PauseCanvas/SettingsPanel").gameObject;
        pauseBlur = pauseMenu.transform.Find("PauseBlur").GetComponent<Volume>();

        EnsureInputModule();
        AddHoverEvents(mainPanel, pauseMenuOptions);
        AddHoverEvents(confirmationPanel, confirmationMenuOptions);
        SetupSettingsPanel();
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isSettingsMenuActive)
            {
                CloseSettingsMenu();
            }
            else if (isPausedMenuActive && !isConfirmationMenuActive)
            {
                EnablePauseMenu(false);
                ResetSelection(mainPanel, pauseMenuOptions);
            }
            else
            {
                EnablePauseMenu(true);
            }
        }

        if (isSettingsMenuActive)
        {
            SettingsSelection();
        }
        else if (isConfirmationMenuActive)
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
        AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
    }
    else if (Keyboard.current.sKey.wasPressedThisFrame && currentIndex < options.Length - 1)
    {
        // Pone blanco cualquier opción que no sea la última (options[i] : i < options.Length)
        currentButton.color = Color.white;
        currentIndex++;
        // Pone amarillo la opción siguiente (options[i - 1])
        GetButtonText(panel, options[currentIndex]).color = Color.yellow;
        AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
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
        AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
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
        AudioManager.Play(AudioClipName.ButtonConfirmationSound, AudioChannel.UI);

        switch(currentSelection)
        {
            case MenuOptionsEnum.Continue:
            EnablePauseMenu(false);
            ResetSelection(mainPanel, pauseMenuOptions);
            break;

            case MenuOptionsEnum.Options:
            OpenSettingsMenu();
            break;

            case MenuOptionsEnum.LastCheckPoint:
            case MenuOptionsEnum.MainMenu:
            pendingConfirmation = (MenuOptionsEnum)(object)currentSelection;
            EnableConfirmationMenu(true);
            lastIndex = currentIndex;
            currentIndex = 0;
            isConfirmationMenuActive = true;
            break;

            case ConfirmationOptionsEnum.Yes:
            EnableConfirmationMenu(false);
            ResetSelection(confirmationPanel, confirmationMenuOptions, lastIndex);

            if (pendingConfirmation == MenuOptionsEnum.MainMenu)
            {
                GoToMainMenu();
            }
            else
            {
                Debug.Log("En proceso ... (Yes - LastCheckPoint)");
            }
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
            pauseBlur.enabled = true;
            Time.timeScale = 0;

            // Pausa todo el audio en reproducción (música, ambientes 3D como el de ObjectiveItem, etc.)
            // salvo los SFX de UI, que se marcan ignoreListenerPause en AudioManager para seguir sonando.
            AudioListener.pause = true;

            // Libera el cursor para poder usar el mouse en el menú
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
        else
        {
            // Desactiva el menu de pausa
            mainPanel.SetActive(enable);
            pauseBlur.enabled = false;
            Time.timeScale = 1;
            AudioListener.pause = false;

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

    void GoToMainMenu()
    {
        // Restaura tiempo, audio y cursor antes de salir; la escena del menú no se encarga de esto.
        Time.timeScale = 1;
        AudioListener.pause = false;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        // No tiene sentido mantener el PauseMenu (ni este singleton) vivo en el menú principal.
        Destroy(pauseMenu);
        Destroy(gameObject);

        SceneManager.LoadScene(mainMenuSceneName);
    }

    void SetupSettingsPanel()
    {
        uiVolumeSlider = settingsPanel.transform.Find("UIVolumeSlider").GetComponent<UnityEngine.UI.Slider>();
        gameVolumeSlider = settingsPanel.transform.Find("GameVolumeSlider").GetComponent<UnityEngine.UI.Slider>();

        uiVolumeSlider.onValueChanged.AddListener(AudioManager.SetUIVolume);
        gameVolumeSlider.onValueChanged.AddListener(AudioManager.SetGameVolume);

        UnityEngine.UI.Button backButton = settingsPanel.transform.Find("BackButton").GetComponent<UnityEngine.UI.Button>();
        backButton.onClick.AddListener(OnSettingsBackConfirmed);

        // Resalta la fila Back al pasar el mouse, igual que los botones del panel principal.
        EventTrigger trigger = backButton.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry hoverEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        hoverEntry.callback.AddListener((_) =>
        {
            settingsIndex = settingsTexts.Length - 1;
            HighlightSettingsIndex();
            AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
        });
        trigger.triggers.Add(hoverEntry);

        // Textos resaltables para la navegación por teclado: fila UI, fila Juego, Back.
        settingsTexts = new TextMeshProUGUI[]
        {
            settingsPanel.transform.Find("UIVolumeLabel").GetComponent<TextMeshProUGUI>(),
            settingsPanel.transform.Find("GameVolumeLabel").GetComponent<TextMeshProUGUI>(),
            settingsPanel.transform.Find("BackButton/Back").GetComponent<TextMeshProUGUI>()
        };
    }

    void OpenSettingsMenu()
    {
        isSettingsMenuActive = true;
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);

        // Refleja el volumen persistido cada vez que se abre, por si cambió desde otra escena.
        uiVolumeSlider.value = AudioManager.UIVolume;
        gameVolumeSlider.value = AudioManager.GameVolume;

        settingsIndex = 0;
        HighlightSettingsIndex();
    }

    void CloseSettingsMenu()
    {
        isSettingsMenuActive = false;
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    void OnSettingsBackConfirmed()
    {
        // Comparte el mismo sonido de confirmación que Continue/Options/LastCheckPoint/MainMenu/Yes/No.
        AudioManager.Play(AudioClipName.ButtonConfirmationSound, AudioChannel.UI);
        CloseSettingsMenu();
    }

    void HighlightSettingsIndex()
    {
        for (int i = 0; i < settingsTexts.Length; i++)
        {
            settingsTexts[i].color = i == settingsIndex ? Color.yellow : Color.white;
        }
    }

    void SettingsSelection()
    {
        if (Keyboard.current.wKey.wasPressedThisFrame && settingsIndex > 0)
        {
            settingsIndex--;
            HighlightSettingsIndex();
            AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
        }
        else if (Keyboard.current.sKey.wasPressedThisFrame && settingsIndex < settingsTexts.Length - 1)
        {
            settingsIndex++;
            HighlightSettingsIndex();
            AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
        }

        bool onVolumeRow = settingsIndex == 0 || settingsIndex == 1;

        if (onVolumeRow)
        {
            UnityEngine.UI.Slider slider = settingsIndex == 0 ? uiVolumeSlider : gameVolumeSlider;

            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                slider.value = Mathf.Clamp01(slider.value - settingsVolumeStep);
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                slider.value = Mathf.Clamp01(slider.value + settingsVolumeStep);
            }
        }
        else if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            OnSettingsBackConfirmed();
        }
    }

    public bool IsPausedMenuActive
    {
        get {return isPausedMenuActive;}
    }
}