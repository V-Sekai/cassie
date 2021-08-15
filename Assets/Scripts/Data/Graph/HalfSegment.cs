
using UnityEngine;

namespace VRSketch
{
    // This is a simple wrapper around the segment class that enables storing an orientation as well
    // Useful to describe a cycle, and to reconstitute the curve loop for patch tessellation
    public class HalfSegment
    {

        private readonly ISegment _segment;
        public bool IsReversed { get; }

        public HalfSegment(ISegment segment, bool reversed)
        {
            _segment = segment;
            IsReversed = reversed;
        }

        public int GetSegmentID()
        {
            return _segment.ID;
        }

        public int GetStrokeID()
        {
            return _segment.Stroke.ID;
        }

        public INode GetStart()
        {
            return IsReversed ? _segment.GetEndNode() : _segment.GetStartNode();
        }

        public INode GetEnd()
        {
            return IsReversed ? _segment.GetStartNode() : _segment.GetEndNode();
        }

        public Vector3[] GetSamples(float targetEdgeLength)
        {
            int sampleCount = _segment.GetSamplesCount(targetEdgeLength);
            Vector3[] pos = new Vector3[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float u = IsReversed ? i / (float)sampleCount : (1f - i / (float)sampleCount);
                pos[i] = _segment.GetPointAt(u);
            }

            return pos;
        }
    }

}
