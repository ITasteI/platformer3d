using UnityEngine;

// A slow, glowing, DODGEABLE bolt fired by Shooter wraiths. Touching it resets the player (same
// rule as touching the wraith itself); it vanishes on any solid world hit or after a few seconds.
// Built from primitives at runtime (no prefab needed); kinematic rigidbody so trigger events fire
// against static geometry.
public class EnemyProjectile : MonoBehaviour
{
    public Vector3 velocity;
    public float life = 6f;

    static Material sharedMat; // one shared glow material for every bolt

    public static EnemyProjectile Spawn(Vector3 origin, Vector3 target, float speed)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "WraithBolt";
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * 0.26f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.1f; // ~0.29 m world hit radius on the 0.26-scaled sphere

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        if (sharedMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            sharedMat = new Material(sh);
            Color c = new Color(1f, 0.25f, 0.2f) * 3f; // hot red - blooms at night
            if (sharedMat.HasProperty("_BaseColor")) sharedMat.SetColor("_BaseColor", c);
            else sharedMat.color = c;
        }
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = sharedMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var p = go.AddComponent<EnemyProjectile>();
        p.velocity = (target - origin).normalized * speed;
        return p;
    }

    void Start() => Destroy(gameObject, life);

    void Update() => transform.position += velocity * Time.deltaTime;

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.IsOwner)
        {
            pc.Die();
            Destroy(gameObject);
            return;
        }

        // Pass through triggers (coins, checkpoints, wraiths) and remote players; pop on the world.
        if (!other.isTrigger && pc == null && other.GetComponentInParent<Enemy>() == null)
            Destroy(gameObject);
    }
}
