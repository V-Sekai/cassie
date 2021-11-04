using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using System.Collections.Generic;
using System;

namespace VRSketch
{

    public static class FewColors
    {
        private static Color[] _colors = new Color[]
        {
            Color.red,
            Color.green,
            Color.yellow,
            Color.magenta,
            Color.cyan,
            Color.blue,
            new Color(1f,0.388f,0.278f), // Coral
            new Color(0.133f,0.545f,0.133f), // Forest Green
            new Color(0.855f,0.627f,0.125f), // Golden Rod
            new Color(0.098f,0.098f,0.439f), // Midnight Blue
            new Color(0.5f,0,0.5f), // Purple
            new Color(0.627f,0.32f,0.176f), // Sienna (brown)
            new Color(1f,0.753f,0.8f) // Pink
        };

        public static Color Get(int idx)
        {
            int loopedIdx = (_colors.Length + idx) % _colors.Length;
            return _colors[loopedIdx];
        }
    }

    public class Plane
    {
        public Vector3 n { get; private set; }
        public Vector3 p0 { get; private set; }

        public Plane(Vector3 n, Vector3 p0)
        {
            this.n = n;
            this.p0 = p0;
        }

        public float Distance(Vector3 p)
        {
            return Mathf.Abs(Vector3.Dot(p - p0, n));
        }

        public Vector3 Project(Vector3 p)
        {
            return p - Vector3.Dot(n, p - p0) * n;
        }

        public Vector3 Mirror(Vector3 p)
        {
            float a = Vector3.Dot(n, p - p0);
            //Vector3 dir = a * n;
            return p - 2f * a * n;
        }

        public Vector3 MirrorDir(Vector3 v)
        {
            return -Vector3.Reflect(v, n);
        }

        public void SnapToOrtho(Vector3[] orthoDirections, float threshold)
        {

            float maxCoord = 0f;
            int maxIdx = 0;
            for (int i = 0; i < 3; i++)
            {
                float angle = Mathf.Abs(Vector3.Dot(n, orthoDirections[i]));
                if (angle > maxCoord)
                {
                    maxCoord = angle;
                    maxIdx = i;
                }
            }
            if (maxCoord > threshold)
            {
                n = orthoDirections[maxIdx];
                //Debug.Log("snapped plane normal to " + n);
            }
        }

        public static Plane FitPlane(Vector3[] points)
        {
            Vector3 n, p0;
            Vector3 sum = Vector3.zero;
            foreach (Vector3 p in points)
            {
                sum += p;
            }
            Vector3 centroid = sum / points.Length;

            float xx = 0f; float xy = 0f; float xz = 0f;
            float yy = 0f; float yz = 0f; float zz = 0f;

            foreach (Vector3 p in points)
            {
                Vector3 r = p - centroid;
                xx += r.x * r.x;
                xy += r.x * r.y;
                xz += r.x * r.z;
                yy += r.y * r.y;
                yz += r.y * r.z;
                zz += r.z * r.z;
            }

            float det_x = yy * zz - yz * yz;
            float det_y = xx * zz - xz * xz;
            float det_z = xx * yy - xy * xy;

            float det_max = Mathf.Max(det_x, det_y, det_z);
            if (det_max <= 0f)
            {
                return null; // The points don't span a plane
            }
            // Pick path with best conditioning:
            n =
                det_max == det_x ?
                    new Vector3(det_x, xz * yz - xy * zz, xy * yz - xz * yy) :
                det_max == det_y ?
                    new Vector3(xz * yz - xy * zz, det_y, xy * xz - yz * xx) :
                    // det_z is the max
                    new Vector3(xy * yz - xz * yy, xy * xz - yz * xx, det_z);

            n = Vector3.Normalize(n);
            p0 = centroid;
            return new Plane(n, p0);
        }
    }

    public static class Constants
    {
        public const float eps = 10e-6f;
    }

    public static class Utils
    {

        public static Vector3 ChangeHandedness(Vector3 v)
        {
            return new Vector3(-v.x, v.y, v.z);
        }

        public static Quaternion ChangeHandedness(Quaternion q)
        {
            return new Quaternion(-q.x, q.y, q.z, -q.w);
        }

        public static float[] Flatten(Vector3 vec)
        {
            return new float[] { vec.x, vec.y, vec.z };
        }

        public static float[] FlattenArray(Vector3[] vecs)
        {
            float[] flat = new float[3 * vecs.Length];
            int i = 0;
            foreach (Vector3 v in vecs)
            {
                flat[i] = v.x;
                flat[i + 1] = v.x;
                flat[i + 2] = v.x;
                i += 3;
            }

            return flat;
        }

        public static Vector3[] ToVector3Array(float[] coordsArray)
        {
            int N = (int)(coordsArray.Length / 3);
            Vector3[] vectors = new Vector3[N];

            for (int i = 0; i < N; i++)
            {
                vectors[i] = new Vector3(coordsArray[3 * i],
                                         coordsArray[3 * i + 1],
                                         coordsArray[3 * i + 2]
                                        );
            }

            return vectors;
        }

        public static Matrix<float> CrossMatrix(Vector3 V)
        {
            Matrix<float> M = Matrix<float>.Build.Dense(3, 3);

            M[0, 1] = -V[2];
            M[1, 0] = V[2];
            M[0, 2] = V[1];
            M[2, 0] = -V[1];
            M[1, 2] = -V[0];
            M[2, 1] = V[0];

            return M;
        }

        public static (Plane, float) FitPlane(Vector3[] points)
        {

            if (points.Length < 3)
                return (null, float.PositiveInfinity);

            Plane P = Plane.FitPlane(points);

            if (P == null)
                return (null, Mathf.Infinity);

            float maxDist = 0;
            // Compute maximum fitting error
            foreach (Vector3 p in points)
            {
                float d = P.Distance(p);
                if (d > maxDist)
                    maxDist = d;
            }

            return (P, maxDist);
        }

        public static (Plane, float) FitPlane(Vector3 point, Vector3[] vectors)
        {
            if (vectors.Length < 2)
                return (null, float.PositiveInfinity);

            float score = 0f; // How non-collinear the set of vectors are
            Vector3[] pointsToFitPlane = new Vector3[vectors.Length + 1];
            for (int i = 0; i < vectors.Length; i++)
            {
                pointsToFitPlane[i] = point + vectors[i];
                float nonCollinearity = Vector3.Cross(vectors[i], vectors[(i + 1) % vectors.Length]).magnitude;
                if (nonCollinearity > score)
                    score = nonCollinearity;
            }
            pointsToFitPlane[vectors.Length] = point;

            if (score < 0.1f)
                return (null, float.PositiveInfinity);

            Plane P = Plane.FitPlane(pointsToFitPlane);

            float maxError = 0f;
            // Compute max of dot(plane.normal, tangent)
            foreach (var t in vectors)
            {
                float error = Mathf.Abs(Vector3.Dot(P.n, t));
                if (error > maxError)
                    maxError = error;
            }

            return (P, maxError);
        }

        // Project point P on line of direction dir (unit length vector) passing through p0
        public static Vector3 ProjectOnLine(Vector3 p0, Vector3 dir, Vector3 P)
        {
            return p0 + Vector3.Dot(P - p0, dir) * dir;
        }




    }

}