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

    // Dos fuentes para la música/ambience: permiten que, al cambiar de pista (ambience <-> chase),
    // una baje mientras la otra sube al mismo tiempo, en vez de silenciar del todo antes de arrancar
    // la siguiente. activeMusicSource es la que suena "al frente" en cada momento.
    static AudioSource musicSourceA;
    static AudioSource musicSourceB;
    static AudioSource activeMusicSource;
    static AudioClipName currentMusicClip;

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

    const float musicFadeDuration = 1f; // duración del crossfade (ambas pistas se mueven a la vez, no una tras otra)

    // Boost aplicado a los pasos y sonidos propios del enemigo (reacciones/parry/comeback) mientras
    // está en chase (ver EnemyController.InChase): se multiplica sobre el volumen base del clip,
    // pero nunca puede pasar de ChaseBoostCeiling, así que nada queda "reventado" aunque su volumen
    // base ya estuviera alto (ver GetMixedVolume). La música de chase (BS_Chase) ya suena fuerte por
    // su propio volumen base (ver GetClipVolume) y no pasa por este boost.
    const float ChaseBoostMultiplier = 1.4f;
    const float ChaseBoostCeiling = 0.95f;
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

        musicSourceA = loopSource;
        musicSourceA.loop = true;
        musicSourceB = loopSource.gameObject.AddComponent<AudioSource>();
        musicSourceB.loop = true;
        musicSourceB.volume = 0f;
        activeMusicSource = musicSourceA;

        damageSource = damageLoopSource;
        damageSource.loop = true;

        runner = musicSourceA.GetComponent<AudioManagerRunner>();
        if (runner == null)
        {
            runner = musicSourceA.gameObject.AddComponent<AudioManagerRunner>();
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
        audioClips.Add(AudioClipName.ProximitySound, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.ProximitySound.ToString()));

        audioClips.Add(AudioClipName.MenuTheme, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.MenuTheme.ToString()));
        audioClips.Add(AudioClipName.StartMusic, Resources.Load<AudioClip>("AmbientSounds/" + AudioClipName.StartMusic.ToString()));
        // El archivo no sigue la convención "NombreDelEnum.ext" (mismo caso que KeyPickUpSound/DoorOpenSound).
        audioClips.Add(AudioClipName.JumpscareDeathSound, Resources.Load<AudioClip>("SoundEffects/jumpScare01"));
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
    /// Volumen base (0-1) propio de cada clip dentro de su canal: evita que todo se reproduzca al
    /// tope y se empaste/distorsione al sonar varios sonidos a la vez. Los de persecución y los que
    /// produce el enemigo (reacciones, parry) pegan más fuerte, sin llegar al máximo; los pasos
    /// quedan deliberadamente sutiles; el resto queda en un rango medio.
    /// </summary>
    static float GetClipVolume(AudioClipName name)
    {
        switch (name)
        {
            case AudioClipName.React1:
            case AudioClipName.React2:
            case AudioClipName.React3:
            case AudioClipName.Parry:
            case AudioClipName.JumpscareDeathSound:
                return 0.9f;
            case AudioClipName.ProximitySound:
                return 0.85f; // duro cuando el jugador está cerca, pero por debajo de reacciones/chase para no reventar
            case AudioClipName.BS_Chase:
                return 0.95f;
            case AudioClipName.Comeback:
            case AudioClipName.DoorOpenSound:
                return 0.8f;
            case AudioClipName.PickUpSound:
            case AudioClipName.KeyPickUpSound:
            case AudioClipName.MenuTheme:
            case AudioClipName.GameplayTheme:
            case AudioClipName.StartMusic:
                return 0.75f;
            case AudioClipName.BS_Damage:
                return 0.7f;
            case AudioClipName.ButtonConfirmationSound:
                return 0.55f;
            case AudioClipName.ButtonSelectionSound:
                return 0.5f;
            case AudioClipName.BS_Ambience1:
            case AudioClipName.BS_Ambience2:
            case AudioClipName.BS_Ambience3:
            case AudioClipName.BS_Ambience4:
            case AudioClipName.BS_Ambience5:
            case AudioClipName.BS_Enemy1Idle:
                return 0.65f;
            case AudioClipName.EnemyFootstep:
                return 0.55f;
            case AudioClipName.PlayerFootstep:
                return 0.45f;
            default:
                return 1f;
        }
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
        float volume = (channel == AudioChannel.UI ? uiVolume : gameVolume) * volumeScale * GetClipVolume(name);
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
    /// Volumen final (0-1) para reproducir un clip dado en un canal dado desde una fuente propia
    /// (pasos, reacciones de enemigo, etc.): combina el slider de ese canal con el volumen base
    /// del clip (ver GetClipVolume), para que esas fuentes locales respeten la misma mezcla que Play()/PlayMusic().
    /// </summary>
    /// <param name="chaseBoost">
    /// True mientras el enemigo que reproduce este clip está en chase (ver EnemyController.InChase):
    /// refuerza el volumen base del clip (ChaseBoostMultiplier) sin superar nunca ChaseBoostCeiling,
    /// para que los pasos y sonidos del enemigo peguen más fuerte al ser perseguido, sin reventarse.
    /// </param>
    public static float GetMixedVolume(AudioClipName name, AudioChannel channel, bool chaseBoost = false)
    {
        float clipVolume = GetClipVolume(name);
        if (chaseBoost)
        {
            clipVolume = Mathf.Min(clipVolume * ChaseBoostMultiplier, ChaseBoostCeiling);
        }
        return GetChannelVolume(channel) * clipVolume;
    }

    /// <summary>
    /// Plays an audio clip on loop through the dedicated music channel, scaled by the given channel's volume,
    /// crossfading out whatever was previously playing on it. Used for ambience/chase/menu-theme tracks.
    /// </summary>
    /// <param name="name"> The name of the audio file stored in the Resources folder. </param>
    /// <param name="channel"> Which volume slider (UI or Game) scales this track, and keeps scaling it live while it plays. </param>
    public static void PlayMusic(AudioClipName name, AudioChannel channel = AudioChannel.UI)
    {
        musicChannel = channel;
        currentMusicClip = name;
        float targetVolume = GetChannelVolume(channel) * GetClipVolume(name);

        if (musicFadeRoutine != null)
        {
            runner.StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        if (activeMusicSource.isPlaying)
        {
            AudioSource outgoing = activeMusicSource;
            AudioSource incoming = activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;
            musicFadeRoutine = runner.StartCoroutine(CrossfadeMusic(outgoing, incoming, audioClips[name], targetVolume));
        }
        else
        {
            activeMusicSource.clip = audioClips[name];
            activeMusicSource.volume = targetVolume;
            activeMusicSource.Play();
        }
    }

    /// <summary>
    /// Como PlayMusic, pero sin crossfade: corta lo que sonaba y arranca la pista nueva ya a su
    /// volumen final, en el mismo instante. Se usa para el arranque de BS_Chase, que tiene que
    /// sonar exactamente en sincro con el golpe visual del jumpscare de persecución (EnemyController.
    /// ChaseJumpscare) — un fade de un segundo se sentiría desacoplado de un golpe que es instantáneo.
    /// </summary>
    public static void PlayMusicImmediate(AudioClipName name, AudioChannel channel = AudioChannel.Game)
    {
        musicChannel = channel;
        currentMusicClip = name;
        float targetVolume = GetChannelVolume(channel) * GetClipVolume(name);

        if (musicFadeRoutine != null)
        {
            runner.StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        // Por si había un crossfade a medio camino, deja la otra fuente completamente muda:
        // solo debe sonar activeMusicSource, ya al volumen final.
        AudioSource other = activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;
        other.Stop();
        other.volume = 0f;

        activeMusicSource.clip = audioClips[name];
        activeMusicSource.volume = targetVolume;
        activeMusicSource.Play();
    }

    // Cruza las dos pistas al mismo tiempo: la saliente baja de su volumen actual a 0 mientras la
    // entrante sube de 0 al volumen objetivo, ambas en el mismo tramo (no una detrás de la otra).
    // Así, al terminar una persecución, el chase se apaga suavemente ("fade out ligero") a la vez
    // que el ambience/theme reaparece hasta su volumen óptimo, sin hueco de silencio ni corte seco.
    static IEnumerator CrossfadeMusic(AudioSource outgoing, AudioSource incoming, AudioClip clip, float targetVolume)
    {
        float outgoingStartVolume = outgoing.volume;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();

        float t = 0f;
        while (t < musicFadeDuration)
        {
            t += Time.deltaTime;
            float progress = t / musicFadeDuration;
            outgoing.volume = Mathf.Lerp(outgoingStartVolume, 0f, progress);
            incoming.volume = Mathf.Lerp(0f, targetVolume, progress);
            yield return null;
        }

        outgoing.volume = 0f;
        outgoing.Stop();
        outgoing.clip = null;
        incoming.volume = targetVolume;

        activeMusicSource = incoming;
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

        if (musicSourceA != null) musicSourceA.Stop();
        if (musicSourceB != null) musicSourceB.Stop();
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
        damageSource.volume = damageVolumeScale * gameVolume * GetClipVolume(AudioClipName.BS_Damage);

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
    /// Called when an enemy starts actively chasing the player. Overrides whatever ambience/theme is
    /// playing with BS_Chase immediately (no crossfade), in sync with the chase jumpscare's visual
    /// hit. Additional enemies chasing at the same time are a no-op.
    /// </summary>
    public static void EnemyStartedChasing()
    {
        chaseCount++;
        if (chaseCount == 1)
        {
            PlayMusicImmediate(AudioClipName.BS_Chase, AudioChannel.Game);
        }
    }

    /// <summary>
    /// Called when an enemy gives up the chase and returns to patrolling. Once the last chasing
    /// enemy stops, crossfades BS_Chase back out and restores the ambience track matching the
    /// current parry count (fading it back in to its optimal volume).
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

        if (activeMusicSource != null && musicChannel == AudioChannel.UI)
        {
            activeMusicSource.volume = uiVolume * GetClipVolume(currentMusicClip);
        }
    }

    /// <summary>
    /// Sets and persists the Game channel volume (0-1), used by clips played with AudioChannel.Game.
    /// </summary>
    public static void SetGameVolume(float volume)
    {
        gameVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(GameVolumePrefKey, gameVolume);

        if (activeMusicSource != null && musicChannel == AudioChannel.Game)
        {
            activeMusicSource.volume = gameVolume * GetClipVolume(currentMusicClip);
        }

        if (damageSource != null && damageSource.isPlaying)
        {
            damageSource.volume = damageVolumeScale * gameVolume * GetClipVolume(AudioClipName.BS_Damage);
        }
    }
    #endregion
}
