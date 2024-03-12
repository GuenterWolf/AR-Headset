#region " Using statements "

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Tinkerforge;

#endregion

// Receive IMU data from a TINKERFORGE IMU Brick V2, connected to a Raspberry Pi USB connector
public class GetIMUData : MonoBehaviour
{
    #region " Variables definitions "

    static float wf, xf, yf, zf;
    static float wfin, xfin, yfin, zfin;
    static bool onlyOnce = true;

    // IMU variables
    public static Quaternion qActual;
    public static Quaternion qOrigin;
    static readonly string HOST = "localhost";
    static readonly int PORT = 4223;
    public string UID = "6s7cwH";       // UID of IMU Brick 2.0
    static IPConnection ipcon = new();  // Create IP connection
    static BrickIMUV2 imu;              // Create IMU device object

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        try
        {
            // Connect to IMU - don't use device before ipcon is connected
            imu = new(UID, ipcon);
            Thread.Sleep(2000);
            ipcon.Connect(HOST, PORT);

            // Set period for quaternion callback to 50 ms. Try this max. 5 times, then give up
            bool achieved = false;
            int i = 0;
            while (achieved == false && i < 5)
            {
                try
                {
                    Thread.Sleep(500);
                    imu.SetQuaternionPeriod(50);
                    achieved = true;
                }
                catch (Exception err)
                {
                    Debug.LogError(err.ToString());
                    i++;
                }
            }

            // Register quaternion callback to QuaternionCB
            imu.QuaternionCallback += QuaternionCB;
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }
    }

    // Quaternion callback
    void QuaternionCB(BrickIMUV2 sender, short w, short x, short y, short z)
    {
        try
        {
            wf = w / 16383f;
            xf = x / 16383f;
            yf = y / 16383f;
            zf = z / 16383f;

            if (onlyOnce == true)
            {
                onlyOnce = false;

                // Set rotational origin to wherever the IMU is pointing at program start
                qOrigin = new Quaternion(xf, yf, zf, wf);
            }

            // Multiply with origin quaternion
            xfin = wf * qOrigin.x - xf * qOrigin.w - yf * qOrigin.z + zf * qOrigin.y;
            yfin = wf * qOrigin.y + xf * qOrigin.z - yf * qOrigin.w - zf * qOrigin.x;
            zfin = wf * qOrigin.z - xf * qOrigin.y + yf * qOrigin.x - zf * qOrigin.w;
            wfin = wf * qOrigin.w + xf * qOrigin.x + yf * qOrigin.y + zf * qOrigin.z;

            qActual = new Quaternion(xfin, yfin, zfin, wfin);
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }
    }

    // Called when the program finishes
    void OnApplicationQuit()
    {
        // Disconnect from IMU
        ipcon.Disconnect();
    }
}