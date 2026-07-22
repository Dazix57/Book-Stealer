using UnityEngine;
using UnityEngine.InputSystem; // Required namespace

public class test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float velocity;
    Rigidbody rb;
    float speedX, speedZ;
    void Start()
    {
        velocity = 2.0f;
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        speedX = 0f;
        speedZ = 0f;
        if (Keyboard.current.wKey.isPressed)
        {
            speedZ = velocity;
        } else if (Keyboard.current.sKey.isPressed)
        {
            speedZ = -velocity;
        } 

        if (Keyboard.current.aKey.isPressed)
        {
            speedX = -velocity;
        } else if (Keyboard.current.dKey.isPressed)
        {
            speedX = velocity;
        } 
        
        rb.linearVelocity = new Vector3(speedX,0,speedZ);
    }
}
