using UnityEngine;
using System.Collections;

public class MirrorPlaneConstraint : Constraint
{
    public Vector3 PlaneNormal { get; }

    public MirrorPlaneConstraint(Vector3 position, Vector3 planeNormal) : base(position)
    {
        this.PlaneNormal = planeNormal;
    }
}
