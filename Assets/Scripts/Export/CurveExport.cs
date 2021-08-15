//using UnityEngine;
//using Curve;
//using System.Text;
//using VRSketch;
//using System.Collections.Generic;

//public static class CurveExport
//{
//    public static (string, int) CurveToPoints(Curve.Curve c, Transform transform, int samples = 10)
//    {
//        StringBuilder sb = new StringBuilder();
//        if (c is LineCurve)
//        {
//            // Line
//            sb.Append("2 0 2\r\n"); // {nb of points} {any int} {any int}
//            Vector3 A = transform.TransformPoint(c.GetPoint(0f));
//            Vector3 B = transform.TransformPoint(c.GetPoint(1f));

//            sb.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", A.x, A.y, A.z));
//            sb.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", B.x, B.y, B.z));

//            return (sb.ToString(), 1);
//        }
//        else if (c is BezierCurve)
//        {
//            BezierCurve bezierCurve = (BezierCurve)c;
//            Vector3[][] polylines = bezierCurve.GetPolylines(samples);
//            foreach(Vector3[] polyline in polylines)
//            {
//                int n = polyline.Length;
//                sb.Append(n.ToString() + " 0 2\r\n"); // {nb of points} {any int} {any int}
//                foreach(Vector3 point in polyline)
//                {
//                    Vector3 sample = transform.TransformPoint(point);
//                    sb.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", sample.x, sample.y, sample.z));
//                }
//            }
//            return (sb.ToString(), polylines.Length);
//        }
//        else
//        {
//            // Curve header
//            sb.Append(samples.ToString() + " 0 2\r\n"); // {nb of points} {any int} {any int}
//            float step = 1f / (samples - 1);
//            for (int i = 0; i < samples; i++)
//            {
//                Vector3 sample = transform.TransformPoint(c.GetPoint(i * step));

//                sb.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", sample.x, sample.y, sample.z));
//            }
//            return (sb.ToString(), 1);
//        }
//    }

//    public static (string, string) SamplesToPoints(List<Sample> samples, Transform transform, bool exportNormals = true)
//    {
//        StringBuilder sb = new StringBuilder();
//        StringBuilder sbNormals = new StringBuilder();

//        sb.Append(samples.Count.ToString() + " 0 2\r\n"); // {nb of points} {any int} {any int}

//        foreach (Sample s in samples)
//        {
//            Vector3 p = transform.TransformPoint(s.position);
//            Vector3 n = transform.TransformDirection(Vector3.up); // No well defined normals
//            sb.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", p.x, p.y, p.z));
//            sbNormals.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", n.x, n.y, n.z));
//        }

//        return (sb.ToString(), sbNormals.ToString());
//    }

//    public static (string, string) SegmentToPoints(ISegment s, Transform transform, int samples = 10, bool exportNormals = true)
//    {
//        StringBuilder sbPoints = new StringBuilder();
//        StringBuilder sbNormals = new StringBuilder();

//        Curve.Curve c = s.Stroke.Curve;

//        float startParam = s.GetStartParam();
//        float endParam = s.GetEndParam();

//        if (c is LineCurve)
//        {
//            // Line
//            sbPoints.Append("2 0 2\r\n"); // {nb of points} {any int} {any int}
//            Vector3 A = transform.TransformPoint(c.GetPoint(startParam));
//            Vector3 B = transform.TransformPoint(c.GetPoint(endParam));

//            sbPoints.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", A.x, A.y, A.z));
//            sbPoints.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", B.x, B.y, B.z));

//            if (exportNormals)
//            {
//                sbNormals.Append("2 0 2\r\n"); // {nb of points} {any int} {any int}
//                Vector3 nA = transform.TransformVector(s.GetStartNode().Normal);
//                Vector3 nB = transform.TransformVector(s.GetEndNode().Normal);
//                sbNormals.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", nA.x, nA.y, nA.z));
//                sbNormals.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", nB.x, nB.y, nB.z));
//            }

//            return (sbPoints.ToString(), sbNormals.ToString());
//        }
//        else if (c is BezierCurve)
//        {
//            BezierCurve bezierCurve = (BezierCurve)c;
//            // Curve header
//            sbPoints.Append(samples.ToString() + " 0 2\r\n"); // {nb of points} {any int} {any int}

//            if (exportNormals)
//                sbNormals.Append(samples.ToString() + " 0 2\r\n"); // {nb of points} {any int} {any int}

//            float step = (endParam - startParam) / (samples - 1);

//            Vector3 startNormal = s.GetStartNode().Normal;
//            Vector3 endNormal = s.GetEndNode().Normal;
//            float reverse = Vector3.Dot(endNormal, s.Transport(startNormal, s.GetEndNode())) < 0 ? -1 : 1;
//            endNormal *= reverse;

//            for (int i = 0; i < samples; i++)
//            {
//                float t = startParam + i * step;
//                Vector3 sample = transform.TransformPoint(bezierCurve.GetPoint(t));

//                sbPoints.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", sample.x, sample.y, sample.z));

//                if (exportNormals)
//                {
//                    Vector3 normalFromStart = s.Transport(startNormal, startParam, t);
//                    Vector3 normalFromEnd = s.Transport(endNormal, endParam, t);

//                    Vector3 normal = transform.TransformVector(Vector3.Normalize(Vector3.Lerp(normalFromStart, normalFromEnd, t)));

//                    sbNormals.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", normal.x, normal.y, normal.z));
//                }
//            }

//            return (sbPoints.ToString(), sbNormals.ToString());
//        }
//        else
//        {
//            // Curve header
//            sbPoints.Append(samples.ToString() + " 0 2\r\n"); // {nb of points} {any int} {any int}
//            if (exportNormals)
//                sbPoints.Append(samples.ToString() + " 0 2\r\n"); // {nb of points} {any int} {any int}

//            float step = 1f / (samples - 1);
//            for (int i = 0; i < samples; i++)
//            {
//                Vector3 sample = transform.TransformPoint(c.GetPoint(i * step));

//                sbPoints.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", sample.x, sample.y, sample.z));

//                if (exportNormals)
//                {
//                    Vector3 normal = Vector3.up;
//                    sbNormals.Append(string.Format("{0:0.000} {1:0.000} {2:0.000}\r\n", normal.x, normal.y, normal.z));
//                }
//            }
//            return (sbPoints.ToString(), sbNormals.ToString());
//        }
//    }
//}