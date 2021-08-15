using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZoomController : MonoBehaviour
{
    public DrawingCanvas Canvas;
    public float MaxScale = 3f;
    public float MinScale = 1f;

    private float startHandsDistance;
    private float startScale;

    public void StartZoom(float handsDistance)
    {
        startHandsDistance = handsDistance;
        startScale = Canvas.transform.localScale.x;
    }

    public bool UpdateZoom(Vector3 zoomCenter, float currentHandsDistance, out float newScale)
    {
        newScale = startScale * currentHandsDistance / startHandsDistance;
        if (newScale <= MinScale)
        {
            newScale = MinScale;
            return false;
        }
        if (newScale >= MaxScale)
        {
            newScale = MaxScale;
            return false;
        }
        Canvas.Scale(newScale, zoomCenter);
        return true;
    }

    public void ResetScale()
    {
        Canvas.Scale(1f, Vector3.zero);
    }
}
