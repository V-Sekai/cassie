using UnityEngine;

public class SetCameraBackground : MonoBehaviour
{
    void Start()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Editing grey (darker grey)
        }
    }
}