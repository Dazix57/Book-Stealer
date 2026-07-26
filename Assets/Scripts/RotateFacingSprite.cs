using UnityEngine;

public class RotateFacingSprite : MonoBehaviour
{
    private SpriteRenderer sprite;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (sprite.enabled)
        {
            RotateTowardsCamera();
        }
    }

    void RotateTowardsCamera()
    {
        Camera mainCamera = Camera.main;

        sprite.transform.LookAt(mainCamera.transform.position);
    }
}
