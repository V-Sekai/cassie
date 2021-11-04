using Curve;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRSketch;

public class MirrorPlane : MonoBehaviour
{

    //public DrawingCanvas drawingCanvas;
    public GameObject sphereGizmoPrefab;

    private Dictionary<int, FinalStroke> MirroredStrokes = new Dictionary<int, FinalStroke>();
    private VRSketch.Plane plane;
    private MeshCollider planeCollider;
    private MeshRenderer planeRenderer;
    private MeshFilter planeMeshFilter;

    private void Awake()
    {
        planeMeshFilter = GetComponent<MeshFilter>();
        planeCollider = GetComponent<MeshCollider>();
        planeRenderer = GetComponent<MeshRenderer>();

        //SetPlane(Vector3.right, Vector3.zero);

        // Hide plane
        Hide();
    }

    public void Hide()
    {
        // Clear pairings
        //MirroredStrokes.Clear();
        // Hide plane
        planeCollider.enabled = false;
        planeRenderer.enabled = false;
    }

    public void Show()
    {
        planeCollider.enabled = true;
        planeRenderer.enabled = true;
    }

    public void Clear()
    {
        MirroredStrokes.Clear();
    }

    public void SetPlane(Vector3 normal, Vector3 position)
    {
        // Clear pairings
        MirroredStrokes.Clear();

        plane = new VRSketch.Plane(normal, position);

        // Generate plane mesh
        //MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh planeMesh = new Mesh();

        float width = 10f;
        float height = 10f;

        Vector3 v = Vector3.up;
        if (Vector3.Cross(v, normal).magnitude < 0.1f)
            v = Vector3.right;
        if (Vector3.Cross(v, normal).magnitude < 0.1f)
            v = Vector3.forward;
        Vector3 u1 = Vector3.Cross(v, normal).normalized;
        Vector3 u2 = Vector3.Cross(u1, normal);

        planeMesh.vertices = new Vector3[] {
             position + u1 * -width + u2 * -height,
             position + u1 * width + u2 * -height,
             position + u1 * width + u2 * height,
             position + u1 * -width + u2 * height,
         };

        planeMesh.uv = new Vector2[]
        {
            new Vector2(-width, - height),
            new Vector2(width, -height),
            new Vector2(width, height),
            new Vector2(-width, height)
        };

        planeMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };

        planeMeshFilter.mesh = planeMesh;

        // Collider
        planeCollider.sharedMesh = null;
        planeCollider.sharedMesh = planeMesh;

        Show();
    }

    public Vector3 Project(Vector3 p)
    {
        return plane.Project(p);
    }

    public Vector3 GetNormal()
    {
        return plane.n;
    }

    public Vector3 Mirror(Vector3 p)
    {
        return plane.Mirror(p);
    }

    public bool Mirror(
        FinalStroke inputStroke,
        ref FinalStroke mirroredStroke,
        bool projectedOnMirror,
        IntersectionConstraint[] intersections, MirrorPlaneConstraint[] mirrorIntersections,
        bool closedLoop,
        float projectionReflectionThreshold,
        float snapToExistingNodeThreshold,
        float mergeConstraintsThreshold,
        bool prevent_extra_mirroring)
    {
        if (projectedOnMirror)
        {
            //Debug.Log("[Mirror] stroke in plane!");
            MirroredStrokes.Add(inputStroke.ID, inputStroke);
            return false;
        }

        Curve.Curve inputCurve = inputStroke.Curve;
        Vector3 start = inputCurve.GetPointOnCurve(0f).Position;
        Vector3 end = inputCurve.GetPointOnCurve(1f).Position;

        Curve.Curve mirrorCurve = Mirror(inputCurve, out float score);
        //Debug.Log("[Mirror] score: " + score);

        // If score is low, it indicates that the mirror version is not viable
        // stroke almost in plane or crossing plane
        if (prevent_extra_mirroring)
        {
            //if (score < proximity_threshold * 2f)
            if (score < projectionReflectionThreshold)
            {
                //Debug.Log("[Mirror] stroke crosses plane!");
                return false;
            }
        }



        mirroredStroke.SetCurve(mirrorCurve, closedLoop);
        mirroredStroke.SaveInputSamples(Mirror(inputStroke.inputSamples));

        // Pair both strokes
        MirroredStrokes.Add(inputStroke.ID, mirroredStroke);
        MirroredStrokes.Add(mirroredStroke.ID, inputStroke);

        MirrorIntersections(inputStroke, mirroredStroke, intersections, mirrorIntersections, snapToExistingNodeThreshold, mergeConstraintsThreshold);

        return true;
    }

    public bool TryGetSymmetric(FinalStroke stroke, out FinalStroke mirroredStroke)
    {
        if (MirroredStrokes.TryGetValue(stroke.ID, out mirroredStroke))
        {
            return true;
        }
        return false;
    }

    public void Delete(FinalStroke stroke)
    {
        MirroredStrokes.Remove(stroke.ID);
    }

    public Curve.Curve Project(Curve.Curve inputCurve, float angularProximityThreshold, out float score)
    {
        Curve.Curve result = null;
        score = 0f;

        // Check tangents: if they are orthogonal to the plane, it indicates that the user probably didn't intend to have the stroke projected in plane
        Vector3 startTangent = inputCurve.GetPointOnCurve(0f).Tangent;
        Vector3 endTangent = inputCurve.GetPointOnCurve(1f).Tangent;

        if (Mathf.Abs(Vector3.Dot(plane.n, startTangent)) > Mathf.Cos(angularProximityThreshold) || Mathf.Abs(Vector3.Dot(plane.n, endTangent)) > Mathf.Cos(angularProximityThreshold))
        {
            score = 100f;
            return result;
        }

        if (inputCurve as LineCurve != null)
        {
            result = LineCurve.ProjectOnPlane((LineCurve)inputCurve, plane, out score);
        }
        if (inputCurve as BezierCurve != null)
        {
            result = BezierCurve.ProjectOnPlane((BezierCurve)inputCurve, plane, out score);
        }
        return result;
    }

    private Curve.Curve Mirror(Curve.Curve inputCurve, out float score)
    {
        Curve.Curve mirrorCurve = null;
        score = 0f;

        if (inputCurve as LineCurve != null)
        {
            mirrorCurve = LineCurve.Mirror((LineCurve)inputCurve, plane, out score);
        }
        if (inputCurve as BezierCurve != null)
        {
            mirrorCurve = BezierCurve.Mirror((BezierCurve)inputCurve, plane, out score);
        }

        return mirrorCurve;
    }

    private Vector3[] Mirror(Vector3[] points)
    {
        Vector3[] mirroredPts = new Vector3[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            mirroredPts[i] = plane.Mirror(points[i]);
        }

        return mirroredPts;
    }

    private void MirrorIntersections(
        FinalStroke inputStroke,
        FinalStroke mirroredStroke,
        IntersectionConstraint[] intersections, MirrorPlaneConstraint[] mirrorIntersections,
        float snapToExistingNodeThreshold,
        float mergeConstraintsThreshold)
    {
        // Update curve network graph with new intersections
        foreach (var intersection in intersections)
        {
            // Create or fetch node and create new segments on old stroke if needed
            if (MirroredStrokes.TryGetValue(intersection.IntersectedStroke.ID, out FinalStroke mirroredIntersectedStroke))
            {
                //Debug.Log("mirrored intersected stroke " + mirroredIntersectedStroke.ID);
                PointOnCurve mirroredOldStrokeData = intersection.OldCurveData.Mirror(plane);
                INode node = mirroredIntersectedStroke.AddIntersectionOldStroke(mirroredOldStrokeData, snapToExistingNodeThreshold);

                // Create segments on new stroke
                PointOnCurve mirroredNewStrokeData = intersection.NewCurveData.Mirror(plane);
                mirroredStroke.AddIntersectionNewStroke(node, mirroredNewStrokeData, mergeConstraintsThreshold);

                //Debug.Log("added mirrored node");
            }
        }

        foreach (var selfInters in mirrorIntersections)
        {
            INode node = inputStroke.AddIntersectionOldStroke(selfInters.NewCurveData, snapToExistingNodeThreshold);
            mirroredStroke.AddIntersectionNewStroke(node, selfInters.NewCurveData, mergeConstraintsThreshold);
        }
    }
}
