using System.Collections;
using Valve.VR;
using UnityEngine;
using Unity.Profiling;
using UnityEditor;
using VRSketch;
using System;
using System.IO;

public class InputController : MonoBehaviour
{
    // SETTINGS
    [Header("Settings")]
    public bool HapticOnCollision = false;

    public KeyCode saveStudyLogKey = KeyCode.S;
    public KeyCode exportSketchKey = KeyCode.X;

    // HANDS
    [Header("SteamVR Controllers")]
    public SteamVR_Behaviour_Pose primaryHandObject;
    public SteamVR_Behaviour_Pose secondaryHandObject;

    // INPUTS
    [Header("SteamVR Actions")]
    public SteamVR_Action_Single drawAction;
    public SteamVR_Action_Boolean addPatchAction;
    public SteamVR_Action_Boolean eraseAction;
    public SteamVR_Action_Boolean grabAction;
    public SteamVR_Action_Boolean zoomAction;
    public SteamVR_Action_Boolean toggleGridStateAction;
    public SteamVR_Action_Boolean toggleMirror;

    public SteamVR_Action_Boolean switchSystemAction;
    


    public SteamVR_Action_Pose pose;
    public SteamVR_Input_Sources primarySource = SteamVR_Input_Sources.RightHand;
    public SteamVR_Input_Sources secondarySource = SteamVR_Input_Sources.LeftHand;
    public Transform headTransform;

    public SteamVR_Action_Vibration hapticAction;

    // STUDY INPUTS
    [Header("Study related inputs")]
    public SteamVR_Action_Boolean studyNextAction;
    //public KeyCode nextStepKey = KeyCode.Space;

    // ACTION CONTROLLERS
    [Header("App controllers")]
    public DrawController drawController;
    public EraseController eraseController;
    public GrabController grabController;
    public ZoomController zoomController;
    public AddPatchController addPatchController;
    public SelectController selectController;
    public ExportController exportController;

    // APPEARANCE CONTROLLERS
    [Header("App appearance controllers")]
    public BrushAppearance primaryHandAppearance;
    public BrushAppearance secondaryHandAppearance;
    public ZoomInteractionAppearance zoomInteractionAppearance;
    public Grid3D grid;

    // STUDY STUFF
    [Header("Study")]
    public StudyScenario scenario;
    public float IdleRecordFrequency = 2f;
    public SketchModelController sketchModelController;
    public InstructionsDisplay instructionsDisplay;
    public ControllerType controllerType = ControllerType.Vive;
    public bool ShowInstructions = true;
    public bool ShowModel = true;

    // Canvas transform
    //public Transform canvasTransform;
    public DrawingCanvas canvas;
    public MirrorPlane mirrorPlane;


    private Action currentAction = Action.Idle;

    private float lastRecordedIdle = 0f;

    // Study task settings
    private VRSketch.InteractionMode mode;
    private VRSketch.SketchSystem sketchSystem;
    private VRSketch.SketchModel sketchModel;
    private bool mirrorAvailable = false;
    private bool mirroring = true;
    private bool limitedTime;
    private float countdownTime;

    // Special moments during the study
    private bool isInBreakMode = false;
    private bool waitingForConfirm = false;
    private bool lookingAtExample = false;


    private float lastContinueInputTime;

    static ProfilerMarker s_InputLoopMarker = new ProfilerMarker("VRSketch.TreatInput");

    private enum Action
    {
        Draw,
        Grab,
        Zoom,
        Idle
    }

    private void Awake()
    {
        // Sort out handed-ness
        bool rightHanded = StudyUtils.IsRightHandedConfig();
        if (!rightHanded)
        {
            Debug.Log("left handed");
            // Change controller mappings
            primaryHandObject.inputSource = SteamVR_Input_Sources.LeftHand;
            secondaryHandObject.inputSource = SteamVR_Input_Sources.RightHand;

            // Change actions source mappings
            primarySource = SteamVR_Input_Sources.LeftHand;
            secondarySource = SteamVR_Input_Sources.RightHand;
        }
        else
        {
            Debug.Log("right handed");
            // Change controller mappings
            primaryHandObject.inputSource = SteamVR_Input_Sources.RightHand;
            secondaryHandObject.inputSource = SteamVR_Input_Sources.LeftHand;

            // Change actions source mappings
            primarySource = SteamVR_Input_Sources.RightHand;
            secondarySource = SteamVR_Input_Sources.LeftHand;
        }
    }

    private void Start()
    {
        StudyScenario.setStudyStepEvent.AddListener(OnStepChange);

        // Set up controller cheatsheet
        controllerType = StudyUtils.GetControllerType();
        instructionsDisplay.SetControllers(controllerType, StudyUtils.IsRightHandedConfig());
    }


    // Update is called once per frame
    void Update()
    {

        // SPECIAL MODES (ignore all input)
        if (isInBreakMode || waitingForConfirm)
            return;

        s_InputLoopMarker.Begin();

        // Current input data
        Vector3 pos = Pos(primarySource);
        Quaternion rot = Rot(primarySource);

        Vector3 drawingPos = pos;

        // Refresh objects appearance
        grid.Refresh(pos);
        sketchModelController.UpdateHandPos(pos);

        if (currentAction.Equals(Action.Idle))
        {
            if (Time.time - lastRecordedIdle > 2f)
            {
                lastRecordedIdle = Time.time;
                scenario.CurrentStep.Idle(headTransform, Pos(primarySource), canvas.transform, mirroring);
            }

            // Check for new input

            // Dominant hand: drawing, deleting, adding patch, zooming
            if (!lookingAtExample && drawAction.GetAxis(primarySource) > 0.1f)
            {
                currentAction = Action.Draw;
                primaryHandAppearance.OnDrawStart();
                drawController.NewStroke(drawingPos);
            }
            else if (zoomAction.GetStateDown(primarySource))
            {
                currentAction = Action.Zoom;
                primaryHandAppearance.OnZoomStart();
                secondaryHandAppearance.OnZoomStart();
                zoomInteractionAppearance.OnZoomStart(Pos(primarySource), Pos(secondarySource));
                grid.OnTransformStart();
                float handsDistance = Vector3.Distance(Pos(primarySource), Pos(secondarySource));
                zoomController.StartZoom(handsDistance);
            }
            else if (!lookingAtExample && addPatchAction.GetStateDown(primarySource))
            {
                Debug.Log("add patch action");
                if (addPatchController.TryAddPatch(pos, mirroring))
                    hapticAction.Execute(0f, 0.1f, 25, 5, primarySource);
                else
                    primaryHandAppearance.OnNoOp();
            }
            else if (!lookingAtExample && eraseAction.GetStateDown(primarySource))
            {
                bool deleteSuccess = eraseController.TryDelete(Pos(primarySource), out InteractionType type, out int elementID, mirror: mirroring);
                if (deleteSuccess)
                {
                    hapticAction.Execute(0f, 0.1f, 25, 5, primarySource);

                    // Log data
                    scenario.CurrentStep.Delete(headTransform, Pos(primarySource), canvas.transform, type, elementID, mirroring);
                }

            }

            // Secondary hand: grabbing
            else if (grabAction.GetStateDown(secondarySource))
            {
                currentAction = Action.Grab;
                secondaryHandAppearance.OnGrabStart();
                grid.OnTransformStart();
                grabController.GrabStart(Pos(secondarySource), Rot(secondarySource));
            }

            else if (toggleGridStateAction.GetStateDown(secondarySource))
            {
                // Toggle grid state
                //Debug.Log("toggle grid state");
                grid.ToggleGridState();
            }
            else if (toggleMirror.GetStateDown(secondarySource) && mirrorAvailable)
            {
                if (mirroring)
                {
                    mirroring = false;
                    mirrorPlane.Hide();
                }
                else
                {
                    mirroring = true;
                    mirrorPlane.Show();
                }
            }
            else if (switchSystemAction.GetStateDown(secondarySource) && mode.Equals(VRSketch.InteractionMode.FreeCreation))
            {
                int currentSystemID = (int)this.sketchSystem;
                int newSystemID = (currentSystemID + 2) % 4; // Switch between freehand and patch
                OnSystemChange((SketchSystem)newSystemID, clearCanvas: false);
                UpdateInstructions();
                hapticAction.Execute(0f, 0.1f, 25, 5, secondarySource);
            }

            // Keyboard
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Debug.Log("clearing scene");

                eraseController.Clear();
            }
#endif
        }

        else if (currentAction.Equals(Action.Draw))
        {
            if (drawAction.GetAxis(primarySource) > 0.1f)
            {
                // Still drawing
                Vector3 velocity = pose.GetVelocity(primarySource);
                float pressure = drawAction.GetAxis(primarySource);
                drawController.UpdateStroke(drawingPos, rot, velocity, pressure);
            }
            else
            {
                // Commit current stroke
                currentAction = Action.Idle;
                primaryHandAppearance.OnDrawEnd();
                bool success = drawController.CommitStroke(Pos(primarySource), out SerializableStroke strokeData, mirror: mirroring);

                // Log data
                if (success)
                    scenario.CurrentStep.StrokeAdd(headTransform, Pos(primarySource), canvas.transform, strokeData, mirroring);
            }
        }

        else if (currentAction.Equals(Action.Grab))
        {
            if(grabAction.GetStateUp(secondarySource))
            {
                currentAction = Action.Idle;
                secondaryHandAppearance.OnGrabEnd();
                grid.OnTransformEnd();

                // Log data
                scenario.CurrentStep.CanvasTransform(headTransform, Pos(primarySource), canvas.transform, mirroring);
            }
            else
            {
                grabController.GrabUpdate(Pos(secondarySource), Rot(secondarySource));
                grid.OnCanvasMove();
            }
        }

        else if (currentAction.Equals(Action.Zoom))
        {
            if (zoomAction.GetStateUp(primarySource))
            {
                currentAction = Action.Idle;
                zoomInteractionAppearance.OnZoomEnd();
                primaryHandAppearance.OnZoomEnd();
                secondaryHandAppearance.OnZoomEnd();
                grid.OnTransformEnd();

                // Log data
                scenario.CurrentStep.CanvasTransform(headTransform, Pos(primarySource), canvas.transform, mirroring);
            }
            else
            {
                float handsDistance = Vector3.Distance(Pos(primarySource), Pos(secondarySource));
                bool success = zoomController.UpdateZoom(Pos(primarySource), handsDistance, out float newScale);
                zoomInteractionAppearance.OnZoomUpdate(Pos(primarySource), Pos(secondarySource), success, newScale);
            }
        }

        s_InputLoopMarker.End();


#if UNITY_EDITOR
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            EditorApplication.isPlaying = false;
        }
#endif

        if (this.mode.Equals(VRSketch.InteractionMode.FreeCreation) && Input.GetKeyUp(KeyCode.Escape))
        {
            // Quit app
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        if (!lookingAtExample && studyNextAction.GetStateUp(SteamVR_Input_Sources.Any))
        {
            Debug.Log("on to the next step");
            waitingForConfirm = true;
            lastContinueInputTime = Time.time;
            StartCoroutine("WaitForConfirm");

        }


        if (Input.GetKeyUp(saveStudyLogKey))
        {
            Debug.Log("saving system state");
            SaveMidStepAndContinue();
        }

        if (Input.GetKeyUp(exportSketchKey))
        {
            Debug.Log("exporting sketch");
            exportController.ExportSketch();
        }

    }

    // Break mode
    IEnumerator BreakTime()
    {
        string nextButton = controllerType.Equals(ControllerType.Vive) ? "MENU" :
                            controllerType.Equals(ControllerType.Oculus) ? "B/Y" :
                            "B";
        instructionsDisplay.SetText(
            "Break time\n\n" +
            "Take a break for as long as you like \n (without closing the application)\n" +
            "Press " + nextButton + " when you are ready to start the next task.",
            modalMode: true
            );

        instructionsDisplay.PauseCountdown();

        // Hide everything from screen
        canvas.gameObject.SetActive(false);

        //wait for button to be pressed (wait at least 0.5s for the break)
        while (true)
        {
            if (Time.time > lastContinueInputTime + 0.5f && studyNextAction.GetStateUp(SteamVR_Input_Sources.Any))
                break;
            yield return null;
        }

        //canvas.gameObject.SetActive(true);
        //isInBreakMode = false;

        lastContinueInputTime = Time.time;
        StartCoroutine("WaitForConfirmBreakEnd");
        //scenario.NextStep();
    }

    // Confirm end break
    IEnumerator WaitForConfirmBreakEnd()
    {
        string nextButton = controllerType.Equals(ControllerType.Vive) ? "MENU" :
                    controllerType.Equals(ControllerType.Oculus) ? "B/Y" :
                    "B";
        instructionsDisplay.SetText(
        "You are about to start the next task.\n" +
        "Press " + nextButton + " to confirm.",
        modalMode: true
        );

        // Hide everything from screen
        //canvas.gameObject.SetActive(false);


        while (true)
        {
            if (Time.time > lastContinueInputTime + 0.5f && studyNextAction.GetStateUp(SteamVR_Input_Sources.Any))
            {
                break;
            }
            yield return null;
        }

        // Confirm end break
        isInBreakMode = false;
        canvas.gameObject.SetActive(true);
        scenario.NextStep();
    }

    // Confirm dialog
    IEnumerator WaitForConfirm()
    {
        string nextButton = controllerType.Equals(ControllerType.Vive) ? "MENU" :
                    controllerType.Equals(ControllerType.Oculus) ? "B/Y" :
                    "B";
        instructionsDisplay.SetText(
        "You are about to end the task.\n" +
        "Press " + nextButton + " to confirm, \n or the TRIGGER to go back to the task.",
        modalMode: true
        );

        // Hide everything from screen
        canvas.gameObject.SetActive(false);

        instructionsDisplay.PauseCountdown();

        bool confirm = false;

        while(true)
        {
            if (Time.time > lastContinueInputTime + 0.5f && studyNextAction.GetStateUp(SteamVR_Input_Sources.Any))
            {
                confirm = true;
                break;
            }
            if (Time.time > lastContinueInputTime + 0.5f && drawAction.GetAxis(SteamVR_Input_Sources.Any) > 0.5f)
            {
                confirm = false;
                break;
            }
            yield return null;
        }

        waitingForConfirm = false;
        canvas.gameObject.SetActive(true);

        if (confirm)
        {
            // Confirm action
            ConfirmEndAction();
        }

        else
        {
            // Cancel
            CancelEndAction();
            instructionsDisplay.UnpauseCountdown(Time.time - lastContinueInputTime);
        }
    }

    IEnumerator LookingAtExample()
    {
        string nextButton = controllerType.Equals(ControllerType.Vive) ? "MENU" :
                    controllerType.Equals(ControllerType.Oculus) ? "B/Y" :
                    "B";
        instructionsDisplay.SetText(
        "Here is an example sketch for the task that you're about to do.\n" +
        "Take your time to look at how the 3D object is represented using a sparse set of curves.\n" +
        "Press " + nextButton + " when you're done."
        );

        instructionsDisplay.PauseCountdown();

        while (true)
        {
            if (Time.time > lastContinueInputTime + 0.5f && studyNextAction.GetStateUp(SteamVR_Input_Sources.Any))
                break;
            yield return null;
        }

        lookingAtExample = false;
        sketchModelController.HideExample();
        instructionsDisplay.UnpauseCountdown(Time.time - lastContinueInputTime);
        UpdateInstructions();
    }

    IEnumerator FinishStudy()
    {
        string nextButton = controllerType.Equals(ControllerType.Vive) ? "MENU" :
                    controllerType.Equals(ControllerType.Oculus) ? "B/Y" :
                    "B";
        // Display end text
        instructionsDisplay.SetText(
        "You have completed all tasks.\n" +
        "Thanks for participating in the study!\n" +
        "Press " + nextButton + " to exit the application."
        );

        // Hide everything from screen
        canvas.gameObject.SetActive(false);

        while (true)
        {
            if (Time.time > lastContinueInputTime + 0.5f && studyNextAction.GetStateUp(SteamVR_Input_Sources.Any))
                break;
            yield return null;
        }

        // Quit app
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    // Event listeners

    public void OnPatchAdd(SerializablePatch patch)
    {
        scenario.CurrentStep.SurfaceAdd(headTransform, Pos(primarySource), canvas.transform, patch, mirroring);
    }

    public void OnStepChange()
    {
        // Reset canvas transform
        canvas.transform.position = Vector3.zero;
        canvas.transform.rotation = Quaternion.identity;
        zoomController.ResetScale();

        instructionsDisplay.SetCountdown(scenario.CurrentStep.TimeLimit);

        OnSystemChange(scenario.CurrentStep.System);
        OnModeChange(scenario.CurrentStep.Mode);
        OnModelChange(scenario.CurrentStep.Model);

        UpdateInstructions();

    }

    private void OnSystemChange(SketchSystem newSystem, bool clearCanvas = true)
    {

        this.sketchSystem = newSystem;

        primaryHandAppearance.OnModeChange(newSystem);

        // Set parameters according to system
        bool surfacing = false;
        switch (newSystem)
        {
            case SketchSystem.Baseline:
            {
                // Deactivate both beautification and surfacing
                drawController.Beautification = false;
                break;
            }
            case SketchSystem.Snap:
            {
                // Activate beautification
                drawController.Beautification = true;
                break;
            }
            case SketchSystem.SnapSurface:
            {
                // Activate both
                drawController.Beautification = true;
                surfacing = true;
                break;
            }
            default:
            {
                drawController.Beautification = false;
                break;
            }
        }

        if (clearCanvas)
            drawController.Init(surfacing);
        else
            drawController.SwitchSystem(surfacing);

        //UpdateInstructions();
    }

    private void OnModeChange(VRSketch.InteractionMode mode)
    {

        this.mode = mode;
    }

    private void OnModelChange(SketchModel model)
    {

        // Compute origin position
        Vector3 lookAt = transform.InverseTransformPoint(new Vector3(0f, 1.2f, 0f) +  0.8f * Vector3.forward);
        Vector3 origin = new Vector3(
            lookAt.x - (lookAt.x % 0.25f),
            lookAt.y - (lookAt.y % 0.25f),
            lookAt.z - (lookAt.z % 0.25f)
            );

        this.sketchModel = model;

        // Load model and display (hide old model)
        if (ShowModel && !this.mode.Equals(VRSketch.InteractionMode.FreeCreation))
            sketchModelController.SetModel(model, origin);

        // If a mirror plane is defined, activate it
        if (StudyUtils.MirrorModelMapping.TryGetValue(model, out VRSketch.Plane plane))
        {
            mirroring = true;
            mirrorAvailable = true;
            mirrorPlane.SetPlane(plane.n, plane.p0 + origin);
        }
        else
        {
            mirrorAvailable = false;
            mirrorPlane.Hide();
            mirrorPlane.Clear();
            mirroring = false;
        }

        // Reset canvas transform
        //canvas.transform.position = Vector3.zero;
        //canvas.transform.rotation = Quaternion.identity;
        //zoomController.ResetScale();

        if (scenario.CurrentStep.ShowExampleBefore)
        {
            // Put the scene in "looking at example mode" and show example
            bool exampleExists = sketchModelController.ShowExample(model, origin);
            if (exampleExists)
            {
                lookingAtExample = true;
                lastContinueInputTime = Time.time;
                StartCoroutine("LookingAtExample");
            }

        }

    }

    private void UpdateInstructions()
    {
        if (!ShowInstructions)
        {
            instructionsDisplay.SetText("");
            instructionsDisplay.LeftHandCheatSheet.enabled = false;
            instructionsDisplay.RightHandCheatSheet.enabled = false;
            return;
        }


        string instructions = "";
        instructions += StudyUtils.SystemInstructions[this.sketchSystem];
        instructions += "\n";
        instructions += "\n";
        instructions += StudyUtils.InteractionModeInstructions[this.mode];

        if (!this.mode.Equals(VRSketch.InteractionMode.FreeCreation))
        {
            instructions += "\n";
            instructions += "\n";
            instructions += "Model: " + StudyUtils.ModelNames[this.sketchModel];
        }

        instructionsDisplay.SetText(instructions);
    }

    //public void OnPatchDelete(int patchID)
    //{
    //    scenario.CurrentStep.Delete(headTransform, Pos(primarySource), canvasTransform, InteractionType.SurfaceDelete, patchID);
    //}

    private Vector3 Pos(SteamVR_Input_Sources source)
    {
        return pose.GetLocalPosition(source);
    }

    private Quaternion Rot(SteamVR_Input_Sources source)
    {
        return pose.GetLocalRotation(source);
    }

    private void ConfirmEndAction()
    {
        EndStep();
    }

    private void CancelEndAction()
    {
        UpdateInstructions();
    }

    private void EndStep()
    {
        if (this.mode.Equals(VRSketch.InteractionMode.FreeCreation))
        {
            // Export sketch and clear canvas
            //exportController.ExportToOBJ(scenario.CurrentStep.ToString());
            exportController.ExportSketch(scenario.CurrentStep.ToString()); // export formats are set in ExportController properties
            eraseController.Clear();
            scenario.EndStep();
            scenario.RedoStep();
        }
        else
        {
            // Eventual break time
            if (scenario.CurrentStep.BreakAfterStep)
            {
                scenario.EndStep();
                exportController.ExportToOBJ(scenario.CurrentStep.ToString());

                // Clear canvas
                eraseController.Clear();

                isInBreakMode = true;
                lastContinueInputTime = Time.time;
                StartCoroutine("BreakTime");
            }
            else
            {
                // End step and export result to OBJs
                scenario.EndStep();
                exportController.ExportToOBJ(scenario.CurrentStep.ToString());

                // Clear canvas
                eraseController.Clear();

                bool nextStepExists = scenario.NextStep();
                if (!nextStepExists)
                {
                    lastContinueInputTime = Time.time;
                    isInBreakMode = true;
                    StartCoroutine("FinishStudy");
                }
            }
        }

    }


    private void SaveMidStepAndContinue()
    {
        // Export sketch and data
        //exportController.ExportToOBJ(scenario.CurrentStep.ToString());
        exportController.ExportSketch(scenario.CurrentStep.ToString()); // export formats are set in ExportController properties
        scenario.CurrentStep.SaveMidStepAndContinue();
    }

}
