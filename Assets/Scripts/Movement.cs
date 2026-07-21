using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [SerializeField]
    private float speed = 10f;

    [SerializeField]
    private bool useRigidbody = false;

    [SerializeField]
    private Rigidbody rb;

    private Vector3 inputDirection;

    private void Awake()
    {
        if (useRigidbody && rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        ReadInput();
        if (!useRigidbody)
        {
            MoveTransform();
        }
    }

    private void FixedUpdate()
    {
        if (useRigidbody)
        {
            MoveRigidbody();
        }
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

    private void MoveTransform()
    {
        Vector3 move = (transform.forward * inputDirection.z + transform.right * inputDirection.x) * speed * Time.deltaTime;
        transform.position += move;
    }

    private void MoveRigidbody()
    {
        if (rb == null)
        {
            return;
        }

        Vector3 velocity = (transform.forward * inputDirection.z + transform.right * inputDirection.x) * speed;
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }
}
