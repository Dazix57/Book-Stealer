using UnityEngine;

public class GameAudioSource : MonoBehaviour
{
    /// <summary>
    /// Initilizes the AudioManager with an AudioSource component attached to this GameObject.
    /// The GameObject will persist across scene loads using DontDestroyOnLoad.
    /// If the AudioManager is already initialized, this GameObject will be destroyed to avoid duplicates.
    /// </summary>
    void Awake()
    {
        if (!AudioManager.Initialized)
        {
            AudioSource audioSource = gameObject.AddComponent<AudioSource>();

            AudioManager.Initialize(audioSource);

            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}