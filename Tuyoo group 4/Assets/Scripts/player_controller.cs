using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test1 : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("test sucess"); // console check
    }

    // Update is called once per frame
    void Update()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horizontalInput, 0, verticalInput); // x,y,z
        transform.Translate(movement * Time.deltaTime * 5f); 
        // move the object based on input and time, Time.deltaTime makes movement frame rate independent 

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("space key was pressed");
        }
    }
}