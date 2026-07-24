using System;
using System.Collections;
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

        promptPanel.SetActive(false);

        originalPos = player.transform.position;
        originalRot = player.transform.rotation;
        isHidden = true;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(TransitionRoutine(originalPos, originalRot, hideoutPos, transform.rotation, true));
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
        Movement movement = player.GetComponent<Movement>();

        // Durante la animación no se puede caminar ni rotar la cámara
        if (movement != null)
        {
            movement.CanMove = false;
            movement.CanRotate = false;
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

        if (hidingIntoSpot)
        {
            // Ya escondido: sigue sin poder caminar; la cámara depende de canRotateCameraX
            if (movement != null) movement.CanRotate = canRotateCameraX;
        }
        else
        {
            if (rb != null) rb.isKinematic = false;
            if (movement != null)
            {
                movement.CanMove = true;
                movement.CanRotate = true;
            }
            isHidden = false;
        }
    }
}
