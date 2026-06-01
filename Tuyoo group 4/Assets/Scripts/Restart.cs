using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class RespawnOnFall : MonoBehaviour
{
    public static bool DeathScreenActive { get; private set; }

    public Transform respawnPoint;

    [Tooltip("Optional: drag a pre-made Canvas prefab here to override the default death UI.")]
    public GameObject deathUIPrefab;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isDead;
    private GameObject deathCanvas;
    private GameObject deadPlayer;

    void Start()
    {
        if (respawnPoint != null)
        {
            initialPosition = respawnPoint.position;
            initialRotation = respawnPoint.rotation;
        }

        SetupDeathUI();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isDead)
            Die(other.gameObject);
    }

    void Die(GameObject player)
    {
        isDead = true;
        deadPlayer = player;

        Test1 controller = player.GetComponent<Test1>();
        if (controller != null) controller.enabled = false;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DeathScreenActive = true;
        deathCanvas.SetActive(true);
    }


    public void RestartScene()
    {
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

    public void TryAgain()
    {
        if (deadPlayer == null) return;

        //deadPlayer.transform.position = initialPosition;
        //deadPlayer.transform.rotation = initialRotation;
        RestartScene();



        Test1 controller = deadPlayer.GetComponent<Test1>();
        if (controller != null) controller.enabled = true;

        Rigidbody rb = deadPlayer.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        DeathScreenActive = false;
        deathCanvas.SetActive(false);
        isDead = false;
    }


    void SetupDeathUI()
    {
        EnsureEventSystem();

        if (deathUIPrefab != null)
        {
            // If it's already in the scene, use it directly; otherwise instantiate the prefab
            if (deathUIPrefab.scene.name != null)
            {
                deathCanvas = deathUIPrefab;
            }
            else
            {
                deathCanvas = Instantiate(deathUIPrefab);
                deathCanvas.name = "DeathCanvas";
            }

            WireDeathButtons(deathCanvas);
        }
        else
        {
            BuildDefaultDeathUI();
        }

        deathCanvas.SetActive(false);
    }

    void WireDeathButtons(GameObject canvas)
    {
        Transform tryAgain = canvas.transform.Find("TryAgainButton");
        if (tryAgain != null)
            tryAgain.GetComponent<Button>()?.onClick.AddListener(TryAgain);

        Transform exitGame = canvas.transform.Find("ExitGameButton");
        if (exitGame != null)
            exitGame.GetComponent<Button>()?.onClick.AddListener(ExitGame);
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

    void BuildDefaultDeathUI()
    {
        deathCanvas = new GameObject("DeathCanvas");
        Canvas canvas = deathCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        deathCanvas.AddComponent<CanvasScaler>();
        deathCanvas.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("DeathPanel");
        panel.transform.SetParent(deathCanvas.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.75f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        GameObject textObj = new GameObject("DeathText");
        textObj.transform.SetParent(panel.transform, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "You Died";
        text.fontSize = 72;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.red;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400, 100);
        textRect.anchoredPosition = new Vector2(0, 60);

        BuildDeathButton(panel.transform, "TryAgainButton", "Try Again", new Vector2(0, -10), TryAgain);
        BuildDeathButton(panel.transform, "ExitGameButton", "Exit Game", new Vector2(0, -90), ExitGame);
    }

    void BuildDeathButton(Transform parent, string name, string label, Vector2 position, UnityAction action)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        Button button = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = Color.white;
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(200, 60);
        buttonRect.anchoredPosition = position;

        GameObject buttonTextObj = new GameObject("ButtonText");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = label;
        buttonText.fontSize = 28;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.black;
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;

        button.onClick.AddListener(action);
    }
}
