using UnityEngine;
using Curve;
using System.Text;
using VRSketch;

public static class CurvesExport
{
    public static string CurveToPolyline(Curve.Curve c, int subdivisionsPerUnit)
    {
        StringBuilder sb = new StringBuilder();
        if (c is LineCurve)
        {
            // Line
            sb.Append("v 2\r\n"); // v {nb of points}
            Vector3 A = Utils.ChangeHandedness(c.GetPoint(0f));
            Vector3 B = Utils.ChangeHandedness(c.GetPoint(1f));

            sb.Append(Format(A));
            sb.Append(Format(B));

            return sb.ToString();
        }
        else
        {
            int nPts = Mathf.CeilToInt(c.GetLength() * subdivisionsPerUnit);

            // Curve header
            sb.Append("v " + nPts.ToString() + "\r\n"); // v {nb of points}
            float step = 1f / (nPts - 1);
            for (int i = 0; i < nPts; i++)
            {
                Vector3 sample = Utils.ChangeHandedness(c.GetPoint(i * step));

                sb.Append(Format(sample));
            }
            return sb.ToString();
        }
    }

    public static string SamplesToPolyline(Vector3[] inputSamples)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("v " + inputSamples.Length.ToString() + "\r\n"); // v {nb of points}

        foreach (var p in inputSamples)
        {
            sb.Append(Format(Utils.ChangeHandedness(p)));
        }

        return sb.ToString();
    }

    private static string Format(Vector3 v)
    {
        return string.Format("{0:+0.000000;-0.000000} {1:+0.000000;-0.000000} {2:+0.000000;-0.000000}\r\n", v.x, v.y, v.z);
    }
}