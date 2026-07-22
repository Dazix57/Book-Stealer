using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [SerializeField]
    private float speed = 10f;

    [SerializeField]
    private float mouseSensitivity = 2f;

    private Rigidbody rb;
    private BoxCollider collider;
    private Vector3 inputDirection;
    private float mouseX;
    private float yaw; // Rotación acumulada en el eje Y (horizontal)

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

        yaw = transform.eulerAngles.y;

        // Oculta y bloquea el cursor en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        RotateRigidbody();
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

        // Lee el movimiento horizontal del mouse
        mouseX = Mouse.current.delta.x.ReadValue() * mouseSensitivity;
    }

    private void RotateRigidbody()
    {
        yaw += mouseX;
        Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(targetRotation);
    }

    private void MoveRigidbody()
    {
        Vector3 velocity = (transform.forward * inputDirection.z + transform.right * inputDirection.x) * speed;
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }
}