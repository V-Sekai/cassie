using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VRSketch;

public class InstructionsDisplay : MonoBehaviour
{

    public Transform cameraTransform;
    public Image LeftHandCheatSheet;
    public Image RightHandCheatSheet;

    public Sprite ViveDominant;
    public Sprite ViveNonDominant;
    public Sprite OculusDominant;
    public Sprite OculusNonDominant;
    public Sprite ValveDominant;
    public Sprite ValveNonDominant;

    private RectTransform rectTransform;
    private Text label;

    private string mainText;

    private float countdownStartTime;
    private float timeLimit;
    private bool displayCountdown;

    // Start is called before the first frame update
    void Start()
    {
        rectTransform = GetComponentInChildren<RectTransform>();
        label = GetComponentInChildren<Text>();

        label.enabled = true;
        label.text = "VR Sketching Study";
        rectTransform.anchoredPosition3D = new Vector3(0f, 1.7f, 2.5f);
        rectTransform.LookAt(cameraTransform.position);
    }

    // Update is called once per frame
    void Update()
    {
        rectTransform.LookAt(cameraTransform.position);
        if (displayCountdown)
            UpdateCountdown();
    }

    public void SetControllers(ControllerType controllerType, bool rightHanded)
    {
        switch(controllerType)
        {
            case ControllerType.Vive:
                {
                    if (rightHanded)
                    {
                        LeftHandCheatSheet.sprite = ViveNonDominant;
                        RightHandCheatSheet.sprite = ViveDominant;
                    }
                    else
                    {
                        LeftHandCheatSheet.sprite = ViveDominant;
                        RightHandCheatSheet.sprite = ViveNonDominant;
                    }
                    break;
                }
            case ControllerType.Oculus:
                {
                    if (rightHanded)
                    {
                        LeftHandCheatSheet.sprite = OculusNonDominant;
                        RightHandCheatSheet.sprite = OculusDominant;
                    }
                    else
                    {
                        LeftHandCheatSheet.sprite = OculusDominant;
                        RightHandCheatSheet.sprite = OculusNonDominant;
                    }
                    break;
                }
            case ControllerType.Knuckles:
                {
                    if (rightHanded)
                    {
                        LeftHandCheatSheet.sprite = ValveNonDominant;
                        RightHandCheatSheet.sprite = ValveDominant;
                    }
                    else
                    {
                        LeftHandCheatSheet.sprite = ValveDominant;
                        RightHandCheatSheet.sprite = ValveNonDominant;
                    }
                    break;
                }
        }
    }

    public void SetText(string text, bool modalMode = false)
    {
        mainText = text;
        label.text = text;
        label.color = Color.black;

        if (modalMode)
        {
            rectTransform.anchoredPosition3D = cameraTransform.position + cameraTransform.forward * 0.8f + new Vector3(0f, -0.2f, 0f);
            //rectTransform.
            rectTransform.LookAt(cameraTransform.position);
            label.fontSize = 6;

            // Hide cheatsheets
            LeftHandCheatSheet.enabled = false;
            RightHandCheatSheet.enabled = false;
        }
        else
        {
            rectTransform.anchoredPosition3D = new Vector3(0f, 1.7f, 2.5f);
            rectTransform.LookAt(cameraTransform.position);
            label.fontSize = 14;

            // Hide cheatsheets
            LeftHandCheatSheet.enabled = true;
            RightHandCheatSheet.enabled = true;
        }
    }

    public void SetCountdown(float timeLimit)
    {
        if (timeLimit != 0f)
        {
            countdownStartTime = Time.time;
            this.timeLimit = timeLimit;
            displayCountdown = true;
        }
        else
        {
            displayCountdown = false;
        }

    }

    public void PauseCountdown()
    {
        displayCountdown = false;

    }

    public void UnpauseCountdown(float timeToIgnore)
    {
        if (timeLimit != 0f)
        {
            countdownStartTime += timeToIgnore;
            displayCountdown = true;
        }
    }

    private void UpdateCountdown()
    {
        float timeElapsed = (Time.time - countdownStartTime);
        float timeRemaining = Mathf.Max(0f, timeLimit - timeElapsed);


        TimeSpan t = TimeSpan.FromSeconds(timeRemaining);
        string instructions = mainText;
        instructions += "\n";
        instructions += "\n";
        instructions += "Time remaining: " + string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);

        if (timeRemaining == 0f)
        {
            instructions += "\n";
            instructions += "Time for this task is elapsed, please finish your drawing now and end the task.";
            label.color = Color.red;
        }

        label.text = instructions;
    }
}
