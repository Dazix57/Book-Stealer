using UnityEngine;

public class EnemyFacingSprite : MonoBehaviour
{
    [SerializeField] private GameObject forwardSprite;
    [SerializeField] private GameObject backwardSprite;

    private Transform cameraTransform;
    private bool showingForward;

    void Start()
    {
        cameraTransform = Camera.main.transform;
        SetFacing(true);
    }

    void FixedUpdate()
    {
        Vector3 toCamera = cameraTransform.position - transform.position;
        bool cameraSeesFront = Vector3.Dot(transform.forward, toCamera) > 0f;

        if (cameraSeesFront != showingForward)
            SetFacing(cameraSeesFront);
    }

    private void SetFacing(bool front)
    {
        showingForward = front;
        forwardSprite.SetActive(front);
        backwardSprite.SetActive(!front);
    }
}
