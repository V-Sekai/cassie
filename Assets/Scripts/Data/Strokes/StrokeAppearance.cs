using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StrokeAppearance : MonoBehaviour
{
    // Different states, different materials
    public Material DeleteSelectMaterial;

    private LineRenderer lineRenderer;
    private Material baseMaterial;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        baseMaterial = lineRenderer.material;
    }

    public void OnDeleteSelect()
    {
        lineRenderer.material = DeleteSelectMaterial;
    }

    public void OnDeleteDeselect()
    {
        lineRenderer.material = baseMaterial;
    }

}
