using UnityEngine;

public class RotateFacingSprite : MonoBehaviour
{
    private SpriteRenderer sprite;

    void Start()
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

        // Durante una transicion de escena (ej. volver al menu), este objeto puede
        // seguir recibiendo Update() por un frame mas mientras Camera.main ya no
        // resuelve ninguna camara (la vieja se destruyo, la nueva aun no se activo).
        if (mainCamera == null) return;

        sprite.transform.LookAt(mainCamera.transform.position);
    }
}
