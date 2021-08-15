using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VRSketch;


public class StudyScenario : MonoBehaviour
{
    public StudyStep CurrentStep { get; private set; }

    public static UnityEvent setStudyStepEvent;

    //public static SetStudyStepEvent setStudyStep;

    private StudySequenceData sequenceData;
    private int stepID;

    private void Awake()
    {
        // Load study data
        StudyUtils.TryLoadStudyData(out sequenceData);

        setStudyStepEvent = new UnityEvent();
    }

    // Start is called before the first frame update
    void Start()
    {
        stepID = 0;
        SketchSystem system = (SketchSystem)sequenceData.SystemSequence[stepID];
        SketchModel model = (SketchModel)sequenceData.ModelSequence[stepID];
        InteractionMode mode = (InteractionMode)sequenceData.InteractionModeSequence[stepID];
        bool breakTime = sequenceData.BreakTime[stepID];
        bool showExample = sequenceData.ShowExample[stepID];
        float timeLimit = sequenceData.TimeLimit[stepID];
        CurrentStep = new StudyStep(system, model, mode, breakTime, showExample, timeLimit);

        // Scene settings
        setStudyStepEvent.Invoke();
    }

    public void EndStep()
    {
        CurrentStep.Finish();
    }

    public void RedoStep()
    {
        SketchSystem system = (SketchSystem)sequenceData.SystemSequence[stepID];
        SketchModel model = (SketchModel)sequenceData.ModelSequence[stepID];
        InteractionMode mode = (InteractionMode)sequenceData.InteractionModeSequence[stepID];
        bool breakTime = sequenceData.BreakTime[stepID];
        bool showExample = sequenceData.ShowExample[stepID];
        float timeLimit = sequenceData.TimeLimit[stepID];
        CurrentStep = new StudyStep(system, model, mode, breakTime, showExample, timeLimit);

        // Scene settings
        setStudyStepEvent.Invoke();
    }

    public bool NextStep()
    {

        stepID++;
        if (stepID < sequenceData.ModelSequence.Count)
        {
            SketchSystem system = (SketchSystem)sequenceData.SystemSequence[stepID];
            SketchModel model = (SketchModel)sequenceData.ModelSequence[stepID];
            InteractionMode mode = (InteractionMode)sequenceData.InteractionModeSequence[stepID];
            bool breakTime = sequenceData.BreakTime[stepID];
            bool showExample = sequenceData.ShowExample[stepID];
            float timeLimit = sequenceData.TimeLimit[stepID];
            CurrentStep = new StudyStep(system, model, mode, breakTime, showExample, timeLimit);

            // Scene settings
            setStudyStepEvent.Invoke();
            return true;
        }
        else
            return false;
    }
    
}
