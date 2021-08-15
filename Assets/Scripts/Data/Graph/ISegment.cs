using UnityEngine;
using System.Collections;

namespace VRSketch
{
    public interface ISegment
    {
        int ID { get; }
        FinalStroke Stroke { get; }
        Vector3 GetPointAt(float u);
        Vector3 GetTangentAt(INode endpoint);

        INode GetOpposite(INode from);

        INode GetStartNode();
        INode GetEndNode();
        (INode, float) GetClosest(Vector3 position);

        float GetStartParam();
        float GetEndParam();
        float GetParam(INode from);

        Vector3 Transport(Vector3 v, INode to);
        Vector3 Transport(Vector3 v, float fromParam, float toParam);

        int GetSamplesCount(float targetEdgeLength);
        Vector3 ProjectInPlane(INode n, Vector3 normal);
        bool IsInReverse(INode from);
    }

}

