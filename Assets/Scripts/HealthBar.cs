using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;

    private void OnEnable()
    {
        EventManager.OnPlayerHealthChanged += UpdateHealthBar;
    }

    private void OnDisable()
    {
        EventManager.OnPlayerHealthChanged -= UpdateHealthBar;
    }

    private void UpdateHealthBar(float current, float max)
    {
        //healthSlider.maxValue = max;
        //healthSlider.value = current;
    }
}
