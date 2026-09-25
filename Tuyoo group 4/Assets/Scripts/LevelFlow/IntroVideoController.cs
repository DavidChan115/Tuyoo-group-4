using UnityEngine;
using UnityEngine.Video;

public class IntroVideoController : MonoBehaviour
{
    [Header("Video")]
    [Tooltip("Assign the VideoPlayer here (or leave empty to auto-find).")]
    public VideoPlayer videoPlayer;

    [Header("Next Scene")]
    public string nextSceneName = "SampleScene";

    [Header("Timeout")]
    [Tooltip("Max seconds to wait for video preparation before giving up.")]
    public float prepareTimeout = 5f;

    private bool started;
    private float prepareStartTime;

    void Start()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = GetComponentInChildren<VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = GetComponentInParent<VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = FindObjectOfType<VideoPlayer>();

        if (videoPlayer == null)
        {
            Debug.LogError("[IntroVideo] No VideoPlayer found. Skipping to next scene.");
            GoToNextScene();
            return;
        }

        Debug.Log("[IntroVideo] Found VideoPlayer. Preparing video...");
        videoPlayer.skipOnDrop = true;
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.errorReceived += OnError;
        videoPlayer.Prepare();
        prepareStartTime = Time.time;
    }

    void OnError(VideoPlayer vp, string message)
    {
        Debug.LogError("[IntroVideo] VideoPlayer error: " + message + ". Skipping to next scene.");
        GoToNextScene();
    }

    void OnPrepared(VideoPlayer vp)
    {
        Debug.Log("[IntroVideo] Video prepared. Playing.");
        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.loopPointReached += OnVideoEnd;
        videoPlayer.Play();
        started = true;
    }

    void Update()
    {
        if (!started)
        {
            // Timeout: if preparation takes too long, skip the video.
            if (Time.time - prepareStartTime > prepareTimeout)
            {
                Debug.LogWarning("[IntroVideo] Preparation timed out. Skipping video.");
                GoToNextScene();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
            GoToNextScene();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        GoToNextScene();
    }

    void GoToNextScene()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
            videoPlayer.prepareCompleted -= OnPrepared;
        }

        SceneTransition.Instance.TransitionToScene(nextSceneName,
            SceneTransition.GetLevelDisplayName(nextSceneName));
    }
}
