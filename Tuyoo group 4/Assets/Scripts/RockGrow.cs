using UnityEngine;

public class RockGrow : MonoBehaviour
{
    [Header("Time Control")]
    [Tooltip("How many seconds the growth takes. Matches the ending cinematic duration by default.")]
    [SerializeField] private float growDuration = 2f;

    private Vector3 fullScale;
    private bool growing;
    private float growStartTime;
    private float currentDuration;

    void Start()
    {
        fullScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        if (growing) return;

        bool allCollected = CollectableManager.Instance != null
                         && CollectableManager.Instance.AllCollected();

        bool endpointReached = FinishTrigger.EndpointReached;

        if (allCollected && endpointReached)
        {
            growing = true;
            growStartTime = Time.time;

            currentDuration = growDuration;
        }
    }

    void LateUpdate()
    {
        if (!growing) return;

        float elapsed = Time.time - growStartTime;
        float t = Mathf.Clamp01(elapsed / currentDuration);

        transform.localScale = Vector3.Lerp(Vector3.zero, fullScale, t);
    }
}
