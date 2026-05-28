using UnityEngine;

public class EndPoint_Trigger : MonoBehaviour
{
    public GameObject endText;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("游戏结束！");
            
            if (endText == null)
            {
                Debug.Log("错误：endText 没有赋值！");
            }
            else
            {
                endText.SetActive(true);
                Debug.Log("文字应该显示了，当前状态：" + endText.activeSelf);
            }
        }
    }
}