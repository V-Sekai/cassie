using Curve;
using UnityEngine;

public class IntersectionConstraint : Constraint
{
    public FinalStroke IntersectedStroke { get; }

    public PointOnCurve OldCurveData { get; }

    public bool IsAtNode { get; }


    public IntersectionConstraint(FinalStroke intersected, PointOnCurve intersectedData, bool isAtNode)
        : base(intersectedData.Position)
    {
        this.OldCurveData = intersectedData;
        this.IntersectedStroke = intersected;
        this.IsAtNode = isAtNode;
    }

}