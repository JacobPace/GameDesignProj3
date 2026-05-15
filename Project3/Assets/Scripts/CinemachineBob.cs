using UnityEngine;
using UnityEngine.Audio;

public class CinemachineBob : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;

    [Header("Bob Settings")]
    public float stepFrequency = 8f;
    public float verticalBob = 0.05f;
    public float horizontalBob = 0.03f;

    [Header("Speed Scaling")]
    public float minSpeedToBob = 0.1f;
    public float maxSpeed = 6f;

    [Header("Smoothing")]
    public float returnSpeed = 8f;

    private Vector3 startLocalPos;
    private float bobCycle;
    private float currentSpeed;

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Step Timing")]
    public float baseStepInterval = 0.5f; // seconds between steps at normal speed
    private float stepTimer;

    void Start()
    {
        startLocalPos = transform.localPosition;

        if (audioSource != null && sfxGroup != null)
        {
            audioSource.outputAudioMixerGroup = sfxGroup;
        }
    }

    void Update()
    {
        Vector3 horizontalVel = new Vector3(
            controller.velocity.x,
            0,
            controller.velocity.z
        );

        currentSpeed = horizontalVel.magnitude;

        // If not moving → reset everything smoothly
        if (currentSpeed < minSpeedToBob || !controller.isGrounded)
        {
            bobCycle = 0f;
            stepTimer = 0f;

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                startLocalPos,
                Time.deltaTime * returnSpeed
            );

            return;
        }

        // Normalize movement speed
        float speedPercent = Mathf.Clamp01(currentSpeed / maxSpeed);

        // Advance bob cycle (visual)
        bobCycle += Time.deltaTime * stepFrequency * speedPercent;

        // ----------------------------
        // FOOTSTEP SYSTEM (FIXED)
        // ----------------------------

        // Step interval shrinks as player moves faster
        float stepInterval = baseStepInterval / Mathf.Lerp(0.6f, 1.8f, speedPercent);

        stepTimer += Time.deltaTime;

        if (stepTimer >= stepInterval)
        {
            PlayFootstep();
            stepTimer = 0f;
        }

        // ----------------------------
        // BOB MOTION (Minecraft-style)
        // ----------------------------

        float bobX = Mathf.Sin(bobCycle) * horizontalBob * speedPercent;
        float bobY = Mathf.Abs(Mathf.Cos(bobCycle)) * verticalBob * speedPercent;

        Vector3 targetPos = startLocalPos + new Vector3(bobX, bobY, 0f);

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            Time.deltaTime * returnSpeed
        );
    }

    void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0 || audioSource == null)
            return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        audioSource.PlayOneShot(clip);
    }
}