using UnityEngine;

public class Collectable : MonoBehaviour
{
    private AudioSource audioSource;
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (CollectableManager.Instance != null)
                CollectableManager.Instance.OnCollected();
            
            // 先播放音效
            if (audioSource != null)
            {
                audioSource.Play();
            }
            
            // 隐藏水晶（视觉上消失）
            GetComponent<MeshRenderer>().enabled = false;
            GetComponent<Collider>().enabled = false;
            
            // 延迟销毁，等音效播完
            float delay = audioSource != null && audioSource.clip != null ? 
                          audioSource.clip.length : 0.1f;
            Destroy(gameObject, delay);
            
            Debug.Log("收集到了水晶！");
        }
    }
}