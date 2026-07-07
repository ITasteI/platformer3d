using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float distance = 6f;
    public float height = 2.5f;
    public float mouseSensitivity = 3f;
    public float pitchMin = -20f;
    public float pitchMax = 60f;
    public float positionSmoothTime = 0.08f;
    public float collisionRadius = 0.3f;
    public float collisionPadding = 0.2f;
    public float minDistance = 1f;
    // Layers the camera collides with. Defaults to everything except the player layer (8),
    // so looking level doesn't snap the camera onto the player's own collider.
    public LayerMask collisionMask = ~(1 << 8);

    private float yaw;
    private float pitch = 20f;
    private Vector3 velocity;
    private float currentDistance;
    private float distanceVelocity;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (target != null)
            yaw = target.eulerAngles.y;
        currentDistance = distance;
    }

    void Update()
    {
        if (MainMenu.IsBlockingGameplay || WinScreen.HasWon || TutorialOverlay.IsVisible)
            return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * height;
        Vector3 camDir = -(rotation * Vector3.forward);

        float targetDistance = distance;
        if (Physics.SphereCast(pivot, collisionRadius, camDir, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            targetDistance = Mathf.Clamp(hit.distance - collisionPadding, minDistance, distance);

        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, positionSmoothTime);
        Vector3 desiredPosition = pivot + camDir * currentDistance;

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);
        transform.LookAt(target.position + Vector3.up * (height * 0.5f));
    }
}
