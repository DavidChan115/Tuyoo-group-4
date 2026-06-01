using UnityEngine;

public class Collectable : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (CollectableManager.Instance != null)
                CollectableManager.Instance.OnCollected();

            Destroy(gameObject);
            Debug.Log("收集到了水晶！");
        }
    }
}