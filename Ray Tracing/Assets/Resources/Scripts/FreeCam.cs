using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeCam : MonoBehaviour
{
    private Vector2 CursorStartPosition;

    // Update is called once per frame
    void Update()
    {
        //Allows the player to move the camera around the scene
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            CursorStartPosition = Input.mousePosition;

        if (Input.GetMouseButton(0))
        {
            float x = (CursorStartPosition.x - Input.mousePosition.x) / 20f;
            float y = (CursorStartPosition.y - Input.mousePosition.y) / 20f;

            Camera.main.transform.localPosition += Camera.main.gameObject.transform.right * x;
            Camera.main.transform.localPosition += Camera.main.gameObject.transform.up * y;

            CursorStartPosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(1))
        {
            float x = (Input.mousePosition.x - CursorStartPosition.x) / 20f;
            float y = (CursorStartPosition.y - Input.mousePosition.y) / 20f;

            Camera.main.transform.localEulerAngles += new Vector3(y, x, 0f);

            CursorStartPosition = Input.mousePosition;
        }

        if ((Input.GetAxis("Mouse ScrollWheel") > 0 && Camera.main.gameObject.transform.position.y > -5) || Input.GetAxis("Mouse ScrollWheel") < 0 && Camera.main.gameObject.transform.position.y < 300)
            Camera.main.gameObject.transform.localPosition += (Camera.main.gameObject.transform.forward * Input.GetAxis("Mouse ScrollWheel") * 15f);
    }
}
