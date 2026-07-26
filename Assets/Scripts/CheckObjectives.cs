using UnityEngine;

public class CheckObjectives : MonoBehaviour
{

    [SerializeField]
    GameObject[] objectives;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter(Collider other)
    {
        foreach(GameObject objective in objectives)
        {
            if (objective)
            {
                
            }
        }
    }
}
