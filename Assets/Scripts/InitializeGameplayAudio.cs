using UnityEngine;

public class InitializeGameplayAudio : MonoBehaviour
{
    void Start()
    {
        AudioManager.PlayMusic(AudioClipName.GameplayTheme, AudioChannel.Game);
    }

    void OnDestroy()
    {
        // La escena de gameplay es la única dueña de su theme song; al salir (cargar otra escena), se corta.
        AudioManager.StopMusic();
    }
}
