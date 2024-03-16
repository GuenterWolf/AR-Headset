#region " Using statements "

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Unity.XR.MockHMD;

#endregion

// Displays 4 scenes based on ArUco markers
[RequireComponent(typeof(AudioSource))]
public class GameObjectManager : MonoBehaviour
{
    #region " Variables definitions "

    public GameObject ghost;
    public GameObject johnLemon;
    public GameObject theStreet;
    public GameObject rollaBall;
    public GameObject stereoCamera;
    public Text dataDisplay;
    public GameObject angryBirds;
    public GameObject angryBirdsCatapult;
    public GameObject angryBirdsLevel1;
    public GameObject angryBirdsLevel2;
    public GameObject angryBirdsLevel3;
    public GameObject red;
    public GameObject chuck;
    public GameObject matilda;
    public GameObject hal;
    public GameObject jay;
    public GameObject bomb;
    public GameObject bubbles;
    public GameObject terence;
    public GameObject handGestureVictory;
    public GameObject handGestureThumbDown;
    public AudioClip angryBirdsTheme1;
    public AudioClip angryBirdsTheme2;
    public AudioClip angryBirdsTheme3;
    public AudioClip angryBirdsVictory;
    public AudioClip angryBirdsPigVictory;
    public bool doScreenRecord = false;

    private AndroidJavaClass unityClass;        // Unity Android class
    private AndroidJavaObject unityActivity;    // Unity Android activity that references to the .AAR-plugin in Assets/Plugins/Android
    JsonData jsonData;                          // Class which holds the ArUco and MediaPipe variables that should be received in JSON format
    string json;                                // JSON data
    int[] arUcoIDs;                             // Detected ArUco marker IDs
    Vector3[] tvec, rvec;                       // Detected translation and rotation vectors
    Vector3 cameraOriginPosition;
    Quaternion cameraOriginRotation;
    float theta;
    Vector3 axis;
    Quaternion target;
    readonly float speed = 15f;
    readonly float rotateSpeed = 4f;
    Vector3 objectRotation = new(0, 7, 0);
    int FPS;
    float fpsTimer = 0;
    int currentFPS;
    int delayFrames = 30;
    float screenRecordTimer = 20;

    // UDP variables
    UdpClient udpClient;
    readonly int udpPort = 49200;
    IPEndPoint anyIP;
    byte[] udpData = new byte[1];

    // Angry Birds variables
    bool victoryFound;
    bool thumbDownFound;
    public static bool angryBirdsRunning = false;
    bool victoryRunning = false;
    bool showGestureRunning = false;
    string activeLevel;
    GameObject level;
    GameObject slingshot;
    GameObject bird;
    GameObject victory;
    GameObject thumbDown;
    float targetAngle;
    float currentAngle;
    float yAngle;
    float currentVelocity = 0;
    float angryBirdsTimer = 0f;
    bool onlyOnce = true;
    int birdNr = 0;
    AudioSource audioSource;

    #endregion

    // Awake is called when an enabled script instance is being loaded
    private void Awake()
    {
        if (doScreenRecord)
        {
            // Initialize and start Android screen recording
            int width = Screen.width / 2;   // = 1280 pixels on Waveshare 5,5" 2K display
            int height = Screen.height / 2; // =  720 pixels on Waveshare 5,5" 2K display
            int bitrate = (int)(1f * width * height / 100 * 240 * 7);
            int fps = 30;
            bool audioEnable = false;       // Ingame audio recording is not supported, only microphone audio
            string videoEncoder = VideoEncoder.H264.ToString();

            try
            {
                // Create Unity Android class and Unity activity
                unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                unityActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity");

                // Set save folder to "Movies/ScreenRecorder", which is also the default path
                unityActivity.Call("setupSaveFolder", "ScreenRecorder");

                // Define video settings
                unityActivity.Call("setupVideo", width, height, bitrate, fps, audioEnable, videoEncoder);

                // Start screen recording
                if (!AndroidUtils.IsPermitted(AndroidPermission.RECORD_AUDIO))
                {
                    // RECORD_AUDIO is already declared inside the Android plugin manifest, but we also need to grant this permission manually
                    AndroidUtils.RequestPermission(AndroidPermission.RECORD_AUDIO);
                    AndroidUtils.onAllowCallback = () => { unityActivity.Call("startRecording"); };
                    AndroidUtils.onDenyCallback = () => { AndroidUtils.ShowToast("Need RECORD_AUDIO permission to record audio"); };
                    AndroidUtils.onDenyAndNeverAskAgainCallback = () => { AndroidUtils.ShowToast("Need RECORD_AUDIO permission to record audio"); };
                }
                else
                    unityActivity.Call("startRecording");
            }
            catch (Exception err)
            {
                Debug.LogError(err.ToString());
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        try
        {
            // Set the target frame rate of the app to the vertical refresh rate of the display
            Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;

            // Get MockHMD stereo display
            List<XRDisplaySubsystem> xrDisplays = new();
            SubsystemManager.GetInstances(xrDisplays);

            // Configure MockHMD to use single-pass rendering
            Unity.XR.MockHMD.MockHMD.SetRenderMode(MockHMDBuildSettings.RenderMode.SinglePassInstanced);

            // Configure MockHMD to use left & right eyes display
            xrDisplays[0].SetPreferredMirrorBlitMode(XRMirrorViewBlitMode.SideBySide);

            // Configure MockHMD stereo mode
            xrDisplays[0].textureLayout = XRDisplaySubsystem.TextureLayout.SingleTexture2D;

            // Configure MockHMD to match the display resolution
            //Unity.XR.MockHMD.MockHMD.SetEyeResolution(1280, 1440);
            Unity.XR.MockHMD.MockHMD.SetMirrorViewCrop(0.0f);

            // Store original camera position & rotation, for restore after Angry Birds was left
            cameraOriginPosition = stereoCamera.transform.position;
            cameraOriginRotation = stereoCamera.transform.rotation;

            // Initialize variables for receiving JSON formatted data via UDP socket
            jsonData = new JsonData();
            udpClient = new UdpClient(udpPort);
            udpClient.Client.Blocking = false;
            anyIP = new IPEndPoint(IPAddress.Any, udpPort);

            // Flip JohnLemon around y-axis
            Vector3 scale = johnLemon.transform.localScale;
            scale.y *= -1;
            johnLemon.transform.localScale = scale;

            // Flip TheStreet and Roll-a-Ball  around x/y/z-axis
            scale = theStreet.transform.localScale;
            scale.x *= -1;
            scale.y *= -1;
            scale.z *= -1;
            theStreet.transform.localScale = scale;
            rollaBall.transform.localScale = scale;

            // Initialize Angry Birds
            slingshot = angryBirdsCatapult.transform.Find("Slingshot").gameObject;
            slingshot.SetActive(true);
            victory = stereoCamera.transform.Find("Victory").gameObject;
            thumbDown = Instantiate(handGestureThumbDown, new Vector3(0, 0, 0.2f), Quaternion.identity);
            thumbDown.SetActive(false);

            // Initialize audio source
            audioSource = GetComponent<AudioSource>();
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }
    }

    // Called every frame the app is running
    void Update()
    {
        try
        {
            // Get ArUco data from Android service
            //json = customClass.CallStatic<string>("GetArucoData");

            // Receive ArUco and MediaPipe data as byte array from UDP socket
            if (udpClient.Available > 0)
            {
                // If excess UDP packages are available, accept only the last one to prevent data cache accumulation
                while (udpClient.Available > 0)
                {
                    udpData = udpClient.Receive(ref anyIP);
                }

                // Convert byte array using UTF8-encoding into text format
                json = Encoding.UTF8.GetString(udpData);
            }
            else
            {
                // No data received, use default JSON string
                json = "{\"arUcoIDs\":[],\"tvec\":[],\"rvec\":[],\"victoryFound\":false,\"thumbDownFound\":false}";
            }

            JsonUtility.FromJsonOverwrite(json, jsonData);

            arUcoIDs = jsonData.arUcoIDs;
            tvec = jsonData.tvec;
            rvec = jsonData.rvec;
            victoryFound = jsonData.victoryFound;
            thumbDownFound = jsonData.thumbDownFound;

            if ((arUcoIDs.Length != 0 && tvec != null && rvec != null) || angryBirdsRunning == true)
            {
                if (tvec.Length != 0)
                {
                    // Move perspective from camera vision point downwards to eyes vision point
                    tvec[0].y -= tvec[0].magnitude * 1.4f;
                }

                if (thumbDownFound == true && angryBirdsRunning == true && victoryRunning == false)
                {
                    // "Thumb down" hand gesture found, terminate Angry Birds
                    angryBirdsRunning = false;
                    ResetAngryBirds();
                    angryBirds.SetActive(false);

                    if (showGestureRunning == false)
                    {
                        // Display "thumb down" pictogram
                        thumbDown.SetActive(true);
                        StartCoroutine(ShowGesture(thumbDown));
                    }
                }

                for (int i = 0; i < arUcoIDs.Length; i++)
                {
                    if (arUcoIDs.Length > 0 && arUcoIDs[i] == 0 && angryBirdsRunning == false)
                    // Display JohnLemon on ArUco marker with ID = 0
                    {
                        if (ghost.activeSelf == true || johnLemon.activeSelf == false)
                        {
                            // Disable ghost, enable JohnLemon
                            ghost.SetActive(false);
                            johnLemon.SetActive(true);
                        }

                        // Rotate JohnLemon smoothly to rvec
                        theta = (float)(Math.Sqrt(rvec[i].x * rvec[i].x + rvec[i].y * rvec[i].y + rvec[i].z * rvec[i].z) * 180 / Math.PI);
                        axis = new Vector3(-rvec[i].x, rvec[i].z, rvec[i].y);   // y-axis and z-axis are flipped, because in Unity the y-axis is vertical and the z-axis is horizontal
                        target = Quaternion.AngleAxis(theta, axis);
                        target = new Quaternion(target.x, target.y, -target.z, -target.w);
                        johnLemon.transform.rotation = Quaternion.Lerp(johnLemon.transform.rotation, target, speed * Time.deltaTime);
                        // Rotate JohnLemon constantly to rvec
                        //johnLemon.transform.rotation = Quaternion.RotateTowards(johnLemon.transform.rotation, target, speed * Time.deltaTime);

                        // Rotate JohnLemon relative to the world frame axes, by the roll/pitch/yaw values of the Aruco marker
                        //johnLemon.transform.rotation = Quaternion.identity;
                        //johnLemon.transform.Rotate(Vector3.right * rvec[i].x, Space.World);
                        //johnLemon.transform.Rotate(Vector3.forward * rvec[i].z, Space.World);   // y-axis and z-axis are flipped, because in Unity the y-axis is vertical and the z-axis is horizontal
                        //johnLemon.transform.Rotate(Vector3.up * -rvec[i].y, Space.World);

                        // Move JohnLemon smoothly to tvec
                        johnLemon.transform.position = Vector3.Lerp(johnLemon.transform.position, tvec[i], speed * Time.deltaTime);
                        // Move JohnLemon constantly to rvec
                        //johnLemon.transform.position = Vector3.MoveTowards(johnLemon.transform.position, tvec[i], speed * Time.deltaTime);
                    }

                    if (arUcoIDs.Length > 0 && arUcoIDs[i] == 1 && angryBirdsRunning == false)
                    // Display TheStreet on ArUco marker with ID = 1
                    {
                        if (ghost.activeSelf == true || theStreet.activeSelf == false)
                        {
                            // Disable ghost, enable TheStreet
                            ghost.SetActive(false);
                            theStreet.SetActive(true);
                        }

                        // Rotate TheStreet smoothly to rvec
                        theta = (float)(Math.Sqrt(rvec[i].x * rvec[i].x + rvec[i].y * rvec[i].y + rvec[i].z * rvec[i].z) * 180 / Math.PI);
                        axis = new Vector3(rvec[i].x, rvec[i].y, rvec[i].z);
                        target = Quaternion.AngleAxis(theta, axis);
                        theStreet.transform.rotation = Quaternion.Lerp(theStreet.transform.rotation, target, speed * Time.deltaTime);
                        // Rotate TheStreet constantly to rvec
                        //theStreet.transform.rotation = Quaternion.RotateTowards(theStreet.transform.rotation, target, speed * Time.deltaTime);

                        // Rotate TheStreet relative to the world frame axes, by the roll/pitch/yaw values of the Aruco marker
                        //theStreet.transform.rotation = Quaternion.identity;
                        //theStreet.transform.Rotate(Vector3.right * (float)(rvec[i].x * 180 / Math.PI), Space.World);
                        //theStreet.transform.Rotate(Vector3.up * (float)(rvec[i].y * 180 / Math.PI), Space.World);
                        //theStreet.transform.Rotate(Vector3.forward * (float)(rvec[i].z * 180 / Math.PI), Space.World);

                        // Move TheStreet smoothly to tvec
                        theStreet.transform.position = Vector3.Lerp(theStreet.transform.position, tvec[i], speed * Time.deltaTime);
                        // Move TheStreet constantly to rvec
                        //theStreet.transform.position = Vector3.MoveTowards(theStreet.transform.position, tvec[i], speed * Time.deltaTime);
                    }

                    if (arUcoIDs.Length > 0 && arUcoIDs[i] == 2 && angryBirdsRunning == false)
                    // Display Roll-a-Ball on ArUco marker with ID = 2
                    {
                        if (ghost.activeSelf == true || rollaBall.activeSelf == false)
                        {
                            // Disable ghost, enable Roll-a-Ball
                            ghost.SetActive(false);
                            rollaBall.SetActive(true);
                        }

                        // Rotate Roll-a-Ball into convenient view
                        rvec[i].x -= (float)Math.PI * 1.7f;

                        // Exchange y- and z-axes
                        (rvec[i].y, rvec[i].z) = (rvec[i].z, rvec[i].y);

                        // Rotate Roll-a-Ball smoothly to rvec
                        theta = (float)(Math.Sqrt(rvec[i].x * rvec[i].x + rvec[i].y * rvec[i].y + rvec[i].z * rvec[i].z) * 180 / Math.PI);
                        axis = new Vector3(-rvec[i].x, rvec[i].y, rvec[i].z);
                        target = Quaternion.AngleAxis(theta, axis);
                        rollaBall.transform.rotation = Quaternion.Lerp(rollaBall.transform.rotation, target, speed * Time.deltaTime);
                        // Rotate Roll-a-Ball constantly to rvec
                        //rollaBall.transform.rotation = Quaternion.RotateTowards(rollaBall.transform.rotation, target, speed * Time.deltaTime);

                        // Rotate Roll-a-Ball relative to the world frame axes, by the roll/pitch/yaw values of the Aruco marker
                        //rollaBall.transform.rotation = Quaternion.identity;
                        //rollaBall.transform.Rotate(Vector3.right * (float)(rvec[i].x * 180 / Math.PI), Space.World);
                        //rollaBall.transform.Rotate(Vector3.up * (float)(rvec[i].y * 180 / Math.PI), Space.World);
                        //rollaBall.transform.Rotate(Vector3.forward * (float)(rvec[i].z * 180 / Math.PI), Space.World);

                        // Move Roll-a-Ball into convenient view
                        tvec[i].y -= 5f;
                        tvec[i].z += 30f;

                        // Move Roll-a-Ball smoothly to tvec
                        rollaBall.transform.position = Vector3.Lerp(rollaBall.transform.position, tvec[i], speed * Time.deltaTime);
                        // Move Roll-a-Ball constantly to rvec
                        //rollaBall.transform.position = Vector3.MoveTowards(rollaBall.transform.position, tvec[i], speed * Time.deltaTime);
                    }

                    if (arUcoIDs.Length > 0 == true && (arUcoIDs[i] == 3 || victoryRunning))
                    // Display Angry Birds on ArUco marker with ID = 3
                    {
                        if (ghost.activeSelf == true || angryBirds.activeSelf == false)
                        {
                            // Flag Angry Birds is running
                            angryBirdsRunning = true;
                            // Rotate camera downwards
                            //stereoCamera.transform.Rotate(new Vector3(10, 0, 0));
                            // Disable ghost, enable Angry Birds
                            ghost.SetActive(false);
                            angryBirds.SetActive(true);
                            // Activate Level1
                            Destroy(level);
                            level = Instantiate(angryBirdsLevel1, angryBirds.transform);
                            activeLevel = "Level 1";
                            // Play Angry Birds main theme
                            audioSource.Stop();
                            audioSource.PlayOneShot(angryBirdsTheme1, 0.3f);
                        }

                        // Rotate camera smoothly around y-axis rvec
                        currentAngle = stereoCamera.transform.rotation.eulerAngles.y;
                        targetAngle = -(float)(rvec[i].y * 180 / Math.PI) * 2f;
                        // Damp angle from current y-angle towards target y-angle
                        yAngle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref currentVelocity, 0.25f);
                        // Apply the y-rotation and the distance to the camera position
                        stereoCamera.transform.position = angryBirds.transform.position + Quaternion.Euler(0, yAngle, 0) * new Vector3(0, 10f, -60f * Math.Abs(tvec[i].z));
                        // Look at the target
                        stereoCamera.transform.LookAt(angryBirds.transform);
                        // Rotate camera upwards, this moves the scene towards the bottom of the display
                        stereoCamera.transform.Rotate(new Vector3(-25f, 0, 0));

                        //Rotate camera around y-axis rvec
                        //stereoCamera.transform.RotateAround(angryBirds.transform.position, Vector3.up, -(float)(rvec[i].y * 180 / Math.PI));
                        //stereoCamera.transform.LookAt(angryBirds.transform);
                    }
                }

                if ((victoryFound == true || victoryRunning == true) && angryBirdsRunning == true)
                // "Victory" hand gesture found, throw bird
                {
                    victoryRunning = true;

                    // Rotate the slingshot into view
                    angryBirdsTimer += Time.deltaTime;
                    if (angryBirdsTimer <= 2f)
                    {
                        slingshot.transform.Rotate(0f, 0f, Time.deltaTime * 90);

                        if (showGestureRunning == false && angryBirdsTimer <= 0.5f)
                        {
                            // Display "victory" pictogram
                            victory.SetActive(true);
                            StartCoroutine(ShowGesture(victory));
                        }
                    }

                    // Throw bird, after slingshot rotation finish
                    if (angryBirdsTimer > 3f && onlyOnce == true)
                    {
                        GetAnotherBird();
                        bird.transform.LookAt(angryBirds.transform);
                        bird.transform.Rotate(0f, 90f, 0f);
                        bird.GetComponent<ConstantForce>().relativeForce = new Vector3(-120f, 0f, 0f);
                        bird.SetActive(true);
                        onlyOnce = false;
                    }

                    // Remove force from bird, so that he doesn't roll off the scene
                    if (angryBirdsTimer > 3.5f && angryBirdsTimer <= 4f)
                    {
                        bird.GetComponent<ConstantForce>().relativeForce = new Vector3(0, 0, 0);
                        slingshot.SetActive(false);
                    }

                    // Check if all pigs fell down to the ground
                    if (CheckPigs(activeLevel) == true)
                    {
                        // All pigs fell down to the ground, start bird victory sound
                        if (angryBirdsTimer < 1000f)
                        {
                            angryBirdsTimer = 1000;
                            // Play bird victory sound
                            audioSource.PlayOneShot(angryBirdsVictory, 1f);
                        }

                        // Wait 7 seconds
                        if (angryBirdsTimer > 1007f)
                        {
                            // Switch to the next level
                            ResetAngryBirds();
                            Destroy(level);
                            switch (activeLevel)
                            {
                                case "Level 1":
                                    level = Instantiate(angryBirdsLevel2, angryBirds.transform);
                                    activeLevel = "Level 2";
                                    audioSource.Stop();
                                    audioSource.PlayOneShot(angryBirdsTheme2, 0.3f);
                                    break;

                                case "Level 2":
                                    level = Instantiate(angryBirdsLevel3, angryBirds.transform);
                                    activeLevel = "Level 3";
                                    audioSource.Stop();
                                    audioSource.PlayOneShot(angryBirdsTheme3, 0.3f);
                                    break;

                                case "Level 3":
                                    level = Instantiate(angryBirdsLevel1, angryBirds.transform);
                                    activeLevel = "Level 1";
                                    audioSource.Stop();
                                    audioSource.PlayOneShot(angryBirdsTheme1, 0.3f);
                                    break;
                            }
                        }
                    }
                    // Not all pigs fell down to the ground, wait 4 seconds then continue with the next bird
                    else if (angryBirdsTimer > 7f && angryBirdsTimer < 1000f)
                    {
                        ResetAngryBirds();
                        // Play pig victory sound
                        audioSource.PlayOneShot(angryBirdsPigVictory, 1f);
                    }
                }
            }
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }

        // If no more ArUco markers were detected, wait some frames (depending on current FPS) until ghost is displayed, to prevent ghost/scene flickering
        if (arUcoIDs.Length == 0)
        {
            // Frame delay countdown
            if (delayFrames > 0)
            {
                delayFrames -= 75 / currentFPS;
            }
        }
        else
        {
            // Restart frame delay countdown
            delayFrames = 30;
        }

        // Enable ghost, if no ArUco markers were detected
        if (arUcoIDs.Length == 0 && delayFrames <= 0 && angryBirdsRunning == false && (ghost.activeSelf == false || johnLemon.activeSelf == true || theStreet.activeSelf == true || rollaBall.activeSelf == true))
        {
            // Enable ghost, disable JohnLemon, TheStreet, Roll-a-Ball and Angry Birds                
            ghost.SetActive(true);
            johnLemon.SetActive(false);
            theStreet.SetActive(false);
            rollaBall.SetActive(false);
            angryBirds.SetActive(false);

            // If Angry Birds was left:
            if (stereoCamera.transform.position != cameraOriginPosition || stereoCamera.transform.rotation != cameraOriginRotation)
            {
                // Restore original camera position & rotation
                stereoCamera.transform.position = cameraOriginPosition;
                stereoCamera.transform.rotation = cameraOriginRotation;
                // Stop any running audio clip
                audioSource.Stop();
            }

            // Reset screen recording countdown timer
            screenRecordTimer = 20;
        }

        // Rotate ghost, if enabled. Check if screen recording should be stopped.
        if (ghost.activeSelf == true)
        {
            ghost.transform.Rotate(rotateSpeed * Time.deltaTime * objectRotation);

            // Stop Android screen recording, if ghost was continuously enabled for 20 seconds
            if (doScreenRecord)
            {
                screenRecordTimer -= Time.deltaTime;
                if (screenRecordTimer < 0)
                {
                    doScreenRecord = false;
                    unityActivity.Call("stopRecording");
                    unityActivity.Dispose();
                }
            }
        }

        // Display FPS
        if (fpsTimer < 0.5)
        {
            fpsTimer += Time.deltaTime;
            FPS += 2;
        }
        else
        {
            dataDisplay.text = "\nFPS: " + FPS;
            if (FPS >= 35)
            {
                dataDisplay.color = Color.green;
            }
            else if (FPS >= 25)
            {
                dataDisplay.color = Color.yellow;
            }
            else
            {
                dataDisplay.color = Color.red;
            }
            fpsTimer -= 0.5f;
            currentFPS = FPS;
            FPS = 0;
        }
    }

    // Get a new bird
    private void GetAnotherBird()
    {
        switch (birdNr)
        {
            case 0:
                bird = Instantiate(red, angryBirdsCatapult.transform);
                break;
            case 1:
                bird = Instantiate(chuck, angryBirdsCatapult.transform);
                break;
            case 2:
                bird = Instantiate(matilda, angryBirdsCatapult.transform);
                break;
            case 3:
                bird = Instantiate(hal, angryBirdsCatapult.transform);
                break;
            case 4:
                bird = Instantiate(jay, angryBirdsCatapult.transform);
                break;
            case 5:
                bird = Instantiate(bomb, angryBirdsCatapult.transform);
                break;
            case 6:
                bird = Instantiate(bubbles, angryBirdsCatapult.transform);
                break;
            case 7:
                bird = Instantiate(terence, angryBirdsCatapult.transform);
                break;
        }

        if (bird.GetComponent<ConstantForce>() == null)
        {
            bird.gameObject.AddComponent<ConstantForce>();
        }

        birdNr += 1;
        if (birdNr == 8)
        {
            birdNr = 0;
        }
    }

    // Check if all Angry Birds pigs fell down to the ground
    private bool CheckPigs(string activeLevel)
    {
        switch (activeLevel)
        {
            case "Level 1":
                if (level.transform.Find("Minion Pig1").gameObject.transform.position.y < -2.5f &&
                    level.transform.Find("Minion Pig2").gameObject.transform.position.y < -2.5f &&
                    level.transform.Find("Foreman Pig").gameObject.transform.position.y < -2.5f)
                    return true;
                else
                    return false;

            case "Level 2":
                if (level.transform.Find("Minion Pig1").gameObject.transform.position.y < -2.5f &&
                    level.transform.Find("Minion Pig2").gameObject.transform.position.y < -2.5f &&
                    level.transform.Find("Minion Pig3").gameObject.transform.position.y < -2.5f &&
                    level.transform.Find("Minion Pig4").gameObject.transform.position.y < -2.5f &&
                    level.transform.Find("Minion Pig5").gameObject.transform.position.y < -2.5f &&
                    level.transform.Find("Corporal Pig").gameObject.transform.position.y < -2.5f)
                    return true;
                else
                    return false;

            case "Level 3":
                if (level.transform.Find("Corporal Pig1").gameObject.transform.position.y < -3.2f &&
                    level.transform.Find("Corporal Pig2").gameObject.transform.position.y < -3.2f &&
                    level.transform.Find("King Pig").gameObject.transform.position.y < -4f)
                    return true;
                else
                    return false;
        }
        return false;
    }

    // Display hand gesture as pictogram
    IEnumerator ShowGesture(GameObject handGesture)
    {
        showGestureRunning = true;
        Color c = handGesture.GetComponent<Renderer>().sharedMaterial.color;
        Vector3 scale = handGesture.transform.localScale;
        float alpha;

        // Fade in pictogram
        for (alpha = 0f; alpha <= 1; alpha += 0.05f)
        {
            c.a = alpha;
            handGesture.GetComponent<Renderer>().sharedMaterial.color = c;
            yield return null;
        }

        // Fade out and enlarge pictogram
        for (alpha = 1f; alpha >= 0; alpha -= 0.015f)
        {
            c.a = alpha;
            handGesture.GetComponent<Renderer>().sharedMaterial.color = c;

            scale *= 1.005f;
            if (scale.x < 100f)
                handGesture.transform.localScale = scale;
            yield return null;
        }

        handGesture.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        handGesture.SetActive(false);
        showGestureRunning = false;
    }

    // Reset Angry Birds settings
    private void ResetAngryBirds()
    {
        // Reset variables
        Destroy(bird);
        angryBirdsTimer = 0f;
        victoryRunning = false;
        onlyOnce = true;

        // Reset slingshot
        slingshot.transform.localRotation = Quaternion.Euler(0f, -90f, -180f);
        slingshot.SetActive(true);
    }

    // Called when the program finishes
    void OnApplicationQuit()
    {
        udpClient.Close();

        if (unityActivity != null)
        {
            unityActivity.Call("stopRecording");
            unityActivity.Dispose();
        }
    }
}

// Class which holds the ArUco and MediaPipe variables that should be received in JSON format
[Serializable]
public class JsonData
{
    public int[] arUcoIDs;          // Detected ArUco marker IDs
    public Vector3[] tvec, rvec;    // Detected translation and rotation vectors
    public bool victoryFound;       // "Victory" hand gesture found flag
    public bool thumbDownFound;     // "Thumb down" hand gesture found flag
}
