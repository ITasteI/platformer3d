using UnityEngine;

public class PlayerNameTagDisplay : MonoBehaviour
{
    public PlayerController player;

    private TextMesh label;
    private Transform cam;
    private string lastText;

    void Awake()
    {
        label = GetComponent<TextMesh>();
    }

    void Update()
    {
        if (player == null || label == null)
            return;

        string current = player.playerName.Value.ToString();
        if (current != lastText)
        {
            lastText = current;
            label.text = current;
        }

        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;

        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
    }
}
