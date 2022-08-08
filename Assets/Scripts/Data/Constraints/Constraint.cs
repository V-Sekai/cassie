using UnityEngine;
using System.Collections;
using Curve;



public class Constraint
{
    public Vector3 Position { get; private set; }
    public PointOnCurve NewCurveData { get; private set; }

    public Constraint(Vector3 position)
    {
        this.Position = position;
    }

    public void ProjectOn(BezierCurve newCurve, int anchorIdx)
    {
        this.NewCurveData = newCurve.GetPointOnCurve(anchorIdx);
    }

    public void ProjectOn(Curve.Curve newCurve)
    {
        this.NewCurveData = newCurve.Project(Position);
    }

    public void ReparameterizeNewCurve(Reparameterization? r, Curve.Curve c)
    {
        float t = NewCurveData.t;
        //Debug.Log("old param = " + t.ToString("F6"));

        // If we are provided a simple reparameterization, just use this
        if (r != null)
        {
            float t_prime = c.Reparameterize((Reparameterization)r, t);
            NewCurveData = c.GetPointOnCurve(t_prime);
        }
        else
        {
            // Otherwise we reproject on the curve from the position
            NewCurveData = c.Project(Position);
        }
    }

}
