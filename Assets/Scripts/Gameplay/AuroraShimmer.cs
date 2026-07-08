using UnityEngine;

// Gently animates aurora "curtain" quads high in the night sky: each curtain slowly drifts between two
// colours and pulses in brightness (via a MaterialPropertyBlock so it stays GPU-instanced). Purely
// atmospheric - a living polar-light glow over the moonlit world.
public class AuroraShimmer : MonoBehaviour
{
    public Renderer[] curtains;
    public Color colorA = new Color(0.30f, 1f, 0.6f);   // green
    public Color colorB = new Color(0.55f, 0.42f, 1f);  // violet
    public float speed = 0.18f;
    public float baseIntensity = 2.1f;

    private MaterialPropertyBlock mpb;

    void Awake() => mpb = new MaterialPropertyBlock();

    void Update()
    {
        if (curtains == null)
            return;

        float t = Time.time * speed;
        for (int i = 0; i < curtains.Length; i++)
        {
            if (curtains[i] == null)
                continue;
            float ph = i * 0.8f;
            float mix = 0.5f + 0.5f * Mathf.Sin(t * 3f + ph);
            float pulse = 0.65f + 0.35f * Mathf.Sin(t * 2f + ph * 1.3f);
            Color c = Color.Lerp(colorA, colorB, mix) * (baseIntensity * pulse);

            curtains[i].GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            curtains[i].SetPropertyBlock(mpb);
        }
    }
}
