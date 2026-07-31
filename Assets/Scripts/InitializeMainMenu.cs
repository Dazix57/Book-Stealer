using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InitializeMainMenu : MonoBehaviour
{
    // Escena de introducción (logo + StartMusic) que se carga al elegir "Play", antes de la
    // escena de juego -- ver IntroSceneController, que es quien hace el salto final.
    [SerializeField]
    private string introSceneName;

    private MainMenuOptionsEnum[] menuOptions =
    (MainMenuOptionsEnum[]) Enum.GetValues(typeof(MainMenuOptionsEnum));

    private GameObject buttonsPanel;
    private GameObject titlePanel;
    private GameObject settingsPanel;
    private GameObject creditsPanel;
    private Slider uiVolumeSlider;
    private Slider gameVolumeSlider;
    private Slider mouseSensitivitySlider;
    // Mismo orden que settingsTexts (sin contar la fila Atrás, que no es un slider): permite
    // indexar por settingsIndex en vez de encadenar ternarios por cada fila nueva.
    private Slider[] settingsSliders;
    private int currentIndex = 0;
    private Dictionary<MainMenuOptionsEnum, Color> defaultColors = new Dictionary<MainMenuOptionsEnum, Color>();
    private bool settingsOpen = false;
    private bool creditsOpen = false;

    // Navegación por teclado dentro de Settings: fila 0 = volumen UI, 1 = volumen juego,
    // 2 = sensibilidad de mouse, 3 = Atrás.
    private TextMeshProUGUI[] settingsTexts;
    private Color[] settingsDefaultColors;
    private int settingsIndex = 0;
    private const float settingsVolumeStep = 0.1f;

    void Awake()
    {
        buttonsPanel = transform.Find("Buttons").gameObject;
        titlePanel = transform.Find("Titulo").gameObject;
        settingsPanel = transform.Find("SettingsPanel").gameObject;
        creditsPanel = transform.Find("CreditsPanel").gameObject;

        EnsureInputModule();
        AddHoverEvents();
        SetupSettingsPanel();
        SetupCreditsPanel();

        // Guarda el color con el que viene cada texto desde el prefab, para poder
        // devolverlo tal cual al perder la selección en vez de forzar un color fijo.
        foreach (MainMenuOptionsEnum option in menuOptions)
        {
            defaultColors[option] = GetButtonText(option).color;
        }

        GetButtonText(menuOptions[currentIndex]).color = Color.white;
    }

    void Start()
    {
        AudioManager.PlayMusic(AudioClipName.MenuTheme);
    }

    void OnDestroy()
    {
        // La escena del menú es la única dueña de su theme song; al salir (cargar otra escena), se corta.
        AudioManager.StopMusic();
    }

    void Update()
    {
        if (settingsOpen)
        {
            SettingsSelection();
        }
        else if (creditsOpen)
        {
            CreditsSelection();
        }
        else
        {
            Selection();
        }
    }

    void Selection()
    {
        TextMeshProUGUI currentButton = GetButtonText(menuOptions[currentIndex]);

        if (Keyboard.current.wKey.wasPressedThisFrame && currentIndex > 0)
        {
            // Devuelve la opción actual a su color por defecto (options[i] : i > 0)
            currentButton.color = defaultColors[menuOptions[currentIndex]];
            currentIndex--;
            // Resalta en blanco la opción anterior (options[i - 1])
            GetButtonText(menuOptions[currentIndex]).color = Color.white;
            AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
        }
        else if (Keyboard.current.sKey.wasPressedThisFrame && currentIndex < menuOptions.Length - 1)
        {
            // Devuelve la opción actual a su color por defecto (options[i] : i < options.Length)
            currentButton.color = defaultColors[menuOptions[currentIndex]];
            currentIndex++;
            // Resalta en blanco la opción siguiente (options[i - 1])
            GetButtonText(menuOptions[currentIndex]).color = Color.white;
            AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
        }
        else if (currentIndex == 0)
        {
            currentButton.color = Color.white;
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
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();

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

        GetButtonText(menuOptions[currentIndex]).color = defaultColors[menuOptions[currentIndex]];
        currentIndex = hoverIndex;
        GetButtonText(menuOptions[currentIndex]).color = Color.white;
        AudioManager.Play(AudioClipName.ButtonSelectionSound, AudioChannel.UI);
    }

    void OnClickOption(int index)
    {
        // Sincroniza currentIndex/resaltado con el botón clickeado antes de confirmarlo
        OnHoverOption(index);
        ReadInput(menuOptions[index]);
    }

    void SetupSettingsPanel()
    {
        uiVolumeSlider = settingsPanel.transform.Find("UIVolumeSlider").GetComponent<Slider>();
        gameVolumeSlider = settingsPanel.transform.Find("GameVolumeSlider").GetComponent<Slider>();
        mouseSensitivitySlider = settingsPanel.transform.Find("MouseSensitivitySlider").GetComponent<Slider>();

        uiVolumeSlider.onValueChanged.AddListener(AudioManager.SetUIVolume);
        gameVolumeSlider.onValueChanged.AddListener(AudioManager.SetGameVolume);
        mouseSensitivitySlider.onValueChanged.AddListener(MouseSettings.SetSensitivity);

        settingsSliders = new Slider[] { uiVolumeSlider, gameVolumeSlider, mouseSensitivitySlider };

        settingsPanel.transform.Find("BackButton").GetComponent<Button>().onClick.AddListener(OnBackConfirmed);

        // Textos resaltables para la navegación por teclado: fila UI, fila Juego, fila Sensibilidad, Atrás.
        settingsTexts = new TextMeshProUGUI[]
        {
            settingsPanel.transform.Find("UIVolumeLabel").GetComponent<TextMeshProUGUI>(),
            settingsPanel.transform.Find("GameVolumeLabel").GetComponent<TextMeshProUGUI>(),
            settingsPanel.transform.Find("MouseSensitivityLabel").GetComponent<TextMeshProUGUI>(),
            settingsPanel.transform.Find("BackButton").GetComponentInChildren<TextMeshProUGUI>()
        };

        settingsDefaultColors = new Color[settingsTexts.Length];
        for (int i = 0; i < settingsTexts.Length; i++)
        {
            settingsDefaultColors[i] = settingsTexts[i].color;
        }
    }

    void OpenSettings()
    {
        settingsOpen = true;
        buttonsPanel.SetActive(false);
        titlePanel.SetActive(false);
        settingsPanel.SetActive(true);

        // Refleja los valores persistidos cada vez que se abre, por si cambiaron desde otra escena.
        uiVolumeSlider.value = AudioManager.UIVolume;
        gameVolumeSlider.value = AudioManager.GameVolume;
        mouseSensitivitySlider.value = MouseSettings.Sensitivity;

        settingsIndex = 0;
        HighlightSettingsIndex();
    }

    void CloseSettings()
    {
        settingsOpen = false;
        settingsPanel.SetActive(false);
        buttonsPanel.SetActive(true);
        titlePanel.SetActive(true);
    }

    void OnBackConfirmed()
    {
        // Comparte el mismo sonido de confirmación que Play/Settings/Credits/Exit.
        AudioManager.Play(AudioClipName.ButtonConfirmationSound, AudioChannel.UI);
        CloseSettings();
    }

    void HighlightSettingsIndex()
    {
        for (int i = 0; i < settingsTexts.Length; i++)
        {
            settingsTexts[i].color = i == settingsIndex ? Color.white : settingsDefaultColors[i];
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

        bool onSliderRow = settingsIndex < settingsSliders.Length;

        if (onSliderRow)
        {
            Slider slider = settingsSliders[settingsIndex];

            // Clamp contra el rango propio del slider (no siempre 0-1, ej. sensibilidad de mouse).
            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                slider.value = Mathf.Clamp(slider.value - settingsVolumeStep, slider.minValue, slider.maxValue);
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                slider.value = Mathf.Clamp(slider.value + settingsVolumeStep, slider.minValue, slider.maxValue);
            }
        }
        else if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            OnBackConfirmed();
        }
    }

    void SetupCreditsPanel()
    {
        creditsPanel.transform.Find("BackButton").GetComponent<Button>().onClick.AddListener(OnCreditsBackConfirmed);
    }

    void OpenCredits()
    {
        creditsOpen = true;
        buttonsPanel.SetActive(false);
        titlePanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    void CloseCredits()
    {
        creditsOpen = false;
        creditsPanel.SetActive(false);
        buttonsPanel.SetActive(true);
        titlePanel.SetActive(true);
    }

    void OnCreditsBackConfirmed()
    {
        AudioManager.Play(AudioClipName.ButtonConfirmationSound, AudioChannel.UI);
        CloseCredits();
    }

    void CreditsSelection()
    {
        // Único elemento interactuable del panel: Enter confirma "Atrás" igual que un click.
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            OnCreditsBackConfirmed();
        }
    }

    void ReadInput(MainMenuOptionsEnum currentSelection)
    {
        AudioManager.Play(AudioClipName.ButtonConfirmationSound, AudioChannel.UI);

        switch (currentSelection)
        {
            case MainMenuOptionsEnum.Play:
                // Arrancar desde el menú siempre es una partida nueva (no hay "Continuar"):
                // sin este reset, un área completada en una sesión anterior de este mismo
                // proceso (ej. el jugador murió, volvió al menú y le dio Play de nuevo)
                // quedaría marcada como completa para siempre, y su llave nunca volvería
                // a aparecer en la partida nueva.
                GameManager.ResetProgress();
                SceneManager.LoadScene(introSceneName);
                break;

            case MainMenuOptionsEnum.Settings:
                OpenSettings();
                break;

            case MainMenuOptionsEnum.Credits:
                OpenCredits();
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
