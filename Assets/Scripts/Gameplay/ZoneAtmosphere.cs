using UnityEngine;

public class ZoneAtmosphere : MonoBehaviour
{
    [System.Serializable]
    public struct Zone
    {
        public float height;
        public Color fogColor;
        public Color skyTint;
        public Color lightColor;
        public float lightIntensity;
    }

    public Transform player;
    public Light sunLight;
    public ParticleSystem stars;
    public Zone[] zones;

    private Material skyMat;

    void Awake()
    {
        skyMat = RenderSettings.skybox;
    }

    void Update()
    {
        if (player == null && GameManager.Instance != null)
            player = GameManager.Instance.player;

        if (player == null || zones == null || zones.Length < 2)
            return;

        float h = player.position.y;

        int idx = 0;
        for (int i = 0; i < zones.Length - 1; i++)
        {
            if (h >= zones[i].height)
                idx = i;
        }
        idx = Mathf.Clamp(idx, 0, zones.Length - 2);

        Zone a = zones[idx];
        Zone b = zones[idx + 1];
        float t = Mathf.InverseLerp(a.height, b.height, h);

        RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
        if (skyMat != null)
            skyMat.SetColor("_SkyTint", Color.Lerp(a.skyTint, b.skyTint, t));

        if (sunLight != null)
        {
            sunLight.color = Color.Lerp(a.lightColor, b.lightColor, t);
            sunLight.intensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t);
        }

        if (stars != null)
        {
            float topHeight = zones[zones.Length - 1].height;
            float starT = Mathf.InverseLerp(topHeight - 35f, topHeight, h);
            var emission = stars.emission;
            emission.rateOverTime = Mathf.Lerp(0f, 60f, starT);
        }
    }
}
