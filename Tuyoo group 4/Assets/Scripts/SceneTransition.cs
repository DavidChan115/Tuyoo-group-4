using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("SceneTransition");
                _instance = obj.AddComponent<SceneTransition>();
            }
            return _instance;
        }
        private set { _instance = value; }
    }
    private static SceneTransition _instance;

    [Header("Timing")]
    public float fadeInDuration = 1f;
    public float holdDuration = 2f;
    public float fadeOutDuration = 1f;

    private Canvas canvas;
    private Image blackOverlay;
    private TextMeshProUGUI levelText;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    void BuildOverlay()
    {
        GameObject canvasObj = new GameObject("TransitionCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject overlayObj = new GameObject("BlackOverlay");
        overlayObj.transform.SetParent(canvasObj.transform, false);
        blackOverlay = overlayObj.AddComponent<Image>();
        blackOverlay.color = Color.clear;
        RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        GameObject textObj = new GameObject("LevelText");
        textObj.transform.SetParent(canvasObj.transform, false);
        levelText = textObj.AddComponent<TextMeshProUGUI>();
        levelText.fontSize = 48;
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.color = new Color(1, 1, 1, 0);
        if (TMP_Settings.defaultFontAsset != null)
            levelText.font = TMP_Settings.defaultFontAsset;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(1200, 100);
        textRect.anchoredPosition = Vector2.zero;

        canvasObj.SetActive(false);
    }

    public static string GetLevelDisplayName(string sceneName)
    {
        switch (sceneName)
        {
            case "SampleScene": return "Tutorial: Step on the Shadow";
            case "level2":      return "Level 1: Multi Shadows";
            case "Real level2": return "Level 2: Movable Objects";
            case "Level3":      return "Level 3: Mirror Reflection";
            case "Level4":      return "Level 4: Chain Reflection";
            default:            return sceneName;
        }
    }

    public void TransitionToScene(string sceneName, string levelDisplayName)
    {
        StartCoroutine(Transition(sceneName, levelDisplayName));
    }

    IEnumerator Transition(string sceneName, string displayName)
    {
        canvas.gameObject.SetActive(true);
        levelText.text = displayName;

        // Fade in black
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            blackOverlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
        blackOverlay.color = Color.black;

        // Fade in text
        elapsed = 0f;
        float textFadeDuration = 0.5f;
        while (elapsed < textFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / textFadeDuration);
            levelText.color = new Color(1, 1, 1, t);
            yield return null;
        }
        levelText.color = Color.white;

        // Hold
        yield return new WaitForSecondsRealtime(holdDuration);

        // Load scene while screen is still black — no flash of the old scene
        SceneManager.LoadScene(sceneName);

        // Now fade out to reveal the new scene
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            blackOverlay.color = new Color(0, 0, 0, 1f - t);
            levelText.color = new Color(1, 1, 1, 1f - t);
            yield return null;
        }

        canvas.gameObject.SetActive(false);
    }
}
