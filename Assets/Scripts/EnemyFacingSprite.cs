using UnityEngine;

public class EnemyFacingSprite : MonoBehaviour
{
    [SerializeField] private Sprite frontSprite;
    [SerializeField] private Sprite backSprite;

    private SpriteRenderer spriteRenderer;
    private Transform cameraTransform;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        Vector3 toCamera = cameraTransform.position - transform.position;
        bool cameraSeesFront = Vector3.Dot(transform.forward, toCamera) > 0f;

        spriteRenderer.sprite = cameraSeesFront ? frontSprite : backSprite;

        // Al ver el mismo quad desde su lado trasero, la textura se ve espejada;
        // flipX compensa esto para que el sprite de espalda no salga invertido.
        spriteRenderer.flipX = !cameraSeesFront;
    }
}
