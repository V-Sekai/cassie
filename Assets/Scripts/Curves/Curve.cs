using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

/*
    This code was adapted from https://github.com/mattatz/unity-tubular and somewhat broken down to keep only what was necessary for this project
    Check out mattatz original repository for better generation of tubular meshes, with regular sampling along the curve, Catmull-Rom curves, etc.
 */

namespace Curve {


    // Used to store some reparameterization procedure for a curve,
    // useful when we cut a curve (this induces a new parameterization) and need to compute new parameters for constraints on the curve
    public struct Reparameterization
    {
        public float t0 { get; }
        public float ratio { get; }

        public Reparameterization(float t0, float ratio)
        {
            this.t0 = t0; this.ratio = ratio;
        }
    }

    public abstract class Curve {

        protected bool closed;

        // WARNING: weights property is unused in this project, while it is still in place I have no idea if it still working as expected
        protected List<float> weights;

        protected float[] cacheArcLengths;
        protected bool needsUpdate;

        public Curve(bool closed = false) {
            this.closed = closed;
        }

        public Curve(List<float> weights, bool closed = false)
        {
            this.closed = closed;
            this.weights = weights;
        }

        public abstract PointOnCurve Project(Vector3 point);
        public abstract PointOnCurve GetPointOnCurve(float t);
        public abstract Vector3 ParallelTransport(Vector3 v, float from, float to);
        public abstract List<Vector3> GetControlPoints();
        public abstract List<List<Vector3>> GetControlPointsBetween(float from, float to);

        public abstract Reparameterization? CutAt(float t, bool throwBefore, float snapToExistingAnchorThreshold);

        public abstract bool IsValid(float size_threshold);

        public abstract float LengthBetween(float tA, float tB);
        public float Reparameterize(Reparameterization reparam, float t)
        {
            if (t < reparam.t0)
                return 0f;
            return (t - reparam.t0) * reparam.ratio;
        }

        public abstract Vector3 GetPoint(float t);
        public abstract float GetWeight(float t);

        public virtual Vector3 GetTangent(float t) {
            var delta = 0.001f;
            var t1 = t - delta;
            var t2 = t + delta;

            // Capping in case of danger
            if (t1 < 0f) t1 = 0f;
            if (t2 > 1f) t2 = 1f;

            var pt1 = GetPoint(t1);
            var pt2 = GetPoint(t2);
            return (pt2 - pt1).normalized;
        }

        public float GetLength(int divisions = -1)
        {
            float[] lengths = this.GetLengths(divisions);
            return lengths[lengths.Length - 1];
        }

        float[] GetLengths(int divisions = -1) {
            if (divisions < 0) {
                divisions = 200;
            }

            if (this.cacheArcLengths != null &&
                    (this.cacheArcLengths.Length == divisions + 1) &&
                    !this.needsUpdate) {
                return this.cacheArcLengths;
            }

            this.needsUpdate = false;

            var cache = new float[divisions + 1];
            Vector3 current, last = this.GetPoint(0f);

            cache[0] = 0f;

            float sum = 0f;
            for (int p = 1; p <= divisions; p ++ ) {
                current = this.GetPoint(1f * p / divisions);
                sum += Vector3.Distance(current, last);
                cache[p] = sum;
                last = current;
            }

            this.cacheArcLengths = cache;
            return cache;
        }

        public List<FrenetFrame> ComputeFrenetFrames (int segments, Vector3 N0, bool closed) {
            var normal = new Vector3();

            var tangents = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var binormals = new Vector3[segments + 1];

            float u, theta;

            // compute the tangent vectors for each segment on the curve
            for (int i = 0; i <= segments; i++) {
                u = (1f * i) / segments;
                tangents[i] = GetTangent(u).normalized;
            }

            // A non null initial normal was given
            if (N0.magnitude > float.Epsilon && Vector3.Cross(tangents[0], N0.normalized).magnitude > float.Epsilon)
            {
                var vec = Vector3.Cross(tangents[0], N0.normalized).normalized;
                normals[0] = Vector3.Cross(tangents[0], vec);
            }
            else
            {
                // select an initial normal vector perpendicular to the first tangent vector,
                // and in the direction of the minimum tangent xyz component
                normals[0] = new Vector3();
                var min = float.MaxValue;
                var tx = Mathf.Abs(tangents[0].x);
                var ty = Mathf.Abs(tangents[0].y);
                var tz = Mathf.Abs(tangents[0].z);
                if (tx <= min)
                {
                    min = tx;
                    normal.Set(1, 0, 0);
                }
                if (ty <= min)
                {
                    min = ty;
                    normal.Set(0, 1, 0);
                }
                if (tz <= min)
                {
                    normal.Set(0, 0, 1);
                }
                var vec = Vector3.Cross(tangents[0], normal).normalized;
                normals[0] = Vector3.Cross(tangents[0], vec);
            }
            
            binormals[0] = Vector3.Cross(tangents[0], normals[0]);

            // compute the slowly-varying normal and binormal vectors for each segment on the curve

            for (int i = 1; i <= segments; i++) {
                // copy previous
                normals[i] = normals[i - 1];
                binormals[i] = binormals[i - 1];

                // Rotation axis
				var axis = Vector3.Cross(tangents[i - 1], tangents[i]);
                if (axis.magnitude > float.Epsilon) {
                    axis.Normalize();

                    float dot = Vector3.Dot(tangents[i - 1], tangents[i]);

                    // clamp for floating pt errors
                    theta = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));

                    normals[i] = Quaternion.AngleAxis(theta * Mathf.Rad2Deg, axis) * normals[i];
                }

                binormals[i] = Vector3.Cross(tangents[i], normals[i]).normalized;
            }

            // if the curve is closed, postprocess the vectors so the first and last normal vectors are the same

            if (closed) {
                theta = Mathf.Acos(Mathf.Clamp(Vector3.Dot(normals[0], normals[segments]), -1f, 1f));
                theta /= segments;

                if (Vector3.Dot(tangents[0], Vector3.Cross(normals[0], normals[segments])) > 0f) {
                    theta = - theta;
                }

                for (int i = 1; i <= segments; i++) {
                    normals[i] = (Quaternion.AngleAxis(Mathf.Deg2Rad * theta * i, tangents[i]) * normals[i]);
                    binormals[i] = Vector3.Cross(tangents[i], normals[i]);
                }
            }

            var frames = new List<FrenetFrame>();
            int n = tangents.Length;
            for(int i = 0; i < n; i++) {
                var frame = new FrenetFrame(tangents[i], normals[i], binormals[i]);
                frames.Add(frame);
            }
            return frames;
        }


    }

}

