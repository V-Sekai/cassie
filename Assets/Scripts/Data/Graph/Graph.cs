using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRSketch
{

    public partial class Graph
    {

        // Parameter defined on init: whether or not we want to automatically look for surfaces
        private bool surfacing = false;

        // Graph cycles current update
        private List<Cycle> _toAddCache = new List<Cycle>();
        private HashSet<Cycle> _toRemoveCache = new HashSet<Cycle>();

        // Updated segments cache: for next cycle search
        private HashSet<Segment> _updatedSegmentsCache = new HashSet<Segment>();
        private HashSet<Cycle> _cyclesToCheckCache = new HashSet<Cycle>();

        // Graph data
        //private List<Node> _nodes = new List<Node>();
        private Dictionary<int, Node> _nodes = new Dictionary<int, Node>();
        private Dictionary<int, Segment> _segments = new Dictionary<int, Segment>();
        private Dictionary<int, SegmentCycles> _cyclesBySegment = new Dictionary<int, SegmentCycles>();
        private HashSet<Cycle> _cycles = new HashSet<Cycle>();

        // ID counters
        private int _segmentID = 0;
        private int _nodeID = 0;

        public Graph() {  }

        // Graph initialization
        public void Init(bool surfacing)
        {
            // Set parameter
            this.surfacing = surfacing;
            Clear();
        }

        public void SwitchSystem(bool surfacing)
        {
            // Set parameter
            this.surfacing = surfacing;
        }

        public void Clear()
        {
            // Clearing all graph data

            _toAddCache.Clear();
            _toRemoveCache.Clear();

            // Updated segments cache: for next cycle search
            _updatedSegmentsCache.Clear();
            _cyclesToCheckCache.Clear();

            // Graph data
            _nodes.Clear();
            _segments.Clear();
            _cyclesBySegment.Clear();
            _cycles.Clear();

            // ID counters
            _segmentID = 0;
            _nodeID = 0;
        }


        // Graph elements constructor methods

        public INode NewNode(Vector3 position)
        {
            int newNodeID = _nodeID++;
            Node newNode = new Node(position, newNodeID);
            _nodes.Add(newNodeID, newNode);
            return newNode;
        }

        public ISegment NewSegment(FinalStroke s, float start, float end, INode startNode, INode endNode, bool onNewStroke = false)
        {
            int newSegmentID = _segmentID++;
            Segment newSegment = new Segment(newSegmentID, s, start, end, (Node)startNode, (Node)endNode);
            _segments.Add(newSegmentID, newSegment);

            if (surfacing && onNewStroke)
                _updatedSegmentsCache.Add(newSegment);

            return newSegment;
        }

        private bool TryAddCycle(Cycle newCycle)
        {
            // Check whether cycle is a duplicate
            if (_cycles.Contains(newCycle))
            {
                //Debug.Log("cycle would be a duplicate");
                return false;
            }

            foreach(var halfSegment in newCycle.HalfSegments)
            {
                int id = halfSegment.GetSegmentID();
                if (!_cyclesBySegment.ContainsKey(id))
                {
                    _cyclesBySegment.Add(id, new SegmentCycles());
                }
                _cyclesBySegment[id].Add(newCycle);
            }



            // Add to update cache
            if (_toRemoveCache.Contains(newCycle))
            {
                // Get the patch ID of that old instance of cycle
                List<Cycle> cycles = new List<Cycle>(_toRemoveCache);
                Cycle oldCycle = cycles.Find(c => c.Equals(newCycle));
                // Associate new cycle with the old equivalent patch
                newCycle.AssociateWithPatch(oldCycle.GetPatchID());

                _toRemoveCache.Remove(newCycle);
                //Debug.Log("replacing old patch " + newCycle.GetID());
            }
            else
            {
                _toAddCache.Add(newCycle);
            }

            // Add to hash set
            _cycles.Add(newCycle);

            return true;
        }

        // Public graph editing methods

        public void SetStart(ISegment segment, INode node, bool updateNeighbors = false)
        {
            Segment s = (Segment)segment;
            Node n = (Node)node;
            s.SetStart(n);

            if (updateNeighbors)
            {
                UpdateNeighborsCycles(s, n);
            }
        }

        public void SetEnd(ISegment segment, INode node, bool updateNeighbors = false)
        {
            Segment s = (Segment)segment;
            Node n = (Node)node;
            s.SetEnd(n);

            if (updateNeighbors)
            {
                UpdateNeighborsCycles(s, n);
            }
        }

        public void SetEnd(ISegment segment, float param, INode node, bool updateNeighbors = false)
        {
            Segment s = (Segment)segment;
            Node n = (Node)node;
            s.SetEnd(param, n);

            if (updateNeighbors)
            {
                UpdateNeighborsCycles(s, n);
            }
        }

        public void RepairCycles(ISegment oldSegment, ISegment newSegment, bool inStrokeOrder = true)
        {
            // When an existing segment is divided by a new node,
            // we fix the connectivity of its cycles
            // and add those cycles to the cache, to be checked after graph update ends

            Segment oldS = (Segment)oldSegment;
            Segment newS = (Segment)newSegment;

            if (_cyclesBySegment.ContainsKey(oldSegment.ID))
            {
                foreach (var cycle in _cyclesBySegment[oldSegment.ID].Get())
                {
                    // We're going to mess with the hashcode, so we need to remove from hashsets
                    // then add it again...
                    _cyclesToCheckCache.Remove(cycle);
                    _cycles.Remove(cycle);

                    cycle.RepairAt(oldS, newS, inStrokeOrder);

                    // Add cycle to segment
                    int id = newSegment.ID;
                    if (!_cyclesBySegment.ContainsKey(id))
                    {
                        _cyclesBySegment.Add(id, new SegmentCycles());
                    }
                    _cyclesBySegment[id].Add(cycle);

                    _cyclesToCheckCache.Add(cycle);
                    _cycles.Add(cycle);
                }
            }

        }

        public void Remove(INode node)
        {
            //_nodes.Remove((Node)node);
            _nodes.Remove(node.ID);
        }

        public void Remove(ISegment segment)
        {
            Segment s = (Segment)segment;
            // Endpoint nodes
            Node start = (Node)s.GetStartNode();
            Node end = (Node)s.GetEndNode();

            UpdateNeighbors(s, start);
            UpdateNeighbors(s, end);

            s.Delete();

            // Decide whether the endpoint nodes should be removed altogether
            // If node is not necesssary, remove it and merge dangling segments
            // Otherwise, the neighboring segments should be updated
            if (start.TryRemove())
            {
                Remove(start);
            }

            if (end.TryRemove())
            {
                Remove(end);
            }

            _segments.Remove(s.ID);

            // Cycles
            DeleteCycles(s);
            _updatedSegmentsCache.Remove(s);
        }

        public void ManualDeletePatch(int patchID)
        {
            // Find cycle from its ID
            Cycle c = null;

            foreach(var cycle in _cycles)
            {
                if (cycle.GetPatchID() == patchID)
                {
                    c = cycle;
                    break;
                }
            }

            if (c != null)
                Remove(c, userTriggered: true);
            else
                Debug.LogError("failed to manually delete cycle " + patchID);
        }

        public void MendSegments(ISegment sLeftToKeep, ISegment sRightToRemove)
        {
            Segment sKeep = (Segment)sLeftToKeep;
            Segment sRemove = (Segment)sRightToRemove;
            // Mend cycles by removing sRight from the cycles where it appears
            // (they don't need to be deleted a priori though, as neighboring segments are mended)
            if (_cyclesBySegment.ContainsKey(sRemove.ID))
            {
                foreach (var c in _cyclesBySegment[sRemove.ID].Get())
                {
                    // We're going to change the hashcode, so we need to remove from hashsets
                    // then add it again...
                    _cycles.Remove(c);

                    c.Remove(sRemove);

                    _cycles.Add(c);
                }
            }
            _cyclesBySegment.Remove(sRemove.ID);

            // Prepare to remove sRight
            SetEnd(sKeep, sRightToRemove.GetEndParam(), sRemove.GetEndNode());
            _segments.Remove(sRemove.ID);
            sRemove.Delete();
            _updatedSegmentsCache.Remove(sRemove);
        }

        // Public read methods

        // The following methods getting node information are only used for debug visualization (see DrawingCanvas OnDrawGizmos)
        public int[] GetNodeIDs()
        {
            int[] nodeIDs = new int[Count()];

            int i = 0;
            foreach (var id in _nodes.Keys)
            {
                nodeIDs[i] = id;
                i++;
            }
            return nodeIDs;
        }

        public Vector3 Get(int idx)
        {
            if (_nodes.ContainsKey(idx))
                return _nodes[idx].Position;
            else
                return Vector3.zero;
        }

        public Vector3[] GetNeighbors(int idx)
        {
            if (_nodes.ContainsKey(idx))
                return _nodes[idx].GetNeighbors();
            else
                return new Vector3[0];
        }

        public Vector3 GetNormal(int idx)
        {
            if (_nodes.ContainsKey(idx))
                return _nodes[idx].Normal;
            else
                return Vector3.zero;
        }

        public bool IsSharp(int idx)
        {
            if (_nodes.ContainsKey(idx))
                return _nodes[idx].IsSharp;
            else
                return false;
        }

        public int Count()
        {
            return _nodes.Count;
        }

        public Vector3[] GetNodes()
        {
            List<Vector3> nodes = new List<Vector3>();

            foreach (var n in _nodes)
            {
                if (n.Value.Neighbors.Count > 1)
                    nodes.Add(n.Value.Position);
            }
            return nodes.ToArray();
        }

        public List<SerializableNode> GetNodesData()
        {
            List<SerializableNode> nodesData = new List<SerializableNode>();
            foreach (var n in _nodes)
            {
                nodesData.Add(n.Value.Serialize());
            }
            return nodesData;
        }

        public bool SegmentBoundsCycle(ISegment s, int patchID)
        {
            // Check whether segment s bounds the cycle
            if (_cyclesBySegment.TryGetValue(s.ID, out SegmentCycles cycles))
            {
                return cycles.Contains(patchID);
            }
            else
                return false;
        }

        public int ExistingCyclesCount(ISegment s)
        {
            if (_cyclesBySegment.TryGetValue(s.ID, out SegmentCycles currentCycles))
            {
                return currentCycles.CyclesCount();
            }
            else
                return 0;
        }

        public ISegment FindClosestSegment(Vector3 pos, bool lookAtNonManifold)
        {
            Segment closest = null;
            float minDist = 10f;
            foreach (var s in _segments)
            {
                // Exclude segments that already have 2 cycles attached
                if (!lookAtNonManifold && _cyclesBySegment.TryGetValue(s.Value.ID, out SegmentCycles currentCycles))
                {
                    if (!currentCycles.IsManifold())
                    {
                        continue;
                    }
                }

                if (s.Value.GetStartNode().IncidentCount < 2 || s.Value.GetEndNode().IncidentCount < 2)
                    continue;

                float avgDist = (Vector3.Distance(s.Value.GetStartNode().Position, pos)
                                + Vector3.Distance(s.Value.GetEndNode().Position, pos)
                                + Vector3.Distance(s.Value.GetPointAt(0.5f), pos)) / 3f;
                if (avgDist < minDist)
                {
                    closest = s.Value;
                    minDist = avgDist;
                }
            }

            return closest;
        }

        public ISegment FindClosestAmongNeighbors(Vector3 pos, INode node, ISegment currentSegment, bool lookAtNonManifold)
        {
            Segment closest = null;
            Node n = (Node)node;
            float minDistNext = 10f;


            foreach (var s in n.Neighbors)
            {
                // Exclude self
                if (s.Equals(currentSegment))
                    continue;

                // Check if path is still manifold
                if (!lookAtNonManifold && ExistingCyclesCount(s) >= 2)
                    break;

                if (s.GetOpposite(node).IncidentCount < 2)
                    continue;

                // Take a few samples on the stroke to compute the min distance between stroke and hand pos
                int nSamples = 5;
                float dt = 1f / nSamples;
                float dist = Mathf.Infinity;
                for (int i = 1; i <= nSamples; i++)
                {
                    float dist_t = Vector3.Distance(s.GetPointAt(i * dt), pos);
                    if (dist_t < dist)
                        dist = dist_t;
                }

                if (dist < minDistNext)
                {
                    closest = s;
                    minDistNext = dist;
                }
            }

            return closest;
        }


        // Graph update cache managements
        public void Update(out List<ICycle> toAdd, out List<ICycle> toRemove)
        {
            toAdd = new List<ICycle>(_toAddCache);
            toRemove = new List<ICycle>(_toRemoveCache);

            // Clear cache
            _toAddCache.Clear();
            _toRemoveCache.Clear();
        }

        // Cycle detection triggers
        public void TryFindAllCycles()
        {
            if (!surfacing)
                return;
            // Check all cycles that should potentially be deleted
            // we delete a cycle from this list if one of the newly addded segment
            // has both endpoint nodes lying on the cycle, with the segment interleaved between 2 old segments from the cycle at the node
#if UNITY_EDITOR
            Debug.Log($"[CYCLE DETECTION] {_cyclesToCheckCache.Count} cycles in cache that may need to be deleted.");
#endif
            HashSet<Segment> deleteCyclesSearchSeeds = new HashSet<Segment>();
            foreach (var cycle in _cyclesToCheckCache)
            {
                foreach (var s in _updatedSegmentsCache)
                {
                    Node endpointA = (Node)s.GetStartNode();

                    Segment sA1 = (Segment)endpointA.GetNext(s);
                    Segment sA2 = (Segment)endpointA.GetPrevious(s);

                    Node endpointB = (Node)s.GetEndNode();

                    Segment sB1 = (Segment)endpointB.GetNext(s);
                    Segment sB2 = (Segment)endpointB.GetPrevious(s);

                    if (cycle.Contains(sA1, sA2))
                    {

                        if (TryReachCycle(cycle, s, endpointB))
                        {
                            // New segment cuts this cycle, so it should be deleted
                            //Debug.Log("cycle is removed because of segment " + s.ID);
                            TryRemove(cycle, out Segment searchSeed);
                            deleteCyclesSearchSeeds.Add(searchSeed);
                            break;
                        }
                    }
                    else if (cycle.Contains(sB1, sB2))
                    {
                        // Try to find a node on the other side that lies on the cycle
                        if (TryReachCycle(cycle, s, endpointA))
                        {
                            // New segment cuts this cycle, so it should be deleted
                            //Debug.Log("cycle is removed because of segment " + s.ID);
                            TryRemove(cycle, out Segment searchSeed);
                            deleteCyclesSearchSeeds.Add(searchSeed);
                            break;
                        }
                    }
                }
            }

            // First go through the segments from deleted cycles
#if UNITY_EDITOR
            Debug.Log($"[CYCLE DETECTION] {deleteCyclesSearchSeeds.Count} segments from broken cycles for which we launch a cycle search.");
#endif
            foreach (var segment in deleteCyclesSearchSeeds)
            {
#if UNITY_EDITOR
                Debug.Log($"[CYCLE DETECTION] Trying to find a cycle for segment {segment.ID}");
#endif
                TryFindCycle(segment);
            }

            _cyclesToCheckCache.Clear();

            // Go through all current segments in updated cache and try to detect cycles
#if UNITY_EDITOR
            Debug.Log($"[CYCLE DETECTION] {_updatedSegmentsCache.Count} new segments for which we launch a cycle search.");
#endif
            foreach (var segment in _updatedSegmentsCache)
            {
#if UNITY_EDITOR
                Debug.Log($"[CYCLE DETECTION] Trying to find a cycle for segment {segment.ID}");
#endif
                TryFindCycle(segment);
            }
            _updatedSegmentsCache.Clear();
        }

        public bool TryFindCycleAt(Vector3 inputPos, bool lookAtNonManifold)
        {
            if (!surfacing)
                return false;

            bool success = CycleDetection.DetectCycle(this, inputPos, lookAtNonManifold, out LinkedList<HalfSegment> cycleSegments);

            if (success)
            {
                Cycle cycle = new Cycle(userCreated: true, cycleSegments);
                bool addedSuccessfully = TryAddCycle(cycle);


                if (addedSuccessfully)
                {
#if UNITY_EDITOR
                    Debug.Log($"[GUIDED CYCLE DETECTION] Found: {cycle.Print()}");
#endif
                    return true;
                }
            }
            return false;

        }


        // Private methods

        private void UpdateNeighbors(Segment s, Node n)
        {
            if (!surfacing)
                return;
            // Signify that neighboring segments should become part of next cycle search
            if (n.Neighbors.Count == 2)
            {
                Segment next = (Segment)n.GetNext(s);
                if (next != null)
                    _updatedSegmentsCache.Add(next);
            }
            if (n.Neighbors.Count > 2)
            {
                Segment next = (Segment)n.GetNext(s);
                Segment previous = (Segment)n.GetPrevious(s);
                if (previous != null)
                    _updatedSegmentsCache.Add(previous);
                if (next != null)
                    _updatedSegmentsCache.Add(next);
            }
        }

        private void UpdateNeighborsCycles(Segment s, Node n)
        {
            // Signify that cycles that include both neighboring segments should be examined before next cycle search
            Segment next = (Segment)n.GetNext(s);
            Segment prev = (Segment)n.GetPrevious(s);

            if (_cyclesBySegment.ContainsKey(next.ID))
            {
                foreach (var c in _cyclesBySegment[next.ID].Get())
                {
                    if (c.Contains(next, prev))
                    {
                        _cyclesToCheckCache.Add(c);
                    }
                }
            }
        }

        private void Remove(Cycle c, bool userTriggered = false)
        {
            //Debug.Log("deleting cycle " + c.GetID());
            // Disassociate from each segment in the dictionary
            foreach (var hs in c.HalfSegments)
            {
                int id = hs.GetSegmentID();
                //Debug.Log("removing cycle from segment " + id);
                if (_cyclesBySegment.ContainsKey(id))
                    _cyclesBySegment[id].Remove(c);
            }

            // Remove from hash set
            _cycles.Remove(c);

            // Add to update cache
            if (!userTriggered)
                _toRemoveCache.Add(c);
        }

        private void DeleteCycles(Segment s)
        {
            // Delete cycles
            if (_cyclesBySegment.ContainsKey(s.ID))
            {
                List<Cycle> toDelete = new List<Cycle>(_cyclesBySegment[s.ID].Get());
                foreach (var cycle in toDelete)
                {
                    Remove(cycle);
                }
            }
            _cyclesBySegment.Remove(s.ID);
        }

        private bool TryReachCycle(Cycle c, Segment startSegment, Node startNode)
        {
            // Try to find a node down a trivial path from start node that lies on the cycle c
            int limit = 5;
            int i = 0;

            Segment s1 = (Segment)startNode.GetNext(startSegment);
            Segment s2 = (Segment)startNode.GetPrevious(startSegment);

            Node node = startNode;

            while (node.Neighbors.Count > 1 && i < limit)
            {
                if (c.Contains(s1, s2))
                    return true;

                // If we reach a non trivial node down this path, and it's not on the desired cycle, abort search
                if (node.Neighbors.Count > 2)
                    return false;

                // Look on next node
                node = (Node)s1.GetOpposite(node);
                s2 = (Segment)node.GetPrevious(s1);
                s1 = (Segment)node.GetNext(s1);
                i++;
            }

            return false;
        }

        private void TryRemove(Cycle c, out Segment searchSeed)
        {
            // This cycle was determined to need removal after a stroke addition
            // So we remove it, but store one of its segment to attempt to find it back before any new cycle detection steps
            // That makes its retrieval "prioritary", if it is possible

            HalfSegment hs = c.HalfSegments.First.Value;

            // Store a segment for further cycle search
            searchSeed = _segments[hs.GetSegmentID()];

            // Delete cycle
            Remove(c);
        }

        private void TryFindCycle(Segment startSegment)
        {

            Node startNode = (Node)startSegment.GetStartNode();
            if (startNode.IsSharp && !startSegment.GetEndNode().IsSharp)
                startNode = (Node)startSegment.GetEndNode();

#if UNITY_EDITOR
            Debug.Log($"[CYCLE DETECTION] Segment {startSegment.ID}. First search: node -> prev");
#endif
            TryFindCycle(startSegment, startNode);

#if UNITY_EDITOR
            Debug.Log($"[CYCLE DETECTION] Segment {startSegment.ID}. Second search: node -> next");
#endif
            TryFindCycle(startSegment, startNode, true);

        }

        private void TryFindCycle(Segment startSegment, Node startNode, bool reversed = false)
        {
            bool success = CycleDetection.DetectCycle(this, startSegment, startNode, out LinkedList<HalfSegment> cycleSegments, reversed);

            if (success)
            {
                Cycle cycle = new Cycle(userCreated: false, cycleSegments);
                bool addedSuccessfully = TryAddCycle(cycle);

#if UNITY_EDITOR
                if (addedSuccessfully)
                    Debug.Log($"[CYCLE DETECTION] Found: {cycle.Print()}");
#endif
            }
        }

    }
}
