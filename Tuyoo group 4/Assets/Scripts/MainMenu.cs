using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Scene Names")]
    public string levelSelectScene = "LevelSelect";

    [Header("Settings")]
    public GameObject settingsPanel;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void NewGame()
    {
        SceneManager.LoadScene(levelSelectScene);
    }

    public void LoadGame()
    {
        string lastLevel = PlayerPrefs.GetString("LastLevel", "");
        if (!string.IsNullOrEmpty(lastLevel))
            SceneManager.LoadScene(lastLevel);
    }

    public void ToggleSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
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
