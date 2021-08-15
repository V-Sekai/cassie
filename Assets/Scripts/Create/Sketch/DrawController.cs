using UnityEngine;
using Curve;
using VRSketch;
using System.Collections.Generic;

public class DrawController : MonoBehaviour
{
    [Header("Stroke prefabs")]
    // STROKE
    public GameObject inputStrokePrefab;
    public GameObject finalStrokePrefab;

    [Header("References")]

    [SerializeField]
    private CASSIEParametersProvider parameters = null;

    // BEAUTIFIER
    public Beautifier beautifier;

    // SCENE DATA
    public DrawingCanvas canvas;
    public SurfaceManager surfaceManager;
    public Grid3D grid;
    public MirrorPlane mirrorPlane;

    // Reference to check for collisions on stroke start
    public BrushCollisions collisionDetector;

    [Header("Parameters")]
    // PARAMETERS (systems)
    public bool Beautification;


    private int subdivisionsPerUnit;
    private float colliderRadius;


    private int finalStrokeID = 0;
    private InputStroke currentStroke = null;
    private int currentSelectedPatchID = -1;

    private void Start()
    {
        FinalStroke s = finalStrokePrefab.GetComponent<FinalStroke>();

        subdivisionsPerUnit = Mathf.CeilToInt(s.SubdivisionsPerUnit * 0.5f); // Reduce resolution by half
        colliderRadius = s.BaseCurveWidth * 0.5f; // The collider around the stroke is exactly the same radius as the stroke (BaseCurveWidth gives the diameter)
    }

    public void Init(bool surfacing)
    {
        finalStrokeID = 0;
        canvas.Init(surfacing);
    }

    public void SwitchSystem(bool surfacing)
    {
        // Set parameter
        canvas.SwitchSystem(surfacing);
    }

    public void NewStroke(Vector3 position)
    {
        GameObject strokeObject = canvas.Create(inputStrokePrefab, Primitive.Stroke);
        currentStroke = strokeObject.GetComponent<InputStroke>();

        // Check for stroke start point constraint
        TryAddGridConstraint(canvas.transform.InverseTransformPoint(position));
        collisionDetector.QueryCollision();

        if (currentSelectedPatchID != -1)
        {
            AddInSurfaceConstraint(currentSelectedPatchID, position);
        }
    }

    public void UpdateStroke(Vector3 position, Quaternion rotation, Vector3 velocity, float pressure)
    {
        if (!currentStroke.ShouldUpdate(position, parameters.Current.MinSamplingDistance))
            return;

        // Check if current selected patch is still nearby
        UpdateSelectedPatch(position);

        Vector3 brushNormal = canvas.transform.InverseTransformDirection(rotation * new Vector3(0, 0, 1)); // seems fine
        Vector3 relativePos = canvas.transform.InverseTransformPoint(position);

        //Debug.DrawLine(samplePos, samplePos + brushNormal * 0.1f, Color.white, 100f);

        Sample s = new Sample(relativePos, brushNormal, pressure, velocity);

        currentStroke.AddSample(s);

        TryAddGridConstraint(relativePos);

        if (currentStroke.Samples.Count > 1)
            RenderStroke(currentStroke); // Draw current stroke
    }

    public bool CommitStroke(Vector3 position, out SerializableStroke strokeData, bool mirror = false)
    {
        // Guard against invalid input
        if (!currentStroke.IsValid(parameters.Current.MinStrokeActionTime, parameters.Current.MinStrokeSize))
        {
            // Destroy input stroke
            currentStroke.Destroy();
            strokeData = new SerializableStroke(-1);
            return false;
        }

        (Curve.Curve snappedCurve,
        IntersectionConstraint[] intersections,
        MirrorPlaneConstraint[] mirrorIntersections) =
                beautifier.Beautify(
                    currentStroke,
                    Beautification,
                    mirror,
                    out List<SerializableConstraint> appliedConstraints,
                    out List<SerializableConstraint> rejectedConstraints,
                    out bool planar,
                    out bool onSurface,
                    out bool onMirror,
                    out bool closedLoop);

        if (!snappedCurve.IsValid(parameters.Current.MinStrokeSize))
        {
            // Destroy input stroke
            currentStroke.Destroy();
            strokeData = new SerializableStroke(-1);
            return false;
        }



        // Trim dangling endpoint bits
        // Consider all intersections to find the first and last one
        // If these intersections are near the stroke endpoint, cut the stroke there
        // And correct each on curve parameter t for other intersections 
        if (intersections.Length > 0 || (mirror && mirrorIntersections.Length > 0))
        {
            TrimDanglingEndpoints(snappedCurve, intersections, mirrorIntersections);
        }

        // Create final stroke game object and render the stroke
        GameObject strokeObject = canvas.Create(finalStrokePrefab, Primitive.Stroke);

        FinalStroke finalStroke = strokeObject.GetComponent<FinalStroke>();
        finalStroke.SetID(finalStrokeID);
        finalStrokeID++;
        finalStroke.SetCurve(snappedCurve, closedLoop: closedLoop);
        finalStroke.SaveInputSamples(currentStroke.GetPoints().ToArray());


        foreach (var intersection in intersections)
        {
            // OLD STROKE: Create or fetch node and create new segments if needed
            //Debug.Log("intersection param = " + intersection.NewCurveData.t.ToString("F6"));
            INode node = intersection.IntersectedStroke.AddIntersectionOldStroke(intersection.OldCurveData, parameters.Current.SnapToExistingNodeThreshold);

            // NEW STROKE: Insert node and create new segments if needed
            finalStroke.AddIntersectionNewStroke(node, intersection.NewCurveData, parameters.Current.MergeConstraintsThreshold);

            Debug.Log("[GRAPH UPDATE] added node with " + node.IncidentCount + " neighbors");
        }


        // Mirroring
        if (mirror)
        {

            // Create mirrored final stroke game object
            GameObject mirrorStrokeObject = canvas.Create(finalStrokePrefab, Primitive.Stroke);

            FinalStroke mirrorFinalStroke = mirrorStrokeObject.GetComponent<FinalStroke>();
            mirrorFinalStroke.SetID(finalStrokeID);

            // Set up mirror stroke curve and intersections
            bool mirrorSuccess = mirrorPlane.Mirror(
                finalStroke, ref mirrorFinalStroke,
                onMirror,
                intersections, mirrorIntersections,
                closedLoop,
                parameters.Current.ProximityThreshold,
                parameters.Current.SnapToExistingNodeThreshold,
                parameters.Current.MergeConstraintsThreshold,
                prevent_extra_mirroring: !Beautification);

            if (mirrorSuccess)
            {
                canvas.Add(mirrorFinalStroke);

                RenderStroke(mirrorFinalStroke);

                // Generate collider mesh
                SolidifyStroke(mirrorFinalStroke);

                finalStrokeID++;
            }
            else
            {
                // Abort
                mirrorFinalStroke.Destroy();
            }
        }

        //finalStroke.TrimDanglingSegments();

        canvas.Add(finalStroke);

        RenderStroke(finalStroke);

        // Generate collider mesh
        SolidifyStroke(finalStroke);


        // Record stroke data

        strokeData = new SerializableStroke(
            finalStroke.ID,
            finalStroke.GetControlPoints(),
            currentStroke.GetPoints(parameters.Current.ExportRDPError),
            appliedConstraints,
            rejectedConstraints,
            onSurface,
            planar,
            closedLoop
            );


        // Destroy input stroke
        currentStroke.Destroy();

        currentStroke = null;

        // Stop holding on to patch
        DeselectPatch(position);

        return true; // success in creating final stroke
    }

    private void RenderStroke(Stroke s)
    {
        s.RenderAsLine(canvas.transform.localScale.x);
    }

    // Called only when stroke is done drawing, to generate its collider
    private void SolidifyStroke(FinalStroke s)
    {
        //Mesh strokeMesh = brush.Solidify(s.Curve, true);
        int tubularSegments = Mathf.CeilToInt(s.Curve.GetLength() * subdivisionsPerUnit);

        Mesh strokeMesh = Tubular.Tubular.Build(s.Curve, tubularSegments, colliderRadius);

        s.UpdateCollider(strokeMesh);
    }

    public bool AddConstraint(Vector3 collisionPos, GameObject collided)
    {
        // If is drawing, create a new constraint and add it to the current stroke
        if (currentStroke)
        {
            // Find constraint type and exact position
            Constraint constraint;
            Vector3 relativePos = canvas.transform.InverseTransformPoint(collisionPos); // position of collision in canvas space

            switch (collided.tag)
            {
                case "GridPoint":
                    constraint = new Constraint(collided.transform.localPosition); // Grid point pos in canvas space
                    break;
                case "FinalStroke":
                    FinalStroke stroke = collided.GetComponent<FinalStroke>();
                    // Check if the curve can serve to create constraints
                    if (stroke != null)
                    {
                        constraint = stroke.GetConstraint(relativePos, parameters.Current.SnapToExistingNodeThreshold);
                    }
                    // Otherwise give up this constraint
                    else
                    {
                        constraint = new Constraint(relativePos);
                    }
                    break;
                case "MirrorPlane":
                    //Debug.Log("[Mirror] collided with plane");
                    Vector3 onPlanePos = mirrorPlane.Project(relativePos);
                    constraint = new MirrorPlaneConstraint(onPlanePos, mirrorPlane.GetNormal());
                    break;
                default:
                    //constraint = new Constraint(relativePos);
                    constraint = null;
                    break;
            }

            if (constraint != null)
            {
                currentStroke.AddConstraint(constraint, parameters.Current.MergeConstraintsThreshold);

                // Constraint was successfully added
                return true;
            }
        }
        
        // Didn't add constraint
        return false;
    }

    public void OnPatchCollide(int patchID, Vector3 pos)
    {
        if (currentSelectedPatchID != patchID)
        {
            if (currentSelectedPatchID != -1)
            {
                // Check whether we should select new patch or keep old one
                if (
                    !surfaceManager.ProjectOnPatch(patchID, pos, out Vector3 posOnPatch, canvasSpace: true)
                    || Vector3.Distance(pos, posOnPatch) > parameters.Current.ProximityThreshold)
                {
                    currentSelectedPatchID = patchID;

                    // Add constraint of surface ingoing point, if currently drawing
                    if (currentStroke)
                    {
                        AddInSurfaceConstraint(currentSelectedPatchID, pos);
                    }
                }
            }
            else
            {
                currentSelectedPatchID = patchID;

                // Add constraint of surface ingoing point, if currently drawing
                if (currentStroke)
                {
                    AddInSurfaceConstraint(currentSelectedPatchID, pos);
                }
            }

        }

    }

    public void OnPatchDeselect(Vector3 pos)
    {
        if (currentStroke)
        {
            // If we're currently drawing, prevent patch deselect based on pure collision events
            // we want to keep drawing projected on the same patch as we started

        }
        else
        {
            DeselectPatch(pos);
        }
    }

    private void DeselectPatch(Vector3 pos)
    {
        // Add constraint of surface outgoing point to draw controller
        if (currentStroke)
        {
            AddOutSurfaceConstraint(currentSelectedPatchID, pos);
        }
        else
            surfaceManager.OnDetailDrawStop(currentSelectedPatchID); // still send stop event to surface patch (to have correct appearance)
        currentSelectedPatchID = -1;
    }

    private bool AddInSurfaceConstraint(int patchID, Vector3 onStrokePos)
    {
        if (currentStroke)
        {
            // Check whether we are actually still close to the patch
            if (
                !surfaceManager.ProjectOnPatch(patchID, onStrokePos, out Vector3 posOnPatch, canvasSpace: false)
                || Vector3.Distance(onStrokePos, posOnPatch) > parameters.Current.ProximityThreshold)
            {
                //Debug.Log("went too far from patch");
                currentSelectedPatchID = -1;
                return false;
            }
            //Debug.Log("add constraint to patch " + patchID);
            surfaceManager.OnDetailDrawStart(patchID);
            currentStroke.InConstrainToSurface(patchID, canvas.transform.InverseTransformPoint(onStrokePos));
            return true;
        }
        return false;
    }

    private bool AddOutSurfaceConstraint(int patchID, Vector3 onStrokePos)
    {
        if (currentStroke)
        {
            surfaceManager.OnDetailDrawStop(patchID);
            currentStroke.OutConstrainToSurface(patchID, canvas.transform.InverseTransformPoint(onStrokePos));
            return true;
        }
        return false;
    }

    private bool GetProjectedPos(Vector3 brushPos, out Vector3 projectedPos, float maxDist)
    {
        projectedPos = brushPos;

        if (currentSelectedPatchID == -1)
            return false;

        if (surfaceManager.ProjectOnPatch(currentSelectedPatchID, brushPos, out projectedPos))
        {
            if (Vector3.Distance(projectedPos, brushPos) < maxDist)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateSelectedPatch(Vector3 brushPos)
    {

        if (currentSelectedPatchID != -1)
        {
            // A patch is currently selected,
            // Check whether it should still be selected,
            // otherwise, explicitly deselect it

            Vector3 projectedPos;
            if (!GetProjectedPos(brushPos, out projectedPos, maxDist: parameters.Current.ProximityThreshold))
            {
                //if (currentStroke)
                //    Debug.Log("got too far from patch while drawing");
                DeselectPatch(brushPos);
            }
        }
    }

    private bool TryAddGridConstraint(Vector3 pos)
    {
        if (grid.TryFindConstraint(pos, parameters.Current.ProximityThreshold, out Vector3 gridPointPos))
        {
            // Check for potential stroke/stroke intersection
            Collider[] overlapped = Physics.OverlapSphere(canvas.transform.TransformPoint(gridPointPos), parameters.Current.ProximityThreshold);
            Constraint constraint = null;
            Collider prioritary = null;
            foreach (var obj in overlapped)
            {
                if (obj.CompareTag("FinalStroke") && obj.GetComponent<FinalStroke>() != null)
                {
                    // Add stroke/stroke constraint insteand
                    constraint = obj.GetComponent<FinalStroke>().GetConstraint(pos, parameters.Current.SnapToExistingNodeThreshold);
                    prioritary = obj;
                }
                // Second prioritary is mirror plane
                if (obj.CompareTag("MirrorPlane") && prioritary == null)
                {
                    prioritary = obj;
                    // Add stroke/mirror constraint insteand
                    constraint = new MirrorPlaneConstraint(gridPointPos, mirrorPlane.GetNormal());
                }
            }
            if (constraint == null)
                constraint = new Constraint(gridPointPos); // Grid point pos is in canvas space

            currentStroke.AddConstraint(constraint, parameters.Current.MergeConstraintsThreshold);
            //Debug.Log("found grid constraint at " + pos.ToString("F3"));
            return true;
        }
        else
            return false;
    }

    private void TrimDanglingEndpoints(Curve.Curve curve, IntersectionConstraint[] intersections, MirrorPlaneConstraint[] mirrorIntersections)
    {
        Constraint firstIntersection;
        Constraint lastIntersection;

        // Initialize intersections
        if (intersections.Length > 0)
        {
            firstIntersection = intersections[0];
            lastIntersection = intersections[0];
        }
        else if (mirrorIntersections.Length > 0)
        {
            firstIntersection = mirrorIntersections[0];
            lastIntersection = mirrorIntersections[0];
        }
        else
        {
            // No intersections => won't trim endpoints
            return;
        }

        foreach (var intersection in intersections)
        {
            if (intersection.NewCurveData.t < firstIntersection.NewCurveData.t)
            {
                firstIntersection = intersection;
            }

            else if (intersection.NewCurveData.t > lastIntersection.NewCurveData.t)
            {
                lastIntersection = intersection;
            }

        }

        foreach (var intersection in mirrorIntersections)
        {
            if (intersection.NewCurveData.t < firstIntersection.NewCurveData.t)
            {
                firstIntersection = intersection;
            }

            else if (intersection.NewCurveData.t > lastIntersection.NewCurveData.t)
            {
                lastIntersection = intersection;
            }
        }

        float firstIntersectionParam = firstIntersection.NewCurveData.t;

        // Total length
        float length = curve.GetLength();

        // First segment length
        float firstSegmentLength = curve.LengthBetween(0f, firstIntersectionParam);

        // Cut if first segment is sufficiently small (yet still exists)
        if (firstSegmentLength > Constants.eps && firstSegmentLength < parameters.Current.MaxHookSectionStrokeRatio * length && firstSegmentLength < parameters.Current.MaxHookSectionLength)
        {
            //Debug.Log("cutting stroke at t = " + firstIntersectionParam);
            Reparameterization? r = curve.CutAt(firstIntersectionParam, throwBefore: true, snapToExistingAnchorThreshold: parameters.Current.SmallDistance * 0.1f);

            foreach (var intersection in intersections)
            {
                intersection.ReparameterizeNewCurve(r, curve);
            }

            foreach (var intersection in mirrorIntersections)
            {
                intersection.ReparameterizeNewCurve(r, curve);
            }

        }

        float lastIntersectionParam = lastIntersection.NewCurveData.t;

        length = curve.GetLength();
        float lastSegmentLength = curve.LengthBetween(lastIntersectionParam, 1f);

        // Cut if last segment is sufficiently small (yet still exists)
        if (lastSegmentLength > Constants.eps && lastSegmentLength < parameters.Current.MaxHookSectionStrokeRatio * length && lastSegmentLength < parameters.Current.MaxHookSectionLength)
        {
            //Debug.Log("cutting stroke at t = " + lastIntersectionParam);
            Reparameterization? r = curve.CutAt(lastIntersectionParam, throwBefore: false, snapToExistingAnchorThreshold: parameters.Current.SmallDistance * 0.1f);

            foreach (var intersection in intersections)
            {
                intersection.ReparameterizeNewCurve(r, curve);
            }


            foreach (var intersection in mirrorIntersections)
            {
                intersection.ReparameterizeNewCurve(r, curve);
            }
        }
    }

}

