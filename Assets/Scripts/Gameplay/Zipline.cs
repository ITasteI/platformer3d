using UnityEngine;

// A zipline's START HANDLE: standing near it OFFERS the ride ("E - Zipline" hint in the HUD);
// pressing E attaches you to the cable and slides you to endPoint. Space bails out early. The
// cable/mast visuals are built by SceneBuilder; this component only carries the offer trigger.
[RequireComponent(typeof(Collider))]
public class Zipline : MonoBehaviour
{
    public Vector3 endPoint;
    public float speed = 9f;

    // OnTriggerStay keeps the offer alive every frame the player stands in range; the player
    // decides with E (no more surprise auto-grabs by walking past).
    void OnTriggerStay(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.IsOwner)
            pc.OfferZipline(this);
    }
}
