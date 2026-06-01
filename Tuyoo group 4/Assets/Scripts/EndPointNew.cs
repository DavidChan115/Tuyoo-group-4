using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class FinishTrigger : MonoBehaviour
{
    public static bool EndpointReached { get; private set; }

    [Header("Scene")]
    [Tooltip("Drag the next scene asset here.")]
    public Object nextScene;

    [Header("UI")]
    [Tooltip("Optional: assign a pre-made Canvas to override the auto-created one.")]
    [SerializeField] public GameObject uiPanel;

    private GameObject finishCanvas;
    private bool triggered;

    void Start()
    {
        EndpointReached = false;

        if (uiPanel != null)
        {
            finishCanvas = uiPanel;
            WireButtons();
        }
        else
        {
            BuildFinishUI();
        }

        finishCanvas.SetActive(false);
    }

    void WireButtons()
    {
        Button retryBtn = finishCanvas.transform.Find("RetryButton")?.GetComponent<Button>();
        if (retryBtn != null) retryBtn.onClick.AddListener(Retry);

        Button nextBtn = finishCanvas.transform.Find("NextLevelButton")?.GetComponent<Button>();
        if (nextBtn != null) nextBtn.onClick.AddListener(NextLevel);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !triggered)
        {
            triggered = true;
            EndpointReached = true;

            PlayerPrefs.DeleteKey("LastLevel");
            PlayerPrefs.Save();

            Test1 controller = other.GetComponent<Test1>();
            if (controller != null) controller.enabled = false;

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // UI activation is now handled by EndingCinematicManager after the camera transition completes.
            // finishCanvas.SetActive(true);

            if (EndingCinematicManager.Instance != null)
            {
                EndingCinematicManager.Instance.endUIPanel = finishCanvas;
                EndingCinematicManager.Instance.PlayEndingCinematic();
            }
        }
    }

    public void Retry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        if (nextScene != null)
            SceneManager.LoadScene(nextScene.name);
    }

    void BuildFinishUI()
    {
        EnsureEventSystem();

        finishCanvas = new GameObject("FinishCanvas");
        Canvas canvas = finishCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        finishCanvas.AddComponent<CanvasScaler>();
        finishCanvas.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("FinishPanel");
        panel.transform.SetParent(finishCanvas.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.75f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Title text
        GameObject textObj = new GameObject("FinishText");
        textObj.transform.SetParent(panel.transform, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Level Completed";
        text.fontSize = 64;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.green;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(600, 100);
        textRect.anchoredPosition = new Vector2(0, 80);

        // Retry button
        BuildButton(panel.transform, "RetryButton", "Retry", new Vector2(-110, -40), Retry);

        // Next Level button
        BuildButton(panel.transform, "NextLevelButton", "Next Level", new Vector2(110, -40), NextLevel);
    }

    void BuildButton(Transform parent, string name, string label, Vector2 position, UnityAction action)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        Button button = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = Color.white;
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(180, 50);
        buttonRect.anchoredPosition = position;

        GameObject buttonTextObj = new GameObject("ButtonText");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = label;
        buttonText.fontSize = 24;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.black;
        if (TMP_Settings.defaultFontAsset != null)
            buttonText.font = TMP_Settings.defaultFontAsset;
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;

        button.onClick.AddListener(action);
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
