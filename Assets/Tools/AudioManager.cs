using System.Collections;
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
    static AudioSource musicSource;
    static AudioSource damageSource;
    static float damageVolumeScale = 0f; // 0 a HP lleno, 1 cerca de 0 HP
    const float damageClipStartOffset = 2f; // BS_Damage trae ~2s de silencio al inicio
    static Dictionary<AudioClipName, AudioClip> audioClips = new Dictionary<AudioClipName, AudioClip>();

    const string UIVolumePrefKey = "UIVolume";
    const string GameVolumePrefKey = "GameVolume";
    static float uiVolume = 1f;
    static float gameVolume = 1f;
    static AudioChannel musicChannel = AudioChannel.UI;

    // Ambience escalation (por cantidad de parries) y override de persecución.
    static readonly AudioClipName[] ambienceLevels =
    {
        AudioClipName.BS_Ambience1,
        AudioClipName.BS_Ambience2,
        AudioClipName.BS_Ambience3,
        AudioClipName.BS_Ambience4,
        AudioClipName.BS_Ambience5,
    };
    static int ambienceIndex = 0;
    static int chaseCount = 0; // cuántos enemigos están persiguiendo al jugador ahora mismo

    const float musicFadeDuration = 0.5f;
    static AudioManagerRunner runner; // MonoBehaviour usado únicamente para poder correr el fade como coroutine
    static Coroutine musicFadeRoutine;

    // Componente vacío: existe solo para darle a esta clase estática un host de MonoBehaviour donde correr coroutines.
    private class AudioManagerRunner : MonoBehaviour { }
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
    /// Initializes the AudioManager with the given AudioSources. This method should be called once at the start of the game to set up audio playback.
    /// </summary>
    /// <param name="source"> The AudioSource used for one-shot SFX (button clicks, etc). </param>
    /// <param name="loopSource"> The AudioSource used for looping background music/theme songs. </param>
    /// <param name="damageLoopSource"> The AudioSource used for the looping damage sound (BS_Damage). </param>
    public static void Initialize(AudioSource source, AudioSource loopSource, AudioSource damageLoopSource)
    {
        /** Load all audio clips into the dictionary for easy access.
            Structure of the dictionary: Key = AudioClipName enum, Value = AudioClip loaded from folder 'Resources'.

            e.g:
            audioClips.Add(AudioClipName.[enumName], Resources.Load<AudioClip>(AudioClipName.[enumName].ToString()));

            [enumName] should be replaced with the actual name of the enum value corresponding to the audio clip you want to load.
        **/

        initialized = true;
        audioSource = source;
        musicSource = loopSource;
        musicSource.loop = true;
        damageSource = damageLoopSource;
        damageSource.loop = true;

        runner = musicSource.GetComponent<AudioManagerRunner>();
        if (runner == null)
        {
            runner = musicSource.gameObject.AddComponent<AudioManagerRunner>();
        }

        // Los SFX de UI (incluido el propio menú de pausa) deben seguir escuchándose
        // aunque AudioListener.pause silencie el resto del audio al pausar.
        audioSource.ignoreListenerPause = true;

        // Load all audio clips under this line.
        audioClips.Add(AudioClipName.ButtonSelectionSound, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.ButtonSelectionSound.ToString()));
        audioClips.Add(AudioClipName.ButtonConfirmationSound, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.ButtonConfirmationSound.ToString()));
        audioClips.Add(AudioClipName.PickUpSound, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.PickUpSound.ToString()));
        // El archivo de la llave y el de la puerta no siguen la convención "NombreDelEnum.ext"
        // de los demás SFX, así que se cargan por su nombre de archivo real en vez de ToString().
        audioClips.Add(AudioClipName.KeyPickUpSound, Resources.Load<AudioClip>("SoundEffects/key-twist-in-lock-47832"));
        audioClips.Add(AudioClipName.DoorOpenSound, Resources.Load<AudioClip>("SoundEffects/door_open"));
        audioClips.Add(AudioClipName.PlayerFootstep, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.PlayerFootstep.ToString()));
        audioClips.Add(AudioClipName.EnemyFootstep, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.EnemyFootstep.ToString()));
        audioClips.Add(AudioClipName.BS_Enemy1Idle, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.BS_Enemy1Idle.ToString()));
        audioClips.Add(AudioClipName.React1, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.React1.ToString()));
        audioClips.Add(AudioClipName.React2, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.React2.ToString()));
        audioClips.Add(AudioClipName.React3, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.React3.ToString()));
        audioClips.Add(AudioClipName.Parry, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.Parry.ToString()));
        audioClips.Add(AudioClipName.Comeback, Resources.Load<AudioClip>("SoundEffects/" + AudioClipName.Comeback.ToString()));

        audioClips.Add(AudioClipName.MenuTheme, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.MenuTheme.ToString()));
        audioClips.Add(AudioClipName.GameplayTheme, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.GameplayTheme.ToString()));
       
        audioClips.Add(AudioClipName.BS_Ambience1, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.BS_Ambience1.ToString()));
        audioClips.Add(AudioClipName.BS_Ambience2, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.BS_Ambience2.ToString()));
        audioClips.Add(AudioClipName.BS_Ambience3, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.BS_Ambience3.ToString()));
        audioClips.Add(AudioClipName.BS_Ambience4, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.BS_Ambience4.ToString()));
        audioClips.Add(AudioClipName.BS_Ambience5, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.BS_Ambience5.ToString()));
        audioClips.Add(AudioClipName.BS_Chase, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.BS_Chase.ToString()));
        audioClips.Add(AudioClipName.BS_Damage, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.BS_Damage.ToString()));

        damageSource.clip = audioClips[AudioClipName.BS_Damage];

        uiVolume = PlayerPrefs.GetFloat(UIVolumePrefKey, 1f);
        gameVolume = PlayerPrefs.GetFloat(GameVolumePrefKey, 1f);
    }

    /// <summary>
    /// Plays an audio clip based on the provided AudioClipName enum, scaled by that channel's volume.
    /// This method uses the AudioSource to play the clip without interrupting any currently playing audio.
    /// </summary>
    /// <param name="name"> The name of the audio file stored in the Resources folder. </param>
    /// <param name="channel"> Which volume slider (UI or Game) scales this clip. </param>
    /// <param name="volumeScale"> Extra multiplier on top of the channel volume (e.g. footsteps that should sound quieter/louder than usual). </param>
    public static void Play(AudioClipName name, AudioChannel channel = AudioChannel.Game, float volumeScale = 1f)
    {
        float volume = (channel == AudioChannel.UI ? uiVolume : gameVolume) * volumeScale;
        audioSource.PlayOneShot(audioClips[name], volume);
    }

    /// <summary>
    /// Returns the loaded AudioClip for a given name, for playback through a source other than
    /// the shared one-shot AudioSource (e.g. a local, spatialized source like footsteps).
    /// </summary>
    public static AudioClip GetClip(AudioClipName name)
    {
        return audioClips[name];
    }

    /// <summary>
    /// Returns the current volume multiplier (0-1) for a channel, so a local AudioSource can
    /// scale its own PlayOneShot calls consistently with the shared one.
    /// </summary>
    public static float GetChannelVolume(AudioChannel channel)
    {
        return channel == AudioChannel.UI ? uiVolume : gameVolume;
    }

    /// <summary>
    /// Plays an audio clip on loop through the dedicated music channel, scaled by the given channel's volume,
    /// stopping whatever was previously playing on it. Used for menu/gameplay theme songs.
    /// </summary>
    /// <param name="name"> The name of the audio file stored in the Resources folder. </param>
    /// <param name="channel"> Which volume slider (UI or Game) scales this track, and keeps scaling it live while it plays. </param>
    public static void PlayMusic(AudioClipName name, AudioChannel channel = AudioChannel.UI)
    {
        musicChannel = channel;
        float targetVolume = channel == AudioChannel.UI ? uiVolume : gameVolume;

        if (musicFadeRoutine != null)
        {
            runner.StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        if (musicSource.isPlaying)
        {
            musicFadeRoutine = runner.StartCoroutine(CrossfadeMusic(audioClips[name], targetVolume));
        }
        else
        {
            musicSource.clip = audioClips[name];
            musicSource.volume = targetVolume;
            musicSource.Play();
        }
    }

    // Baja el volumen a 0, cambia el clip, y sube el volumen al objetivo; medio segundo por tramo.
    static IEnumerator CrossfadeMusic(AudioClip clip, float targetVolume)
    {
        float startVolume = musicSource.volume;
        float t = 0f;
        while (t < musicFadeDuration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / musicFadeDuration);
            yield return null;
        }

        musicSource.volume = 0f;
        musicSource.clip = clip;
        musicSource.Play();

        t = 0f;
        while (t < musicFadeDuration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetVolume, t / musicFadeDuration);
            yield return null;
        }

        musicSource.volume = targetVolume;
        musicFadeRoutine = null;
    }

    /// <summary>
    /// Stops whatever is currently playing on the music channel.
    /// </summary>
    public static void StopMusic()
    {
        if (musicFadeRoutine != null)
        {
            runner.StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    /// <summary>
    /// Resets the ambience escalation and chase override, then starts playing the base ambience track.
    /// Should be called once when a gameplay scene starts (including reloads from a checkpoint).
    /// </summary>
    public static void ResetAmbience()
    {
        ambienceIndex = 0;
        chaseCount = 0;
        PlayMusic(ambienceLevels[ambienceIndex], AudioChannel.Game);
        StopDamage();
    }

    /// <summary>
    /// Starts (if not already playing) and updates the volume of the looping damage sound, based on
    /// the player's current health fraction (0-1). Silent at full health (1), full Game volume as
    /// health approaches 0.
    /// </summary>
    public static void PlayDamage(float healthFraction)
    {
        damageVolumeScale = Mathf.Clamp01(1f - healthFraction);
        damageSource.volume = damageVolumeScale * gameVolume;

        if (!damageSource.isPlaying)
        {
            damageSource.time = damageClipStartOffset;
            damageSource.Play();
        }
    }

    /// <summary>
    /// Stops the looping damage sound (e.g. the player stopped taking damage, or died).
    /// </summary>
    public static void StopDamage()
    {
        if (damageSource != null)
        {
            damageSource.Stop();
        }
    }

    /// <summary>
    /// Registers a successful parry: escalates the ambience track one step (capped at BS_Ambience5).
    /// If no enemy is currently chasing, the new ambience starts playing immediately; otherwise it
    /// will kick in as soon as the chase ends.
    /// </summary>
    public static void RegisterParry()
    {
        int previousIndex = ambienceIndex;
        ambienceIndex = Mathf.Min(ambienceIndex + 1, ambienceLevels.Length - 1);

        if (chaseCount == 0 && ambienceIndex != previousIndex)
        {
            PlayMusic(ambienceLevels[ambienceIndex], AudioChannel.Game);
        }
    }

    /// <summary>
    /// Called when an enemy starts actively chasing the player. Overrides whatever ambience is
    /// playing with BS_Chase. Additional enemies chasing at the same time are a no-op.
    /// </summary>
    public static void EnemyStartedChasing()
    {
        chaseCount++;
        if (chaseCount == 1)
        {
            PlayMusic(AudioClipName.BS_Chase, AudioChannel.Game);
        }
    }

    /// <summary>
    /// Called when an enemy gives up the chase and returns to patrolling. Once the last chasing
    /// enemy stops, restores the ambience track matching the current parry count.
    /// </summary>
    public static void EnemyStoppedChasing()
    {
        if (chaseCount == 0) return;

        chaseCount--;
        if (chaseCount == 0)
        {
            PlayMusic(ambienceLevels[ambienceIndex], AudioChannel.Game);
        }
    }

    /// <summary>
    /// Sets and persists the UI channel volume (0-1), used by clips played with AudioChannel.UI.
    /// </summary>
    public static void SetUIVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(UIVolumePrefKey, uiVolume);

        if (musicSource != null && musicChannel == AudioChannel.UI)
        {
            musicSource.volume = uiVolume;
        }
    }

    /// <summary>
    /// Sets and persists the Game channel volume (0-1), used by clips played with AudioChannel.Game.
    /// </summary>
    public static void SetGameVolume(float volume)
    {
        gameVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(GameVolumePrefKey, gameVolume);

        if (musicSource != null && musicChannel == AudioChannel.Game)
        {
            musicSource.volume = gameVolume;
        }

        if (damageSource != null && damageSource.isPlaying)
        {
            damageSource.volume = damageVolumeScale * gameVolume;
        }
    }
    #endregion
}