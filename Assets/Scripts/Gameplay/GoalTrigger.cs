using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null && player.IsOwner)
            WinScreen.Instance?.TriggerWin();
    }
}
