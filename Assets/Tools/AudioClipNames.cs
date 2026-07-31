using UnityEngine;

/// <summary>
/// Enum to represent the names of audio clips in the project.
/// </summary>
public enum AudioClipName
{
    // Add your audio clip names here
    ButtonSelectionSound,
    ButtonConfirmationSound,
    MenuTheme,
    GameplayTheme,
    PickUpSound,
    KeyPickUpSound,
    DoorOpenSound,
    BS_Ambience1,
    BS_Ambience2,
    BS_Ambience3,
    BS_Ambience4,
    BS_Ambience5,
    BS_Chase,
    BS_Damage,
    PlayerFootstep,
    EnemyFootstep,
    BS_Enemy1Idle,
    React1,
    React2,
    React3,
    Parry,
    Comeback,
    ProximitySound,
    // Nuevos valores SIEMPRE al final: Unity serializa este enum por su valor entero (posición),
    // no por nombre -- insertarlos en el medio corre el valor de todo lo que viene después,
    // rompiendo silenciosamente cualquier campo ya serializado en un prefab (ver footstepClip
    // en Player.prefab/Enemy01.prefab/Enemy02.prefab, e idleClip en los enemigos).
    StartMusic,
    JumpscareDeathSound,
}
