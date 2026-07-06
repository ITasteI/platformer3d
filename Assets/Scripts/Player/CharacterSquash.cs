using UnityEngine;

public class CharacterSquash : MonoBehaviour
{
    public PlayerController player;
    public float smooth = 8f;

    private Vector3 baseScale;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        if (player == null)
            return;

        float stretch = Mathf.Clamp(player.VerticalVelocity * 0.02f, -0.25f, 0.3f);
        float crouchMultiplier = player.IsCrouching ? 0.6f : 1f;
        Vector3 target = new Vector3(baseScale.x - stretch, (baseScale.y + stretch) * crouchMultiplier, baseScale.z - stretch);
        transform.localScale = Vector3.Lerp(transform.localScale, target, smooth * Time.deltaTime);
    }
}
