using UnityEngine;
using System.Collections; // <-- This is the missing line required for IEnumerator
using System.Collections.Generic; // Required if you are using IEnumerator<T>
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.VisualScripting;
using Unity.Mathematics;
using TMPro;
using Unity.AppUI.UI;

public class PlayerHandler : MonoBehaviour
{
    [SerializeField]
    private float speed;
    [SerializeField]
    private float sprintMultiplier = 2.0f;

    [SerializeField]
    private float mouseSensitivity;

    private Rigidbody rb;
    private BoxCollider collider;
    private Transform cameraTransform;
    private Vector3 inputDirection;
    private float mouseX;
    private float mouseY;
    private float yaw; // Rotación acumulada en el eje Y (horizontal), aplicada al Rigidbody
    private float pitch; // Rotación acumulada en el eje X (vertical), aplicada solo a la cámara

    [SerializeField]
    private float maxLookDownAngle = 40f; // Límite de inclinación, tanto hacia abajo como hacia arriba

    [SerializeField]
    private bool IsCrouching;

    [SerializeField]
    private bool canMove = true;
    [SerializeField]
    private bool canRotate = true;
    private bool inObjectiveArea = false;

    // Se pone en true externamente (ej. HideOut) mientras el jugador está escondido;
    // bloquea el sneak y el parry mientras esté activo.
    [SerializeField]
    private bool isHidden = false;

    [SerializeField]
    private float PushForce;

    [SerializeField] private Key crouchKey;
    [SerializeField] private Key sprintKey;
    [SerializeField] private Key parryKey;
    [SerializeField] private Key pickUpKey;
    [SerializeField] private Key objectiveMark;

    // Parry setup: es una habilidad puntual (se activa al pulsar F), no un estado mientras se mantiene
    [SerializeField] private bool isParryActive = false;
    [SerializeField] private float parryDuration = 1f; // Ventana en la que el parry puede conectar
    [SerializeField] private float parrySpeed = 1.5f; // Velocidad de movimiento mientras se para (valor absoluto, no multiplicador)
    private float parryTimer = 0f;
    private float ParryDebounce = 0.0f;
    private float ParryCD = 20.0f; // Cooldown tras terminar el parry (conecte o no)

    private Light playerLight; // obtenido en Awake() vía GetComponent
    [SerializeField] private float parryLightRangeMultiplier = 0.3f;
    [SerializeField] private float parryLightIntensityMultiplier = 0.3f;
    private float originalLightRange;
    private float originalLightIntensity;
    private Color originalLightColor;

    // La luz se atenúa a la mitad mientras se está agachado. No se aplica mientras isParryActive,
    // que ya controla la luz de forma exclusiva (y agacharse está bloqueado durante el parry).
    [SerializeField] private float crouchLightMultiplier = 0.5f;

    [SerializeField] private TextMeshProUGUI parryLabel;
    private Color parryLabelOriginalColor;

    // Posición base de la cámara (antes de aplicar el descenso al agacharse y el shake de persecución)
    private Vector3 cameraDefaultLocalPosition;

    [Header("Crouch camera")]
    [SerializeField] private float crouchCameraYOffset = -0.3f; // cuánto baja la cámara al agacharse (metros, espacio local)
    [SerializeField] private float crouchCameraLerpSpeed = 8f; // suavizado del descenso/subida de la cámara
    private float crouchCameraOffsetCurrent = 0f;

    // Camera shake mientras un enemigo está persiguiendo activamente al jugador (ver
    // EnemyController.ChaseStarted/ChaseEnded, que comparten ciclo de vida con BS_Chase:
    // empieza al iniciar la persecución, termina solo cuando todos los enemigos volvieron a patrullar).
    [Header("Chase camera shake")]
    [SerializeField] private float chaseShakeAmplitude = 0.03f; // metros, espacio local de la cámara
    [SerializeField] private float chaseShakeFrequency = 25f; // velocidad del ruido (mientras más alto, más rápido tiembla)
    private bool isBeingChased = false;
    private Vector3 chaseShakeOffset = Vector3.zero;

    // Health setup
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damagePerSecond = 75f;
    private float currentHealth;
    private bool isDead;

    // El sonido de daño (BS_Damage) es un loop: se corta si no llega daño nuevo por este
    // margen, ya que TakeDamage se llama en ticks de física discretos, no de forma continua.
    [SerializeField] private float damageLoopStopDelay = 8f;
    private float lastDamageTime = float.NegativeInfinity;

    // Efectos visuales de salud: viñetas (más visibles cuanto menos vida) y
    // tinte del RawImage (blanco a 100 HP, rojo oscuro a 0 HP)
    [SerializeField] private UnityEngine.UI.Image[] healthVignettes;
    [SerializeField] private UnityEngine.UI.RawImage healthRawImage;
    [SerializeField] private Color healthLowColor = new Color(0.35f, 0f, 0f);

    // Sneak meter setup
    [SerializeField] private float sneakMultiplierBase = 0.4f;
    [SerializeField] private float sneakDrainRate = 0.03f;
    [SerializeField] private float sneakRegenRate = 0.015f;
    [SerializeField] private float sneakSpeedMultiplier = 0.5f; // Velocidad mientras se está sneakeando, fijo sin importar cuánto se haya usado
    private float sneakMeter;
    private bool isSprinting = false;

    // Sprint / stamina setup
    [SerializeField] private float sprintStaminaDuration = 2f; // segundos de sprint continuo hasta vaciar el medidor
    [SerializeField] private float staminaRegenRate = 0.05f; // fracción del medidor por segundo mientras no se está corriendo
    [SerializeField] private float movingStaminaRegenMultiplier = 0.5f; // la regen se reduce a esta fracción mientras el jugador se mueve
    [SerializeField] private float staminaDepletionCooldown = 5f; // tras vaciarse del todo, no regenera nada durante este tiempo
    [SerializeField] private UnityEngine.UI.Image staminaBar; // barra vertical rellenable sobre el texto del parry
    private float staminaMeter = 1f; // 1 = lleno, 0 = vacío
    private float staminaRegenCooldownTimer = 0f;

    // Referencia al collider que puso inObjectiveArea en true, para poder resetearlo
    // al salir aunque su hijo 'Mark' ya no exista (ej. si el área se completó
    // mientras el jugador seguía adentro).
    private GameObject currentObjectiveAreaTrigger = null;

    private GameObject closestObjective = null;
    private Timer markerTimer = null;
    private Timer markerCooldown = null;
    [SerializeField] private float coolDownDuration;

    [SerializeField] private GameObject coolDownMessagePreFab;
    private GameObject coolDownMessage;

    // Distancia del rayo que detecta escondites (HideOut) frente al jugador
    [SerializeField] private float hideOutDetectionDistance = 5f;
    // Intensidad del resaltado (emisión aditiva): bajo para que sea un brillo
    // suave y transparente y no tape la textura del mueble.
    [SerializeField] private float hideOutHighlightIntensity = 0.05f;
    private Renderer highlightedHideOutRenderer = null;
    private Color highlightedHideOutOriginalEmission;

    // Evita que Update() use referencias (coolDownMessage, markerCooldown, etc.)
    // antes de que Awake() termine de inicializarlas, o luego de que una recarga
    // de escena (ej. cargar checkpoint) las haya destruido.
    private bool isInitialized = false;

    // Only applies the sneak bonus while actively crouching; otherwise enemies see at full FOV.
    public float SneakFOVMultiplier => IsCrouching ? sneakMeter : 1f;

    // Consultado por HideOut para bloquear el parry mientras el jugador está escondido
    public bool IsHidden
    {
        get { return isHidden; }
        set { isHidden = value; }
    }

    // Consultado por HideOut para bloquear el hide mientras se está parriando
    public bool IsParryActive => isParryActive;

    // Consultado por PlayerFootsteps para atenuar el volumen y el alcance auditivo de los pasos
    public bool Crouching => IsCrouching;

    // Consultado por PlayerFootsteps: si suena mientras esto es true, el enemigo que lo oiga
    // entra en Search directamente, sin el roll de probabilidad normal.
    public bool IsSprinting => isSprinting && !IsCrouching && !isParryActive;

    // Consultado por sistemas externos que necesitan infligir daño directo al jugador
    // (ej. el anti-cheese de HideOut por quedarse escondido demasiado tiempo).
    public void ApplyDamage(float amount)
    {
        TakeDamage(amount);
    }

    private void OnEnable()
    {
        EnemyController.ChaseStarted += HandleChaseStarted;
        EnemyController.ChaseEnded += HandleChaseEnded;
    }

    private void OnDisable()
    {
        EnemyController.ChaseStarted -= HandleChaseStarted;
        EnemyController.ChaseEnded -= HandleChaseEnded;
    }

    private void HandleChaseStarted()
    {
        isBeingChased = true;
    }

    private void HandleChaseEnded()
    {
        isBeingChased = false;
        chaseShakeOffset = Vector3.zero;
    }

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

        // La cámara es hija del Player; el pitch se aplica solo a ella, no al Rigidbody
        cameraTransform = Camera.main.transform;
        cameraDefaultLocalPosition = cameraTransform.localPosition;

        yaw = transform.eulerAngles.y;
        IsCrouching = false;

        // Luz del jugador: es hija de Camera.main, no de este GameObject (que puede no ser
        // el padre de la cámara en la jerarquía), así que hay que buscarla desde ahí.
        playerLight = cameraTransform.GetComponentInChildren<Light>();
        if (playerLight != null)
        {
            originalLightRange = playerLight.range;
            originalLightIntensity = playerLight.intensity;
            originalLightColor = playerLight.color;
        }

        if (parryLabel != null)
        {
            parryLabelOriginalColor = parryLabel.color;
        }

        // Oculta y bloquea el cursor en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Set up keybinds
        parryKey = Key.F;
        crouchKey = Key.LeftCtrl;
        sprintKey = Key.LeftShift;

        // Health setup
        currentHealth = maxHealth;
        isDead = false;
        EventManager.RaisePlayerHealthChanged(currentHealth, maxHealth);
        UpdateHealthVisuals();

        // Sneak meter setup
        sneakMeter = sneakMultiplierBase;

        // marker timer
        markerTimer = GetComponent<Timer>();

        // cooldown de activación
        markerCooldown = gameObject.AddComponent<Timer>();
        markerCooldown.Duration = coolDownDuration;

        // Instancia del prefab 'coolDownMessagePreFab'
        coolDownMessage = Instantiate<GameObject>(coolDownMessagePreFab, Camera.main.transform.position, Quaternion.identity);

        // Stamina bar: fuerza el tipo de relleno para que funcione como barra vertical
        if (staminaBar != null)
        {
            staminaBar.type = UnityEngine.UI.Image.Type.Filled;
            staminaBar.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            staminaBar.fillAmount = staminaMeter;
        }

        isInitialized = true;
    }

    private void Update()
    {
        // No corre la lógica de Update hasta que Awake() haya terminado de
        // inicializar todas las referencias (ej. coolDownMessage, markerCooldown).
        if (!isInitialized) return;

        // Deshabilita el player input si se esta en el menu de pausa
        if (!GetComponent<InitializePauseMenu>().IsPausedMenuActive)
        {
            ReadInput();
        }

        UpdateSneakMeter();
        UpdateStamina();
        UpdateParry();
        UpdateParryLabel();
        UpdateDamageAudio();
        UpdatePlayerLight();

        // revisa si puede enseñar el marcador del objetivo
        EnableObjectiveMark();

        // revisa si hay un escondite frente al jugador y lo resalta
        CheckForHideOut();
    }

    private void FixedUpdate()
    {
        if (CanRotate) RotateRigidbody();
        if (CanMove) MoveRigidbody();
    }

    private void LateUpdate()
    {
        if (!isInitialized) return;

        UpdateCameraPosition();
    }

    // La luz del jugador se atenúa a la mitad al agacharse. No se toca mientras isParryActive,
    // que ya la controla de forma exclusiva (agacharse está bloqueado durante el parry).
    private void UpdatePlayerLight()
    {
        if (playerLight == null || isParryActive) return;

        float multiplier = IsCrouching ? crouchLightMultiplier : 1f;
        playerLight.range = originalLightRange * multiplier;
        playerLight.intensity = originalLightIntensity * multiplier;
    }

    // Combina el descenso suave de la cámara al agacharse con el shake mientras un enemigo persigue
    // activamente al jugador; ambos son offsets sobre la posición local original de la cámara.
    private void UpdateCameraPosition()
    {
        float crouchTarget = IsCrouching ? crouchCameraYOffset : 0f;
        crouchCameraOffsetCurrent = Mathf.Lerp(crouchCameraOffsetCurrent, crouchTarget, Time.deltaTime * crouchCameraLerpSpeed);

        if (isBeingChased)
        {
            float shakeX = (Mathf.PerlinNoise(Time.time * chaseShakeFrequency, 0f) - 0.5f) * 2f * chaseShakeAmplitude;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * chaseShakeFrequency) - 0.5f) * 2f * chaseShakeAmplitude;
            chaseShakeOffset = new Vector3(shakeX, shakeY, 0f);
        }
        else
        {
            chaseShakeOffset = Vector3.zero;
        }

        cameraTransform.localPosition = cameraDefaultLocalPosition + new Vector3(0f, crouchCameraOffsetCurrent, 0f) + chaseShakeOffset;
    }

    private void OnTriggerStay(Collider collision)
    {
        if (!collision.CompareTag("Enemy")) return;

        EnemyController Enemy_Controller = collision.GetComponent<EnemyController>();

        if (isParryActive)
        {
            if (Enemy_Controller == null) return;

            Vector3 pushDirection = collision.transform.position - transform.position;
            pushDirection.y = 0; // Keep the push flat on the ground if needed
            pushDirection.Normalize();

            Debug.Log("Parried monster...");
            // Aturde al enemigo y lo aleja; retomará la búsqueda cuando pase el aturdimiento
            Enemy_Controller.Parry(pushDirection);

            // El parry conectó: termina de inmediato en vez de esperar a que se acabe la ventana
            EndParry();
        }
        else
        {
            Texture jumpscareImage = Enemy_Controller != null ? Enemy_Controller.JumpscareImage : null;
            TakeDamage(damagePerSecond * Time.fixedDeltaTime, jumpscareImage);
        }
    }

    // Intenta activar la habilidad de parry al pulsar la tecla; no hace nada si ya
    // está activa, en cooldown, o si el jugador está escondido.
    private void TryStartParry()
    {
        if (isParryActive) return;
        if (Time.time < ParryDebounce) return;
        if (IsHidden) return;

        isParryActive = true;
        parryTimer = parryDuration;

        // Corta cualquier sneak en curso: no se puede sneakear mientras se para
        IsCrouching = false;

        if (playerLight != null)
        {
            playerLight.range = originalLightRange * parryLightRangeMultiplier;
            playerLight.intensity = originalLightIntensity * parryLightIntensityMultiplier;
            playerLight.color = Color.white;
        }
    }

    private void UpdateParry()
    {
        if (!isParryActive) return;

        parryTimer -= Time.deltaTime;
        if (parryTimer <= 0f)
        {
            EndParry();
        }
    }

    // Termina la ventana de parry (haya conectado o no) y empieza el cooldown
    private void EndParry()
    {
        isParryActive = false;
        ParryDebounce = Time.time + ParryCD;

        if (playerLight != null)
        {
            playerLight.range = originalLightRange;
            playerLight.intensity = originalLightIntensity;
            playerLight.color = originalLightColor;
        }
    }

    

    private void UpdateParryLabel()
    {
        if (parryLabel == null) return;

        if (isParryActive)
        {
            parryLabel.text = "[PARRYING]";
            parryLabel.color = Color.red;
            return;
        }

        float remainingCooldown = ParryDebounce - Time.time;
        if (remainingCooldown > 0f)
        {
            parryLabel.text = $"[PARRY]: {Mathf.CeilToInt(remainingCooldown)}s";
        }
        else
        {
            parryLabel.text = "[PARRY]: Ready";
        }
        parryLabel.color = parryLabelOriginalColor;
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

        // -- Detect parry input (activación puntual al pulsar, no mientras se mantiene)
        if (Keyboard.current[parryKey].wasPressedThisFrame)
        {
            TryStartParry();
        }

        // -- Detect crouch input
        bool crouchKeyHeld = Keyboard.current[crouchKey].isPressed;

        // -- Detect sprint input (no se puede iniciar sin stamina disponible)
        isSprinting = Keyboard.current[sprintKey].isPressed && staminaMeter > 0f;

        // No se puede sneakear mientras se está escondido o parriando
        IsCrouching = crouchKeyHeld && !isHidden && !isParryActive;

        // Revisa que el jugador pueda visualizar el marcador
        if (Keyboard.current[objectiveMark].wasPressedThisFrame && !markerCooldown.Running)
        {
            CalculateObjectiveDistance();
        }


        inputDirection = inputDirection.normalized;

        // Lee el movimiento del mouse
        mouseX = Mouse.current.delta.x.ReadValue() * mouseSensitivity;
        mouseY = Mouse.current.delta.y.ReadValue() * mouseSensitivity;
    }

    private void TakeDamage(float amount, Texture jumpscareImage)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        EventManager.RaisePlayerHealthChanged(currentHealth, maxHealth);
        UpdateHealthVisuals();

        lastDamageTime = Time.time;
        float healthFraction = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        AudioManager.PlayDamage(healthFraction);

        if (currentHealth <= 0f)
        {
            isDead = true;

            // Bloquea el control del jugador durante el jumpscare y el menú de muerte
            canMove = false;
            canRotate = false;

            EventManager.RaisePlayerDeath(jumpscareImage);
        }
    }

    // BS_Damage es un loop: se corta en cuanto pasa damageLoopStopDelay sin daño nuevo
    // (el jugador dejó de tocar al enemigo, o empezó a parriar).
    private void UpdateDamageAudio()
    {
        if (Time.time - lastDamageTime > damageLoopStopDelay)
        {
            AudioManager.StopDamage();
        }
    }

    // Viñetas: invisibles a 100 HP, totalmente visibles a 0 HP (lineal).
    // RawImage: blanco a 100 HP, vira a healthLowColor a 0 HP (lineal).
    private void UpdateHealthVisuals()
    {
        float healthFraction = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        float vignetteAlpha = 1f - healthFraction;

        if (healthVignettes != null)
        {
            foreach (UnityEngine.UI.Image vignette in healthVignettes)
            {
                if (vignette == null) continue;

                Color vignetteColor = vignette.color;
                vignetteColor.a = vignetteAlpha;
                vignette.color = vignetteColor;
            }
        }

        if (healthRawImage != null)
        {
            Color tint = Color.Lerp(healthLowColor, Color.white, healthFraction);
            tint.a = healthRawImage.color.a;
            healthRawImage.color = tint;
        }
    }

    // Al agotarse (sneakMeter llega a 1) ya no se fuerza la salida del sneak: el jugador se
    // queda agachado sin bonus de sigilo (SneakFOVMultiplier vuelve a 1) hasta que suelte la tecla.
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

    // Drena el medidor de stamina mientras se corre de verdad (no cuenta si el
    // sprint no se está aplicando, ej. agachado o parriando); lo regenera el
    // resto del tiempo, a mitad de ritmo mientras el jugador se mueve. Corta el
    // sprint en cuanto se vacía y bloquea la regen durante staminaDepletionCooldown.
    private void UpdateStamina()
    {
        bool effectivelySprinting = isSprinting && !IsCrouching && !isParryActive;

        if (effectivelySprinting)
        {
            staminaMeter = Mathf.Max(0f, staminaMeter - Time.deltaTime / sprintStaminaDuration);

            if (staminaMeter <= 0f)
            {
                isSprinting = false;
                staminaRegenCooldownTimer = staminaDepletionCooldown;
            }
        }
        else if (staminaRegenCooldownTimer > 0f)
        {
            staminaRegenCooldownTimer -= Time.deltaTime;
        }
        else
        {
            bool isMoving = inputDirection.sqrMagnitude > 0.0001f;
            float regenRate = staminaRegenRate * (isMoving ? movingStaminaRegenMultiplier : 1f);
            staminaMeter = Mathf.Min(1f, staminaMeter + regenRate * Time.deltaTime);
        }

        if (staminaBar != null)
        {
            staminaBar.fillAmount = staminaMeter;
        }
    }

    private void RotateRigidbody()
    {
        yaw += mouseX;
        Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(targetRotation);

        // Pitch de la cámara: sube y baja el mismo ángulo máximo en ambas direcciones.
        pitch = Mathf.Clamp(pitch - mouseY, -maxLookDownAngle, maxLookDownAngle);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
        float currentSpeed = speed;
        if (isParryActive)
        {
            currentSpeed = parrySpeed;
        }
        else if (IsCrouching)
        {
            currentSpeed = speed * sneakSpeedMultiplier;
        }
        else if (isSprinting)
        {
            currentSpeed = speed * sprintMultiplier;
        }

        Vector3 velocity = (transform.forward * inputDirection.z + transform.right * inputDirection.x) * currentSpeed;
        rb.AddForce(velocity*rb.linearDamping); //*Time.fixedDeltaTime);
        //rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.transform.childCount == 1 && // Revisa que tenga un solo hijo
            other.gameObject.transform.GetChild(0).tag == "Mark") // Revisa que el tag del primer hijo sea 'Mark'
        {
            inObjectiveArea = true;
            currentObjectiveAreaTrigger = other.gameObject;
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Compara por el GameObject que activó inObjectiveArea, no por su hijo 'Mark':
        // ese hijo puede haber sido destruido (área completada) mientras el jugador
        // seguía adentro, y aun así hay que resetear la bandera al salir.
        if (other.gameObject == currentObjectiveAreaTrigger)
        {
            inObjectiveArea = false;
            currentObjectiveAreaTrigger = null;
        }
    }

    void CalculateObjectiveDistance()
    {
        // Revisa si NO esta en el area del objetivo
        if (!inObjectiveArea)
        {
            List<GameObject> objectives = new(GameObject.FindGameObjectsWithTag("Mark"));
            Vector3 currentPosition = transform.position;
            float closestDistance = Mathf.Infinity;

            foreach (GameObject mark in objectives)
            {
                Vector3 areaPosition = mark.transform.position;
                float distance = Vector3.Distance(currentPosition, areaPosition);

                if (distance <= closestDistance)
                {
                    closestObjective = mark;
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
            SpriteRenderer mark = closestObjective.GetComponentInChildren<SpriteRenderer>();

            mark.enabled = false;

            if(!markerTimer.Finished)
            {
                mark.enabled = true;
            }
            else
            {
                closestObjective = null;
            }
        }
        else
        {
            // coolDownMessage puede haber sido destruido junto con la escena anterior
            // (ej. al cargar un checkpoint) mientras este frame ya estaba en curso.
            if (coolDownMessage == null) return;

            coolDownMessage.transform.GetChild(0).gameObject.SetActive(markerCooldown.Running);
            TextMeshProUGUI panel = coolDownMessage.transform.GetChild(0).GetComponentInChildren<TextMeshProUGUI>();
            panel.text = $"Mark available in {Mathf.CeilToInt(markerCooldown.Remaining)} s";
        }
    }

    // Tira un rayo desde la cabeza (cámara) del jugador y, si golpea el collider
    // de un escondite (tag 'HideOut' en el propio collider), resalta en blanco
    // el Renderer del escondite. Usa GetComponentInParent porque el collider
    // golpeado puede ser el trigger hijo (ej. HideOutChild) que no tiene
    // Renderer propio; el mueble visible está en el padre. Restaura el color
    // original en cuanto el rayo deja de apuntarlo.
    private void CheckForHideOut()
    {
        Renderer hitRenderer = null;

        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, hideOutDetectionDistance))
        {
            if (hit.collider.transform.CompareTag("HideOut"))
            {
                hitRenderer = hit.collider.GetComponentInParent<Renderer>();
            }
        }

        if (hitRenderer == highlightedHideOutRenderer) return;

        if (highlightedHideOutRenderer != null)
        {
            highlightedHideOutRenderer.material.SetColor("_EmissionColor", highlightedHideOutOriginalEmission);
        }

        highlightedHideOutRenderer = hitRenderer;

        if (highlightedHideOutRenderer != null)
        {
            Material hitMaterial = highlightedHideOutRenderer.material;
            hitMaterial.EnableKeyword("_EMISSION");
            highlightedHideOutOriginalEmission = hitMaterial.GetColor("_EmissionColor");
            hitMaterial.SetColor("_EmissionColor", Color.lightCyan * hideOutHighlightIntensity);
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