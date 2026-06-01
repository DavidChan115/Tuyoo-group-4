using UnityEngine;
using TMPro;

public class CollectableManager : MonoBehaviour
{
    public static CollectableManager Instance { get; private set; }

    [Tooltip("Drag a TextMeshProUGUI here, or leave empty to auto-create one.")]
    public TextMeshProUGUI counterText;

    private int totalCount;
    private int collectedCount;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        totalCount = FindObjectsByType<Collectable>(FindObjectsSortMode.None).Length;
        UpdateDisplay();
    }

    public bool AllCollected()
    {
        return collectedCount >= totalCount && totalCount > 0;
    }

    public void OnCollected()
    {
        collectedCount++;
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (counterText != null)
            counterText.text = "Stars Counter: " + collectedCount + " / " + totalCount;
    }
}
