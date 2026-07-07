using UnityEngine;

public class WindSway : MonoBehaviour
{
    public float swayAmount = 3f;
    public float swaySpeed = 1f;

    private Quaternion baseRotation;
    private float offset;

    void Awake()
    {
        baseRotation = transform.localRotation;
        offset = Random.Range(0f, 10f);
    }

    void Update()
    {
        float angle = Mathf.Sin((Time.time + offset) * swaySpeed) * swayAmount;
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
