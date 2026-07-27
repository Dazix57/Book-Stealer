using UnityEngine;

/// <summary>
/// Keeps an AudioSource's volume synced with AudioManager.GameVolume. For ambient/positional
/// sources that loop on their own (outside AudioManager.Play), so the gameplay volume slider
/// still affects them.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class GameVolumeAudioSource : MonoBehaviour
{
    private AudioSource audioSource;
    private float baseVolume;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        baseVolume = audioSource.volume;
    }

    void Update()
    {
        audioSource.volume = baseVolume * AudioManager.GameVolume;
    }
}
