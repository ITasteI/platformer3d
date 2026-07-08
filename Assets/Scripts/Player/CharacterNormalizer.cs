using UnityEngine;

// Scales the character to a target height at RUNTIME. Skinned-mesh bounds are only reliable once the
// mesh is actually posed/rendered (they lie during headless build), so this measures in-game and
// corrects once. The Humanoid rig guarantees the character stands upright (Y = up), so we measure the
// bounds HEIGHT (size.y) - not the largest dimension, which in a T-pose/arms-out frame would be the
// arm span (width) and would shrink the character wrongly. Height is also invariant to yaw rotation.
public class CharacterNormalizer : MonoBehaviour
{
    public float targetHeight = 1.75f;
    private bool done;

    void OnEnable() => done = false;

    void LateUpdate()
    {
        if (done)
            return;

        var smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
            return;

        float height = smr.bounds.size.y;
        if (height < 0.01f)
            return; // not posed yet

        transform.localScale *= targetHeight / height;

        // Re-center horizontally so the mesh's XZ center sits exactly on the parent's (player root's)
        // vertical axis. If the model's pivot is offset from its body, rotating the root to face
        // movement would swing the body out in a circle and separate it from the hitbox/name tag.
        // With the center on the axis, the character rotates in place instead. No-op if already centered.
        Vector3 meshCenter = smr.bounds.center;
        Vector3 axis = transform.parent.position;
        transform.position += new Vector3(axis.x - meshCenter.x, 0f, axis.z - meshCenter.z);

        done = true;
    }
}
