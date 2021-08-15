using UnityEngine;
using System.Collections;
using Curve;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.IO;
using MathNet.Numerics.LinearAlgebra.Single;

namespace VRSketch
{

    public struct SolverParams
    {
        public float mu_fidelity { get; }
        public float w_p { get; }
        public float w_t { get; }
        public float proximity_threshold { get; }
        public float angular_proximity_threshold { get; }
        public bool planarity_allowed { get; }

        public float min_distance_between_anchors { get; }

        public SolverParams(float mu_fidelity, float w_p, float w_t, float proximity_threshold, float min_distance_between_anchors, float angular_proximity_threshold, bool planarity_allowed)
        {
            this.mu_fidelity = mu_fidelity;
            this.w_p = w_p;
            this.w_t = w_t;
            this.proximity_threshold = proximity_threshold;
            this.angular_proximity_threshold = angular_proximity_threshold;
            this.planarity_allowed = planarity_allowed;
            this.min_distance_between_anchors = min_distance_between_anchors;
        }
    }

    struct CurveFitCandidate
    {
        public Vector3[] controlPoints { get; }
        public float energy { get; }
        public Dictionary<int, ConstraintCandidate> activeConstraints { get; }
        public bool planar { get; }
        public bool isClosed { get; }

        public CurveFitCandidate(Vector3[] controlPoints, float energy, Dictionary<int, ConstraintCandidate> activeConstraints, bool planar, bool isClosed)
        {
            this.controlPoints = controlPoints;
            this.energy = energy;
            this.activeConstraints = activeConstraints;
            this.planar = planar;
            this.isClosed = isClosed;
        }
    }

    public class ConstraintCandidate
    {
        public Constraint constraint { get; }
        public float t { get; }
        public float likelihood { get; }
        public float score { get; }
        public bool active;
        public bool closeToEndpoint { get; }
        public bool alignedTangents { get; }

        public ConstraintCandidate(Constraint c, float t, float likelihood, float score, bool closeToEndpoint, bool alignedTangents)
        {
            constraint = c;
            active = true;
            this.t = t;
            this.likelihood = likelihood;
            this.score = score;
            this.closeToEndpoint = closeToEndpoint;
            this.alignedTangents = alignedTangents;
        }
    }

    public class ConstraintSolver
    {
        private Vector3[] controlPoints;
        private ConstraintCandidate[] allConstraints; // sorted by ascending t
        private bool isClosed;
        private SolverParams solverParams;
        private Vector3[] orthoDirections;
        private float allConstraintsScore;

        public static StreamWriter LogStream;
        public static bool LogPerformance = true;

        private static Stopwatch performanceTimer = new Stopwatch();

        public ConstraintSolver(
            BezierCurve curve,
            Constraint[] constraints,
            Vector3[] orthoDirections,
            float mu_fidelity,
            float proximityThreshold,
            float angularProximityThreshold,
            float minDistanceBetweenAnchors, // Minimum distance allowed between 2 Bezier curve anchor points (control points that lie on the curve: eg B0 or B3)
            float w_p = 0.5f, // Weight of the fidelity term for control points position
            float w_t = 0.5f, // Weight of the fidelity term for control polygon tangents
            bool is_closed = false,
            bool planarity_allowed = true)
        {
            this.controlPoints = curve.GetPoints().ToArray();
            this.orthoDirections = orthoDirections;
            this.isClosed = is_closed;
            ConstraintCandidate[] candidates = new ConstraintCandidate[constraints.Length];
            this.allConstraintsScore = 0;
            
            for (int i = 0; i < constraints.Length; i++)
            {
                curve.ProjectConstraint(constraints[i].Position, minDistanceBetweenAnchors, out float t, out float likelihood, out bool closeToEndpoint);
                bool alignTangent = false;
                // Will we enforce tangent alignment?
                if (constraints[i] as IntersectionConstraint != null)
                {
                    Vector3 T = ((IntersectionConstraint)constraints[i]).OldCurveData.Tangent;
                    // Do not make tangents collinear if this intersection is on a node and the target tangent is in the same orientation
                    bool isAtNode = ((IntersectionConstraint)constraints[i]).IsAtNode;
                    // Check if tangent should be constrained
                    Vector3 tangentToConstrain = curve.GetTangent(t);
                    if (T != null && T.magnitude > 0.9f && tangentToConstrain.magnitude > 0.9f && !(isAtNode && Vector3.Dot(T, tangentToConstrain) > 0))
                    {
                        float theta = Mathf.Abs(Vector3.Dot(T, tangentToConstrain));
                        //Debug.Log("Angle to candidate tangent constraint: " + theta);
                        if (theta > Mathf.Cos(angularProximityThreshold))
                        {
                            alignTangent = true;
                        }
                    }
                }
                else if (constraints[i] as MirrorPlaneConstraint != null)
                {
                    Vector3 T = ((MirrorPlaneConstraint)constraints[i]).PlaneNormal;

                    Vector3 tangentToConstrain = curve.GetTangent(t);
                    if (T != null && T.magnitude > 0.9f && tangentToConstrain.magnitude > 0.9f)
                    {
                        float theta = Mathf.Abs(Vector3.Dot(T, tangentToConstrain));
                        //Debug.Log("Angle to candidate tangent constraint with mirror: " + theta);
                        if (theta > Mathf.Cos(angularProximityThreshold))
                        {
                            alignTangent = true;
                        }
                    }
                }


                // Compute constraint score
                float constraintScore = 0f;
                // Depending on intersection type
                if (constraints[i] as IntersectionConstraint != null)
                {
                    if (((IntersectionConstraint)constraints[i]).IsAtNode)
                        constraintScore = 2f;
                    else
                        constraintScore = 1.5f;
                }
                else
                    constraintScore = 1f;

                // Depending on constraint position on new stroke
                if (closeToEndpoint)
                    constraintScore = Mathf.Max(constraintScore, 1.25f);

                if (alignTangent)
                    constraintScore += 0.5f;

                candidates[i] = new ConstraintCandidate(constraints[i], t, likelihood, constraintScore, closeToEndpoint, alignTangent);

                // Increment overall score of all constraints
                this.allConstraintsScore += constraintScore;
            }
            // Sort constraints by ascending t
            this.allConstraints = candidates.OrderBy(x => x.t).ToArray();
            this.solverParams = new SolverParams(mu_fidelity, w_p, w_t, proximityThreshold, minDistanceBetweenAnchors, angularProximityThreshold, planarity_allowed);
        }

        public (BezierCurve,IntersectionConstraint[],MirrorPlaneConstraint[]) GetBestFit(
            out List<SerializableConstraint> appliedConstraints,
            out List<SerializableConstraint> rejectedConstraints,
            out List<int> constrainedAnchorIdx,
            out bool planar,
            out bool isClosed)
        {
            // Initialize out vars
            appliedConstraints = new List<SerializableConstraint>();
            rejectedConstraints = new List<SerializableConstraint>();
            constrainedAnchorIdx = new List<int>();
            planar = false;
            isClosed = false;


            // Start with all constraint candidates
            CurveFitCandidate bestCandidate = FitForConstraints(this.allConstraints.ToArray());
            bool foundBestFit = false;

            while (!foundBestFit && bestCandidate.activeConstraints.Count > 0)
            {
                (CurveFitCandidate? newCandidate, ConstraintCandidate toRemove) = GetBestSubset(bestCandidate.activeConstraints);

                // Check if new candidate is better than previous candidate
                if (newCandidate != null && ((CurveFitCandidate)newCandidate).energy < bestCandidate.energy)
                {
                    bestCandidate = ((CurveFitCandidate)newCandidate);
                    // Deactivate constraint candidate
                    toRemove.active = false;
                }
                else
                {
                    // End
                    foundBestFit = true;
                }
            }
            Debug.Log("[STRUCTURING] best constraint subset with " + bestCandidate.activeConstraints.Count + " active constraints out of " + this.allConstraints.Length);
            Vector3[] ctrlPoints = bestCandidate.controlPoints;

            // Check for NaNs
            if (ctrlPoints.Length > 0 && (float.IsNaN(ctrlPoints[0].x) || float.IsInfinity(ctrlPoints[0].x)))
            {
                Debug.LogError("[STRUCTURING] Constraint solver encountered NaN or Infinity point values");
                BezierCurve initialCurve = new BezierCurve(this.controlPoints);

                return (initialCurve, new IntersectionConstraint[0], new MirrorPlaneConstraint[0]);
            }

            BezierCurve curve = new BezierCurve(ctrlPoints);

            // Curve/Curve intersections
            List<IntersectionConstraint> intersections = new List<IntersectionConstraint>();
            // Mirror/Curve intersections
            List<MirrorPlaneConstraint> mirrorPlaneConstraints = new List<MirrorPlaneConstraint>();

            foreach (var item in bestCandidate.activeConstraints)
            {
                if (item.Value.constraint as IntersectionConstraint != null)
                {
                    //float curveParam = curve.GetAnchorParameter(item.Key);
                    IntersectionConstraint intersection = (IntersectionConstraint)item.Value.constraint;
                    intersection.ProjectOn(curve, item.Key); // Store intersection data
                    intersections.Add(intersection);
                }
                if (item.Value.constraint as MirrorPlaneConstraint != null)
                {
                    MirrorPlaneConstraint intersection = (MirrorPlaneConstraint)item.Value.constraint;
                    intersection.ProjectOn(curve, item.Key); // Store intersection data
                    mirrorPlaneConstraints.Add(intersection);
                }

                constrainedAnchorIdx.Add(item.Key);
            }




            // Fill in data
            planar = bestCandidate.planar;
            isClosed = bestCandidate.isClosed;
            //appliedConstraints = bestCandidate.activeConstraints.Values.Select(x => x.constraint.Position).ToList();
            //rejectedConstraints = new List<Vector3>();
            foreach (var c in allConstraints)
            {
                if (bestCandidate.activeConstraints.Values.Contains(c))
                {
                    appliedConstraints.Add(
                        new SerializableConstraint(
                            c.constraint.Position,
                            isIntersection: c.constraint as IntersectionConstraint != null,
                            isAtExistingNode: c.constraint as IntersectionConstraint != null ? ((IntersectionConstraint)c.constraint).IsAtNode : false,
                            isAtNewEndpoint: c.closeToEndpoint,
                            alignTangents: c.alignedTangents // kind of a dirty way to get it, should be correct but really flimsy
                            )
                        );
                }
                else
                {
                    rejectedConstraints.Add(
                        new SerializableConstraint(
                            c.constraint.Position,
                            isIntersection: c.constraint as IntersectionConstraint != null ,
                            isAtExistingNode: c.constraint as IntersectionConstraint != null ? ((IntersectionConstraint)c.constraint).IsAtNode : false,
                            isAtNewEndpoint: c.closeToEndpoint,
                            alignTangents: false
                            )
                        );
                }
            }

            return (curve, intersections.ToArray(), mirrorPlaneConstraints.ToArray());

        }

        private (CurveFitCandidate?, ConstraintCandidate) GetBestSubset(Dictionary<int, ConstraintCandidate> parentSet)
        {
            List<CurveFitCandidate> subsetCandidates = new List<CurveFitCandidate>();

            // Get current list of active constraints
            List<ConstraintCandidate> activeConstraints = allConstraints.Where(c => c.active).ToList();

            Debug.Log("[STRUCTURING] looking for best subset on parent set of " + activeConstraints.Count + " constraint");

            // Initialize variables to empties
            CurveFitCandidate? best = null;
            ConstraintCandidate toRemove = null;
            float minEnergy = 10e8f;

            foreach (var item in parentSet)
            {
                ConstraintCandidate removedConstraint = item.Value;
                // Form the subset of constraints formed by all active constraints except removedConstraint
                List<ConstraintCandidate> subset = new List<ConstraintCandidate>(activeConstraints);
                subset.Remove(removedConstraint);

                // Get subdivided poly bezier and corresponding list of hard constraints
                CurveFitCandidate candidate = FitForConstraints(subset.ToArray());
                subsetCandidates.Add(candidate);

                if (candidate.energy < minEnergy)
                {
                    best = candidate;
                    minEnergy = candidate.energy;
                    toRemove = removedConstraint;
                }
            }
            Debug.Log("[STRUCTURING] best energy = " + minEnergy);
            // Get best candidate (lowest energy)
            return (best, toRemove);
        }

        private CurveFitCandidate FitForConstraints(ConstraintCandidate[] constraints)
        {
            // Create new bezier curve from control points
            BezierCurve curve = new BezierCurve(this.controlPoints);

            // Subdivide curve to match the given constraints
            Dictionary<int, ConstraintCandidate> constraintsByAnchorPt = curve.SplitForConstraints(constraints, isClosed, this.solverParams.min_distance_between_anchors);
            Vector3[] newControlPoints = curve.GetPoints().ToArray();

            // From the dictionary of anchor index / constraints pairs, create the list of hard constraints
            List<IHardConstraint> hardConstraints = new List<IHardConstraint>();
            List<ISoftConstraint> softConstraints = new List<ISoftConstraint>();

            Vector3 startEndPointTangent = curve.GetTangent(0);
            foreach(var item in constraintsByAnchorPt)
            {
                int anchorIdx = item.Key;
                ConstraintCandidate c = item.Value;
                Vector3 d = c.constraint.Position - curve.GetAnchor(anchorIdx);
                int ctrlPtIdx = anchorIdx * 3;
                hardConstraints.Add(new PositionConstraint(ctrlPtIdx, d, newControlPoints.Length));

                if (c.alignedTangents)
                {
                    // Intersection constraint
                    if (c.constraint as IntersectionConstraint != null)
                    {
                        Vector3 T = ((IntersectionConstraint)c.constraint).OldCurveData.Tangent;
                        if (ctrlPtIdx == 0 || ctrlPtIdx == curve.beziers.Length - 1)
                            startEndPointTangent = T;
                        softConstraints.Add(new TangentConstraint(ctrlPtIdx, T, newControlPoints));
                    }

                    else if (c.constraint as MirrorPlaneConstraint != null)
                    {
                        Vector3 T = ((MirrorPlaneConstraint)c.constraint).PlaneNormal;
                        softConstraints.Add(new TangentConstraint(ctrlPtIdx, T, newControlPoints));
                    }
                }
            }

            // Self intersection constraint?
            int N = curve.beziers.Length;
            bool closeLoop = false;
            if (isClosed && curve.beziers.Length > 1 && !(constraintsByAnchorPt.ContainsKey(0) && constraintsByAnchorPt.ContainsKey(N)))
            {
                Vector3 AB0 = curve.GetAnchor(N) - curve.GetAnchor(0);
                hardConstraints.Add(new SelfIntersectionConstraint(0, N, AB0, newControlPoints.Length));

                // Add soft tangent alignment constraint
                if (Mathf.Abs(Vector3.Dot(startEndPointTangent, curve.GetTangent(N))) > 0.5f)
                    softConstraints.Add(new TangentConstraint(N * 3, startEndPointTangent, newControlPoints));

                closeLoop = true;
            }

            // Add G1 constraint
            if (newControlPoints.Length > 4)
            {
                G1Constraint g1 = new G1Constraint(newControlPoints);
                if (g1.nJoints > 0)
                    hardConstraints.Add(g1);
            }

            // Planarity soft constraint
            bool planar = false;
            if (solverParams.planarity_allowed)
            {
                (Plane bestPlane, float distToPlane) = Utils.FitPlane(newControlPoints);
                //Debug.Log(distToPlane);
                if (distToPlane < solverParams.proximity_threshold)
                {
                    planar = true;
                    Debug.Log("[STRUCTURING] Add planarity constraint");
                    bestPlane.SnapToOrtho(this.orthoDirections, Mathf.Abs(Mathf.Cos(solverParams.angular_proximity_threshold)));
                    //Debug.Log(bestPlane.n);
                    softConstraints.Add(new PlanarityConstraint(bestPlane.n, newControlPoints));
                }
                else
                {
                    Debug.Log("[STRUCTURING] No planarity constraint");
                }
            }


            // Solve
            (Vector3[] fittedCtrlPts, float fittingEnergy) = Solve(
                                                                    newControlPoints,
                                                                    hardConstraints.ToArray(),
                                                                    softConstraints.ToArray(),
                                                                    this.solverParams.w_p, this.solverParams.w_t,
                                                                    this.solverParams.proximity_threshold);

            // Compute energy
            float energy = this.solverParams.mu_fidelity * fittingEnergy + (1f - this.solverParams.mu_fidelity) * this.ConstraintEnergy(constraintsByAnchorPt.Values);

            Debug.Log("[STRUCTURING] constraint energy = " + this.ConstraintEnergy(constraintsByAnchorPt.Values));
            Debug.Log("[STRUCTURING] subset energy = " + energy + " , for " + constraintsByAnchorPt.Values.Count + " hard constraints");
            Debug.Log("[STRUCTURING] fidelity energy = " + fittingEnergy);

            // Store result
            CurveFitCandidate candidate = new CurveFitCandidate(fittedCtrlPts, energy, constraintsByAnchorPt, planar, closeLoop);
            return candidate;
        }

        private float ConstraintEnergy(IEnumerable<ConstraintCandidate> constraintsSubset)
        {
            if (allConstraintsScore == 0f)
                return 0f;
            float subsetScore = 0f;
            //Debug.Log("all constraints score " + allConstraintsScore);
            foreach (var c in constraintsSubset)
            {
                subsetScore += c.score;
            }
            //Debug.Log("subset score " + subsetScore);
            return Mathf.Exp(-(subsetScore * subsetScore) / (allConstraintsScore * allConstraintsScore));
        }

        private static (Vector3[], float) Solve(Vector3[] controlPoints, IHardConstraint[] hardConstraints, ISoftConstraint[] softConstraints, float w_p, float w_t, float proximity_threshold)
        {
            int N = controlPoints.Length;

            float[] displacements = new float[3 * N];
            // Initialize to zero
            for (int i = 0; i < 3 * N; i++)
                displacements[i] = 0f;

            FidelityEnergy fidelityEnergy = new FidelityEnergy(controlPoints, w_p, w_t, proximity_threshold);

            if (hardConstraints.Length > 0 || softConstraints.Length > 0)
            {
                // Build right hand side vector
                List<float> b_temp = new List<float>();

                // - Block for gradient of fidelity energy
                Matrix<float> A_grad = fidelityEnergy.GetBlock();
                Vector<float> b_top = Vector<float>.Build.Dense(N * 3);

                // Soft constraints
                foreach(var c in softConstraints)
                {
                    (Matrix<float> A_c, Vector<float> b_c) = c.GetBlocks();
                    A_grad += A_c;
                    b_top += b_c;
                }

                b_temp.AddRange(b_top.ToArray());

                // Build left hand side block matrix
                Matrix<float> A = null;

                if (hardConstraints.Length > 0)
                {
                    // Build left hand side block matrix
                    Matrix<float>[,] A_temp = new Matrix<float>[2, 2];

                    // - Assemble the blocks for hard constraints
                    Matrix<float>[,] C_temp = new Matrix<float>[hardConstraints.Length, 1];
                    for (int i = 0; i < hardConstraints.Length; i++)
                    {
                        (Matrix<float> C_i, Vector<float> b_i) = hardConstraints[i].GetBlocks();
                        C_temp[i, 0] = C_i;
                        b_temp.AddRange(b_i.AsEnumerable());
                    }
                    Matrix<float> C = Matrix<float>.Build.DenseOfMatrixArray(C_temp);

                    A_temp[0, 0] = A_grad;
                    A_temp[1, 0] = C;
                    A_temp[0, 1] = C.Transpose();
                    A_temp[1, 1] = Matrix<float>.Build.Dense(C.RowCount, C.RowCount);

                    // Final assembly
                    A = Matrix<float>.Build.DenseOfMatrixArray(A_temp);
                }
                else
                {
                    A = A_grad;
                }

                Vector<float> b = Vector<float>.Build.DenseOfEnumerable(b_temp);

                if (LogPerformance)
                    performanceTimer.Restart();
                // Solve
                Vector<float> x = A.Solve(b);

                if (LogPerformance)
                {
                    performanceTimer.Stop();

                    LogStream.WriteLine("|A| = " + A.RowCount * A.ColumnCount +
                        " (" + A.RowCount + "*" + A.ColumnCount + ")" +
                        " Time: " + performanceTimer.Elapsed.TotalMilliseconds + "ms");
                }


                displacements = x.AsEnumerable().Take(3 * N).ToArray();
            }

            // Compute energy
            float energy = fidelityEnergy.Compute(Utils.ToVector3Array(displacements));

            // Rebuild list of control point Vector3 from flat Vector
            Vector3[] B = new Vector3[N]; // Copy initial positions
            Vector3[] d = Utils.ToVector3Array(displacements);
            for (int i = 0; i < N; i++)
                B[i] = controlPoints[i] + d[i];

            return (B, energy);
        }


    }
}