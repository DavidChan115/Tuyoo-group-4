using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [Header("Credits")]
    public GameObject creditsPanel;

    public void NewGame()
    {
        PlayerPrefs.DeleteKey("LastLevel");
        PlayerPrefs.Save();
        SceneTransition.Instance.TransitionToScene("SampleScene", SceneTransition.GetLevelDisplayName("SampleScene"));
    }

    public void LoadGame()
    {
        string lastLevel = PlayerPrefs.GetString("LastLevel", "");
        string targetScene;
        string displayName;

        if (!string.IsNullOrEmpty(lastLevel))
        {
            targetScene = lastLevel;
            displayName = SceneTransition.GetLevelDisplayName(lastLevel);
        }
        else
        {
            targetScene = "SampleScene";
            displayName = SceneTransition.GetLevelDisplayName("SampleScene");
        }

        SceneTransition.Instance.TransitionToScene(targetScene, displayName);
    }

    public void ToggleCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(!creditsPanel.activeSelf);
    }

    void Update()
    {
        if (creditsPanel != null && creditsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            creditsPanel.SetActive(false);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
