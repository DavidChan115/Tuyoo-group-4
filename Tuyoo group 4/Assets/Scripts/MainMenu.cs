using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void NewGame()
    {
        PlayerPrefs.DeleteKey("LastLevel");
        PlayerPrefs.Save();
        SceneTransition.Instance.TransitionToScene("IntroVideo", "");
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

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
