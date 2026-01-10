# Quest Touch Controller Setup Guide

## Quick Reference

### Button Layout Summary

| Hand | Button | Single Press | Double Press | Trigger | Grip |
|------|--------|-------------|--------------|---------|------|
| **Right (Primary)** | A | Add Patch | Delete Selection | Draw | Zoom* |
| **Right (Primary)** | B | Next/Confirm | - | - | - |
| **Left (Secondary)** | X | Toggle Grid | Toggle Mirror | Switch System | Grab Volume |
| **Left (Secondary)** | Y | Next/Confirm | - | - | - |

*Zoom requires both grips pressed simultaneously

---

## Step-by-Step Configuration

### 1. Enable OpenXR Quest Controller Profile

**Status**: ✅ Already enabled in your project

The `Oculus Touch Controller Profile` is already active for Standalone builds in:
- File: `Assets/XR/Settings/OpenXR Package Settings.asset`
- Profile: `OculusTouchControllerProfile Standalone` (m_enabled: 1)

**For Quest Android builds**, enable:
- Navigate to: `Edit > Project Settings > XR Plug-in Management > OpenXR`
- Select **Android** tab
- Enable: `Oculus Touch Controller Profile`

---

### 2. Import Input Actions Asset

**File Created**: `Assets/XR/QuestTouchInputActions.inputactions`

**In Unity Editor**:

1. Select the file in Project window
2. In Inspector, click **"Generate C# Class"** (if button appears)
   - This creates a C# wrapper class for easy access
3. Note the structure:
   - **XRI LeftHand** action map (X, Y, Trigger, Grip)
   - **XRI RightHand** action map (A, B, Trigger, Grip)

---

### 3. Configure Tilia Input Components

Tilia uses `InputActionPropertyTo*` converters to bridge Unity Input System with its framework.

**You need to create/configure these components** on your Tilia input objects:

#### Option A: Manual Setup (Recommended for Understanding)

1. **Locate Input Source GameObjects**:
   - Find under: `Player/CameraRigs.UnityXR/CameraRigs.UnityXR/`
   - Look for: `Controller (primary hand)` and `Controller (secondary hand)`
   - Or similar Tilia input processors

2. **Add InputActionPropertyTo* Components**:
   - Right-click in Hierarchy
   - Add: `Tilia > Input > UnityInputSystem > Transformation > Conversion > InputActionPropertyToFloat`
   - Add: `Tilia > Input > UnityInputSystem > Transformation > Conversion > InputActionPropertyToBoolean`

3. **Configure Each Component**:

   **For Right Hand (Primary)**:
   - `InputActionPropertyToFloat` → Assign: `XRI RightHand/Trigger`
   - `InputActionPropertyToBoolean` → Assign: `XRI RightHand/Primary Button` (A)
   - `InputActionPropertyToBoolean` → Assign: `XRI RightHand/Secondary Button` (B)
   - `InputActionPropertyToBoolean` → Assign: `XRI RightHand/Grip`

   **For Left Hand (Secondary)**:
   - `InputActionPropertyToFloat` → Assign: `XRI LeftHand/Trigger`
   - `InputActionPropertyToBoolean` → Assign: `XRI LeftHand/Primary Button` (X)
   - `InputActionPropertyToBoolean` → Assign: `XRI LeftHand/Secondary Button` (Y)
   - `InputActionPropertyToBoolean` → Assign: `XRI LeftHand/Grip`

#### Option B: Use Existing Tilia Prefabs

If you've installed Tilia properly, it should have prefabs with pre-configured input:

1. Check for prefab: `Tilia.CameraRigs.UnityXR > Prefabs > CameraRigs.UnityXR.UnityInputActions`
2. If exists, this may already have input actions configured
3. You can reference these components from your InputController

---

### 4. Assign Actions to InputController

**Open Scene**: `Assets/Scenes/VRSketching.unity`

**Select GameObject**: `InputController`

**In Inspector**, assign the following fields by dragging the InputActionPropertyTo* components:

#### Primary Hand Actions (Right):
- ✏️ **drawAction**: Drag `InputActionPropertyToFloat` (Right Trigger)
- ➕ **addPatchAction**: Drag `InputActionPropertyToBoolean` (Right A Button)
- 🗑️ **eraseAction**: Same as addPatchAction (double-press handled in code)
- 🔍 **zoomAction**: Drag `InputActionPropertyToBoolean` (Right Grip)
- ▶️ **studyNextAction**: Drag `InputActionPropertyToBoolean` (Right B Button)

#### Secondary Hand Actions (Left):
- 🔄 **switchSystemAction**: Drag `InputActionPropertyToFloat` (Left Trigger)
- ✊ **grabAction**: Drag `InputActionPropertyToBoolean` (Left Grip)
- 🔲 **toggleGridStateAction**: Drag `InputActionPropertyToBoolean` (Left X Button)
- 🪞 **toggleMirror**: Same as toggleGridStateAction (double-press handled in code)

---

### 5. Configure Double-Press Logic

**Already Added** in InputController.cs:

Helper methods for detecting double-press:
- `DetectSinglePress()` - Returns true on single button press
- `DetectDoublePress()` - Returns true on rapid second press

**To implement for A and X buttons**:

You'll need to modify the Update() method logic to use these helpers:

```csharp
// Example for Right A button (addPatch vs erase)
if (DetectSinglePress(addPatchAction, ref lastAddPatchPressTime, ref addPatchPressCount))
{
    // Single press: Add patch
    if (addPatchController.TryAddPatch(pos, mirroring))
    {
        SendHaptic(primaryNode, 0.1f, 0.1f);
    }
}
else if (DetectDoublePress(addPatchAction, ref lastAddPatchPressTime, ref addPatchPressCount))
{
    // Double press: Delete/Erase
    bool deleteSuccess = eraseController.TryDelete(Pos(true), out InteractionType type, out int elementID, mirror: mirroring);
    if (deleteSuccess)
    {
        SendHaptic(primaryNode, 0.1f, 0.1f);
        scenario.CurrentStep.Delete(headTransform, Pos(true), canvas.transform, type, elementID, mirroring);
    }
}

// Similar for Left X button (toggleGrid vs toggleMirror)
if (DetectSinglePress(toggleGridStateAction, ref lastToggleGridPressTime, ref toggleGridPressCount))
{
    // Single press: Toggle grid
    grid.ToggleGridState();
}
else if (DetectDoublePress(toggleGridStateAction, ref lastToggleGridPressTime, ref toggleGridPressCount))
{
    // Double press: Toggle mirror
    if (mirrorAvailable)
    {
        mirroring = !mirroring;
        if (mirroring)
            mirrorPlane.Show();
        else
            mirrorPlane.Hide();
    }
}
```

---

### 6. Verify Hand Transform Assignment

The InputController auto-assigns hand transforms in `Start()`:

```csharp
primaryHandTransform   → "Player/Controller (primary hand)"
secondaryHandTransform → "Player/Controller (secondary hand)"
```

**Check your scene hierarchy**:
- Ensure these GameObjects exist under Player
- Or manually assign transforms in Inspector
- Watch Console for "Auto-assigned" messages or errors

---

### 7. Test in VR

#### Option A: Unity Editor with Link/Air Link
1. Connect Quest via Link or Air Link
2. Press Play in Unity Editor
3. Test each button mapping

#### Option B: Build to Quest
1. Switch platform: `File > Build Settings > Android`
2. Enable: `XR Plug-in Management > Android > OpenXR`
3. Build and Run to Quest device

#### Test Checklist:
- ✅ Right Trigger: Draws strokes
- ✅ Right Grip (both): Zooms when both grips pressed
- ✅ Right A (single): Adds patch
- ✅ Right A (double): Deletes selection
- ✅ Right B: Advances study step
- ✅ Left Trigger: Switches system/mode
- ✅ Left Grip: Grabs drawing volume
- ✅ Left X (single): Toggles grid visibility
- ✅ Left X (double): Toggles mirror
- ✅ Left Y: Advances study step

---

## Troubleshooting

### Actions Not Responding

**Check 1**: Input Actions Asset
```
Assets/XR/QuestTouchInputActions.inputactions exists?
Generated C# class exists?
```

**Check 2**: InputActionPropertyTo* Components
```
Are they on the correct GameObjects?
Are actions assigned in Inspector?
Are they enabled?
```

**Check 3**: OpenXR Runtime
```
Is Quest using OpenXR (not Oculus XR)?
Is controller profile enabled?
```

**Check 4**: Console Errors
```
Look for XR initialization errors
Check for missing component warnings
```

---

### Double-Press Not Working

**Issue**: Current code doesn't use the new DetectDoublePress helpers yet

**Fix**: Update the Input handling in `Update()` method:
1. Replace direct `addPatchAction.Result` checks with `DetectSinglePress()`
2. Add `DetectDoublePress()` checks for erase
3. Same for toggleGrid (single) vs toggleMirror (double)

**Note**: The helper methods are written but not yet integrated into Update() logic.

---

### Zoom Not Activating

**Check**:
- Both grip actions assigned?
- ZoomController checking both grips?
- Try code:
  ```csharp
  if (zoomAction.Result && secondaryGripAction.Result)
  {
      // Zoom active
  }
  ```

**Current limitation**: You may need a separate action for secondary grip or check both separately.

---

### Hand Transforms Missing

**Error**: "Could not find 'Controller (primary hand)' under Player!"

**Fix**:
1. Check scene hierarchy under Player
2. Verify Tilia CameraRig is properly set up
3. Manually assign transforms in InputController Inspector
4. Ensure Tilia prefabs are instantiated correctly

---

## Files Created

1. ✅ `Assets/XR/QuestTouchInputActions.inputactions` - Unity Input Actions
2. ✅ `QUEST_CONTROLLER_MAPPING.md` - Detailed mapping documentation
3. ✅ `QUEST_CONTROLLER_SETUP.md` - This setup guide
4. ✅ `Assets/Scripts/InputController.cs` - Added double-press helpers

---

## Next Steps

1. **Assign InputActionPropertyTo* components** in scene
2. **Connect them to InputController** public fields
3. **Integrate double-press detection** in Update() method
4. **Test on Quest device** with checklist above
5. **Fine-tune timing** (`doublePressThreshold = 0.3f`)

---

## Reference Images

Your provided controller images show:

**Right Controller (Primary - Blue dot = draw)**:
- B (top): Next/Confirm
- A (bottom): Add patch (single) / Delete (double)
- Trigger: Draw
- Side Button (Grip): Zoom (both hands)

**Left Controller (Secondary - Grey dot)**:
- Y (top): Next/Confirm
- X (bottom): Toggle grid (single) / Toggle mirror (double)
- Trigger: Switch system
- Side Button (Grip): Grab and move volume

---

## Additional Resources

- **Unity XR Interaction Toolkit**: https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest
- **OpenXR Plugin**: https://docs.unity3d.com/Packages/com.unity.xr.openxr@latest
- **Tilia Documentation**: https://academy.vrtk.io/
- **Input System**: https://docs.unity3d.com/Packages/com.unity.inputsystem@latest
