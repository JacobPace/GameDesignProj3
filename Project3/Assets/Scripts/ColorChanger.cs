using UnityEngine;

public class ColorChanger : MonoBehaviour
{

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Color newColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);

        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in childRenderers)
        {
            rend.material.color = newColor;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
