using UnityEngine;

public class RespawnOnFall : MonoBehaviour
{
    // 起始点位置（把 player 初始位置拖进来）
    public Transform respawnPoint;

    void OnTriggerEnter(Collider other)
    {
        // 检查掉进来的是不是玩家
        if (other.CompareTag("Player"))
        {
            // 把玩家移动到起始点
            other.transform.position = respawnPoint.position;
            
            // 可选：重置玩家的速度（如果有 Rigidbody）
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            Debug.Log("掉入虚空，已复活！");
        }
    }
}