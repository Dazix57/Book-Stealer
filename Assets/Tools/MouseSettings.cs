using UnityEngine;

// Sensibilidad del mouse, persistida entre sesiones (PlayerPrefs) igual que los volúmenes de
// AudioManager. El menú principal necesita poder mostrar/ajustar este valor sin que exista
// ningún PlayerHandler en esa escena, así que vive en un store estático en vez de un campo
// de instancia (que además se recrea de cero cada vez que la escena de juego se recarga).
public static class MouseSettings
{
    const string SensitivityPrefKey = "MouseSensitivity";
    public const float DefaultSensitivity = 0.7f;
    public const float MinSensitivity = 0.1f;
    public const float MaxSensitivity = 2f;

    static float sensitivity = PlayerPrefs.GetFloat(SensitivityPrefKey, DefaultSensitivity);

    public static float Sensitivity => sensitivity;

    public static void SetSensitivity(float value)
    {
        sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        PlayerPrefs.SetFloat(SensitivityPrefKey, sensitivity);
    }
}
