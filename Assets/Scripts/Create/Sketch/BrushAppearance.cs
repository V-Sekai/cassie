using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRSketch;

public class BrushAppearance : MonoBehaviour
{
    public GameObject DotGizmoPrefab;
    public Color GrabColor = Color.yellow;
    public Color ZoomColor = Color.yellow;
    public Color NoOpColor = Color.red;
    public Color FreehandColor = Color.green;
    public Color CASSIEColor = Color.blue;

    private TrailRenderer trail;
    private Material material;
    private Color currentDrawingColor;

    IEnumerator FadeNoOp()
    {
        for (float ft = 0f; ft <= 1f; ft += 0.01f)
        {
            Color c = Color.Lerp(NoOpColor, currentDrawingColor, ft);
            material.color = c;

            if (ft == 1f)
                trail.enabled = true;
            yield return null;
        }
    }


    void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        material = GetComponent<MeshRenderer>().material;
        currentDrawingColor = material.color;
    }

    public void OnDrawStart()
    {
        material.color = currentDrawingColor;
        trail.enabled = false;
    }

    public void OnDrawEnd()
    {
        trail.enabled = true;
    }

    public void OnGrabStart()
    {
        trail.enabled = false;
        material.color = GrabColor;
    }

    public void OnGrabEnd()
    {
        trail.enabled = true;
        material.color = currentDrawingColor;
    }

    public void OnZoomStart()
    {
        trail.enabled = false;
        material.color = ZoomColor;
    }
    public void OnZoomEnd()
    {
        trail.enabled = true;
        material.color = currentDrawingColor;
    }

    public void OnNoOp()
    {
        trail.enabled = false;
        StartCoroutine("FadeNoOp");
    }

    public void OnModeChange(SketchSystem mode)
    {
        if (mode.Equals(SketchSystem.Baseline))
        {
            material.color = FreehandColor;
            trail.material.color = FreehandColor;
            currentDrawingColor = FreehandColor;
        }
        else
        {
            material.color = CASSIEColor;
            trail.material.color = CASSIEColor;
            currentDrawingColor = CASSIEColor;
        }
    }
}
