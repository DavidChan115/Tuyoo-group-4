using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TutorialChecklist : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The panel that contains the checklist. Hidden when all tasks complete.")]
    [SerializeField] private GameObject checklistPanel;
    [Tooltip("The TextMeshProUGUI that displays the task list.")]
    [SerializeField] private TextMeshProUGUI checklistText;

    [Header("Tasks")]
    [Tooltip("Define each tutorial objective here. They unlock one at a time in order.")]
    [SerializeField] private TutorialTask[] tasks;

    [Header("Settings")]
    [Tooltip("When enabled, the entire panel is disabled once all tasks are complete.")]
    [SerializeField] private bool hideOnCompletion = true;

    private int currentTaskIndex;
    private bool allTasksDone;

    [Serializable]
    public class TutorialTask
    {
        [Tooltip("The text shown to the player for this objective.")]
        public string displayText = "Objective";

        [Tooltip("All keys the player must press at least once to complete this task.")]
        public KeyCode[] requiredKeys;

        [HideInInspector] public HashSet<KeyCode> pressedKeys = new HashSet<KeyCode>();
        [HideInInspector] public bool isComplete;
    }

    void Start()
    {
        if (checklistPanel == null)
            checklistPanel = gameObject;

        if (checklistText == null)
        {
            checklistText = GetComponent<TextMeshProUGUI>();
            if (checklistText == null)
                checklistText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (checklistText == null)
        {
            Debug.LogWarning("[TutorialChecklist] No TextMeshProUGUI found. " +
                             "Drag your TMP text into the 'Checklist Text' slot.");
            return;
        }

        foreach (var task in tasks)
            task.pressedKeys = new HashSet<KeyCode>();

        currentTaskIndex = 0;

        // Force-overwrite any placeholder text in the TMP component
        // with the Inspector-defined display strings.
        UpdateDisplay();
    }

    void Update()
    {
        if (allTasksDone || currentTaskIndex >= tasks.Length) return;

        TutorialTask currentTask = tasks[currentTaskIndex];
        bool anyKeyPressed = false;

        foreach (KeyCode key in currentTask.requiredKeys)
        {
            if (!currentTask.pressedKeys.Contains(key) && Input.GetKeyDown(key))
            {
                currentTask.pressedKeys.Add(key);
                anyKeyPressed = true;
            }
        }

        if (anyKeyPressed && currentTask.pressedKeys.Count >= currentTask.requiredKeys.Length)
        {
            currentTask.isComplete = true;
            currentTaskIndex++;
            UpdateDisplay();

            if (currentTaskIndex >= tasks.Length && hideOnCompletion)
            {
                allTasksDone = true;
                checklistPanel.SetActive(false);
            }
        }
    }

    void UpdateDisplay()
    {
        if (checklistText == null || tasks.Length == 0) return;

        // Build fresh display from Inspector task data only.
        // Any placeholder text in the TMP component is ignored.
        string display = "";

        for (int i = 0; i < tasks.Length; i++)
        {
            string line = tasks[i].displayText;

            if (tasks[i].isComplete)
                line = $"<s><color=#888888>{line}</color></s>";

            display += line;
            if (i < tasks.Length - 1)
                display += "\n";
        }

        checklistText.text = display;
    }
}
