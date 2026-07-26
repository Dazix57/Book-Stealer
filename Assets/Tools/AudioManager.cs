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

    const string UIVolumePrefKey = "UIVolume";
    const string GameVolumePrefKey = "GameVolume";
    static float uiVolume = 1f;
    static float gameVolume = 1f;
    #endregion

    #region Properties
    /// <summary>
    /// Indicates whether the AudioManager has been initialized. This is important to ensure that audio playback can occur without errors.
    /// </summary>
    public static bool Initialized
    {
        get { return initialized; }
    }

    /// <summary>
    /// Volume multiplier (0-1) applied to clips played on the UI channel (menus, buttons).
    /// </summary>
    public static float UIVolume
    {
        get { return uiVolume; }
    }

    /// <summary>
    /// Volume multiplier (0-1) applied to clips played on the Game channel (gameplay SFX).
    /// </summary>
    public static float GameVolume
    {
        get { return gameVolume; }
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
        audioClips.Add(AudioClipName.ButtonSelectionSound, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.ButtonSelectionSound.ToString()));
        audioClips.Add(AudioClipName.ButtonConfirmationSound, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.ButtonConfirmationSound.ToString()));


        uiVolume = PlayerPrefs.GetFloat(UIVolumePrefKey, 1f);
        gameVolume = PlayerPrefs.GetFloat(GameVolumePrefKey, 1f);
    }

    /// <summary>
    /// Plays an audio clip based on the provided AudioClipName enum, scaled by that channel's volume.
    /// This method uses the AudioSource to play the clip without interrupting any currently playing audio.
    /// </summary>
    /// <param name="name"> The name of the audio file stored in the Resources folder. </param>
    /// <param name="channel"> Which volume slider (UI or Game) scales this clip. </param>
    public static void Play(AudioClipName name, AudioChannel channel = AudioChannel.Game)
    {
        float volume = channel == AudioChannel.UI ? uiVolume : gameVolume;
        audioSource.PlayOneShot(audioClips[name], volume);
    }

    /// <summary>
    /// Sets and persists the UI channel volume (0-1), used by clips played with AudioChannel.UI.
    /// </summary>
    public static void SetUIVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(UIVolumePrefKey, uiVolume);
    }

    /// <summary>
    /// Sets and persists the Game channel volume (0-1), used by clips played with AudioChannel.Game.
    /// </summary>
    public static void SetGameVolume(float volume)
    {
        gameVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(GameVolumePrefKey, gameVolume);
    }
    #endregion
}