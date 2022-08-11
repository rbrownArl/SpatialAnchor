using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*public class DebugWindow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText = default;

    private ScrollRect scrollRect;

    private void Start()
    {
        // Cache references
        scrollRect = GetComponentInChildren<ScrollRect>();

        // Subscribe to log message events
        Application.logMessageReceived += HandleLog;

        // Set the starting text
        debugText.text = "Debug messages will appear here.\n\n";
        //debugText.text += System.Security.Principal.WindowsIdentity.GetCurrent().User + "\n";
        debugText.text += SystemInfo.deviceName + "\n";
        debugText.text += System.Environment.UserName + "\n";
        debugText.text += System.Environment.MachineName + "\n";
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string message, string stackTrace, LogType type)
    {
        debugText.text += message + " \n";
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0;
    }
}*/


public class DebugWindow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText = default;

    private ScrollRect scrollRect;

    //queue of actions to execute on main thread.
    private static readonly Queue<Action> dispatchQueue = new Queue<Action>();

    private void Start()
    {
        // Cache references
        scrollRect = GetComponentInChildren<ScrollRect>();

        // Subscribe to log message events
        Application.logMessageReceived += HandleLog;

        // Set the starting text
        debugText.text = "Debug messages will appear here.\n\n";
    }

    private void Update()
    {
        DispatchMessage();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }
    private void HandleLog(string message, string stackTrace, LogType type)
    {
        Color temp = debugText.color;
        if (type == LogType.Error)
        {
            debugText.color = Color.red; //debugText.GetComponent<Renderer>().material.color = Color.red;
        }
        debugText.text += message + " \n";
        debugText.color = temp;
       // debugText.GetComponent<Renderer>().material.color = Color.red;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0;
    }

    private static void DispatchMessage()
    {
        lock (dispatchQueue)
        {
            if (dispatchQueue.Count > 0)
            {
                dispatchQueue.Dequeue()();
            }
        }
    }

    //queues the specified <see cref="Action"/> on update
    protected static void QueueOnUpdate(Action updateAction)
    {
        lock (dispatchQueue)
        {
            dispatchQueue.Enqueue(updateAction);
        }
    }

    public static void DebugMessage(string message)
    {
        QueueOnUpdate(() =>
        {
            Debug.Log(string.Format("{0,5:###0.00}",Time.time) + ": " + message);

        });
    }
}