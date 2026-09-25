using UnityEngine;

public class Goal : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // 检查碰到的是不是玩家
        if (other.CompareTag("Player"))
        {
            // 切换音乐
            if (MusicManager.instance != null)
            {
                MusicManager.instance.SwitchToEndingMusic();
            }
        }
    }
}