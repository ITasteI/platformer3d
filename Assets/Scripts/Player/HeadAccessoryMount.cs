using UnityEngine;

// Keeps a worn head accessory (hat/crown) sitting perfectly on top of the character's head. The
// character is scaled + XZ-recentered at runtime by CharacterNormalizer and its head bobs/turns with
// the animation, so a fixed offset on the player root can't stay on the head. This drives the anchor
// in WORLD space each frame: horizontally at the Head bone, vertically at the very TOP of the head
// mesh (the crown), upright and facing the body's forward - independent of rig scale or head size.
//
// The crown height is MEASURED from the live skinned bounds (the Kenney head is big and its Head bone
// sits low inside it, so a guessed offset buries hats in the head). Measured once, a few frames after
// spawn, so CharacterNormalizer's rescale/recenter has settled and the character is in its idle pose.
public class HeadAccessoryMount : MonoBehaviour
{
    public Transform headBone;              // humanoid Head bone (Animator.GetBoneTransform)
    public Transform facing;                // defines forward/up for the accessory (the character root)
    public SkinnedMeshRenderer bodyRenderer; // measured to find the crown (mesh top)

    // Small gap left above the measured crown so hats rest ON the head rather than clipping into it.
    public float extraLift = 0.02f;

    // Extra 180deg yaw if the character mesh faced away from the body forward (data toggle so it can be
    // corrected without code). The Kenney protagonist faces +Z, so default is false.
    public bool flip;

    private float crownOffset = -1f; // headBone -> crown vertical distance, measured once
    private int frames;

    void OnEnable()
    {
        crownOffset = -1f;
        frames = 0;
    }

    void LateUpdate()
    {
        Transform f = facing != null ? facing : transform.parent;
        if (f == null)
            return;

        Quaternion rot = f.rotation * (flip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);

        if (headBone != null)
        {
            // Measure the crown a few frames in (after CharacterNormalizer has scaled/centered), taking
            // the top of the posed mesh above the head bone. Clamped to a sane range as a safety net.
            if (crownOffset < 0f && bodyRenderer != null)
            {
                frames++;
                if (frames >= 3)
                {
                    float d = bodyRenderer.bounds.max.y - headBone.position.y;
                    crownOffset = Mathf.Clamp(d, 0.12f, 0.7f);
                }
            }
            float off = crownOffset > 0f ? crownOffset : 0.28f; // sane Kenney-head default until measured
            transform.position = headBone.position + f.up * (off + extraLift);
        }
        else if (transform.parent != null)
        {
            transform.position = transform.parent.TransformPoint(new Vector3(0f, 1.78f, 0f)); // fallback
        }

        transform.rotation = rot;
        // World-size accessory regardless of the (runtime-scaled) rig it visually rides on.
        transform.localScale = Vector3.one;
    }
}
