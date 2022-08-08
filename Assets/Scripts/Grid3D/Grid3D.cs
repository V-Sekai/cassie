using System.Collections.Generic;
using UnityEngine;

public struct GridState
{
    public bool GridActive { get; }
    public bool GridLinesDisplay { get; }

    public GridState(bool gridActive, bool displayLines)
    {
        this.GridActive = gridActive;
        this.GridLinesDisplay = displayLines;
    }
}

public class Grid3D : MonoBehaviour
{
    [Header("Grid Procedural Shader")]
    public Shader shader;

    [Header("Grid Settings")]
    public Vector3 Center;
    public bool DisplayGridLines = false;
    public int BasePointsPerDimension = 30;
    public float BaseDistanceBetweenPoints = 0.01f;

    [Header("Grid Appearance Settings")]
    [Tooltip("This value is in pixels I think, so it's resolution dependent.")]
    public float PointRadius = 20;
    public float NearFade = 0.01f; // Relative to the eye
    public float FarFade = 0.2f; // Relative to the hand
    public float FadeZone = 0.1f;
    public float BaseOpacity = 1f;
    public Color Color = Color.blue;

    private LinkedListNode<GridState> state;
    private LinkedList<GridState> possibleStates;

    private Material material;

    // Shader uniforms indices
    private int _DisplayGridLinesIdx;
    private int _CanvasToWorldMatrixIdx;
    private int _GridAnchorPosIdx;
    private int _DistanceBetweenPointsIdx;
    private int _PointsPerDimIdx;
    private int _PointRadiusIdx;
    private int _NearFadeIdx;
    private int _FarFadeIdx;
    private int _FadeZoneIdx;
    private int _FocusPointIdx;
    private int _BaseOpacityIdx;
    private int _ColorIdx;

    private float distanceBetweenPoints;
    private int pointsPerDim;
    private Vector3 gridAnchorPos;
    private float gridMaxDim;
    private Vector3 primaryHandPos = Vector3.zero;


    void Awake()
    {
        possibleStates = new LinkedList<GridState>();
        possibleStates.AddLast(new GridState(true, DisplayGridLines));
        possibleStates.AddLast(new GridState(false, false));

        state = possibleStates.First;

        // From desired grid properties,
        // Compute grid anchor position and grid bounds
        pointsPerDim = BasePointsPerDimension;
        gridMaxDim = (pointsPerDim - 1) * BaseDistanceBetweenPoints;

        // Place anchor position such that Center is approx the center of the grid
        gridAnchorPos = Center - new Vector3(gridMaxDim * 0.5f, gridMaxDim * 0.5f, gridMaxDim * 0.5f);

        material = new Material(shader);

        distanceBetweenPoints = BaseDistanceBetweenPoints;


        // Shader uniform indices

        _DisplayGridLinesIdx = Shader.PropertyToID("_DisplayGridLines");

        _GridAnchorPosIdx = Shader.PropertyToID("_GridAnchorPos");
        _DistanceBetweenPointsIdx = Shader.PropertyToID("_DistanceBetweenPoints");
        _PointsPerDimIdx = Shader.PropertyToID("_PointsPerDim");
        _PointRadiusIdx = Shader.PropertyToID("_PointRadius");
        _CanvasToWorldMatrixIdx = Shader.PropertyToID("_CanvasToWorldMatrix");

        _NearFadeIdx = Shader.PropertyToID("_NearFade");
        _FarFadeIdx = Shader.PropertyToID("_FarFade");
        _FadeZoneIdx = Shader.PropertyToID("_FadeZone");
        _FocusPointIdx = Shader.PropertyToID("_FocusPoint");
        _BaseOpacityIdx = Shader.PropertyToID("_BaseOpacity");
        _ColorIdx = Shader.PropertyToID("_Color");

        // Set shader uniform values

        material.SetInt(_DisplayGridLinesIdx, DisplayGridLines ? 1 : 0);

        material.SetMatrix(_CanvasToWorldMatrixIdx, transform.localToWorldMatrix);
        material.SetVector(_GridAnchorPosIdx, gridAnchorPos);
        material.SetInt(_PointsPerDimIdx, pointsPerDim);
        material.SetFloat(_DistanceBetweenPointsIdx, distanceBetweenPoints);
        material.SetFloat(_PointRadiusIdx, PointRadius);

        material.SetFloat(_NearFadeIdx, NearFade);
        material.SetFloat(_FarFadeIdx, FarFade);
        material.SetFloat(_FadeZoneIdx, FadeZone);
        material.SetVector(_FocusPointIdx, primaryHandPos);
        material.SetColor(_ColorIdx, Color);
        material.SetFloat(_BaseOpacityIdx, BaseOpacity);
    }

    void OnRenderObject()
    {
        if (state.Value.GridActive)
        {
            material.SetPass(0);

            // Draw all grid points
            int N = pointsPerDim * pointsPerDim * pointsPerDim;
            Graphics.DrawProceduralNow(MeshTopology.Points, N, 1);
        }
    }

    public void ToggleGridState()
    {
        // Change state to the next possible one
        state = state.Next ?? possibleStates.First;

        // Update display grid lines uniform
        material.SetInt(_DisplayGridLinesIdx, state.Value.GridLinesDisplay ? 1 : 0);
    }

    public void Refresh(Vector3 primaryHandPos)
    {
        this.primaryHandPos = primaryHandPos;
        // Update focus point position
        material.SetVector(_FocusPointIdx, primaryHandPos);
    }

    public void OnCanvasMove()
    {
        // Update transform
        material.SetMatrix(_CanvasToWorldMatrixIdx, transform.localToWorldMatrix);
    }

    public bool TryFindConstraint(Vector3 inputPos, float proximity_threshold, out Vector3 gridPointPos)
    {
        bool success = false;
        gridPointPos = Vector3.zero;

        if (!state.Value.GridActive)
            return false;

        // Find closest grid point position from inputPos (in canvas space)
        Vector3 inputPosRel = inputPos - gridAnchorPos;

        int xIdx = Mathf.RoundToInt(inputPosRel.x / distanceBetweenPoints);
        int yIdx = Mathf.RoundToInt(inputPosRel.y / distanceBetweenPoints);
        int zIdx = Mathf.RoundToInt(inputPosRel.z / distanceBetweenPoints);

        // Check if we are in grid bounds
        if (xIdx < 0 || xIdx >= pointsPerDim
         || yIdx < 0 || yIdx >= pointsPerDim
         || zIdx < 0 || zIdx >= pointsPerDim
         )
        {
            Debug.LogError("out of grid bounds");
            return false;
        }

        Vector3 closestGridPoint = gridAnchorPos + new Vector3(xIdx * distanceBetweenPoints,
                                                        yIdx * distanceBetweenPoints,
                                                        zIdx * distanceBetweenPoints);

        if (Vector3.Distance(closestGridPoint, inputPos) < proximity_threshold)
        {
            gridPointPos = closestGridPoint;
            return true;
        }

        return success;
    }

    public void Scale(float newScale)
    {
        material.SetFloat(_PointRadiusIdx, PointRadius * Mathf.Pow(newScale, 0.3f));

        // Double the resolution past a threshold
        float farFadeNew = FarFade;
        if (newScale >= 2f)
        {
            distanceBetweenPoints = BaseDistanceBetweenPoints / 2f;
            farFadeNew = FarFade * 1.2f;
            pointsPerDim = BasePointsPerDimension * 2;
        }
        else
        {
            distanceBetweenPoints = BaseDistanceBetweenPoints;
            pointsPerDim = BasePointsPerDimension;
        }

        // Update grid properties
        gridMaxDim = (pointsPerDim - 1) * BaseDistanceBetweenPoints;

        // Update shader uniforms
        material.SetFloat(_DistanceBetweenPointsIdx, distanceBetweenPoints);
        material.SetInt(_PointsPerDimIdx, pointsPerDim);
        material.SetFloat(_FarFadeIdx, farFadeNew);

        // Update transform
        material.SetMatrix(_CanvasToWorldMatrixIdx, transform.localToWorldMatrix);
    }

    public void OnTransformStart()
    {
        material.SetFloat(_BaseOpacityIdx, 0.5f);
    }

    public void OnTransformEnd()
    {
        material.SetFloat(_BaseOpacityIdx, BaseOpacity);
    }

}
