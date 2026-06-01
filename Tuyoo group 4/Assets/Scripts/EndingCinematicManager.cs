using UnityEngine;
using System.Collections;

public class EndingCinematicManager : MonoBehaviour
{
    public static EndingCinematicManager Instance { get; private set; }

    [Header("Target Transform")]
    [Tooltip("An empty Transform placed in the scene that defines the final camera position and rotation.")]
    public Transform targetTransform;

    [Header("Transition Settings")]
    [Tooltip("How long the camera transition takes in seconds.")]
    public float transitionDuration = 2f;

    [Tooltip("Controls the pacing of the camera movement. The X axis is normalized time (0 to 1) and the Y axis is the blend factor.")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("UI")]
    [Tooltip("Reference to the ending UI panel. It will be hidden during the camera transition and shown once the camera reaches its target.")]
    public GameObject endUIPanel;

    private Coroutine activeTransition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public Coroutine PlayEndingCinematic()
    {
        if (activeTransition != null)
            return activeTransition;

        activeTransition = StartCoroutine(TransitionToTarget());
        return activeTransition;
    }

    private IEnumerator TransitionToTarget()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = FindObjectOfType<Camera>();
            if (cam != null)
                Debug.Log("[Cinematic] Camera.main was null, fell back to FindObjectOfType: " + cam.name);
        }

        if (cam == null)
        {
            Debug.LogWarning("[Cinematic] No camera found in the scene at all.");
            activeTransition = null;
            yield break;
        }

        Debug.Log("[Cinematic] Using camera: " + cam.name);

        if (targetTransform == null)
        {
            Debug.LogWarning("[Cinematic] Target Transform is not assigned.");
            activeTransition = null;
            yield break;
        }

        if (endUIPanel != null)
        {
            endUIPanel.SetActive(false);
            Debug.Log("[Cinematic] End UI panel hidden until transition completes.");
        }

        Test1 playerController = FindObjectOfType<Test1>();
        if (playerController != null)
        {
            playerController.enabled = false;
            Debug.Log("[Cinematic] Disabled Test1 on " + playerController.name);
        }
        else
        {
            Debug.Log("[Cinematic] Test1 not found via FindObjectOfType (may already be disabled).");
        }

        cam.transform.SetParent(null);

        Vector3 startPosition = cam.transform.position;
        Quaternion startRotation = cam.transform.rotation;

        Debug.Log("[Cinematic] Camera start pos: " + startPosition + " rot: " + startRotation.eulerAngles);
        Debug.Log("[Cinematic] Camera target pos: " + targetTransform.position + " rot: " + targetTransform.rotation.eulerAngles);
        Debug.Log("[Cinematic] Duration: " + transitionDuration + "s. Beginning transition loop.");

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / transitionDuration);
            float curveValue = movementCurve.Evaluate(normalizedTime);

            cam.transform.position = Vector3.Lerp(startPosition, targetTransform.position, curveValue);
            cam.transform.rotation = Quaternion.Slerp(startRotation, targetTransform.rotation, curveValue);

            yield return null;
        }

        cam.transform.position = targetTransform.position;
        cam.transform.rotation = targetTransform.rotation;

        Debug.Log("[Cinematic] Transition complete.");

        if (endUIPanel != null)
        {
            endUIPanel.SetActive(true);
            Debug.Log("[Cinematic] End UI panel shown.");
        }

        activeTransition = null;
    }
}
