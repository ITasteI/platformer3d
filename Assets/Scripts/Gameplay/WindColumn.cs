using UnityEngine;

// A rising wind column above a fan platform: while inside, you're carried upward - hold Space and
// ride it with the glider. The Wolkenreich's signature set-piece.
[RequireComponent(typeof(Collider))]
public class WindColumn : MonoBehaviour
{
    public float lift = 26f;     // upward acceleration while inside
    public float maxRise = 8f;   // terminal upward speed inside the column

    void OnTriggerStay(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.IsOwner)
            pc.ApplyUpdraft(lift, maxRise);
    }
}
