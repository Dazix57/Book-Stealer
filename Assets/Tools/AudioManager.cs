using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Class responsible for managing audio playback in the game. It uses a static AudioSource and a dictionary of AudioClips to play sounds.
/// </summary>
public static class AudioManager
{
    #region Fields
    static bool initialized = false;
    static AudioSource audioSource;
    static Dictionary<AudioClipName, AudioClip> audioClips = new Dictionary<AudioClipName, AudioClip>();
    #endregion

    #region Properties
    /// <summary>
    /// Indicates whether the AudioManager has been initialized. This is important to ensure that audio playback can occur without errors.
    /// </summary>
    public static bool Initialized
    {
        get { return initialized; }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Initializes the AudioManager with a given AudioSource. This method should be called once at the start of the game to set up audio playback.
    /// </summary>
    /// <param name="source"> The AudioSource component attached to the AudioManager GameObject. </param>
    public static void Initialize(AudioSource source)
    {
        /** Load all audio clips into the dictionary for easy access.
            Structure of the dictionary: Key = AudioClipName enum, Value = AudioClip loaded from folder 'Resources'.
            
            e.g:
            audioClips.Add(AudioClipName.[enumName], Resources.Load<AudioClip>(AudioClipName.[enumName].ToString()));
            
            [enumName] should be replaced with the actual name of the enum value corresponding to the audio clip you want to load.
        **/

        initialized = true;
        audioSource = source;

        // Load all audio clips under this line.

    }

    /// <summary>
    /// Plays an audio clip based on the provided AudioClipName enum. This method uses the AudioSource to play the clip without interrupting any currently playing audio.
    /// </summary>
    /// <param name="name"> The name of the audio file stored in the Resources folder. </param>
    public static void Play(AudioClipName name)
    {
        audioSource.PlayOneShot(audioClips[name]);
    }
    #endregion
}