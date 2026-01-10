# Quest Touch Controller Mapping Configuration

## Overview
This document defines the Quest Touch controller button mappings for the VR Sketching application based on the controller layout images provided.

## Controller Profile
- **Platform**: Meta Quest (Quest 1, Quest 2, Quest Pro)
- **OpenXR Profile**: Oculus Touch Controller Profile (enabled in OpenXR Package Settings)
- **Input System**: Unity Input System with Tilia wrappers

## Button Mappings

### Primary Hand (Right - Drawing Hand)

#### Physical Buttons:
- **Trigger (Index Finger)**: Draw strokes
- **Grip (Side Button)**: Zoom in/out (requires both hands)
- **A Button** (Lower face button):
  - Single Press: Add patch to surface
  - Double Press: Delete selected geometry
- **B Button** (Upper face button): Next step/Confirm action

#### InputController Mappings:
```csharp
// Right Hand Actions (Primary)
drawAction              → Right Trigger (analog 0.0-1.0)
zoomAction              → Right Grip (boolean, both grips pressed)
addPatchAction          → Right A Button (single press)
eraseAction             → Right A Button (double press)
studyNextAction         → Right B Button
```

### Secondary Hand (Left - Grabbing Hand)

#### Physical Buttons:
- **Trigger (Index Finger)**: Switch drawing system/mode
- **Grip (Side Button)**: Grab and move the drawing volume
- **X Button** (Lower face button):
  - Single Press: Toggle grid visibility
  - Double Press: Toggle mirror mode
- **Y Button** (Upper face button): Next step/Confirm action

#### InputController Mappings:
```csharp
// Left Hand Actions (Secondary)
switchSystemAction      → Left Trigger
grabAction              → Left Grip (boolean)
toggleGridStateAction   → Left X Button (single press)
toggleMirror            → Left X Button (double press)
studyNextAction         → Left Y Button (duplicate of right B)
```

## Unity Input Action Asset Structure

**File**: `Assets/XR/QuestTouchInputActions.inputactions`

### Action Maps:
1. **XRI LeftHand**
   - Trigger (Axis)
   - Grip (Axis)
   - Primary Button (X)
   - Secondary Button (Y)

2. **XRI RightHand**
   - Trigger (Axis)
   - Grip (Axis)
   - Primary Button (A)
   - Secondary Button (B)

## Implementation Notes

### 1. Single vs Double Press Detection
For buttons that support both single and double press (A button and X button), implement timing logic:
- **Single Press**: Detected after button release if < 300ms and no second press
- **Double Press**: Detected if second press occurs within 300ms of first press

### 2. Zoom Interaction
Zoom requires **both grip buttons** pressed simultaneously:
- Monitor both `zoomAction` states
- Enable zoom only when both hands are gripping
- Calculate zoom based on hand distance change

### 3. Tilia Integration
Each action uses Tilia's `InputActionPropertyTo*` converters:
- `InputActionPropertyToFloat` for analog triggers
- `InputActionPropertyToBoolean` for buttons and grips

### 4. Hand Assignment
The InputController auto-assigns hand transforms in `Start()`:
```csharp
primaryHandTransform   = "Player/CameraRigs.UnityXR/CameraRigs.UnityXR/Controller (primary hand)"
secondaryHandTransform = "Player/CameraRigs.UnityXR/CameraRigs.UnityXR/Controller (secondary hand)"
```

## Configuration Steps

### In Unity Editor:

1. **Import Input Actions**:
   - Select `QuestTouchInputActions.inputactions`
   - Click "Generate C# Class" (if needed)
   - Ensure it's referenced by Tilia input components

2. **Configure InputController GameObject**:
   - Open scene `VRSketching.unity`
   - Select the InputController GameObject
   - In Inspector, assign each `InputActionPropertyTo*` field:

   **Right Hand (Primary)**:
   - `drawAction` → XRI RightHand/Trigger
   - `addPatchAction` → XRI RightHand/Primary Button (A)
   - `eraseAction` → XRI RightHand/Primary Button (A) + double-press logic
   - `zoomAction` → XRI RightHand/Grip
   - `studyNextAction` → XRI RightHand/Secondary Button (B)

   **Left Hand (Secondary)**:
   - `switchSystemAction` → XRI LeftHand/Trigger
   - `grabAction` → XRI LeftHand/Grip
   - `toggleGridStateAction` → XRI LeftHand/Primary Button (X)
   - `toggleMirror` → XRI LeftHand/Primary Button (X) + double-press logic

3. **Enable OpenXR Controller Profile**:
   - Already enabled: `Oculus Touch Controller Profile` in Standalone build settings
   - For Quest builds: Enable `OculusTouchControllerProfile Android` in OpenXR Package Settings

4. **Test in VR**:
   - Build to Quest device or use Link/Air Link
   - Verify each button responds correctly
   - Test single vs double press timing
   - Validate zoom with both grips

## Quest Controller Button Reference

### Left Controller (Secondary):
```
        [Y]  ← Secondary Button (studyNextAction)
        [X]  ← Primary Button (toggleGrid/toggleMirror)
    
    Trigger  ← switchSystemAction
       Grip  ← grabAction
```

### Right Controller (Primary):
```
        [B]  ← Secondary Button (studyNextAction)
        [A]  ← Primary Button (addPatch/erase)
    
    Trigger  ← drawAction
       Grip  ← zoomAction
```

## Haptic Feedback

Haptics are sent using `InputController.SendHaptic(XRNode node, float amplitude, float duration)`:
- Uses `UnityEngine.XR.InputDevice.SendHapticImpulse()`
- `XRNode.LeftHand` or `XRNode.RightHand`
- Amplitude: 0.0-1.0
- Duration: seconds

Example:
```csharp
SendHaptic(XRNode.RightHand, 0.5f, 0.1f); // Medium pulse on right controller
```

## Troubleshooting

### Actions Not Responding:
1. Check Input Actions asset is imported and enabled
2. Verify Tilia `InputActionPropertyTo*` components are assigned in Inspector
3. Ensure OpenXR runtime is using Quest Touch profile
4. Check Unity Input System package is installed and active

### Double Press Not Working:
- Implement timing logic in InputController to detect multiple presses within threshold
- Consider using Unity Input System's MultiTap interaction

### Zoom Not Working:
- Verify both grip actions are assigned
- Ensure ZoomController checks both `zoomAction.Result` states
- Check hand distance calculation logic

## References
- OpenXR Specification: https://www.khronos.org/openxr/
- Unity XR Interaction Toolkit: https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest
- Tilia Documentation: https://academy.vrtk.io/
