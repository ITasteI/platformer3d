using UnityEngine;

public static class ColliderUtil
{
    public static void FitToRenderBounds(Transform target, BoxCollider box)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        Vector3 lossyScale = target.lossyScale;
        box.center = target.InverseTransformPoint(bounds.center);
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, lossyScale.x),
            bounds.size.y / Mathf.Max(0.0001f, lossyScale.y),
            bounds.size.z / Mathf.Max(0.0001f, lossyScale.z));
    }
}
