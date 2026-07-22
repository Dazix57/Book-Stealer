using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class InitializeMenu : MonoBehaviour
{
    private static InitializeMenu instance;

    [SerializeField]
    private GameObject pauseMenuPreFab;

    private MenuOptionsEnum[] options = (MenuOptionsEnum[]) Enum.GetValues(typeof(MenuOptionsEnum));

    private GameObject pauseMenu;
    private GameObject mainPanel;
    private GameObject confirmationPanel;

    private int currentSelection = 0;

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
    }

    void Update()
    {
        if (mainPanel == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame && !mainPanel.activeSelf)
        {
            mainPanel.SetActive(true);
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame && mainPanel.activeSelf)
        {
            mainPanel.SetActive(false);
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame && mainPanel.activeSelf && confirmationPanel.activeSelf)
        {
            confirmationPanel.SetActive(false);
        }

        if (mainPanel.activeSelf)
        {
            ReadInput();
        }
    }

    void ReadInput()
    {

        if (Keyboard.current.wKey.wasPressedThisFrame && currentSelection > 0)
        {
            currentSelection -= 1;
        }
        else if (Keyboard.current.sKey.wasPressedThisFrame && currentSelection < options.Length)
        {
            currentSelection += 1;
        }

        switch(options[currentSelection])
        {
            case MenuOptionsEnum.None:
            return;

            case MenuOptionsEnum.Continue:
            TextMeshProUGUI continueButton = 
            mainPanel.transform.Find($"ContinueButton/{options[currentSelection].ToString()}").gameObject.GetComponent<TextMeshProUGUI>();
            continueButton.color = Color.yellow;
            break;

            
            case MenuOptionsEnum.Options:
            TextMeshProUGUI optionsButton = 
            mainPanel.transform.Find($"OptionsButton/{options[currentSelection].ToString()}").gameObject.GetComponent<TextMeshProUGUI>();
            optionsButton.color = Color.yellow;
            break;

            
            case MenuOptionsEnum.LastCheckPoint:
            TextMeshProUGUI lastCheckpointButton = 
            mainPanel.transform.Find($"LastCheckPointButton/{options[currentSelection].ToString()}").gameObject.GetComponent<TextMeshProUGUI>();
            lastCheckpointButton.color = Color.yellow;
            break;

            
            case MenuOptionsEnum.MainMenu:
            TextMeshProUGUI mainMenuButton = 
            mainPanel.transform.Find($"MainMenuButton/{options[currentSelection].ToString()}").gameObject.GetComponent<TextMeshProUGUI>();
            mainMenuButton.color = Color.yellow;
            break;


            case MenuOptionsEnum.Exit:
            TextMeshProUGUI exitButton = 
            mainPanel.transform.Find($"ExitButton/{options[currentSelection].ToString()}").gameObject.GetComponent<TextMeshProUGUI>();
            exitButton.color = Color.yellow;
            break;
        }
    }
}