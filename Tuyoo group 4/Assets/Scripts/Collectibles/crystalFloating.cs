using UnityEngine;

public class FloatingRotate : MonoBehaviour
{
    public float floatSpeed = 1f;      // 浮动速度
    public float floatHeight = 0.3f;   // 浮动幅度
    public float rotateSpeed = 90f;    // 旋转速度

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 上下浮动
        float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(startPos.x, startPos.y + yOffset, startPos.z);

        // 自转（绕 Y 轴旋转）
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
    }
}