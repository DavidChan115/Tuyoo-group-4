using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelect : MonoBehaviour
{
    [Tooltip("Scene names for each level button, in order.")]
    public string[] levelScenes;

    [Tooltip("Assign level buttons in the same order as scene names.")]
    public Button[] levelButtons;

    void Start()
    {
        for (int i = 0; i < levelButtons.Length && i < levelScenes.Length; i++)
        {
            string sceneName = levelScenes[i];
            levelButtons[i].onClick.AddListener(() => StartLevel(sceneName));
        }
    }

    void StartLevel(string sceneName)
    {
        PlayerPrefs.SetString("LastLevel", sceneName);
        PlayerPrefs.Save();
        SceneManager.LoadScene(sceneName);
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
