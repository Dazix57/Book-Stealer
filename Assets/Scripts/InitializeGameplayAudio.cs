using UnityEngine;

public class InitializeGameplayAudio : MonoBehaviour
{
    void Start()
    {
        AudioManager.ResetAmbience();
        EnemyController.ResetChaseState();
    }

    void OnDestroy()
    {
        // La escena de gameplay es la única dueña de su theme song; al salir (cargar otra escena), se corta.
        AudioManager.StopMusic();
    }
}
