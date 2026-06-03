using UnityEngine;

public class Crystal : MonoBehaviour
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
            // 新版 Unity 用 Play() 就可以了
            // 只要 Audio Generator 槽里有东西，它就会播放
            if (audioSource != null)
            {
                audioSource.Play();
            }
            
            // 让水晶消失
            GetComponent<MeshRenderer>().enabled = false;
            GetComponent<Collider>().enabled = false;
            
            // 延迟销毁
            Destroy(gameObject, 0.2f);
        }
    }
}