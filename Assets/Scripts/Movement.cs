using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [SerializeField]
    private float speed = 10f;

    private Rigidbody rb;
    private BoxCollider collider;
    private Vector3 inputDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();


        // Configuración necesaria para que resuelva colisiones correctamente
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Evita que la física rote el personaje al chocar contra otros objetos
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        collider = GetComponent<BoxCollider>();

    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        MoveRigidbody();
    }
    private void ReadInput()
    {
        inputDirection = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
        {
            inputDirection += Vector3.forward;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            inputDirection += Vector3.back;
        }

        if (Keyboard.current.aKey.isPressed)
        {
            inputDirection += Vector3.left;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            inputDirection += Vector3.right;
        }

        inputDirection = inputDirection.normalized;
    }

    private void MoveRigidbody()
    {
        Vector3 velocity = (transform.forward * inputDirection.z + transform.right * inputDirection.x) * speed;
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }
}