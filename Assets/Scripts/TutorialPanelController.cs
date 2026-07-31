using UnityEngine;
using UnityEngine.UI;

// Panel de mecánicas que se muestra una única vez, apenas se entra a la escena de juego viniendo
// de la escena de introducción (ver GameManager.RequestTutorial/ConsumePendingTutorial). Restart
// y LoadCheckpoint recargan la escena de juego directamente, sin pasar por la introducción, así
// que nunca vuelven a activarlo. Mientras está abierto, bloquea el movimiento/rotación del
// jugador y libera el cursor para poder clickear el botón.
public class TutorialPanelController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button continueButton;

    private PlayerHandler player;

    void Start()
    {
        if (!GameManager.ConsumePendingTutorial())
        {
            panel.SetActive(false);
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.GetComponent<PlayerHandler>() : null;

        if (player != null)
        {
            player.CanMove = false;
            player.CanRotate = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        panel.SetActive(true);
        continueButton.onClick.AddListener(ClosePanel);
    }

    void ClosePanel()
    {
        panel.SetActive(false);

        if (player != null)
        {
            player.CanMove = true;
            player.CanRotate = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
