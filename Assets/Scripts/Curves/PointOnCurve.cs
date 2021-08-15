using UnityEngine;

namespace Curve
{
    public struct PointOnCurve
    {
        public float t { get; private set; }
        public Vector3 Position { get; }
        public Vector3 Tangent { get; }
        public Vector3 LocalNormal { get; }

        public PointOnCurve(float t, Vector3 pos, Vector3 tan, Vector3 nor)
        {
            this.t = t; Position = pos; Tangent = tan; LocalNormal = nor;
        }

        public PointOnCurve Mirror(VRSketch.Plane mirrorPlane)
        {
            PointOnCurve mirrorPoint = new PointOnCurve(t, mirrorPlane.Mirror(Position), mirrorPlane.MirrorDir(Tangent), mirrorPlane.MirrorDir(LocalNormal));
            return mirrorPoint;
        }

    }
}