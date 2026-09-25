using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTracker : MonoBehaviour
{
    void Start()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        PlayerPrefs.SetString("LastLevel", currentScene);
        PlayerPrefs.Save();
    }
}
