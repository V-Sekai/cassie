using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Curve;
using VRSketch;
using System.Text;

public class FinalStroke : Stroke
{
    public bool DebugGizmos = false;

    public int ID { get; private set; }

    public Curve.Curve Curve { get; private set; } = null;

    private LinkedList<ISegment> segments = new LinkedList<ISegment>();
    private Graph _graph;

    public Vector3[] inputSamples { get; private set; } = null;

    // FinalStroke should always be initialized as a child to a DrawingCanvas
    protected override void Awake()
    {
        base.Awake();
        // Fetch reference to scene graph
        DrawingCanvas parent = GetComponentInParent<DrawingCanvas>();
        if (parent != null)
        {
            _graph = parent.Graph;
        }

        else
            throw new System.Exception("FinalStroke should always be initialized as a child to a DrawingCanvas");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Bezier ctrl points
        List<Vector3> pts = Curve.GetControlPoints();

        Gizmos.color = Color.blue;

        int idx = 0;
        foreach (var p in pts)
        {
            Gizmos.color = idx % 3 == 0 ? Color.blue: Color.yellow;
            Gizmos.DrawSphere(transform.TransformPoint(p), 0.002f);
            idx++;
        }

        foreach (var s in segments)
        {
            // Label: ID
            Vector3 midpoint = this.transform.TransformPoint(s.GetPointAt(0.5f));
            Handles.Label(midpoint, s.ID.ToString());
        }


    }
#endif

    public override void RenderAsLine(float scale)
    {
        int N = Mathf.Max(Mathf.CeilToInt(Curve.GetLength() * SubdivisionsPerUnit * scale), 4);
        Vector3[] points = new Vector3[N];

        float step = 1f / (N - 1);

        for (int i = 0; i < N; i++)
        {
            float t = i * step;
            points[i] = Curve.GetPoint(t);
        }

        RenderPoints(points, scale);
    }

    public void SetID(int id)
    {
        this.ID = id;
    }


    public List<Vector3> GetControlPoints()
    {
        return Curve.GetControlPoints();
    }


    public void SetCurve(Curve.Curve c, bool closedLoop = false)
    {
        this.Curve = c;

        // Create a segment representing the whole stroke
        if (!closedLoop)
        {
            INode startNode = _graph.NewNode(this.Curve.GetPoint(0f));
            INode endNode = _graph.NewNode(this.Curve.GetPoint(1f));
            ISegment init = _graph.NewSegment(this, 0f, 1f, startNode, endNode, onNewStroke: true);
            segments.AddFirst(init);
        }
        else
        {
            INode closingNode = _graph.NewNode(this.Curve.GetPoint(0f));

            ISegment initA = _graph.NewSegment(this, 0f, 1f, closingNode, closingNode, onNewStroke: true);
            segments.AddFirst(initA);
        }

    }

    public void SaveInputSamples(Vector3[] samples)
    {
        inputSamples = samples;
    }

    public override void Destroy()
    {
        // Remove each segment from graph
        foreach(var segment in this.segments)
        {
            _graph.Remove(segment);
        }

        // Destroy game object
        base.Destroy();
    }

    // Position has to be in canvas space
    public IntersectionConstraint GetConstraint(Vector3 position, float snapToExistingNodeThreshold)
    {
        // Get closest point on curve
        PointOnCurve onCurve = this.Curve.Project(position);

        bool isAtNode = false;

        // Try snapping to existing nodes
        if (segments.Count > 0)
        {
            LinkedListNode<ISegment> temp = GetSegmentInListContaining(onCurve.t);
            ISegment s = temp != null ? temp.Value : null;
            if (s != null)
            {
                // Check if we are within a radius of the existing node
                (INode closest, float onCurveParam) = s.GetClosest(onCurve.Position);
                //Debug.Log("closest intersection at: " + Vector3.Distance(closest.Position, onCurve.Position));
                if (
                    Vector3.Distance(closest.Position, onCurve.Position) < snapToExistingNodeThreshold)
                {
                    //Debug.Log("snapped to existing intersection");
                    onCurve = this.Curve.GetPointOnCurve(onCurveParam);
                    if (closest.IncidentCount > 1)
                        isAtNode = true;
                }
            }
        }

        return new IntersectionConstraint(this, onCurve, isAtNode);
    }

    public Vector3 ClosestPoint(Vector3 position, bool canvasSpace = false)
    {
        // Position in world space
        Vector3 canvasSpacePos = canvasSpace ? position : gameObject.transform.InverseTransformPoint(position);
        // Get closest point on curve
        PointOnCurve onCurve = this.Curve.Project(canvasSpacePos);

        return onCurve.Position;
    }

    public INode AddIntersectionOldStroke(PointOnCurve point, float proximity_threshold)
    {
        // Register an intersection on an old stroke
        // by either creating a new node at the given point
        // or determining which existing node the intersection lies on

        // We return a reference to the node so that we can then register the new stroke at that intersection

        LinkedListNode<ISegment> segmentContainingIntersection = GetSegmentInListContaining(point.t);
        INode newNode = _graph.NewNode(point.Position);
        AddOrReplaceNewNode(ref newNode, segmentContainingIntersection, point, proximity_threshold);

        return newNode;
    }

    public void AddIntersectionNewStroke(INode newNode, PointOnCurve point, float proximity_threshold)
    {
        // Register an intersection on a new stroke
        // by inserting the newNode on the stroke

        LinkedListNode<ISegment> segmentContainingIntersection = GetSegmentInListContaining(point.t);
        AddOrReplaceOldNode(newNode, segmentContainingIntersection, point, proximity_threshold);
    }

    public bool MendSegments(INode mendAt, ISegment sA, ISegment sB)
    {
        LinkedListNode<ISegment> sALink = this.segments.Find(sA);

        LinkedListNode<ISegment> sLeft = null;
        LinkedListNode<ISegment> sRight = null;

        if (sALink.Next != null && sALink.Next.Value.Equals(sB)
            && sA.GetEndNode().Equals(mendAt) && sB.GetStartNode().Equals(mendAt)
            )
        {
            // sA is before sB
            sLeft = sALink;
            sRight = sALink.Next;
        }
        else if (sALink.Previous != null && sALink.Previous.Value.Equals(sB)
            && sA.GetStartNode().Equals(mendAt) && sB.GetEndNode().Equals(mendAt)
            )
        {
            // then sB is before sA
            sLeft = sALink.Previous;
            sRight = sALink;
        }
        else
        {
            // Node is closing the loop, don't remove it
            return false;
        }

        // Mend segments in graph:
        // - Delete sRight and edit end of sLeft
        // - Deal with cycles
        _graph.MendSegments(sLeft.Value, sRight.Value);

        // Actually remove it from stroke too
        this.segments.Remove(sRight);
        return true;
    }

    public Vector3 ParallelTransport(Vector3 v, float from, float to)
    {
        return Curve.ParallelTransport(v, from, to);
    }


    public void PrintSegments()
    {
        Debug.Log("Stroke with " + this.segments.Count + " segments");
        //if (this.segments.Count > 0)
        //    Debug.Log(segments.First.Value.GetStartNode().Position.ToString("F6"));
        foreach(var segment in this.segments)
        {
            Debug.Log(segment.GetStartNode().Position.ToString("F6"));
            Debug.Log(segment.GetStartParam());
            Debug.Log(segment.GetEndNode().Position.ToString("F6"));
            Debug.Log(segment.GetEndParam());
            Debug.Log("------------------");
        }
    }

    public (SerializableStrokeInGraph, List<SerializableSegment>) GetGraphData()
    {
        List<SerializableSegment> segmentsData = new List<SerializableSegment>(segments.Count);

        List<int> segmentsID = new List<int>(segments.Count);

        foreach (var s in segments)
        {
            List<List<Vector3>> ctrlPts = GetControlPointsForSegment(s);
            List<int> nodesID = new List<int> { s.GetStartNode().ID, s.GetEndNode().ID };
            SerializableSegment sData = new SerializableSegment(s.ID, this.ID, ctrlPts, nodesID);

            segmentsData.Add(sData);
            segmentsID.Add(s.ID);
        }

        SerializableStrokeInGraph strokeData = new SerializableStrokeInGraph(this.ID, segmentsID);

        return (strokeData, segmentsData);
    }

    public ISegment GetSegmentContaining(float param)
    {
        return GetSegmentInListContaining(param).Value;
    }

    private LinkedListNode<ISegment> GetSegmentInListContaining(float param)
    {
        LinkedListNode<ISegment> segment = this.segments.First;
        while (segment.Next != null && segment.Value.GetEndParam() < param)
        {
            segment = segment.Next;
        }
        return segment;
    }

    private INode GetClosest(ISegment segment, Vector3 position)
    {
        INode oldLeft = segment.GetStartNode();
        INode oldRight = segment.GetEndNode();

        // Find closest among those 2
        INode closest = Vector3.Distance(oldLeft.Position, position) < Vector3.Distance(oldRight.Position, position) ? oldLeft : oldRight;

        return closest;
    }

    private void AddOrReplaceNewNode(ref INode node, LinkedListNode<ISegment> segment, PointOnCurve point, float proximity_threshold)
    {
        // Given a position for a candidate new intersection, and a segment on which the candidate lies
        // Determine whether we should add the node by splitting the segment,
        // or if we should "merge" this node with an existing one (based on proximity)

        // Merge old and new node => keep old node and change reference of new node to point to that

        INode closest = GetClosest(segment.Value, point.Position);
        if (Vector3.Distance(closest.Position, point.Position) < proximity_threshold)
        {
            // Do nothing and replace by existing node
            _graph.Remove(node);
            node = closest;
            return;
        }

        // Otherwise the node is added and a new segment is created
        // old segment -- new node -- new segment
        AddNode(node, segment, point);

    }

    private void AddOrReplaceOldNode(INode node, LinkedListNode<ISegment> segment, PointOnCurve point, float proximity_threshold)
    {
        // Given a position for a candidate new intersection, and a segment on which the candidate lies
        // If needed, create new segment and associate segments with node,
        // otherwise replace existing node with this new node and join segments

        // Merge old and new node => keep new node

        INode closest = GetClosest(segment.Value, point.Position);
        if (Vector3.Distance(closest.Position, point.Position) < proximity_threshold)
        {
            // Remove old node and replace by new one
            _graph.Remove(closest);

            // Update segment to replace old node by new one
            // It is possible that both end and start nodes of this segment are actually the same node "closest". That happens when a segment is a loop (start node == end node)
            // That's why we test both possibilities, and replace the node wherever necessary
            // We keep the parameter value the same as before however, as it should be unchanged, we're only swapping the reference to the node object.

            if (segment.Value.GetStartNode().Equals(closest))
                _graph.SetStart(segment.Value, node, updateNeighbors: true);
            if (segment.Value.GetEndNode().Equals(closest))
                _graph.SetEnd(segment.Value, node, updateNeighbors: true);

            return;
        }

        // Otherwise the node is added
        // old segment -- new node -- new segment
        AddNode(node, segment, point, onNewStroke: true);
    }

    private void AddNode(INode node, LinkedListNode<ISegment> segment, PointOnCurve point, bool onNewStroke = false)
    {

        // Create new segment and add it to the list
        ISegment newSegment = _graph.NewSegment(
            this,
            point.t, segment.Value.GetEndParam(),
            node, segment.Value.GetEndNode(),
            onNewStroke // If we are adding a node to a new stroke, query the neighboring segments and update them
            );
        this.segments.AddAfter(segment, newSegment);

        // Update old segment to set its end properly
        _graph.SetEnd(segment.Value, point.t, node);

        // If we are dividing an existing segment,
        // take care of its potential cycles
        if (!onNewStroke)
            _graph.RepairCycles(segment.Value, newSegment);
    }

    private List<List<Vector3>> GetControlPointsForSegment(ISegment s)
    {
        float startParam = s.GetStartParam();
        float endParam = s.GetEndParam();

        return Curve.GetControlPointsBetween(startParam, endParam);
    }

}
