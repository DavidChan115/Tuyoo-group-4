using UnityEngine;

public class SimpleTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("触发成功！物体是：" + other.name);
    }
}