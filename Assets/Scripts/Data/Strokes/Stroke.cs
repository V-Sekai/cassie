using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using VRSketch;
using Curve;

public abstract class Stroke : MonoBehaviour
{
    public bool CanvasSpaceConstantScale = true;
    public int SubdivisionsPerUnit = 150;
    public float BaseCurveWidth = 0.005f;

    private StrokeAppearance strokeAppearance;
    private LineRenderer lineRenderer;


    protected virtual void Awake()
    {
        strokeAppearance = gameObject.GetComponent<StrokeAppearance>();
        lineRenderer = GetComponent<LineRenderer>();
    }

    public abstract void RenderAsLine(float scale);

    protected void RenderPoints(Vector3[] points, float scale)
    {
        lineRenderer.enabled = true;
        lineRenderer.widthMultiplier = CanvasSpaceConstantScale ? BaseCurveWidth * scale : BaseCurveWidth;
        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
    }

    public void UpdateWidth(float newScale)
    {
        if (CanvasSpaceConstantScale)
            lineRenderer.widthMultiplier = BaseCurveWidth * newScale;
    }

    public void UpdateCollider(Mesh colliderMesh)
    {
        // Generate collider
        MeshCollider collider = gameObject.GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = colliderMesh;
    }

    public void OnDeleteSelect()
    {
        strokeAppearance.OnDeleteSelect();
    }

    public void OnDeleteDeselect()
    {
        strokeAppearance.OnDeleteDeselect();
    }


    public virtual void Destroy()
    {
        Destroy(gameObject);
    }
}
