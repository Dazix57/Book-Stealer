using UnityEngine;
using UnityEngine.SceneManagement;

// Escena de introducción entre el menú principal y la escena de juego: reproduce StartMusic
// y, apenas termina, salta a la escena de juego.
public class IntroSceneController : MonoBehaviour
{
    [SerializeField]
    private string gameSceneName = "MAPAAVANZADOPUYO";

    void Start()
    {
        AudioManager.PlayMusic(AudioClipName.StartMusic);
        float duration = AudioManager.GetClip(AudioClipName.StartMusic).length;
        Invoke(nameof(LoadGameScene), duration);
    }

    void LoadGameScene()
    {
        // Avisa que la próxima carga de la escena de juego viene de la introducción, para que
        // el panel de tutorial (ver GameManager.RequestTutorial/ConsumePendingTutorial) sepa
        // que debe mostrarse. Restart/LoadCheckpoint recargan la escena de juego directamente,
        // sin pasar por acá, así que nunca vuelven a pedirlo.
        GameManager.RequestTutorial();
        SceneManager.LoadScene(gameSceneName);
    }
}
