using System.Collections;
using UnityEngine;
using Unity.Profiling;
using UnityEditor;
using VRSketch;
using System;
using System.IO;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class InputController : MonoBehaviour
{
    // SETTINGS
    [Header("Settings")]
    public bool HapticOnCollision = false;

    public KeyCode saveStudyLogKey = KeyCode.S;
    public KeyCode exportSketchKey = KeyCode.X;

    // HANDS
    [Header("Tilia Tracked Transforms")]
    public Transform primaryHandTransform;
    public Transform secondaryHandTransform;

    // INPUTS - OpenXR Quest Touch Controller Input Actions
    [Header("Input Actions")]
    private QuestTouchInputActions inputActions;

    public Transform headTransform;

    private const bool primarySource = true;
    private const bool secondarySource = false;

    // Sources (for compatibility)
    private bool rightHanded = true;
    private XRNode primaryNode;
    private XRNode secondaryNode;
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

    // Double-press detection
    private float doublePressThreshold = 0.3f; // 300ms window for double-press
    private float lastAddPatchPressTime = -1f;
    private float lastToggleGridPressTime = -1f;
    private int addPatchPressCount = 0;
    private int toggleGridPressCount = 0;

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
        rightHanded = StudyUtils.IsRightHandedConfig();
        if (!rightHanded)
        {
            Debug.Log("left handed");
        }
        else
        {
            Debug.Log("right handed");
        }

        primaryNode = rightHanded ? XRNode.RightHand : XRNode.LeftHand;
        secondaryNode = rightHanded ? XRNode.LeftHand : XRNode.RightHand;
        
        // Initialize OpenXR input actions
        inputActions = new QuestTouchInputActions();
        inputActions.Enable();
    }

    private void SendHaptic(XRNode node, float amplitude, float duration)
    {
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid)
        {
            device.SendHapticImpulse(0, amplitude, duration);
        }
    }

    /// <summary>
    /// Detects button press type: returns 1 for single press, 2 for double press, 0 for no action
    /// </summary>
    private int DetectPressType(bool isPressed, ref float lastPressTime, ref int pressCount)
    {
        if (isPressed)
        {
            float timeSinceLastPress = Time.time - lastPressTime;
            
            if (timeSinceLastPress < doublePressThreshold && pressCount == 1)
            {
                // Double-press detected!
                pressCount = 0; // Reset for next sequence
                lastPressTime = Time.time;
                return 2; // Double press
            }
            else if (pressCount == 0 || timeSinceLastPress >= doublePressThreshold)
            {
                // First press
                pressCount = 1;
                lastPressTime = Time.time;
                return 0; // Wait for potential second press
            }
        }
        else
        {
            // Button released
            if (pressCount == 1 && Time.time - lastPressTime > doublePressThreshold)
            {
                // Single press confirmed (no second press within threshold)
                pressCount = 0;
                return 1; // Single press
            }
            // If pressCount == 2, it was already handled as double press
            if (pressCount == 2)
            {
                pressCount = 0;
            }
        }
        return 0; // No action
    }

    /// <summary>
    /// Helper to check if study next action should proceed
    /// </summary>
    private bool IsStudyNextPressed()
    {
        // Check for secondary button presses (B/Y buttons) which handle next/confirm actions
        bool rightSecondaryPressed = inputActions.XRIRightHand.SecondaryButton.IsPressed();
        bool leftSecondaryPressed = inputActions.XRILeftHand.SecondaryButton.IsPressed();
        
        return rightSecondaryPressed || leftSecondaryPressed;
    }

    private void Start()
    {
        StudyScenario.setStudyStepEvent.AddListener(OnStepChange);

        // Set up controller cheatsheet
        controllerType = StudyUtils.GetControllerType();
        instructionsDisplay.SetControllers(controllerType, StudyUtils.IsRightHandedConfig());
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
        }
    }

    // Update is called once per frame
    void Update()
    {

        // SPECIAL MODES (ignore all input)
        if (isInBreakMode || waitingForConfirm)
            return;

        s_InputLoopMarker.Begin();

        // Current input data
        Vector3 pos = Pos(true);
        Quaternion rot = Rot(true);

        Vector3 drawingPos = pos;

        // Refresh objects appearance
        grid.Refresh(pos);
        sketchModelController.UpdateHandPos(pos);

        if (currentAction.Equals(Action.Idle))
        {
            if (Time.time - lastRecordedIdle > 2f)
            {
                lastRecordedIdle = Time.time;
                scenario.CurrentStep.Idle(headTransform, Pos(true), canvas.transform, mirroring);
            }

            // Check for new input

            // Dominant hand (Right): drawing, deleting, adding patch, zooming
            float rightTrigger = inputActions.XRIRightHand.Trigger.ReadValue<float>();
            float rightGrip = inputActions.XRIRightHand.Grip.ReadValue<float>();
            bool rightPrimaryButton = inputActions.XRIRightHand.PrimaryButton.IsPressed();
            bool rightSecondaryButton = inputActions.XRIRightHand.SecondaryButton.IsPressed();
            
            // Secondary hand (Left): grabbing, system switching, grid/mirror toggle
            float leftTrigger = inputActions.XRILeftHand.Trigger.ReadValue<float>();
            float leftGrip = inputActions.XRILeftHand.Grip.ReadValue<float>();
            bool leftPrimaryButton = inputActions.XRILeftHand.PrimaryButton.IsPressed();
            bool leftSecondaryButton = inputActions.XRILeftHand.SecondaryButton.IsPressed();

            // Right Trigger: Drawing (dominant hand)
            if (!lookingAtExample && rightTrigger > 0.1f)
            {
                currentAction = Action.Draw;
                primaryHandAppearance.OnDrawStart();
                drawController.NewStroke(drawingPos);
            }
            // Right Grip: Zoom (both hands)
            else if (rightGrip > 0.1f && leftGrip > 0.1f)
            {
                currentAction = Action.Zoom;
                primaryHandAppearance.OnZoomStart();
                secondaryHandAppearance.OnZoomStart();
                zoomInteractionAppearance.OnZoomStart(Pos(true), Pos(false));
                grid.OnTransformStart();
                float handsDistance = Vector3.Distance(Pos(true), Pos(false));
                zoomController.StartZoom(handsDistance);
            }
            // Right A Button (Primary): Add Patch (single press) or Delete (double press)
            if (!lookingAtExample)
            {
                int pressTypeA = DetectPressType(rightPrimaryButton, ref lastAddPatchPressTime, ref addPatchPressCount);
                
                if (pressTypeA == 1) // Single press
                {
                    Debug.Log("add patch action");
                    if (addPatchController.TryAddPatch(pos, mirroring))
                    {
                        SendHaptic(primaryNode, 0.1f, 0.1f);
                    }
                    else
                        primaryHandAppearance.OnNoOp();
                }
                else if (pressTypeA == 2) // Double press
                {
                    bool deleteSuccess = eraseController.TryDelete(Pos(true), out InteractionType type, out int elementID, mirror: mirroring);
                    if (deleteSuccess)
                    {
                        SendHaptic(primaryNode, 0.1f, 0.1f);
                        // Log data
                        scenario.CurrentStep.Delete(headTransform, Pos(true), canvas.transform, type, elementID, mirroring);
                    }
                }
            }
            // Right B Button (Secondary): Next step/Confirm action
            else if (!lookingAtExample && rightSecondaryButton)
            {
                // Handle next/confirm action (study mode functionality)
                if (waitingForConfirm)
                {
                    waitingForConfirm = false;
                    scenario.CurrentStep.Next();
                    UpdateInstructions();
                }
                else if (isInBreakMode)
                {
                    isInBreakMode = false;
                    scenario.CurrentStep.Next();
                    UpdateInstructions();
                }
            }

            // Left Grip: Grabbing (secondary hand)
            else if (leftGrip > 0.1f)
            {
                currentAction = Action.Grab;
                secondaryHandAppearance.OnGrabStart();
                grid.OnTransformStart();
                grabController.GrabStart(Pos(false), Rot(false));
            }

            // Left X Button (Primary): Toggle Grid (single press) or Toggle Mirror (double press)
            if (leftPrimaryButton)
            {
                int pressTypeX = DetectPressType(leftPrimaryButton, ref lastToggleGridPressTime, ref toggleGridPressCount);
                
                if (pressTypeX == 1) // Single press
                {
                    // Toggle grid state
                    //Debug.Log("toggle grid state");
                    grid.ToggleGridState();
                }
                else if (pressTypeX == 2 && mirrorAvailable) // Double press
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
            }
            // Left Y Button (Secondary): Next step/Confirm action
            else if (leftSecondaryButton)
            {
                // Handle next/confirm action (study mode functionality) - duplicate of right B
                if (waitingForConfirm)
                {
                    waitingForConfirm = false;
                    scenario.CurrentStep.Next();
                    UpdateInstructions();
                }
                else if (isInBreakMode)
                {
                    isInBreakMode = false;
                    scenario.CurrentStep.Next();
                    UpdateInstructions();
                }
            }
            // Left Trigger: Switch System (secondary hand)
            else if (leftTrigger > 0.1f && mode.Equals(VRSketch.InteractionMode.FreeCreation))
            {
                int currentSystemID = (int)this.sketchSystem;
                int newSystemID = (currentSystemID + 2) % 4; // Switch between freehand and patch
                OnSystemChange((SketchSystem)newSystemID, clearCanvas: false);
                UpdateInstructions();
                SendHaptic(secondaryNode, 0.1f, 0.1f);
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
                float rightTrigger = inputActions.XRIRightHand.Trigger.ReadValue<float>();
                if (rightTrigger > 0.1f)
                {
                    // Still drawing
                    Vector3 velocity = Vector3.zero; // removed
                    float pressure = rightTrigger;
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
            float leftGrip = inputActions.XRILeftHand.Grip.ReadValue<float>();
            if (leftGrip <= 0.1f)
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
            float rightGrip = inputActions.XRIRightHand.Grip.ReadValue<float>();
            float leftGrip = inputActions.XRILeftHand.Grip.ReadValue<float>();
            if (rightGrip <= 0.1f && leftGrip <= 0.1f)
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

        if (!lookingAtExample && IsStudyNextPressed())
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
            if (Time.time > lastContinueInputTime + 0.5f && !IsStudyNextPressed())
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
            if (Time.time > lastContinueInputTime + 0.5f && !IsStudyNextPressed())
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
            float rightTrigger = inputActions.XRIRightHand.Trigger.ReadValue<float>();
            if (Time.time > lastContinueInputTime + 0.5f && IsStudyNextPressed())
            {
                confirm = true;
                break;
            }
            if (Time.time > lastContinueInputTime + 0.5f && rightTrigger > 0.5f)
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
            if (Time.time > lastContinueInputTime + 0.5f && IsStudyNextPressed())
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
            if (Time.time > lastContinueInputTime + 0.5f && IsStudyNextPressed())
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

    private Vector3 Pos(bool isPrimary)
    {
        return isPrimary ? primaryHandTransform.position : secondaryHandTransform.position;
    }

    private Quaternion Rot(bool isPrimary)
    {
        return isPrimary ? primaryHandTransform.rotation : secondaryHandTransform.rotation;
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
