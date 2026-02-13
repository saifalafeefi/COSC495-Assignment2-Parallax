using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCameraX : MonoBehaviour
{
    public float mouseSensitivity = 3f;
    public float verticalSensitivity = 2f;
    public float minVerticalAngle = -30f; // how far down you can look
    public float maxVerticalAngle = 60f;  // how far up you can look
    public GameObject player;

    private float verticalAngle = 0f;

    void Update()
    {
        // rotate horizontally with mouse X
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up, mouseX * mouseSensitivity);

        // tilt vertically with mouse Y, clamped so you can't flip
        float mouseY = Input.GetAxis("Mouse Y");
        verticalAngle -= mouseY * verticalSensitivity;
        verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);

        Vector3 currentEuler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(verticalAngle, currentEuler.y, 0f);

        // follow the player
        transform.position = player.transform.position;
    }
}
