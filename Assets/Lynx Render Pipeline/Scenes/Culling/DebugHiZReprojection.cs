using UnityEngine;

public class DebugHiZReprojection : MonoBehaviour
{
    [SerializeField] float rotation;
    [SerializeField] float magnitude = 1f;
    
    bool odd = false;
    void Update()
    {
        float currRotation = rotation * (odd ? -1 : 1);
        transform.Rotate(Vector3.up, currRotation);
        odd = !odd;

        var pos = transform.position;
        pos.x += Random.Range(-magnitude, magnitude);
        pos.z += Random.Range(-magnitude, magnitude);
        transform.position = pos;
    }
}
