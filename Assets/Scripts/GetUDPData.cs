#region " Using statements "

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

#endregion

// Receive headset IMU and ArUco data via UDP
public class GetUDPData : MonoBehaviour
{
    #region " Variables definitions "

    // IMU variables
    public static Quaternion qActual;
    public static Quaternion qOrigin = Quaternion.identity;

    // ArUco variables
    public static int[] arUcoIDs;
    public static Vector3[] tvec, rvec;
    string arUcoID;

    // "Victory" and "thumb down" hand gesture variables
    public static bool victoryFound = false;
    public static bool thumbDownFound = false;

    // UDP variables
    UdpClient udpClient;
    int udpPort = 65000;
    byte[] udpData;
    IPEndPoint anyIP;

    public char commaSign = ',';
    string message;
    string quaternion;
    MatchCollection mc;
    float wf, xf, yf, zf;
    float vecX, vecY, vecZ;

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        // Initialize UDP variables
        udpClient = new UdpClient(udpPort);
        udpClient.Client.Blocking = false;
        anyIP = new IPEndPoint(IPAddress.Any, 0);
    }

    // Called every frame the app is running
    void Update()
    {
        try
        {
            // Receive data as byte array from UDP socket. Throws an exception, if no data were received.
            udpData = udpClient.Receive(ref anyIP);

            // Convert byte array using UTF8-encoding into text format
            //message = "Quaternion(w=16271, x=-905, y=523, z=1612)\nArUco IDs: [[0] [1]]\nRotation vectors: [[[ 2.34344331 - 0.59096554  0.14699297]] [[ 2.31456782 - 0.59309466  0.13278583]]]\nTranslation vectors: [[[ -8.99640018  95.38097878 371.65644744]] [[136.60372418  32.7005468  397.77077947]]]\nVictory found: True\nThumb down found: False\n";
            message = Encoding.UTF8.GetString(udpData);

            if (!string.IsNullOrEmpty(message))
            {
                // Extract quaternion
                quaternion = message.Substring(0, message.IndexOf("\n"));
                mc = Regex.Matches(quaternion, @"-?\d+");
                wf = float.Parse(mc[0].Value) / 16383f;
                xf = float.Parse(mc[1].Value) / 16383f;
                yf = float.Parse(mc[2].Value) / 16383f;
                zf = float.Parse(mc[3].Value) / 16383f;
                qActual = new Quaternion(xf, yf, zf, wf);
                if (qOrigin == Quaternion.identity)
                {
                    qOrigin = qActual;
                }

                // Check if message contains ArUco data
                if (message.IndexOf("ArUco") != -1)
                {
                    // Delete quaternion in message
                    message = message.Substring(message.IndexOf("ArUco"));

                    // Extract ArUco IDs and parse them into int array
                    arUcoID = message.Substring(0, message.IndexOf("\n"));
                    mc = Regex.Matches(arUcoID, @"\d+");
                    arUcoIDs = new int[mc.Count];
                    for (int i = 0; i < mc.Count; i++)
                    {
                        arUcoIDs[i] = int.Parse(mc[i].Value);
                    }

                    // Delete ArUco IDs in message
                    message = message.Substring(message.IndexOf("Rotation"));

                    // Replace comma "dots" with commaSign character
                    message = message.Replace('.', commaSign);

                    // Find all float numbers in the message text
                    mc = Regex.Matches(message, @"-?\d+(\" + commaSign + @"\d+)?(e[+-]?\d+)?");

                    rvec = new Vector3[arUcoIDs.Length];
                    tvec = new Vector3[arUcoIDs.Length];

                    // Parse all matches into Vector3 arrays
                    for (int i = 0; i < arUcoIDs.Length * 3; i += 3)
                    {
                        // Parse next rvec (rotation vector) in radians
                        vecX = -float.Parse(mc[i].Value);
                        vecY = -float.Parse(mc[i + 1].Value);
                        vecZ = float.Parse(mc[i + 2].Value);
                        rvec[i / 3] = new Vector3(vecX, vecY, vecZ);

                        // Parse next tvec (translation vector) in meters
                        vecX = -float.Parse(mc[i + arUcoIDs.Length * 3].Value) / 100;       // Orientation of x-axis is reversed, because the Moonlight-embedded display is mirrored along the x-axis
                        vecY = -float.Parse(mc[i + arUcoIDs.Length * 3 + 1].Value) / 100;   // Orientation of y-axis is reversed, because OpenCV has right-handed coordinates system, Unity has left-handed coordinates system)
                        vecZ = float.Parse(mc[i + arUcoIDs.Length * 3 + 2].Value) / 100;
                        tvec[i / 3] = new Vector3(vecX, vecY, vecZ);
                    }
                }
                else
                {
                    // Message doesn't contain ArUco data
                    arUcoIDs = null;
                }

                // Delete quaternion and ArUco in message
                message = message.Substring(message.IndexOf("Victory"));

                // Check if "victory" hand gesture was found
                if (message.IndexOf("Victory found: True") > -1)
                {
                    victoryFound = true;
                }
                else
                {
                    victoryFound = false;
                }

                // Check if "thumb down" hand gesture was found
                if (message.IndexOf("Thumb down found: True") > -1)
                {
                    thumbDownFound = true;
                }
                else
                {
                    thumbDownFound = false;
                }
            }
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }
    }

    // Called when the program finishes
    void OnApplicationQuit()
    {
        udpClient.Close();
    }
}

//// HERE STARTS THE FIRST ALTERNATIVE USING THREADS
//public class UDPManager : MonoBehaviour
//{    
//    public TextMeshPro imuDataDisplay;
//    public int udpPort = 65000;
//    Thread receiveThread;
//    UdpClient client;
//    string text;

//    // Start is called before the first frame update
//    void Start()
//    {
//        // Start new thread for receiving UDP data
//        receiveThread = new Thread(new ThreadStart(ReceiveData));
//        receiveThread.IsBackground = true;
//        receiveThread.Start();
//    }

//    // UDP data receive thread
//    private void ReceiveData()
//    {
//        client = new UdpClient(udpPort);
//        client.Client.Blocking = false;
//        //client.Client.ReceiveTimeout = 1000;
//        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

//        while (true)
//        {
//            try
//            {
//                // Receive data as byte array
//                byte[] data = client.Receive(ref anyIP);

//                // Convert byte array using UTF8-encoding into text format
//                string text = Encoding.UTF8.GetString(data);

//                // Print the text
//                Debug.Log("Received: " + text);
//                imuDataDisplay.text += "\n" + text;
//            }
//            catch (Exception err)
//            {
//                print(err.ToString());
//            }
//        }
//    }

//    // Called when the program finishes
//    void OnApplicationQuit()
//    {
//        // Terminate the thread
//        if (receiveThread.IsAlive)
//        {
//            receiveThread.Abort();
//        }
//        client.Close();
//    }
//}
//// END OF FIRST ALTERNATIVE USING THREADS


// ---------------------------------------


//// HERE STARTS THE SECOND ALTERNATIVE USING THREADS
//public class UDPManager : MonoBehaviour
//{
//    public TextMeshPro imuDataDisplay;
//    private UdpConnection connection;

//    // Start is called before the first frame update
//    void Start()
//    {
//        string sendIP = "127.0.0.1";
//        int sendPort = 65000;
//        int receivePort = 65000;

//        connection = new UdpConnection();
//        connection.StartConnection(sendIP, sendPort, receivePort);
//    }

//    // Called every frame the app is running
//    void Update()
//    {
//        foreach (var message in connection.getMessages()) imuDataDisplay.text += "\n" + message;

//        connection.Send("Hi!");
//    }

//    // Called when MomoBehaviour is destroyed
//    void OnDestroy()
//    {
//        connection.Stop();
//    }
//}

//public class UdpConnection
//{
//    private UdpClient udpClient;

//    private readonly Queue<string> incomingQueue = new Queue<string>();
//    Thread receiveThread;
//    private bool threadRunning = false;
//    private string senderIP;
//    private int senderPort;

//    public void StartConnection(string sendIP, int sendPort, int receivePort)
//    {
//        try { udpClient = new UdpClient(receivePort); }
//        catch (Exception e)
//        {
//            Debug.Log("Failed to listen for UDP at port " + receivePort + ": " + e.Message);
//            return;
//        }
//        Debug.Log("Created receiving client at port " + receivePort);
//        this.senderIP = sendIP;
//        this.senderPort = sendPort;

//        Debug.Log("Set sender at IP " + sendIP + " and port " + sendPort);

//        StartReceiveThread();
//    }

//    private void StartReceiveThread()
//    {
//        receiveThread = new Thread(() => ListenForMessages(udpClient));
//        receiveThread.IsBackground = true;
//        threadRunning = true;
//        receiveThread.Start();
//    }

//    private void ListenForMessages(UdpClient client)
//    {
//        IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

//        while (threadRunning)
//        {
//            try
//            {
//                Byte[] receiveBytes = client.Receive(ref remoteIpEndPoint);     // Blocks until a message returns on this socket from a remote host.
//                string returnData = Encoding.UTF8.GetString(receiveBytes);

//                lock (incomingQueue)
//                {
//                    incomingQueue.Enqueue(returnData);
//                }
//            }
//            catch (SocketException e)
//            {
//                // 10004 is thrown when socket is closed
//                if (e.ErrorCode != 10004) Debug.Log("Socket exception while receiving data from UDP client: " + e.Message);
//            }
//            catch (Exception e)
//            {
//                Debug.Log("Error receiving data from UDP client: " + e.Message);
//            }
//            Thread.Sleep(1);
//        }
//    }

//    public string[] getMessages()
//    {
//        string[] pendingMessages = new string[0];
//        lock (incomingQueue)
//        {
//            pendingMessages = new string[incomingQueue.Count];
//            int i = 0;
//            while (incomingQueue.Count != 0)
//            {
//                pendingMessages[i] = incomingQueue.Dequeue();
//                i++;
//            }
//        }

//        return pendingMessages;
//    }

//    public void Send(string message)
//    {
//        Debug.Log(String.Format("Send msg to IP:{0} Port:{1} Msg:{2}", senderIP, senderPort, message));
//        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(senderIP), senderPort);
//        Byte[] sendBytes = Encoding.UTF8.GetBytes(message);
//        udpClient.Send(sendBytes, sendBytes.Length, serverEndpoint);
//    }

//    public void Stop()
//    {
//        threadRunning = false;
//        receiveThread.Abort();
//        udpClient.Close();
//    }
//}
//// END OF SECOND ALTERNATIVE USING THREADS