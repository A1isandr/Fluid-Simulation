using UnityEngine;


public class CameraRotation : MonoBehaviour
{
    public float sensitivity = 2; // чувствительность мышки
    public float limit = 80; // ограничение вращения по Y
    private float _orbitDamping = 10;
    private Vector3 _localRot;

    void LateUpdate ()
    {
        if (!Input.GetMouseButton(0)) return;
        
        _localRot.x += Input.GetAxis("Mouse X") * sensitivity;
        _localRot.y -= Input.GetAxis("Mouse Y") * sensitivity;

        _localRot.y = Mathf.Clamp(_localRot.y, -limit, limit);

        var qt = Quaternion.Euler(0, _localRot.x, _localRot.y);
        transform.rotation = Quaternion.Lerp(transform.rotation, qt, Time.deltaTime * _orbitDamping);
    }
}
