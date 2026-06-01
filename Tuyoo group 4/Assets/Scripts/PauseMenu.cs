using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    [Tooltip("Optional: assign a pre-made Canvas to override the auto-created one.")]
    public GameObject pauseCanvasPrefab;

    private GameObject pauseCanvas;
    private bool isPaused;
    private Test1 playerController;
    private Rigidbody playerRb;
    private CursorLockMode savedLockMode;
    private bool savedCursorVisible;

    void Start()
    {
        SetupPauseUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (RespawnOnFall.DeathScreenActive || FinishTrigger.EndpointReached)
                return;

            TogglePause();
        }
    }

    void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<Test1>();
            if (playerController != null) playerController.enabled = false;

            playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
                playerRb.isKinematic = true;
            }
        }

        savedLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        pauseCanvas.SetActive(true);
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (playerController != null) playerController.enabled = true;

        if (playerRb != null)
        {
            playerRb.isKinematic = false;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        Cursor.lockState = savedLockMode;
        Cursor.visible = savedCursorVisible;

        pauseCanvas.SetActive(false);
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetupPauseUI()
    {
        EnsureEventSystem();

        if (pauseCanvasPrefab != null)
        {
            if (pauseCanvasPrefab.scene.name != null)
                pauseCanvas = pauseCanvasPrefab;
            else
                pauseCanvas = Instantiate(pauseCanvasPrefab);

            pauseCanvas.name = "PauseCanvas";
            WirePauseButtons(pauseCanvas);
        }
        else
        {
            BuildDefaultPauseUI();
        }

        pauseCanvas.SetActive(false);
    }

    void WirePauseButtons(GameObject canvas)
    {
        Transform resumeBtn = canvas.transform.Find("ResumeButton");
        if (resumeBtn != null)
            resumeBtn.GetComponent<Button>()?.onClick.AddListener(ResumeGame);

        Transform restartBtn = canvas.transform.Find("RestartButton");
        if (restartBtn != null)
            restartBtn.GetComponent<Button>()?.onClick.AddListener(RestartScene);

        Transform exitBtn = canvas.transform.Find("ExitButton");
        if (exitBtn != null)
            exitBtn.GetComponent<Button>()?.onClick.AddListener(ExitGame);
    }

    void BuildDefaultPauseUI()
    {
        pauseCanvas = new GameObject("PauseCanvas");
        Canvas canvas = pauseCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        pauseCanvas.AddComponent<CanvasScaler>();
        pauseCanvas.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("PausePanel");
        panel.transform.SetParent(pauseCanvas.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.75f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        GameObject textObj = new GameObject("PauseTitle");
        textObj.transform.SetParent(panel.transform, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Paused";
        text.fontSize = 64;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400, 100);
        textRect.anchoredPosition = new Vector2(0, 80);

        BuildPauseButton(panel.transform, "ResumeButton", "Resume", new Vector2(0, 20), ResumeGame);
        BuildPauseButton(panel.transform, "RestartButton", "Restart", new Vector2(0, -50), RestartScene);
        BuildPauseButton(panel.transform, "ExitButton", "Exit Game", new Vector2(0, -120), ExitGame);
    }

    void BuildPauseButton(Transform parent, string name, string label, Vector2 position, UnityAction action)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        Button button = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = Color.white;
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(200, 50);
        buttonRect.anchoredPosition = position;

        GameObject buttonTextObj = new GameObject("ButtonText");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = label;
        buttonText.fontSize = 28;
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
