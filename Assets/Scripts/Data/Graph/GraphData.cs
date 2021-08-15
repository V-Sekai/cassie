using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRSketch
{
    [Serializable]
    public struct CurveNetworkData
    {
        public List<SerializableStrokeInGraph> strokes;
        public List<SerializableSegment> segments;
        public List<SerializableNode> nodes;

        public CurveNetworkData(List<SerializableStrokeInGraph> strokes, List<SerializableSegment> segments, List<SerializableNode> nodes)
        {
            this.strokes = strokes;
            this.segments = segments;
            this.nodes = nodes;
        }
    }

    [Serializable]
    public struct SerializableStrokeInGraph
    {
        public int id;
        public List<int> segments;

        public SerializableStrokeInGraph(int id, List<int> segments)
        {
            this.id = id;
            this.segments = segments;
        }
    }

    [Serializable]
    public struct SerializableSegment
    {
        public int id;
        public int stroke_id;
        public List<List<SerializableVector3>> ctrl_pts;
        public List<int> nodes;

        public SerializableSegment(int id, int strokeID, List<List<Vector3>> ctrlPts, List<int> nodes)
        {
            this.id = id;
            this.stroke_id = strokeID;
            this.ctrl_pts = ctrlPts.ConvertAll(pts => pts.ConvertAll(x => (SerializableVector3)x));
            this.nodes = nodes;
        }
    }

    [Serializable]
    public struct SerializableNode
    {
        public int id;
        public SerializableVector3 position;
        public List<int> neighbor_edges;

        public SerializableNode(int id, Vector3 position, List<int> neighbor_ids)
        {
            this.id = id;
            this.position = (SerializableVector3)position;
            this.neighbor_edges = neighbor_ids;
        }
    }

}
