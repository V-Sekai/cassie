using UnityEngine;
using Curve;
using VRSketch;
using System.Collections.Generic;
using System;
using MathNet.Numerics.LinearAlgebra;
using System.IO;

public class Beautifier : MonoBehaviour
{

    public int MaxBeziersForSolver = 15;

    public bool DebugVisualization = false;

    [SerializeField]
    private CASSIEParametersProvider parameters = null;

    // 3D GRID
    public Grid3D grid;
    // CANVAS
    public DrawingCanvas canvas;

    // Visualizer
    private ConstraintsVisualizer visualizer;

    private static StreamWriter BeautifierLogStream;
    private static System.Diagnostics.Stopwatch performanceTimer = new System.Diagnostics.Stopwatch();

    private void Awake()
    {
        MathNet.Numerics.Providers.Common.Mkl.MklProvider.Load(Application.productName + "_Data/Plugins/x86_64");
        var usingNativeMKL = MathNet.Numerics.Control.TryUseNativeMKL();
        Debug.Log("Using native MKL: " + usingNativeMKL);

        //var usingOpenBLAS = MathNet.Numerics.Control.TryUseNativeOpenBLAS();
        //Debug.Log("Using native OpenBLAS: " + usingOpenBLAS);

        //MathNet.Numerics.Control.UseManaged();

        // Call Math.Net here so that it won't lag on first call at runtime
        double[,] m_array = {{ 1.0, 2.0 },
               { 3.0, 4.0 }};
        var m = Matrix<double>.Build.DenseOfArray(m_array);
        Vector<double> v = Vector<double>.Build.Dense(2);

        Vector<double> x = m.Solve(v);

        if (DebugVisualization)
            visualizer = GetComponent<ConstraintsVisualizer>();
        else
        {
            GetComponent<ConstraintsVisualizer>().enabled = false;
        }

        if (ConstraintSolver.LogPerformance)
            ConstraintSolver.LogStream = new System.IO.StreamWriter("solver_log.txt");
    }

    private void OnDestroy()
    {
        if (ConstraintSolver.LogPerformance)
            ConstraintSolver.LogStream.Close();
    }

    // Replaces s by best matching candidate
    public (Curve.Curve, IntersectionConstraint[], MirrorPlaneConstraint[]) Beautify(
        InputStroke inputStroke,
        bool FitToConstraints,
        bool mirror,
        out List<SerializableConstraint> appliedConstraints, out List<SerializableConstraint> rejectedConstraints, out bool planar, out bool onSurface, out bool onMirror, out bool isClosed)
    {

        planar = false;
        onSurface = false;
        onMirror = false;
        isClosed = false;
        appliedConstraints = new List<SerializableConstraint>(0);
        rejectedConstraints = new List<SerializableConstraint>(0);

        Curve.Curve curve;
        IntersectionConstraint[] intersections = new IntersectionConstraint[0];
        MirrorPlaneConstraint[] mirrorPlaneConstraints = new MirrorPlaneConstraint[0];

        // Snap to line?
        // Only if we're not constrained to a surface

        List<Vector3> inputSamples = inputStroke.GetSafePoints(ablationDuration: parameters.Current.SamplesAblationDuration);

        float curveLength = inputStroke.Length;

        // Strokes that should become lines:
        //    - very short strokes
        //    - strokes that are close to linear and drawn fast

        float lineLength = Vector3.Distance(inputSamples[0], inputSamples[inputSamples.Count - 1]);
        //float lineDrawingSpeed = canvas.Small / 0.05f;
        float lineDrawingSpeed = parameters.Current.SmallDistance / 0.05f;
        if (curveLength < parameters.Current.SmallDistance || (Mathf.Abs(curveLength - lineLength) / lineLength < parameters.Current.SmallDistance && inputStroke.AverageDrawingSpeed() > lineDrawingSpeed))
        {
            curve = new LineCurve(inputSamples[0], inputSamples[inputSamples.Count - 1], 1f, 1f); // Last 2 parameters are unused pressure values
        }
        else
        {
            // Replace by a cubic bezier curve
            List<float> weights = inputStroke.GetWeights();

            // First remove hooks and break at corners
            List<List<Vector3>> G1sections = inputStroke.GetG1sections(
                discontinuityAngularThreshold: parameters.Current.MaxAngularVariationInG1Section,
                hookDiscontinuityAngularThreshold: parameters.Current.SmallAngle,
                ablationDuration: parameters.Current.SamplesAblationDuration,
                minSectionLength: parameters.Current.MinG1SectionLength,
                maxHookLength: parameters.Current.MaxHookSectionLength,
                maxHookStrokeRatio: parameters.Current.MaxHookSectionStrokeRatio);
            //Debug.Log("[g1sections] there are " + G1sections.Count + " sections");

            // Fit one polybezier curve per section
            List<CubicBezier> allBeziers = new List<CubicBezier>();
            foreach (var section in G1sections)
            {
                CubicBezier[] beziers = BezierCurve.FitCurve(
                    points: section,
                    error: parameters.Current.BezierFittingError,
                    rdpError: parameters.Current.SmallDistance * 0.1f);
                allBeziers.AddRange(beziers);
            }
            // Form back one complete bezier curve object made of all the G1 pieces
            curve = new BezierCurve(allBeziers.ToArray(), weights);

            //canvas.mirrorPlane.GetIntersections(curve);

        }

        if (FitToConstraints)
        {
            // Correct intersections
            // This corrects intersection constraints positions by looking in a small neighborhood on the intersected curve
            List<Constraint> correctedConstraints = CorrectIntersections(
                constraints: inputStroke.Constraints,
                curve: curve,
                search_distance: parameters.Current.SmallDistance,
                N_steps: 5);

            // Self intersection at endpoints?
            if (curve as BezierCurve != null)
            {
                PointOnCurve start = curve.GetPointOnCurve(0f);
                PointOnCurve end = curve.GetPointOnCurve(1f);
                if (Vector3.Distance(start.Position, end.Position) < parameters.Current.ProximityThreshold
                    && ((BezierCurve)curve).beziers.Length > 1
                    )
                {
                    //Debug.Log("curve is closed");
                    isClosed = true;
                }
            }


            // Deal with overlaps with other strokes by cutting the new stroke if needed
            // Look at first and last constraints, check if the new stroke aligns well with the intersected stroke at those points

            if (!isClosed && correctedConstraints.Count > 0 && correctedConstraints[0] as IntersectionConstraint != null)
            {
                IntersectionConstraint firstIntersection = (IntersectionConstraint)correctedConstraints[0];
                PointOnCurve newStrokeStart = curve.GetPointOnCurve(0f);

                // Check if tangents are aligned and if the intersection is close to beginning of the new stroke
                if (Vector3.Distance(firstIntersection.Position, newStrokeStart.Position) < parameters.Current.ProximityThreshold
                    && Mathf.Abs(Vector3.Dot(firstIntersection.OldCurveData.Tangent, newStrokeStart.Tangent)) > Mathf.Cos(parameters.Current.SmallAngle)
                    )
                {
                    //Debug.Log("[OVERLAPPING] prevent overlap at stroke start");
                    // Modify constraint
                    correctedConstraints.RemoveAt(0);
                    IntersectionConstraint newConstraint = firstIntersection.IntersectedStroke.GetConstraint(firstIntersection.Position, parameters.Current.SnapToExistingNodeThreshold);
                    correctedConstraints.Insert(0, newConstraint);

                    // Cut input stroke
                    PointOnCurve newStrokeCorrectedStart = curve.Project(newConstraint.Position);
                    //Debug.Log("cutting at t = " + newStrokeCorrectedStart.t);
                    curve.CutAt(newStrokeCorrectedStart.t, throwBefore: true, snapToExistingAnchorThreshold: parameters.Current.SmallDistance * 0.1f);
                }
            }

            if (!isClosed && correctedConstraints.Count > 0 && correctedConstraints[correctedConstraints.Count - 1] as IntersectionConstraint != null)
            {
                IntersectionConstraint lastIntersection = (IntersectionConstraint)correctedConstraints[correctedConstraints.Count - 1];
                PointOnCurve newStrokeEnd = curve.GetPointOnCurve(1f);

                // Check if tangents are aligned and if the intersection is close to beginning of the new stroke
                if (Vector3.Distance(lastIntersection.Position, newStrokeEnd.Position) < parameters.Current.ProximityThreshold
                    && Mathf.Abs(Vector3.Dot(lastIntersection.OldCurveData.Tangent, newStrokeEnd.Tangent)) > Mathf.Cos(parameters.Current.SmallAngle)
                    )
                {
                    //Debug.Log("[OVERLAPPING] prevent overlap at stroke end");
                    correctedConstraints.RemoveAt(correctedConstraints.Count - 1);
                    IntersectionConstraint newConstraint = lastIntersection.IntersectedStroke.GetConstraint(lastIntersection.Position, parameters.Current.SnapToExistingNodeThreshold);
                    correctedConstraints.Add(newConstraint);

                    // Cut input stroke
                    PointOnCurve newStrokeCorrectedEnd = curve.Project(newConstraint.Position);
                    //Debug.Log("cutting at t = " + newStrokeCorrectedEnd.t);
                    curve.CutAt(newStrokeCorrectedEnd.t, throwBefore: false, snapToExistingAnchorThreshold: parameters.Current.SmallDistance * 0.1f);
                }
            }

            

            if (curve as BezierCurve != null)
            {
                //Debug.Log("count " + ((BezierCurve)curve).GetBezierCountBetween(0f, 1f));
                // Avoid beautifying overly long curves
                int N_bez = ((BezierCurve)curve).GetBezierCountBetween(0f, 1f);
                if (N_bez > MaxBeziersForSolver)
                {
                    return (curve, intersections, mirrorPlaneConstraints);
                }

                // Only treat non degenerate curve (ie, a curve that has non collapsed ctrl polygon edges)
                if (((BezierCurve)curve).IsNonDegenerate())
                {
                    ConstraintSolver solver = new ConstraintSolver(
                        (BezierCurve)curve,
                        //inputStroke.Constraints.ToArray(),
                        correctedConstraints.ToArray(),
                        canvas.OrthoDirections,
                        mu_fidelity: parameters.Current.MuFidelity,
                        proximityThreshold: parameters.Current.ProximityThreshold,
                        minDistanceBetweenAnchors: parameters.Current.MinDistanceBetweenAnchors,
                        angularProximityThreshold: parameters.Current.SmallAngle,
                        is_closed: isClosed,
                        planarity_allowed: true // do not allow planarity if the stroke is constrained to a surface
                    );

                    (curve, intersections, mirrorPlaneConstraints) = solver.GetBestFit(
                        out appliedConstraints,
                        out rejectedConstraints,
                        out List<int> constrainedAnchorIdx,
                        out planar,
                        out isClosed);

                    // Project on surfaces
                    if (parameters.Current.ProjectOnSurface && inputStroke.SurfaceConstraints.Count > 0)
                    {
                        BezierCurve input = (BezierCurve)curve;
                        bool projectedOnSurface = ProjectOnSurfaces(
                            input,
                            inputStroke.SurfaceConstraints,
                            intersections,
                            constrainedAnchorIdx,
                            isClosed,
                            out curve);
                        onSurface = projectedOnSurface;
                    }

                }
                else
                {
                    Debug.LogError("Degenerate bezier, can't constrain.");
                }
            }
            else if (curve as LineCurve != null)
            {

                // A line can only find 2 intersections for now...
                (intersections, mirrorPlaneConstraints) = ((LineCurve)curve).Constrain(
                    inputStroke.Constraints.ToArray(),
                    canvas.OrthoDirections,
                    parameters.Current.SmallAngle,
                    parameters.Current.ProximityThreshold,
                    out appliedConstraints,
                    out rejectedConstraints);
            }


            // Project on mirror if needed
            if (mirror && !onSurface)
            {
                // Check whether all intersections are on mirror
                bool allOnMirror = true;
                foreach (var inter in intersections)
                {
                    if (Vector3.Distance(canvas.mirrorPlane.Project(inter.Position), inter.Position) > parameters.Current.SmallDistance * 0.1f)
                    {
                        allOnMirror = false;
                        break;
                    }
                }

                if (allOnMirror)
                {
                    // Try to project on mirror
                    Curve.Curve result = canvas.mirrorPlane.Project(curve, parameters.Current.SmallAngle, out float score);
                    if (score < parameters.Current.ProjectToMirrorDistanceThreshold && result != null)
                        {
                        //Debug.Log("project on mirror plane");
                        // Project on mirror plane
                        curve = result;
                        onMirror = true;
                    }
                }

            }


            // Visualize
            if (visualizer != null)
            {
                Debug.Log("visualizing constraints");
                visualizer.Display(appliedConstraints, rejectedConstraints);
            }

        }

        return (curve, intersections, mirrorPlaneConstraints);
    }

    private bool ProjectOnSurfaces(BezierCurve input, List<SurfaceConstraint> constraints, IntersectionConstraint[] intersections, List<int> constrainedAnchorIdx, bool closedLoop, out Curve.Curve curve)
    {
        bool projectedOnSurface = false; // true only if the whole stroke is projected, false if only endpoints are projected

        Vector3[] ctrlPts = input.GetPoints().ToArray();


        int lastConstrainedAnchor = 0;

        foreach (var constraint in constraints)
        {
            // Find bounds for projecting on surface
            PointOnCurve start = input.Project(constraint.StartPosition);
            PointOnCurve end = constraint.LeftMidStroke ? input.Project(constraint.EndPosition) : input.GetPointOnCurve(1f);

            int startAnchor = Mathf.Max(lastConstrainedAnchor, input.GetNearestAnchorIdx(start.t));
            int endAnchor = Mathf.Max(lastConstrainedAnchor, input.GetNearestAnchorIdx(end.t));

            lastConstrainedAnchor = endAnchor;

            if (startAnchor == endAnchor)
            {
                // Don' t constrain anchor point that's not at the start or end
                if (startAnchor != 0 && startAnchor != input.beziers.Length)
                    continue;

                // Don't constrain to surface if anchor point already constrained
                if (constrainedAnchorIdx.Contains(startAnchor))
                    continue;

                // Case where only one endpoint is on surface
                int j = startAnchor * 3;

                Vector3 projectedPos;
                if (TryProject(constraint.PatchID, ctrlPts[j], out projectedPos))
                {
                    ctrlPts[j] = projectedPos;
                    //Debug.Log("[DECALS] snapped only anchor point " + startAnchor + " to patch");
                }
                continue;
            }

            // Prevent half stroke snapping to surface
            // In such cases, snap only one anchor point
            if (startAnchor != 0)
            {
                // Don't constrain to surface if anchor point already constrained
                if (constrainedAnchorIdx.Contains(endAnchor))
                    continue;

                if (endAnchor == (input.beziers.Length) && TryProject(constraint.PatchID, ctrlPts[3 * endAnchor], out Vector3 projectedEnd))
                {
                    ctrlPts[3 * endAnchor] = projectedEnd;
                }
                continue;
            }

            if (endAnchor != (input.beziers.Length))
            {
                // Don't constrain to surface if anchor point already constrained
                if (constrainedAnchorIdx.Contains(startAnchor))
                    continue;

                if (startAnchor == 0 && TryProject(constraint.PatchID, ctrlPts[3 * startAnchor], out Vector3 projectedStart))
                {
                    ctrlPts[3 * startAnchor] = projectedStart;
                }
                continue;
            }

            //Debug.Log("[DECALS] constraining to patch " + constraint.PatchID + " between anchor points " + startAnchor + " and " + endAnchor);

            // We may project the whole stroke on the patch!

            // 1 - First check all constrained anchor points
            // If they don't already lie on the surface, abort projection
            bool allConstrainedAnchorsOnPatch = true;
            foreach (int anchorIdx in constrainedAnchorIdx)
            {
                Vector3 anchorPos = ctrlPts[3 * anchorIdx];
                // Does this anchor lie on the surface?
                if (!TryProject(constraint.PatchID, anchorPos, out Vector3 intersectionProj) || Vector3.Distance(anchorPos, intersectionProj) > parameters.Current.SmallDistance * 0.1f)
                {
                    allConstrainedAnchorsOnPatch = false;
                    break;
                }
            }

            if (!allConstrainedAnchorsOnPatch)
                continue;

            // 2 - Then check whether the stroke is intersecting any stroke that forms this patch
            // If they do, the surface we're projecting on will be destroyed anyway, abort projection
            int nbOfIntersectionsOnPatch = 0;
            
            foreach (var intersection in intersections)
            {
                if (canvas.surfaceManager.BoundsPatch(constraint.PatchID, intersection.IntersectedStroke.GetSegmentContaining(intersection.OldCurveData.t)))
                {
                    nbOfIntersectionsOnPatch++;
                }
                if (nbOfIntersectionsOnPatch > 1)
                    break;
            }

            if (nbOfIntersectionsOnPatch > 1)
            {
                //Debug.Log("don't project on patch");
                continue;
            }

            // 3 - OK, all anchor points will be projected
            projectedOnSurface = true;

            //Debug.Log("[DECALS] projecting all stroke on patch");

            for (int bezIdx = startAnchor; bezIdx < endAnchor; bezIdx++)
            {
                // All control points on this bezier
                int nCtrlPts = (bezIdx == endAnchor - 1) ? 4 : 3;

                for (int i = 0; i < nCtrlPts; i++)
                {
                    int j = 3 * bezIdx + i;
                    Vector3 projectedPos;
                    if (TryProject(constraint.PatchID, ctrlPts[j], out projectedPos))
                    {
                        //Debug.Log("[DECALS] project from " + ctrlPts[j].ToString("F3") + " to " + projectedPos.ToString("F3"));
                        ctrlPts[j] = projectedPos;
                    }
                    else
                    {
                        //Debug.Log("[DECALS] failed to project on surface");
                        //ctrlPts[j] = ctrlPts[j];
                    }
                }
            }

        }

        if (closedLoop)
            ctrlPts[ctrlPts.Length - 1] = ctrlPts[0];

        curve = new BezierCurve(ctrlPts);

        // If the result would be degenerate, abort and return the original curve
        if (!curve.IsValid(parameters.Current.MinStrokeSize))
            curve = input;

        return projectedOnSurface;
    }

    private bool TryProject(int patchID, Vector3 pos, out Vector3 projectedPos)
    {
        if (canvas.surfaceManager.ProjectOnPatch(patchID, pos, out projectedPos, canvasSpace: true)
            && Vector3.Distance(projectedPos, pos) < parameters.Current.ProjectToSurfaceDistanceThreshold)
        {
            //Debug.Log("project from " + pos.ToString("F3") + " to " + projectedPos.ToString("F3"));
            return true;
        }
        return false;
    }

    private List<Constraint> CorrectIntersections(List<Constraint> constraints, Curve.Curve curve, float search_distance, int N_steps)
    {
        List<Constraint> correctedConstraints = new List<Constraint>(constraints.Count);

        foreach(var c in constraints)
        {
            if (c as IntersectionConstraint != null)
            {
                PointOnCurve bestPoint = ((IntersectionConstraint)c).OldCurveData;
                Curve.Curve intersectedCurve = ((IntersectionConstraint)c).IntersectedStroke.Curve;


                if (bestPoint.t != 0f && bestPoint.t != 1f)
                {
                    //Debug.Log("look for better intersection point");
                    // Search in a small zone around the initial intersection position
                    Vector3 cPos = c.Position;
                    Vector3 dT = search_distance * 0.5f * ((IntersectionConstraint)c).OldCurveData.Tangent;
                    
                    PointOnCurve zoneStart = intersectedCurve.Project(cPos + dT);
                    PointOnCurve zoneEnd = intersectedCurve.Project(cPos - dT);

                    // If the zone is big enough, search in it
                    if (Vector3.Distance(zoneStart.Position, zoneEnd.Position) > search_distance * 0.1f)
                    {
                        float step = (Mathf.Clamp(zoneEnd.t, 0f, 1f) - Mathf.Clamp(zoneStart.t, 0f, 1f)) / N_steps;
                        float minDist = Vector3.Distance(curve.Project(bestPoint.Position).Position, bestPoint.Position);

                        for (int i = 0; i <= N_steps; i++)
                        {
                            PointOnCurve onOldCurve = intersectedCurve.GetPointOnCurve(zoneStart.t + step * i);
                            Vector3 projOnNewCurve = curve.Project(onOldCurve.Position).Position;
                            float dist = Vector3.Distance(projOnNewCurve, onOldCurve.Position);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                bestPoint = onOldCurve;
                                //Debug.Log("point at t = " + bestPoint.t + " is better");
                            }
                        }
                    }
                }


                // If on endpoint, check other endpoint
                //Vector3 closestOnNewCurve = curve.Project(bestPoint.Position).Position;
                //PointOnCurve startEndpoint = intersectedCurve.GetPointOnCurve(0f);
                //PointOnCurve endEndpoint = intersectedCurve.GetPointOnCurve(1f);
                //if (Vector3.Distance(bestPoint.Position, startEndpoint.Position) < 0.1f * proximity_threshold)
                //{
                //    //Debug.Log("close to start point, check end point");
                //    if (Vector3.Distance(closestOnNewCurve, endEndpoint.Position) < Vector3.Distance(closestOnNewCurve, startEndpoint.Position))
                //    {
                //        bestPoint = endEndpoint;
                //        //Debug.Log("end point is better");
                //    }

                //}
                //else if (Vector3.Distance(bestPoint.Position, endEndpoint.Position) < 0.1f * proximity_threshold)
                //{
                //    if (Vector3.Distance(closestOnNewCurve, startEndpoint.Position) < Vector3.Distance(closestOnNewCurve, endEndpoint.Position))
                //        bestPoint = startEndpoint;
                //}

                IntersectionConstraint correctedC = ((IntersectionConstraint)c).IntersectedStroke.GetConstraint(bestPoint.Position, parameters.Current.SnapToExistingNodeThreshold);
                correctedConstraints.Add(correctedC);
            }
            else
            {
                correctedConstraints.Add(c);
            }
        }

        return correctedConstraints;
    }
    
}
