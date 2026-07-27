using UnityEngine;
using System.Collections; // <-- This is the missing line required for IEnumerator
using System.Collections.Generic; // Required if you are using IEnumerator<T>
using UnityEngine.InputSystem;
using Unity.VisualScripting;
using Unity.Mathematics;
using TMPro;
using Unity.AppUI.UI;

public class PlayerHandler : MonoBehaviour
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

    [SerializeField]
    private bool IsParrying;
    [SerializeField]
    private bool IsCrouching;

    [SerializeField]
    private bool canMove = true;
    [SerializeField]
    private bool canRotate = true;
    private bool inObjectiveArea = false;

    [SerializeField]
    private float PushForce;

    [SerializeField] private Key crouchKey;
    [SerializeField] private Key parryKey;
    [SerializeField] private Key pickUpKey;
    [SerializeField] private Key objectiveMark;

    // Parry setup
    private float ParryDebounce = 0.0f;
    private float ParryCD = 5.0f;

    // Health setup
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damagePerSecond = 75f;
    private float currentHealth;
    private bool isDead;

    // Sneak meter setup
    [SerializeField] private float sneakMultiplierBase = 0.5f;
    [SerializeField] private float sneakDrainRate = 0.1f;
    [SerializeField] private float sneakRegenRate = 0.05f;
    private float sneakMeter;

    private GameObject closestObjective = null;
    private Timer markerTimer = null;
    private Timer markerCooldown = null;
    [SerializeField] private float coolDownDuration;

    [SerializeField] private GameObject coolDownMessagePreFab;
    private GameObject coolDownMessage;

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
        parryKey = Key.F;
        crouchKey = Key.LeftCtrl;

        // Health setup
        currentHealth = maxHealth;
        isDead = false;
        EventManager.RaisePlayerHealthChanged(currentHealth, maxHealth);

        // Sneak meter setup
        sneakMeter = sneakMultiplierBase;

        // marker timer
        markerTimer = GetComponent<Timer>();

        // cooldown de activación
        markerCooldown = gameObject.AddComponent<Timer>();
        markerCooldown.Duration = coolDownDuration;

        // Instancia del prefab 'coolDownMessagePreFab'
        coolDownMessage = Instantiate<GameObject>(coolDownMessagePreFab, Camera.main.transform.position, Quaternion.identity);

    }

    private void Update()
    {
        // Deshabilita el player input si se esta en el menu de pausa
        if (!GetComponent<InitializePauseMenu>().IsPausedMenuActive)
        {
            ReadInput();
        }

        UpdateSneakMeter();

        // revisa si puede enseñar el marcador del objetivo
        EnableObjectiveMark();
    }

    private void FixedUpdate()
    {
        if (CanRotate) RotateRigidbody();
        if (CanMove) MoveRigidbody();
    }

    private void OnTriggerStay(Collider collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            if (IsParrying == true && Time.time >= ParryDebounce)
            {
                ParryDebounce = Time.time + ParryCD;
                EnemyController Enemy_Controller = collision.GetComponent<EnemyController>();
                StartCoroutine(CooldownRoutine());
                if (Enemy_Controller == null) return;

                Vector3 pushDirection = collision.transform.position - transform.position;
                pushDirection.y = 0; // Keep the push flat on the ground if needed
                pushDirection.Normalize();

                Debug.Log("Parried monster...");
                // Aturde al enemigo y lo aleja; retomará la búsqueda cuando pase el aturdimiento
                Enemy_Controller.Parry(pushDirection);
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
        if (Keyboard.current[parryKey].isPressed)
        {
            IsParrying = true;
            //Debug.Log("parrying");
        } else
        {
            IsParrying = false;
        }

        // -- Detect crouch input
        if (Keyboard.current[crouchKey].isPressed)
        {
            IsCrouching = true;
            
        } else
        {
            IsCrouching = false;
        }

        // Revisa que el jugador pueda visualizar el marcador
        if (Keyboard.current[objectiveMark].wasPressedThisFrame && !markerCooldown.Running)
        {
            CalculateObjectiveDistance();
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

    // Sincroniza el yaw acumulado con una rotación aplicada externamente
    // (ej. al esconderse), para que RotateRigidbody no salte de vuelta
    // al valor de yaw anterior en cuanto se reactive CanRotate.
    public void SetYawFromRotation(Quaternion rotation)
    {
        yaw = rotation.eulerAngles.y;
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

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("ObjectiveArea"))
        {
            inObjectiveArea = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("ObjectiveArea"))
        {
            inObjectiveArea = false;
        }
    }

    void CalculateObjectiveDistance()
    {
        // Revisa si NO esta en el area del objetivo
        if (!inObjectiveArea)
        {
            List<GameObject> objectives = new(GameObject.FindGameObjectsWithTag("ObjectiveArea"));
            Vector3 currentPosition = transform.position;
            float closestDistance = Mathf.Infinity;

            foreach (var area in objectives)
            {
                Vector3 areaPosition = area.transform.position;
                float distance = Vector3.Distance(currentPosition, areaPosition);

                if (distance <= closestDistance)
                {
                    closestObjective = area;
                    closestDistance = distance;
                }
            }
        markerTimer.Run();
        markerCooldown.Run();
        }
    }

    void EnableObjectiveMark()
    {
        if (closestObjective != null)
        {
            SpriteRenderer marker = closestObjective.GetComponentInChildren<SpriteRenderer>();

            marker.enabled = false;

            if(!markerTimer.Finished)
            {
                marker.enabled = true;
            }
            else
            {
                closestObjective = null;
            }
        }
        else
        {
            if (coolDownMessage == null) return;
            coolDownMessage.transform.GetChild(0).gameObject.SetActive(markerCooldown.Running);
            TextMeshProUGUI panel = coolDownMessage.transform.GetChild(0).GetComponentInChildren<TextMeshProUGUI>();
            panel.text = $"Mark available in {Mathf.CeilToInt(markerCooldown.Remaining)} s";
        }
    }

    public bool CanMove
    {
        get {return canMove;}
        set {canMove = value;}
    }

    public bool CanRotate
    {
        get {return canRotate;}
        set {canRotate = value;}
    }

    public Key PickUpKey
    {
        get {return pickUpKey;}
    }
}