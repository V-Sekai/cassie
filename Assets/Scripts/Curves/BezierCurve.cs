using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using VRSketch;
using System;

namespace Curve
{

    public class CubicBezier
    {
        // Coefficients
        Vector3 c0, c1, c2, c3;

        // Control points
        Vector3 P0, P1, P2, P3;

        /*
         * Compute coefficients for a cubic bezier curve
         *   p(s) = c0 + c1*s + c2*s^2 + c3*s^3
         * c0, c1, c2, c3 being the control points
         */
        public CubicBezier(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            P0 = v0; P1 = v1; P2 = v2; P3 = v3;
            Update();

        }

        private void Update()
        {
            c0 = P0;
            c1 = 3f * (P1 - P0);
            c2 = 3f * (P0 - 2f * P1 + P2);
            c3 = -P0 + 3f * P1 - 3f * P2 + P3;
        }

        public Vector3 Calculate(float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return c0 + c1 * t + c2 * t2 + c3 * t3;
        }

        public Vector3 CalculateDerivative1(float t)
        {
            var t2 = t * t;
            return c1 + 2f * c2 * t + 3f * c3 * t2;
        }

        public Vector3 CalculateDerivative2(float t)
        {
            return 2f * c2 + 6f * c3 * t;
        }

        public List<Vector3> GetPoints()
        {
            return new List<Vector3> { P0, P1, P2, P3 };
        }

        public Vector3 Get(int idx)
        {
            Vector3[] points = new Vector3[] { P0, P1, P2, P3 };
            return points[(idx + 4) % 4];
        }

        public bool IsNonTrivial()
        {
            return !P0.Equals(P1) || !P0.Equals(P2) || !P0.Equals(P3);
        }

        public bool IsNonDegenerate()
        {
            return Vector3.Distance(P0, P1) > Constants.eps
                && Vector3.Distance(P1, P2) > Constants.eps
                && Vector3.Distance(P2, P3) > Constants.eps;
        }

    }

    public class BezierCurve : Curve
    {
        public CubicBezier[] beziers { get; private set; }

        public BezierCurve(CubicBezier[] beziers, List<float> weights, bool closed = false)
            : base(weights, closed)
        {
            this.beziers = beziers;
        }

        public BezierCurve(Vector3[] ctrlPoints, bool closed = false)
            : base(closed)
        {
            int nBeziers = ctrlPoints.Length / 3;
            beziers = new CubicBezier[nBeziers];

            for (int i = 0; i < nBeziers; i++)
            {
                beziers[i] = new CubicBezier(ctrlPoints[i * 3], ctrlPoints[i * 3 + 1], ctrlPoints[i * 3 + 2], ctrlPoints[i * 3 + 3]);
            }
        }

        public BezierCurve(BezierCurve[] curves, bool closed = false)
            : base(closed)
        {
            List<CubicBezier> beziersList = new List<CubicBezier>();
            foreach(BezierCurve bez in curves)
            {
                beziersList.AddRange(bez.beziers);
            }
            this.beziers = beziersList.ToArray();
        }

        public override Vector3 GetPoint(float t)
        {
            (int splitIdx, float u) = ConvertPolyBezierParameter(t);
            return beziers[splitIdx].Calculate(u);
        }

        private Vector3 GetPoint(int idx, float u)
        {
            return beziers[idx].Calculate(u);
        }

        public bool IsNonDegenerate()
        {
            foreach (var b in beziers)
            {
                if (!b.IsNonDegenerate())
                    return false;
            }
            return true;
        }

        public Vector3 GetAnchor(int anchorIdx)
        {
            if (anchorIdx < beziers.Length)
                return beziers[anchorIdx].Get(0);
            else
                return beziers[beziers.Length - 1].Get(3);
        }

        public float GetAnchorParameter(int anchorIdx)
        {
            return InverseConvertPolyBezierParameter(anchorIdx, 0f);
        }

        public Vector3 GetTangent(int anchorIdx)
        {
            Vector3 T = Vector3.zero;
            if (anchorIdx < beziers.Length)
            {
                T = beziers[anchorIdx].Get(1) - beziers[anchorIdx].Get(0);
            }
            else
            {
                T = beziers[beziers.Length - 1].Get(3) - beziers[beziers.Length - 1].Get(2);
            }

            if (T.magnitude > Constants.eps)
                return T.normalized;
            else
                return Vector3.zero;
        }

        public override float GetWeight(float t)
        {
            int N = weights.Count;
            if (N > 1)
                return Mathf.Lerp(weights[0], weights[N - 1], t);
            return weights[0];
        }

        public List<Vector3> GetPoints()
        {
            return GetControlPoints(this.beziers);
        }

        public override PointOnCurve Project(Vector3 point)
        {
            int nSlices = 10 * beziers.Length;
            float tMin = GetClosestPointParameter(point, nSlices, 5);

            return GetPointOnCurve(tMin);
        }

        public override PointOnCurve GetPointOnCurve(float t)
        {

            Vector3 position = GetPoint(t);
            Vector3 tangent = GetTangent(t);

            return new PointOnCurve(t, position, tangent, Vector3.zero);
        }

        public PointOnCurve GetPointOnCurve(int anchorIdx)
        {
            float t = this.GetAnchorParameter(anchorIdx);
            Vector3 position = GetAnchor(anchorIdx);
            Vector3 tangent = GetTangent(anchorIdx);

            return new PointOnCurve(t, position, tangent, Vector3.zero);
        }

        public void ProjectConstraint(Vector3 constraintPos, float endpointProximityThreshold, out float onCurveParam, out float likelihood, out bool closeToEndpoint)
        {
            // Compute parameter of closest point along curve
            onCurveParam = GetClosestPointParameter(constraintPos, 15, 10);
            Vector3 p = GetPoint(onCurveParam);
            likelihood = 1f / (10e-5f + Vector3.Distance(constraintPos, p)); // Inverse of the distance (kinda arbitrary)

            if (Vector3.Distance(p, GetAnchor(0)) < endpointProximityThreshold || Vector3.Distance(p, GetAnchor(beziers.Length)) < endpointProximityThreshold)
            {
                closeToEndpoint = true;
            }

            else
                closeToEndpoint = false;
            //return (t, likelihood);
        }

        public override Vector3 ParallelTransport(Vector3 v, float from, float to)
        {
            int n = 20 * GetBezierCountBetween(from, to);
            float dt = (to - from) / n; // Simply sample based on parameter for now

            Vector3 prev_tangent = GetTangent(from);
            Vector3 v_t = v;
            for(int i = 1; i <= n; i++)
            {
                float t = from + i * dt;
                Vector3 tangent = GetTangent(t);

                // Rotation axis
                var axis = Vector3.Cross(prev_tangent, tangent);
                if (axis.magnitude > float.Epsilon)
                {
                    axis.Normalize();

                    float dot = Vector3.Dot(prev_tangent, tangent);

                    // clamp for floating pt errors
                    float theta = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));

                    v_t = Quaternion.AngleAxis(theta * Mathf.Rad2Deg, axis) * v_t;
                }

                prev_tangent = tangent;
            }

            return v_t;
        }

        public override bool IsValid(float size_threshold)
        {
            foreach(var b in beziers)
            {
                if (!b.IsNonTrivial())
                    return false;
            }

            if (GetLength() < size_threshold)
                return false;

            return true;
        }

        public override List<Vector3> GetControlPoints()
        {
            return GetPoints();
        }

        public override List<List<Vector3>> GetControlPointsBetween(float from, float to)
        {
            //Debug.Log("endpoint params = " + from + ", " + to);
            // Create a copy of the curve in case we need to split
            BezierCurve curve = new BezierCurve(beziers, weights);

            // Split the curve at start or end param
            Reparameterization? r = curve.CutAt(from, throwBefore: true, snapToExistingAnchorThreshold: Constants.eps);

            // Correct second parameter to account for reparameterization effected by first cut
            // Either use the analytical reparameterization, or project the point on the new curve
            if (r != null)
            {
                to = Reparameterize((Reparameterization)r, to);
            }
            else
            {
                PointOnCurve toPoint = curve.Project(GetPoint(to));
                to = toPoint.t;
            }

            curve.CutAt(to, throwBefore: false, snapToExistingAnchorThreshold: Constants.eps);

            List<List<Vector3>> ctrlPts = new List<List<Vector3>>(curve.beziers.Length);

            foreach (var b in curve.beziers)
                ctrlPts.Add(b.GetPoints());

            return ctrlPts;
        }

        public override Reparameterization? CutAt(float t, bool throwBefore, float snapToExistingAnchorThreshold)
        {
            // Split
            (int idx, float u) = ConvertPolyBezierParameter(t);

            int splitIdx;

            // Check if should split
            Vector3 P = beziers[idx].Calculate(u);
            if (Vector3.Distance(GetAnchor(idx), P) < snapToExistingAnchorThreshold || Vector3.Distance(GetAnchor(idx + 1), P) < snapToExistingAnchorThreshold)
            {
                if (Vector3.Distance(GetAnchor(idx), P) <= Vector3.Distance(GetAnchor(idx + 1), P))
                    splitIdx = idx;
                else
                    splitIdx = idx + 1;

                //Debug.Log("Cut at anchor point " + splitIdx);

                // Reparameterization: since we simply cut off one whole bezier from the poly bezier, we have an analytical formulation for the reparameterization

                if (beziers.Length < 2)
                    return new Reparameterization(0f, 1f); // Identity reparameterization

                //float reparameterizationRatio = beziers.Length / (beziers.Length - 1f);
                float oldBezierCount = beziers.Length;

                // Throw what was before/after t
                if (throwBefore)
                {
                    beziers = beziers.Skip(splitIdx).ToArray();
                    return new Reparameterization(t, oldBezierCount / beziers.Length);
                }
                else
                {
                    beziers = beziers.Take(splitIdx).ToArray();
                    return new Reparameterization(0f, oldBezierCount / beziers.Length);
                }
            }
            else
            {
                splitIdx = SplitAt(idx, u);

                // Throw what was before/after t
                if (throwBefore)
                {
                    beziers = beziers.Skip(splitIdx).ToArray();
                }
                else
                {
                    beziers = beziers.Take(splitIdx).ToArray();
                }

                // Since we split one of the bezier curve, there is no analytical reparameterization available
                // We will need to find the curve parameters again by iterative projection, wherever needed
                return null;
            }

        }


        public int GetBezierCountBetween(float startParam, float endParam)
        {
            (int startBezIdx, float _ ) = ConvertPolyBezierParameter(startParam);
            (int endBezIndx, float _ ) = ConvertPolyBezierParameter(endParam);

            return Math.Abs(endBezIndx - startBezIdx) + 1;
        }

        public override float LengthBetween(float startParam, float endParam)
        {
            int nSamples = 5 * beziers.Length;
            float length = 0;
            Vector3 p = GetPoint(startParam);
            float step = (endParam - startParam) / nSamples;
            for (int i = 0; i < nSamples; i++)
            {
                Vector3 next = GetPoint(startParam + i * step);
                length += Vector3.Distance(p, next);
                p = next;
            }
            return length;
        }

        public int GetNearestAnchorIdx(float t)
        {
            (int idx, float u) = ConvertPolyBezierParameter(t);

            Vector3 p = GetPoint(idx, u);

            return Vector3.Distance(p, GetAnchor(idx)) < Vector3.Distance(p, GetAnchor(idx + 1)) ? idx : idx + 1;
        }


        public Dictionary<int, ConstraintCandidate> SplitForConstraints(ConstraintCandidate[] constraints, bool isClosed, float minDistanceBetweenAnchors)
        {

            Dictionary<int, ConstraintCandidate> constraintByAnchorIdx = new Dictionary<int, ConstraintCandidate>();

            // Initialize global parameter of each anchor along the curve (uniform division)
            List<float> anchorParams = new List<float>(new float[this.beziers.Length]);
            for (int i = 0; i < this.beziers.Length; i++)
                anchorParams[i] = ((float)i) / this.beziers.Length;

            int N = this.beziers.Length;

            for (int i = 0; i < constraints.Length; i++)
            {
                float t = constraints[i].t;
                // Attempt split at t
                // - Find corresponding bezier idx and parameter u

                // Note: given that the parameter t was computed on initial curve parameterization (before any split)
                // we take care to pass the anchor params list (maps each anchor to each original parameter t)
                // so that we can establish the correct mapping of t --> (b_idx, u) even though some splits have already happened

                (int anchorIdx, float u) = ConvertPolyBezierParameter(t, anchorParams);

                int constraintAnchorIdx = -1;

                // Check if candidate split is too close to an existing anchor point
                Vector3 candidatePos = GetPoint(anchorIdx, u);
                float distLeft = Vector3.Distance(candidatePos, GetAnchor(anchorIdx));
                float distRight = Vector3.Distance(candidatePos, GetAnchor(anchorIdx + 1));
                if (Math.Min(distLeft, distRight) < minDistanceBetweenAnchors)
                {
                    int closestAnchorIdx = distLeft < distRight ? anchorIdx : anchorIdx + 1;
                    
                    // Is there already a constraint on that anchor?
                    if (constraintByAnchorIdx.ContainsKey(closestAnchorIdx))
                    {
                        // Is this constraint less likely than the new candidate?
                        if (constraintByAnchorIdx[closestAnchorIdx].likelihood < constraints[i].likelihood)
                        {
                            // Replace by new candidate
                            constraintAnchorIdx = closestAnchorIdx;
                        }
                        // Otherwise ignore the new candidate
                    }
                    // Special case of the endpoints of a closed curve (the anchors 0 and N together should only be constrained once)
                    else if (isClosed && closestAnchorIdx == 0  && constraintByAnchorIdx.ContainsKey(N))
                    {
                        // Is this constraint less likely than the new candidate?
                        if (constraintByAnchorIdx[N].likelihood < constraints[i].likelihood)
                        {
                            // Replace by new candidate
                            constraintAnchorIdx = closestAnchorIdx;
                        }
                        // Otherwise ignore the new candidate
                    }
                    else if (isClosed && closestAnchorIdx == N && constraintByAnchorIdx.ContainsKey(0))
                    {
                        // Is this constraint less likely than the new candidate?
                        if (constraintByAnchorIdx[0].likelihood < constraints[i].likelihood)
                        {
                            // Replace by new candidate
                            constraintAnchorIdx = closestAnchorIdx;
                        }
                        // Otherwise ignore the new candidate
                    }
                    else
                    {
                        constraintAnchorIdx = closestAnchorIdx;
                    }
                }
                else
                {
                    // Split beziers[anchorIdx] at u
                    constraintAnchorIdx = this.SplitAt(anchorIdx, u);
                    // Insert new anchor point param in list
                    anchorParams.Insert(constraintAnchorIdx, constraints[i].t);
                }

                // Store constraint in dictionnary, pairing it with the appropriate anchor index
                if (constraintAnchorIdx != -1)
                    constraintByAnchorIdx[constraintAnchorIdx] = constraints[i];
            }

            return constraintByAnchorIdx;
        }


        private float GetClosestPointParameter(Vector3 point, int slices, int iterations)
        {
            float tMin = GetClosestPointParameter(point, slices, 0f, 1f, iterations);
            return tMin;
        }

        // Recursively get the parameter t such that B(t) is the closest to the point and t in [start, end]
        private float GetClosestPointParameter(Vector3 point, int slices, float start, float end, int iterations)
        {
            if (iterations <= 0) return (start + end) / 2;
            float step = (end - start) / slices;
            if (step < 10e-6f) return (start + end) / 2;
            float tMin = 0, t = start;
            float dMin = Mathf.Infinity;
            float d;
            while (t <= end)
            {
                d = Vector3.Distance(point, GetPoint(t));
                if (d < dMin)
                {
                    tMin = t;
                    dMin = d;
                }
                t += step;
            }

            // Explicitly check at t = end
            d = Vector3.Distance(point, GetPoint(end));
            if (d < dMin)
            {
                tMin = t;
                dMin = d;
            }

            return GetClosestPointParameter(point, slices, Mathf.Max(tMin - step, 0f), Mathf.Min(tMin + step, 1f), iterations - 1);
        }

        private (int, float) ConvertPolyBezierParameter(float t)
        {
            int nSplits = beziers.Length;
            // Prevent index out of range on last point (t = 1)
            if (Mathf.Approximately(t, 1f))
                return (nSplits - 1, 1f);
            float t2 = t; 
            int splitIdx = Mathf.FloorToInt(t2 * nSplits);
            float u = t2 * nSplits - splitIdx;
            //Debug.Log("idx " + splitIdx + ", u " + u);
            return (splitIdx, u);
        }

        private (int, float) ConvertPolyBezierParameter(float t, List<float> anchorParams)
        {
            int nSplits = beziers.Length;
            List<float> paddedParams = new List<float>(anchorParams);
            paddedParams.Add(1f);
            int splitIdx = Math.Max(0, paddedParams.FindIndex(x => x >= t) - 1);
            float u = (t - paddedParams[splitIdx]) / (paddedParams[splitIdx + 1] - paddedParams[splitIdx]);
            return (splitIdx, u);
        }

        private float InverseConvertPolyBezierParameter(int idx, float u)
        {
            int nSplits = beziers.Length;
            if (idx == nSplits)
                return 1f;
            float t = (u + idx) / nSplits;
            return t;
        }

        private int SplitAt(int bezierIdx, float u)
        {
            //if (Mathf.Approximately(u, 0f) || Mathf.Approximately(u, 1f))
            //{
            //    Debug.Log("not splitting cause already on anchor " + bezierIdx);
            //    return bezierIdx;
            //}
            List<Vector3> points = this.beziers[bezierIdx].GetPoints();
            List<Vector3> left = new List<Vector3>();
            List<Vector3> right = new List<Vector3>();

            while (points.Count > 0)
            {
                left.Add(points[0]);
                right.Add(points[points.Count - 1]);

                for (int i = 0; i < points.Count - 1; i++)
                {
                    points[i] = (1 - u) * points[i] + u * points[i + 1];
                }
                points.RemoveAt(points.Count - 1);
            }

            right.Reverse();

            CubicBezier[] newBeziers = new CubicBezier[] { new CubicBezier(left[0], left[1], left[2], left[3]), new CubicBezier(right[0], right[1], right[2], right[3]) };

            // Remove old bezier and add new ones
            Replace(bezierIdx, newBeziers);

            return bezierIdx + 1;
        }

        private void Replace(int oldBezier, CubicBezier[] newBeziers)
        {
            beziers = beziers.Take(oldBezier).Concat(newBeziers).Concat(beziers.Skip(oldBezier + 1)).ToArray();
        }


        public static List<Vector3> GetControlPoints(CubicBezier[] beziers)
        {
            IEnumerable<Vector3> points = new List<Vector3>();
            foreach(CubicBezier b in beziers)
            {
                points = points.Concat(b.GetPoints().Take(3));
            }
            points = points.Append(beziers[beziers.Length - 1].Get(3));

            return points.ToList();
        }

        public static BezierCurve Mirror(BezierCurve inputCurve, VRSketch.Plane mirrorPlane, out float score)
        {
            List<Vector3> inputPts = inputCurve.GetControlPoints();
            Vector3[] ctrlPts = new Vector3[inputPts.Count];

            Vector3 avgDisplacement = Vector3.zero;
            score = 0f;

            for (int i = 0; i < ctrlPts.Length; i++)
            {
                ctrlPts[i] = mirrorPlane.Mirror(inputPts[i]);
                //avgDisplacement += ctrlPts[i] - inputPts[i];
                score += Mathf.Abs(Vector3.Dot(mirrorPlane.n, ctrlPts[i] - inputPts[i])) * 0.5f; // Distance between point and mirror
            }

            if (ctrlPts.Length > 0)
                score /= ctrlPts.Length;

            //score = Mathf.Abs(Vector3.Dot(mirrorPlane.n, avgDisplacement));

            return new BezierCurve(ctrlPts);
        }

        public static BezierCurve ProjectOnPlane(BezierCurve inputCurve, VRSketch.Plane plane, out float score)
        {
            List<Vector3> inputPts = inputCurve.GetControlPoints();
            Vector3[] ctrlPts = new Vector3[inputPts.Count];
            score = 0f;

            for (int i = 0; i < ctrlPts.Length; i++)
            {
                ctrlPts[i] = plane.Project(inputPts[i]);
                score = Mathf.Max(score, Mathf.Abs(Vector3.Dot(plane.n, ctrlPts[i] - inputPts[i])));
            }
            //if (ctrlPts.Length > 0)
            //    score /= (float)ctrlPts.Length;

            return new BezierCurve(ctrlPts);
        }

        // Static methods used to fit a curve to a bunch of points

        public static CubicBezier[] FitCurve(List<Vector3> points, float error = 1e-2f, float rdpError = 2e-3f)
        {
            int N = points.Count;
            Vector3 P0 = points[0];
            Vector3 P3 = points[N - 1];
            Vector3 P1 = Vector3.zero;
            Vector3 P2 = Vector3.zero;

            if (N == 2)
            {
                return new CubicBezier[] { new CubicBezier(P0, P0, P3, P3) };
            }

            if (N == 3)
            {
                return new CubicBezier[] { new CubicBezier(P0, points[1], points[1], P3) };
            }

            var pointsKeep = RamerDouglasPeucker.RDPReduce(points, rdpError, out var keepIndex);
            //Debug.Log(
            //    "[RDPReduce] Keeping " + 
            //    keepIndex.Count + " out of " + 
            //    N + " points.");

            N = keepIndex.Count;

            // Compute tangents at start and end of curve
            Vector3 tangentA = Vector3.Normalize(pointsKeep[1] - pointsKeep[0]);
            Vector3 tangentB = Vector3.Normalize(pointsKeep[N - 2] - pointsKeep[N - 1]);

            
            return FitCurve(pointsKeep, tangentA, tangentB, error);
        }

        private static CubicBezier[] FitCurve(List<Vector3> points, Vector3 tangentA, Vector3 tangentB, float error = 0.01f)
        {

            //const float error = 0.01f;
            const int maxIter = 20;

            //Parameterize points, and attempt to fit curve
            float[] u = ChordLengthParameterize(points);

            CubicBezier bezier = GenerateBezier(points, u, tangentA, tangentB);

            (float maxError, int splitIndex) = ComputeMaxError(points, bezier, u);
            // If the curve fits the points well enough, return
            if (maxError < error)
            {
                return new CubicBezier[] { bezier };
            }

            // If error is not too large, try reparameterization and try to fit again
            if (maxError < error * 10f)
            {
                for (int i = 0; i < maxIter; i++)
                {
                    float[] u2 = Reparameterize(bezier, points, u);
                    bezier = GenerateBezier(points, u2, tangentA, tangentB);
                    (maxError, splitIndex) = ComputeMaxError(points, bezier, u);
                    if (maxError < error)
                    {
                        //Debug.Log("[Bezier] Reparameterized");
                        return new CubicBezier[] { bezier };
                    }
                    u = u2;
                }
            }

            // Failed to fit a single bezier curve
            // --> Split into 2 curves at the point of max error
            Vector3 tangentSplit = ((Vector3.Normalize(points[splitIndex - 1] - points[splitIndex]) + Vector3.Normalize(points[splitIndex] - points[splitIndex + 1])) * 0.5f).normalized;
            //Vector3 tangentLeft = (tangentSplit + Vector3.Normalize(points[splitIndex - 1] - points[splitIndex])) / 2;
            //Vector3 tangentRight = (-tangentSplit + Vector3.Normalize(points[splitIndex + 1] - points[splitIndex])) / 2;
            Vector3 tangentLeft = tangentSplit;
            Vector3 tangentRight = -tangentSplit;
            CubicBezier[] beziersLeft = FitCurve(points.Take( splitIndex < points.Count -1 ? splitIndex + 1 : splitIndex).ToList(), tangentA, tangentLeft);
            CubicBezier[] beziersRight = FitCurve(points.Skip(splitIndex).ToList(), tangentRight, tangentB);

            return beziersLeft.Concat(beziersRight).ToArray();
        }

        private static float[] ChordLengthParameterize(List<Vector3> points)
        {
            int N = points.Count;
            float[] u = new float[N];
            u[0] = 0f;
            for (int i = 1; i < N; i++)
            {
                u[i] = u[i - 1] + Vector3.Distance(points[i], points[i - 1]);
            }
            for (int i = 1; i < N; i++)
            {
                u[i] = u[i] / u[N - 1];
            }
            return u;
        }

        private static CubicBezier GenerateBezier(List<Vector3> points, float[] u, Vector3 tangentA, Vector3 tangentB)
        {
            int N = points.Count;
            Vector3 P0 = points[0];
            Vector3 P3 = points[N - 1];
            Vector3 P1, P2;

            CubicBezier bezier = new CubicBezier(P0, P0, P3, P3);

            // A matrices
            Vector3[] A1 = new Vector3[N];
            Vector3[] A2 = new Vector3[N];

            for (int i = 0; i < N; i++)
            {
                A1[i] = tangentA * 3 * Mathf.Pow((1f - u[i]), 2f) * u[i];
                A2[i] = tangentB * 3 * Mathf.Pow(u[i], 2f) * (1f - u[i]);
            }

            // C and X
            float[,] C = new float[2, 2] { { 0, 0 }, { 0, 0 } };
            float[] X = new float[2] { 0, 0 };

            for (int i = 0; i < N; i++)
            {
                C[0, 0] += Vector3.Dot(A1[i], A1[i]);
                C[0, 1] += Vector3.Dot(A1[i], A2[i]);
                C[1, 0] += Vector3.Dot(A1[i], A2[i]);
                C[1, 1] += Vector3.Dot(A2[i], A2[i]);

                Vector3 temp = points[i] - bezier.Calculate(u[i]);

                X[0] += Vector3.Dot(A1[i], temp);
                X[1] += Vector3.Dot(A2[i], temp);
            }

            // Compute the determinants
            float det_C1_C2 = C[0, 0] * C[1, 1] - C[1, 0] * C[0, 1];
            float det_C1_X = C[0, 0] * X[1] - C[1, 0] * X[0];
            float det_X_C2 = X[0] * C[1, 1] - X[1] * C[0, 1];

            // Alphas
            float alpha_A = Mathf.Approximately(det_C1_C2, 0f) ? 0f : det_X_C2 / det_C1_C2;
            float alpha_B = Mathf.Approximately(det_C1_C2, 0f) ? 0f : det_C1_X / det_C1_C2;

            float segLength = Vector3.Distance(P0, P3);
            float epsilon = 10e-6f * segLength;
            if (alpha_A < epsilon || alpha_B < epsilon)
            {
                // Fallback on heuristic
                P1 = P0 + tangentA * segLength / 3f;
                P2 = P3 + tangentB * segLength / 3f;
            }
            else
            {
                P1 = P0 + tangentA * alpha_A;
                P2 = P3 + tangentB * alpha_B;
            }

            return new CubicBezier(P0, P1, P2, P3);
        }

        // Returns max error and index of point where it is reached
        private static (float, int) ComputeMaxError(List<Vector3> points, CubicBezier bezier, float[] u)
        {
            int N = points.Count;
            float maxDist = 0f;
            int splitPoint = Mathf.FloorToInt(N / 2f);

            for (int i = 0; i < N; i++)
            {
                float dist = Vector3.Distance(bezier.Calculate(u[i]), points[i]);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    splitPoint = i;
                }
            }

            return (maxDist, splitPoint);
        }

        private static float[] Reparameterize(CubicBezier bezier, List<Vector3> points, float[] u)
        {
            float[] u2 = new float[points.Count];
            // Newton Raphson iteration to get a better parameterization and fit the points better
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 D = bezier.Calculate(u[i]) - points[i];
                // Q'(u)
                Vector3 Q1 = bezier.CalculateDerivative1(u[i]);
                // Q''(u)
                Vector3 Q2 = bezier.CalculateDerivative2(u[i]);
                float numerator = D.x * Q1.x + D.y * Q1.y + D.z * Q1.z;
                float denominator = Q1.x * Q1.x + Q1.y * Q1.y + Q1.z * Q1.z
                                   + D.x * Q2.x + D.y * Q2.y + D.z * Q2.z;
                u2[i] = Mathf.Approximately(denominator, 0f) ? u[i] : u[i] - (numerator / denominator);
            }

            return u2;
        }

    }

}

