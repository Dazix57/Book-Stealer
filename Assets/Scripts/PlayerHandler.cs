using UnityEngine;
using System.Collections; // <-- This is the missing line required for IEnumerator
using System.Collections.Generic; // Required if you are using IEnumerator<T>
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

    // Health setup
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damagePerSecond = 25f;
    private float currentHealth;
    private bool isDead;

    // Sneak meter setup
    [SerializeField] private float sneakMultiplierBase = 0.5f;
    [SerializeField] private float sneakDrainRate = 0.1f;
    [SerializeField] private float sneakRegenRate = 0.05f;
    private float sneakMeter;

    // Only applies the sneak bonus while actively crouching; otherwise enemies see at full FOV.
    public float SneakFOVMultiplier => IsCrouching ? sneakMeter : 1f;

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

        // Health setup
        currentHealth = maxHealth;
        isDead = false;
        EventManager.RaisePlayerHealthChanged(currentHealth, maxHealth);

        // Sneak meter setup
        sneakMeter = sneakMultiplierBase;
    }

    private void Update()
    {
        ReadInput();
        UpdateSneakMeter();
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
                StartCoroutine(CooldownRoutine());
                // Check if the object has a valid Rigidbody that is not kinematic
                if (Enemy_RB == null ) return;

                Vector3 pushDirection = collision.transform.position - transform.position;
                pushDirection.y = 0; // Keep the push flat on the ground if needed
                pushDirection.Normalize();

                Debug.Log("Parried monster...");
                // Apply an instant force to send the box away
                Enemy_RB.AddForce(pushDirection * 5000f); //* Enemy_RB.mass * Enemy_RB.linearDamping);
            } else
            {
                TakeDamage(damagePerSecond * Time.fixedDeltaTime);
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
            
        } else
        {
            IsCrouching = false;
        }


        inputDirection = inputDirection.normalized;

        // Lee el movimiento horizontal del mouse
        mouseX = Mouse.current.delta.x.ReadValue() * mouseSensitivity;
    }

    private void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        EventManager.RaisePlayerHealthChanged(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            isDead = true;
            EventManager.RaisePlayerDeath();
        }
    }

    private void UpdateSneakMeter()
    {
        if (IsCrouching)
        {
            sneakMeter = Mathf.Min(1f, sneakMeter + sneakDrainRate * Time.deltaTime);
        }
        else
        {
            sneakMeter = Mathf.Max(sneakMultiplierBase, sneakMeter - sneakRegenRate * Time.deltaTime);
        }
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
    
    IEnumerator CooldownRoutine()
    {


        // Pause execution for the specified duration
        yield return new WaitForSeconds(ParryCD);

        Debug.Log("Parry Ready Again!");
    }
}