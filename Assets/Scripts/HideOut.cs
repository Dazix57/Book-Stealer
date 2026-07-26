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

    [SerializeField]
    private EnemyController enemy;

    private bool inRange = false;
    private bool isHidden = false;
    private GameObject player;

    private Vector3 originalPos;
    private Quaternion originalRot;

    private Coroutine hideRoutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        promptPanel = Instantiate<GameObject>(hidePromptPanelPreFab, Camera.main.transform.position, Quaternion.identity);
    }
    void Update()
    {
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
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            player = other.gameObject;

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
        if (enemy != null && enemy.InChase) return;

        promptPanel.SetActive(false);

        originalPos = player.transform.position;
        originalRot = player.transform.rotation;
        isHidden = true;

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

    IEnumerator TransitionRoutine(Vector3 startPos, Quaternion startRot, Vector3 endPos, Quaternion endRot, bool hidingIntoSpot)
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();
        PlayerHandler playerHandler = player.GetComponent<PlayerHandler>();

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
            }
            isHidden = false;
        }
    }
}
