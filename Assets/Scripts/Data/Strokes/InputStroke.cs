
using System.Collections.Generic;
using System.Linq;
using Curve;
using MathNet.Numerics.LinearAlgebra.Complex;
using UnityEngine;


public class InputStroke : Stroke
{

    public List<Sample> Samples { get; protected set; }

    public List<Constraint> Constraints { get; protected set; } = new List<Constraint>();
    public List<SurfaceConstraint> SurfaceConstraints { get; private set; } = new List<SurfaceConstraint>();

    public float Length { get; private set; } = 0;

    protected override void Awake()
    {
        base.Awake();
        Samples = new List<Sample>();
    }

    public override void RenderAsLine(float scale)
    {
        RenderPoints(Samples.Select(s => s.position).ToArray(), scale);
    }

    public bool ShouldUpdate(Vector3 candidateSamplePos, float inputSamplingDistance)
    {
        return Samples.Count == 0 || GetDistanceToSample(Samples.Count - 1, candidateSamplePos) > inputSamplingDistance;
    }

    public void AddSample(Sample sample)
    {
        if (Samples.Count > 0)
            Length += Vector3.Distance(sample.position, Samples[Samples.Count - 1].position);

        Samples.Add(sample);
    }

    public void AddConstraint(Constraint c, float proximity_threshold)
    {
        // Check if this constraint is not redundant, if yes keep only one of them
        //Debug.Log("considering constraint of type " + (c as IntersectionConstraint != null ? " intersection" : c as MirrorPlaneConstraint != null ? " mirror" : " grid"));
        if (Constraints.Count > 0)
        {
            // Give priority to curve/curve intersections if one of them is
            // Give priority to intersections at node if one of them is
            // Otherwise give priority to new constraint over old one
            Constraint old = Constraints[Constraints.Count - 1];
            //Debug.Log("distance between previous and new constraint: " + Vector3.Distance(old.Position, c.Position));
            if (Vector3.Distance(old.Position, c.Position) < proximity_threshold)
            {
                //Debug.Log("[Constraints detection] 2 successive nearby constraints");
                // The only case where we ignore the new constraint is if the old one is a curve/curve and not the new one
                if (old as IntersectionConstraint != null)
                {
                    if (c as IntersectionConstraint == null)
                        return;
                    else
                    {
                        // new constraint is an intersection constraint
                        // if old one was at a node but not new one, keep old one
                        if (((IntersectionConstraint)old).IsAtNode && !((IntersectionConstraint)c).IsAtNode)
                            return;
                    }
                }

                //Debug.Log("removing previous constraint");
                Constraints.RemoveAt(Constraints.Count - 1);
            }

            // Prevent multiple mirror plane constraints
            else if (Vector3.Distance(old.Position, c.Position) < proximity_threshold * 2f && old as MirrorPlaneConstraint != null && c as MirrorPlaneConstraint != null)
            {
                //Debug.Log("removing previous mirror constraint");
                Constraints.RemoveAt(Constraints.Count - 1);
            }
        }
        //Debug.Log("adding constraint of type " + (c as IntersectionConstraint != null ? " intersection" : c as MirrorPlaneConstraint != null ? " mirror" : " grid"));
        Constraints.Add(c);
    }

    public void InConstrainToSurface(int patchID, Vector3 position)
    {
        if (SurfaceConstraints.Count > 0
            && SurfaceConstraints[SurfaceConstraints.Count - 1].PatchID == patchID
            && !SurfaceConstraints[SurfaceConstraints.Count - 1].LeftMidStroke)
        {
            //Debug.Log("prevent double constraint on patch " + patchID);
            return;
        }
        Debug.Log("[CONSTRAINT DETECTION] add constraint to surface patch " + patchID);
        SurfaceConstraint surfaceConstraint = new SurfaceConstraint(patchID, position);
        SurfaceConstraints.Add(surfaceConstraint);
    }

    public void OutConstrainToSurface(int patchID, Vector3 position)
    {
        if (SurfaceConstraints.Count > 0 && SurfaceConstraints[SurfaceConstraints.Count - 1].PatchID == patchID)
        {
            SurfaceConstraints[SurfaceConstraints.Count - 1].Leave(position);
        }
    }

    public bool IsValid(float minSketchingTime, float minStrokeSize)
    {
        if (Samples.Count < 2)
            return false;
        int N = Samples.Count;

        if (Samples[N - 1].CreationTime - Samples[0].CreationTime < minSketchingTime && Vector3.Distance(Samples[N - 1].position, Samples[0].position) < minStrokeSize)
        {
            Debug.Log("mistake click");
            return false;
        }

        // Find max distance between any sample and first sample
        float maxDist = 0f;
        for (int i = 1; i < N; i++)
        {
            float dist = Vector3.Distance(Samples[0].position, Samples[i].position);
            if (dist > maxDist)
                maxDist = dist;
        }

        if (maxDist < minStrokeSize)
        {
            Debug.Log("stroke is too small");
            return false;
        }


        return true;
    }

    public List<Vector3> GetPoints(float RDPError = 0f)
    {
        var points = Samples.Select(s => s.position).ToList<Vector3>();

        if (RDPError == 0f)
            return points;

        var pointsKeep = RamerDouglasPeucker.RDPReduce(points, RDPError, out var keepIndex);
        return pointsKeep;
    }

    public List<Vector3> GetSafePoints(float ablationDuration = 0.01f)
    {
        // Remove samples at start and end, during some small time frame
        float startTime = Samples[0].CreationTime;
        float endTime = Samples[Samples.Count - 1].CreationTime;

        // Prevent returning nothing
        if (startTime + ablationDuration * 2f >= endTime)
            return Samples.Select(s => s.position).ToList();

        var points = Samples
                        .Where(s =>
                                    s.CreationTime == startTime ||
                                    s.CreationTime == endTime ||
                                    (s.CreationTime > startTime + ablationDuration
                                    && s.CreationTime < endTime - ablationDuration)
                                    )
                        .Select(s => s.position).ToList();
        //Debug.Log("[Ablation] " + points.Count + " samples kept out of " + Samples.Count);
        return points;
    }

    public List<List<Vector3>> GetG1sections(float discontinuityAngularThreshold, float hookDiscontinuityAngularThreshold, float ablationDuration, float minSectionLength, float maxHookLength, float maxHookStrokeRatio)
    {
        float cosAngularThreshold = Mathf.Cos(discontinuityAngularThreshold);
        float cosAngularThresholdHook = Mathf.Cos(hookDiscontinuityAngularThreshold);

        List<Vector3> safeSamples = GetSafePoints(ablationDuration);
        List<List<Vector3>> sections = new List<List<Vector3>>();

        if (safeSamples.Count <= 4)
        {
            sections.Add(safeSamples);
            return sections;
        }

        //Debug.Log("initial samples count = " + safeSamples.Count);

        // First detect and remove hooks (short sections at start and end of stroke)
        // The angular tolerance is higher for hooks cutting (u.v < 0.8) than for mid-stroke segmentation (u.v < 0.7)

        // Hooks should be shorter than 3 * small and than 15% of total stroke length

        // From the start
        float currentLength = Vector3.Distance(safeSamples[0], safeSamples[1]);
        int i_rel = 2;
        int corrected_start_idx = 0;
        while (currentLength < maxHookLength && currentLength < Length * maxHookStrokeRatio)
        {
            Vector3 u = 0.5f * ((safeSamples[i_rel] - safeSamples[i_rel - 2]).normalized + (safeSamples[i_rel] - safeSamples[i_rel - 1]).normalized);
            Vector3 v = 0.5f * ((safeSamples[i_rel + 2] - safeSamples[i_rel]).normalized + (safeSamples[i_rel + 1] - safeSamples[i_rel]).normalized);

            currentLength += Vector3.Distance(safeSamples[i_rel], safeSamples[i_rel - 1]);

            if (Vector3.Dot(u, v) < cosAngularThresholdHook)
            {
                corrected_start_idx = i_rel;
            }
            i_rel++;
        }

        // From the end
        currentLength = Vector3.Distance(safeSamples[safeSamples.Count - 1], safeSamples[safeSamples.Count - 2]);
        i_rel = safeSamples.Count - 3;
        int corrected_end_idx = safeSamples.Count - 1;
        while (currentLength < maxHookLength && currentLength < Length * maxHookStrokeRatio)
        {
            Vector3 u = 0.5f * ((safeSamples[i_rel] - safeSamples[i_rel - 2]).normalized + (safeSamples[i_rel] - safeSamples[i_rel - 1]).normalized);
            Vector3 v = 0.5f * ((safeSamples[i_rel + 2] - safeSamples[i_rel]).normalized + (safeSamples[i_rel + 1] - safeSamples[i_rel]).normalized);

            currentLength += Vector3.Distance(safeSamples[i_rel], safeSamples[i_rel + 1]);

            if (Vector3.Dot(u, v) < cosAngularThresholdHook)
            {
                corrected_end_idx = i_rel;
            }
            i_rel--;
        }

        //Debug.Log("start = " + corrected_start_idx + ", end = " + corrected_end_idx);

        // If more than 4 samples would be kept by the cut, proceed to cut hooks
        if (corrected_end_idx - corrected_start_idx > 4)
        {
            // Work on subset of samples without hooks
            safeSamples = safeSamples.Skip(corrected_start_idx).Take(corrected_end_idx + 1 - corrected_start_idx).ToList();
        }

        List<Vector3> currentSection = new List<Vector3>();
        float currentSectionLength = Vector3.Distance(safeSamples[0], safeSamples[1]);

        currentSection.Add(safeSamples[0]);
        currentSection.Add(safeSamples[1]);
        for (int i = 2; i < safeSamples.Count - 2; i++)
        {
            currentSection.Add(safeSamples[i]);
            currentSectionLength += Vector3.Distance(safeSamples[i], safeSamples[i - 1]);

            // Determine if there is a corner
            Vector3 u = 0.5f * ((safeSamples[i] - safeSamples[i - 2]).normalized + (safeSamples[i] - safeSamples[i - 1]).normalized);
            Vector3 v = 0.5f * ((safeSamples[i + 2] - safeSamples[i]).normalized + (safeSamples[i + 1] - safeSamples[i]).normalized);

            // A section should be at least 4 samples and longer than the proximity threshold
            if (Vector3.Dot(u, v) < cosAngularThreshold && currentSection.Count >= 4 && currentSectionLength > minSectionLength)
            {
                //Debug.Log("[g1sections] corner detected");
                sections.Add(currentSection);
                currentSection = new List<Vector3>();
                currentSectionLength = 0f;
                currentSection.Add(safeSamples[i]);
            }
            // Otherwise the section is too short, don't do anything and continue

        }
        currentSection.Add(safeSamples[safeSamples.Count - 2]);
        currentSection.Add(safeSamples[safeSamples.Count - 1]);
        currentSectionLength += Vector3.Distance(safeSamples[safeSamples.Count - 2], safeSamples[safeSamples.Count - 1]);

        // Add last section if it's the only section found or if it's long enough
        if (sections.Count < 1 || (currentSection.Count >= 4 && currentSectionLength > minSectionLength))
            sections.Add(currentSection);

        return sections;
    }


    public float AverageDrawingSpeed()
    {
        if (Samples.Count < 2)
            return 0f;

        float time = Samples[Samples.Count - 1].CreationTime - Samples[0].CreationTime;
        return time > 10e-8f ? Length / time : 0f;
    }

    public List<float> GetWeights()
    {
        return Samples.Select(s => s.pressure).ToList<float>();
    }

    public float GetDistanceToSample(int i, Vector3 point)
    {
        if (i > Samples.Count - 1 || i < 0)
            return float.MaxValue;
        return Vector3.Distance(Samples[i].position, point);
    }

}