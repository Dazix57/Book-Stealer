using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [SerializeField]
    private float speed;

    [SerializeField]
    private float mouseSensitivity;

    private Rigidbody rb;
    private BoxCollider collider;
    private Vector3 inputDirection;
    private float mouseX;
    private float yaw; // Rotación acumulada en el eje Y (horizontal)

    public bool IsParrying;
    public bool IsCrouching;

    public float PushForce;

    [SerializeField] private Key CrouchKey;
    [SerializeField] private Key ParryKey;
    [SerializeField] private Key PickUpKey;

    // Parry setup
    private float ParryDebounce = 0.0f;
    private float ParryCD = 5.0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Configuración necesaria para que resuelva colisiones correctamente
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Evita que la física rote el personaje al chocar contra otros objetos
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        mouseSensitivity = 0.7f;
        speed = 6f;
        collider = GetComponent<BoxCollider>();
        rb.linearDamping = 10f;
        PushForce = 40f;

        yaw = transform.eulerAngles.y;
        IsParrying = false;
        IsCrouching = false;

        // Oculta y bloquea el cursor en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Set up keybinds
        ParryKey = Key.F;
        CrouchKey = Key.LeftCtrl;

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

   
    private void OnTriggerStay(Collider collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            if (IsParrying == true && Time.time >= ParryDebounce)
            {
                ParryDebounce = Time.time + ParryCD;
                Rigidbody Enemy_RB = collision.GetComponent<Rigidbody>();

                // Check if the object has a valid Rigidbody that is not kinematic
                if (Enemy_RB == null ) return;

                Vector3 pushDirection = collision.transform.position - transform.position;
                pushDirection.y = 0; // Keep the push flat on the ground if needed
                pushDirection.Normalize();

                Debug.Log("outtie!");
                // Apply an instant force to send the box away
                Enemy_RB.AddForce(pushDirection * 1000f); //* Enemy_RB.mass * Enemy_RB.linearDamping);
            } else
            {
                Debug.Log("DIE!");
            }
            
        }
        
        // Example: Check if hitting an enemy using tags
        //if (collision.gameObject.CompareTag("Enemy"))
        //{
            // Handle damage or impact behavior
        //}
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

        // -- Detect parry input
        if (Keyboard.current[ParryKey].isPressed)
        {
            IsParrying = true;
            Debug.Log("parrying");
        } else
        {
            IsParrying = false;
        }

        // -- Detect crouch input
        if (Keyboard.current[CrouchKey].isPressed)
        {
            IsCrouching = true;
            Debug.Log("crouching");
        } else
        {
            IsCrouching = false;
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
        rb.AddForce(velocity*rb.linearDamping); //*Time.fixedDeltaTime);
        //rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }
    
}