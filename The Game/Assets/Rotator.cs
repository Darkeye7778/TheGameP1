using UnityEngine;

public class Rotator : MonoBehaviour
{
    public float baseSpeed = 50f;  // Average rotation speed (degrees/sec)
    public float waveAmplitude = 30f; // Extra speed added/subtracted by sine wave
    public float waveFrequency = 2f;  // How fast the sine wave oscillates

    void Update()
    {
        float wave = Mathf.Sin(Time.time * waveFrequency) * waveAmplitude;
        float rotationSpeed = baseSpeed + wave; // Speed varies up and down
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}
