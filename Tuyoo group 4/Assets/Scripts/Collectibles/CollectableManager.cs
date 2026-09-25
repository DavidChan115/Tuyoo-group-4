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
            if (Instance.counterText == null && counterText != null)
            {
                Destroy(Instance.gameObject);
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Instance = this;
        }

        totalCount = FindObjectsByType<Collectable>().Length;
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
