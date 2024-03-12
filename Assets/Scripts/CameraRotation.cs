using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRotation : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    public float sensitivity = 2; // чувствительность мышки
    public float limit = 80; // ограничение вращения по Y
    public float zoom = 0.5f; // чувствительность при увеличении, колесиком мышки
    public float zoomMax = 10; // макс. увеличение
    public float zoomMin = 3; // мин. увеличение
    private float _orbitDamping = 10;
    private Vector3 _localRot;

    void Start () 
    {
        /*limit = Mathf.Abs(limit);
        if(limit > 90) limit = 90;
        offset = new Vector3(offset.x, offset.y, -Mathf.Abs(zoomMax)/2);
        transform.position = target.position + offset;*/
    }

    void LateUpdate ()
    {
        /*if(Input.GetAxis("Mouse ScrollWheel") > 0) offset.z += zoom;
        else if(Input.GetAxis("Mouse ScrollWheel") < 0) offset.z -= zoom;
        offset.z = Mathf.Clamp(offset.z, -Mathf.Abs(zoomMax), -Mathf.Abs(zoomMin));
        transform.position = new Vector3(offset.z, transform.position.y, transform.position.z);*/

        /*X = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * sensitivity;
        Y += Input.GetAxis("Mouse Y") * sensitivity;
        Y = Mathf.Clamp (Y, -limit, limit);
        transform.localEulerAngles = new Vector3(-Y, X, 0);
        transform.position = transform.localRotation * offset + target.position;
        transform.LookAt(target);*/

        if (!Input.GetMouseButton(1)) return;
        
        _localRot.x += Input.GetAxis("Mouse X") * sensitivity;
        _localRot.y -= Input.GetAxis("Mouse Y") * sensitivity;

        _localRot.y = Mathf.Clamp(_localRot.y, -limit, limit);

        var qt = Quaternion.Euler(0, _localRot.x, _localRot.y);
        transform.rotation = Quaternion.Lerp(transform.rotation, qt, Time.deltaTime * _orbitDamping);
    }
}
