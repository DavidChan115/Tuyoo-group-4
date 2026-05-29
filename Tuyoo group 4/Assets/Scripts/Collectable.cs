using UnityEngine;

public class Collectable : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Destroy(gameObject);  // 水晶消失
            Debug.Log("收集到了水晶！");
        }
    }
}