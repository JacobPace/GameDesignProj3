using UnityEngine;

public class ViewModelRotationLag : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Rotation Lag")]
    public float rotationLagSpeed = 12f;

    [Header("Optional Extra Weight")]
    public float maxRotationOffset = 5f;

    private Quaternion targetRotation;

    void LateUpdate()
    {
        // Desired rotation = camera rotation
        targetRotation = cameraTransform.rotation;

        // Smoothly lag behind camera rotation
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationLagSpeed
        );
    }
}