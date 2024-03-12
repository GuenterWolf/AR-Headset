#region " Using statements "

using UnityEngine;

#endregion

// Move the Unity camera using IMU data
public class CameraManager : MonoBehaviour
{
    #region " Variables definitions "

    public static bool updateCamera = false;
    Quaternion qOrigin;
    Quaternion qActual;
    float wf, xf, yf, zf;

    #endregion

    // Update is called once per frame
    void Update()
    {
        if (updateCamera == true)
        {
            // Move camera using IMU data
            qOrigin = GetUDPData.qOrigin;
            qActual = GetUDPData.qActual;

            // Multiply with origin quaternion
            xf = qActual.w * qOrigin.x - qActual.x * qOrigin.w - qActual.y * qOrigin.z + qActual.z * qOrigin.y;
            yf = qActual.w * qOrigin.y + qActual.x * qOrigin.z - qActual.y * qOrigin.w - qActual.z * qOrigin.x;
            zf = qActual.w * qOrigin.z - qActual.x * qOrigin.y + qActual.y * qOrigin.x - qActual.z * qOrigin.w;
            wf = qActual.w * qOrigin.w + qActual.x * qOrigin.x + qActual.y * qOrigin.y + qActual.z * qOrigin.z;

            qActual = new Quaternion(xf, yf, zf, wf);

            // x-axis is mirrored, because the Moonlight-embedded display is mirrored along the x-axis
            qActual.x *= -1;
            qActual.w *= -1;

            // Rotate the camera relative to the world frame axes, by the roll/pitch/yaw values of the IMU
            transform.rotation = Quaternion.identity;
            transform.Rotate(Vector3.right * qActual.eulerAngles.x, Space.World);
            transform.Rotate(Vector3.forward * qActual.eulerAngles.y, Space.World);
            transform.Rotate(Vector3.up * qActual.eulerAngles.z, Space.World);

            //transform.rotation = Quaternion.Inverse(qActual);  // Perform immediate rotation
            //transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Inverse(qActual), Time.deltaTime * 0.5f);  // Perform smooth rotation
        }
    }
}
