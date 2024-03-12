using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CameraZoom : MonoBehaviour
{
    public Vector3 offset = new Vector3(70, 10, 0);
    public float zoomSensitivity = 0.5f; // чувствительность при увеличении, колесиком мышки
    public float zoomMax = 10; // макс. увеличение
    public float zoomMin = 3; // мин. увеличение
    private float _zoom;
    
    void LateUpdate()
    {
        if(Input.GetAxis("Mouse ScrollWheel") > 0) offset.x += zoomSensitivity;
        else if(Input.GetAxis("Mouse ScrollWheel") < 0) offset.x -= zoomSensitivity;
        _zoom = Mathf.Clamp(_zoom, -Mathf.Abs(zoomMax), -Mathf.Abs(zoomMin));
        transform.position = offset;
    }
}
