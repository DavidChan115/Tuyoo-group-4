using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class RespawnOnFall : MonoBehaviour
{
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

        deathCanvas.SetActive(true);
    }

    public void TryAgain()
    {
        if (deadPlayer == null) return;

        deadPlayer.transform.position = initialPosition;
        deadPlayer.transform.rotation = initialRotation;

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

        deathCanvas.SetActive(false);
        isDead = false;
    }

    void SetupDeathUI()
    {
        EnsureEventSystem();

        if (deathUIPrefab != null)
        {
            deathCanvas = Instantiate(deathUIPrefab);
            deathCanvas.name = "DeathCanvas";

            Button tryAgainButton = deathCanvas.GetComponentInChildren<Button>();
            if (tryAgainButton != null)
                tryAgainButton.onClick.AddListener(TryAgain);
        }
        else
        {
            BuildDefaultDeathUI();
        }

        deathCanvas.SetActive(false);
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

        GameObject buttonObj = new GameObject("TryAgainButton");
        buttonObj.transform.SetParent(panel.transform, false);
        Button button = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = Color.white;
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(200, 60);
        buttonRect.anchoredPosition = new Vector2(0, -40);

        GameObject buttonTextObj = new GameObject("ButtonText");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "Try Again";
        buttonText.fontSize = 28;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.black;
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;

        button.onClick.AddListener(TryAgain);
    }
}
