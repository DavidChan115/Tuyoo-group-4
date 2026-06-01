using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
    public static bool EndpointReached { get; private set; }

    public GameObject uiPanel;   // 这一行会创建一个叫 "UI Panel" 的槽

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            EndpointReached = true;
            uiPanel.SetActive(true);
            Debug.Log("游戏结束！");
        }
    }
}