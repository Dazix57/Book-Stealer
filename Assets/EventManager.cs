using System;

public static class EventManager
{
    // Define your events using Action
    public static event Action OnPlayerDeath;
    public static event Action<int> OnScoreChanged; // Event with a parameter
    public static event Action<float, float> OnPlayerHealthChanged; // current, max


    // Trigger methods
    public static void RaisePlayerDeath()
    {
        // The ?.Invoke() syntax safely checks if anyone is listening before firing
        OnPlayerDeath?.Invoke();
    }

    //public static void RaiseScoreChanged(int value)
    //{
        //OnScoreChanged?.Invoke(value);
    //}

    public static void RaisePlayerHealthChanged(float current, float max)
    {
        OnPlayerHealthChanged?.Invoke(current, max);
    }
}