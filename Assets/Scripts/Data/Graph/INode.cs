using UnityEngine;
using System.Collections.Generic;

namespace VRSketch
{
    public interface INode
    {
        int ID { get; }
        Vector3 Position { get; }
        Vector3 Normal { get; }
        int IncidentCount { get; }
        bool IsSharp { get; }

        ISegment GetNext(ISegment s);
        ISegment GetPrevious(ISegment s);
        ISegment GetInPlane(ISegment s, Vector3 N, bool next);
    }

}

