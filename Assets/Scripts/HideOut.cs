using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class HideOut : MonoBehaviour
{
    [SerializeField]
    private GameObject hidePromptPanelPreFab;
    private GameObject promptPanel;

    [SerializeField]
    private float hideSpeed = 5.0f;

    [SerializeField]
    private bool canRotateCameraX = true;

    [SerializeField]
    private Transform[] faces;

    // Todos los enemigos activos de la escena (puede haber una cantidad variable, y cualquiera
    // de ellos puede terminar cerca de este escondite), obtenidos una vez en Awake().
    private EnemyController[] enemies;

    // Si un enemigo está más lejos que esto cuando el jugador se esconde, pierde el rastro
    // (Searching); si está más cerca, no entra en Searching/Confused: sigue directo hacia el
    // escondite y, al llegar, saca al jugador a la fuerza (ver EnemyController.ForceApproach).
    [SerializeField]
    private float forceExtractionRange = 8f;
    // Ese rango también escala con cuántas veces hayan parriado a cada enemigo (igual que sus
    // otros stats en EnemyController), +10% por parry.
    [SerializeField]
    private float forceExtractionRangeIncreasePerParry = 0.1f;

    // Anti-cheese: pasado este tiempo escondido sin interrupción, el jugador empieza a perder
    // vida (para que esconderse no sea una estrategia segura indefinidamente).
    [SerializeField]
    private float hideCheeseGraceDuration = 10f;
    [SerializeField]
    private float hideCheeseDamagePerSecond = 15f;
    private float hiddenTimer = 0f;

    private bool inRange = false;
    private bool isHidden = false;
    private GameObject player;
    private PlayerHandler playerHandler;

    private Vector3 originalPos;
    private Quaternion originalRot;

    private Coroutine hideRoutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        promptPanel = Instantiate<GameObject>(hidePromptPanelPreFab, Camera.main.transform.position, Quaternion.identity);
        enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
    }
    void Update()
    {
        // No se puede entrar ni salir de un escondite mientras se está parriando
        if (playerHandler != null && playerHandler.IsParryActive) return;

        if (inRange && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (isHidden)
            {
                Unhide();
            }
            else
            {
                Hide(transform.position);
            }
        }

        UpdateHideCheeseDamage();
    }

    // Anti-cheese: mientras siga escondido más allá de hideCheeseGraceDuration, empieza a
    // perder vida cada frame (hideCheeseDamagePerSecond repartido por Time.deltaTime).
    void UpdateHideCheeseDamage()
    {
        if (!isHidden)
        {
            hiddenTimer = 0f;
            return;
        }

        hiddenTimer += Time.deltaTime;
        if (hiddenTimer > hideCheeseGraceDuration && playerHandler != null)
        {
            playerHandler.ApplyDamage(hideCheeseDamagePerSecond * Time.deltaTime);
        }
    }

    float EffectiveForceExtractionRange(EnemyController enemyController)
    {
        return forceExtractionRange * (1f + forceExtractionRangeIncreasePerParry * enemyController.ParryCount);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            player = other.gameObject;
            playerHandler = player.GetComponent<PlayerHandler>();

            // El texto se decide aquí, con el valor real de isHidden en el momento
            // en que el panel se vuelve a mostrar (no cuando se presionó E, ya que
            // Unhide() no pone isHidden en false hasta que termina la animación).
            promptPanel.GetComponentInChildren<TextMeshProUGUI>().text = isHidden ? "Press E to exit" : "Press E to hide";
            promptPanel.SetActive(true);
            inRange = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            promptPanel.SetActive(false);
            inRange = false;
        }
    }

    void Hide(Vector3 hideoutPos)
    {
        if (player == null) return;

        foreach (EnemyController enemyController in enemies)
        {
            // La extracción forzosa (y la pérdida de rastro hacia Searching) quedan reservadas
            // a Chasing: si el enemigo está en Windup, ya buscando, confundido o aturdido,
            // esconderse no lo redirige ni lo hace reaccionar.
            if (enemyController == null || !enemyController.IsChasing) continue;

            float distanceToPlayer = Vector3.Distance(enemyController.transform.position, player.transform.position);

            if (distanceToPlayer <= EffectiveForceExtractionRange(enemyController))
            {
                // Este enemigo está demasiado cerca: no pierde el rastro ni se pone a investigar,
                // sigue directo hacia el escondite y saca al jugador a la fuerza al llegar.
                enemyController.ForceApproach(hideoutPos, ForceExtractPlayer);
            }
            else
            {
                // Este enemigo está lo bastante lejos: pierde el rastro y pasa a buscar
                enemyController.LosePlayerAt(player.transform.position);
            }
        }

        promptPanel.SetActive(false);

        originalPos = player.transform.position;
        originalRot = player.transform.rotation;
        isHidden = true;
        if (playerHandler != null) playerHandler.IsHidden = true;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        Quaternion faceRot = GetClosestFaceRotation(hideoutPos, originalPos);

        hideRoutine = StartCoroutine(TransitionRoutine(originalPos, originalRot, hideoutPos, faceRot, true));
    }

    // Elige la cara (Front, Right, etc.) cuya dirección hacia afuera esté más
    // alineada con la posición del jugador, es decir, la cara desde la que se acercó.
    Quaternion GetClosestFaceRotation(Vector3 hideoutPos, Vector3 playerPos)
    {
        if (faces == null || faces.Length == 0) return transform.rotation;

        Vector3 toPlayer = (playerPos - hideoutPos).normalized;
        Transform closestFace = null;
        float bestDot = float.NegativeInfinity;

        foreach (Transform face in faces)
        {
            if (face == null) continue;

            float dot = Vector3.Dot(face.forward, toPlayer);
            if (dot > bestDot)
            {
                bestDot = dot;
                closestFace = face;
            }
        }

        return closestFace != null ? closestFace.rotation : transform.rotation;
    }

    void Unhide()
    {
        if (player == null) return;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(TransitionRoutine(player.transform.position, player.transform.rotation, originalPos, originalRot, false));
    }

    // Pasado como callback a EnemyController.ForceApproach(): lo saca del escondite en cuanto
    // un enemigo llega, sin que el jugador tenga que presionar E. Si ya había salido por su
    // cuenta, o ya lo sacó otro enemigo que llegó primero, no hace nada.
    void ForceExtractPlayer()
    {
        if (!isHidden) return;

        // Se pone en false de inmediato (no se espera a que termine la corrutina) para que, si
        // varios enemigos convergen casi al mismo tiempo, solo el primero dispare la animación.
        isHidden = false;
        Unhide();
    }

    IEnumerator TransitionRoutine(Vector3 startPos, Quaternion startRot, Vector3 endPos, Quaternion endRot, bool hidingIntoSpot)
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();

        // Durante la animación no se puede caminar ni rotar la cámara
        if (playerHandler != null)
        {
            playerHandler.CanMove = false;
            playerHandler.CanRotate = false;
        }
        if (rb != null) rb.isKinematic = true;

        float duration = Vector3.Distance(startPos, endPos) / hideSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            player.transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, endPos, t),
                Quaternion.Slerp(startRot, endRot, t));

            yield return null;
        }

        player.transform.SetPositionAndRotation(endPos, endRot);

        // Evita que RotateRigidbody salte de vuelta al yaw previo en cuanto se reactive CanRotate
        if (playerHandler != null) playerHandler.SetYawFromRotation(endRot);

        if (hidingIntoSpot)
        {
            // Ya escondido: sigue sin poder caminar; la cámara depende de canRotateCameraX
            if (playerHandler != null) playerHandler.CanRotate = canRotateCameraX;
        }
        else
        {
            if (rb != null) rb.isKinematic = false;
            if (playerHandler != null)
            {
                playerHandler.CanMove = true;
                playerHandler.CanRotate = true;
                playerHandler.IsHidden = false;
            }
            isHidden = false;
        }
    }
}
