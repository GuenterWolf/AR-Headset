#region " Using statements "

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Serialization;
using UnityEngine;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;

#endregion

// Detect and decode ArUco markers using the Android camera
public class GetArucoData : MonoBehaviour
{
    #region " Variables definitions "

    public static int[] arUcoIDs;               // Detected ArUco marker IDs
    public static Vector3[] tvec, rvec;         // Detected translation and rotation vectors
    public static Mat rgbaMat;                  // RGBA camera Mat for use by other methods
    
    ArUcoDictionary dictionaryId = ArUcoDictionary.DICT_4X4_100;
    Dictionary dictionary;                      // Holds one of the predefined ArUco dictionaries
    bool useStoredCameraParameters = true;      // Restore the camera parameters if a calibration file exists
    float markerLength = 0.1f;                  // ArUco marker length in meters
    WebCamTexture webCamTexture;                // Android camera
    bool newMat = false;
    Mat rgbMat;                                 // OpenCV Mat with RGB picture data
    Mat undistortedRgbMat;                      // OpenCV Mat after distortion corrections
    Mat camMatrix;                              // Camera parameters matrix
    MatOfDouble distCoeffs;                     // Camera distortion coefficients
    double[] doubleID;
    Mat ids;                                    // Detected ArUco marker IDs
    List<Mat> corners;                          // Detected ArUco marker corners
    List<Mat> rejectedCorners;                  // Rejected ArUco marker corners
    Mat rvecMat, tvecMat, corner_4x1;
    MatOfPoint2f imagePoints;
    double x, y, z;
    ArucoDetector arucoDetector;                // Function for detection of ArUco markers
    int width;                                  // minimal width resolution of the camera
    int height;                                 // minimal height resolution of the camera
    Color32[] colors;
    GCHandle pinnedArray;

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        try
        {
            // Scan for all available cameras, and use the first one found
            var devices = WebCamTexture.devices;

            // Set width and height to the minimal camera resolutions
            width = devices[0].availableResolutions[0].width;
            height = devices[0].availableResolutions[0].height;

            // Initialize and start camera
            webCamTexture = new WebCamTexture(devices[0].name, width, height, 60);
            webCamTexture.Play();
            InitializeCamera();

            // This algorithm needs much ressources, therefore run it only 5x per second
            InvokeRepeating("GetNewMat", 0, 0.2f);

            // Start the ArUco marker estimation thread
            Thread getArUcoPositions;
            getArUcoPositions = new Thread(GetArUcoPositions);
            getArUcoPositions.Start();
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }
    }

    // Get new photo, and convert it from Unity WebCamTexture to OpenCV Mat
    void GetNewMat()
    {
        try
        {
            if (webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
            {
                webCamTexture.GetPixels32(colors);
                pinnedArray = GCHandle.Alloc(colors, GCHandleType.Pinned);
                OpenCVForUnity_TextureToMat(pinnedArray.AddrOfPinnedObject(), rgbaMat.nativeObj, (int)TextureFormat.RGBA32, false, 0);
                pinnedArray.Free();
                core_Core_flip_10(rgbaMat.nativeObj, rgbaMat.nativeObj, 0);

                // New photo is available
                newMat = true;
            }
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }
    }

    [DllImport("opencvforunity")]
    private static extern void OpenCVForUnity_TextureToMat(IntPtr textureColors, IntPtr Mat, int textureFormat, [MarshalAs(UnmanagedType.U1)] bool flip, int flipCode);

    [DllImport("opencvforunity")]
    private static extern void core_Core_flip_10(IntPtr src_nativeObj, IntPtr dst_nativeObj, int flipCode);

    // Estimate ArUco marker positions
    void GetArUcoPositions()
    {
        // Run continuously
        while (true)
        {
            try
            {
                if (newMat == true)
                {
                    // Detect markers on new photo
                    newMat = false;

                    Imgproc.cvtColor(rgbaMat, rgbMat, Imgproc.COLOR_RGBA2RGB);
                    Calib3d.undistort(rgbMat, undistortedRgbMat, camMatrix, distCoeffs);
                    arucoDetector.detectMarkers(undistortedRgbMat, corners, ids, rejectedCorners);

                    // Estimate ArUco marker positions, if at least one marker was detected
                    arUcoIDs = null;
                    if (ids.total() > 0)
                    {
                        using (MatOfPoint3f objPoints = new MatOfPoint3f(
                            new Point3(-markerLength / 2f, markerLength / 2f, 0),
                            new Point3(markerLength / 2f, markerLength / 2f, 0),
                            new Point3(markerLength / 2f, -markerLength / 2f, 0),
                            new Point3(-markerLength / 2f, -markerLength / 2f, 0)))
                        {
                            arUcoIDs = new int[corners.Count];
                            rvec = new Vector3[corners.Count];
                            tvec = new Vector3[corners.Count];

                            for (int i = 0; i < corners.Count; i++)
                            {
                                doubleID = ids.get(i, 0);
                                arUcoIDs[i] = (int)doubleID[0];

                                using (rvecMat = new Mat(1, 1, CvType.CV_64FC3))
                                using (tvecMat = new Mat(1, 1, CvType.CV_64FC3))
                                using (corner_4x1 = corners[i].reshape(2, 4))   // 1*4*CV_32FC2 => 4*1*CV_32FC2
                                using (imagePoints = new MatOfPoint2f(corner_4x1))
                                {
                                    // Calculate ArUco marker pose
                                    Calib3d.solvePnP(objPoints, imagePoints, camMatrix, distCoeffs, rvecMat, tvecMat);

                                    // Convert rvec (rotation vector) Mat into Vector3 in radians
                                    x = -rvecMat.get(0, 0)[0];
                                    y = -rvecMat.get(1, 0)[0];
                                    z = rvecMat.get(2, 0)[0];
                                    rvec[i] = Matrix4x4.TRS(new Vector3((float)x, (float)y, (float)z), Quaternion.identity, Vector3.one).GetColumn(3);

                                    // Convert tvec (translation vector) Mat into Vector3 in meters
                                    x = -tvecMat.get(0, 0)[0];
                                    y = -tvecMat.get(1, 0)[0];      // Orientation of y-axis is reversed, because OpenCV has right-handed coordinates system, Unity has left-handed coordinates system)
                                    z = tvecMat.get(2, 0)[0];
                                    tvec[i] = Matrix4x4.TRS(new Vector3((float)x, (float)y, (float)z), Quaternion.identity, Vector3.one).GetColumn(3);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Debug.LogError(err.ToString());
            }
        }
    }

    // Camera initialization
    public void InitializeCamera()
    {
        // set camera parameters
        double fx;
        double fy;
        double cx;
        double cy;

        string loadPath = Application.streamingAssetsPath;
        loadPath = loadPath.Replace("file://", string.Empty);
        loadPath = Path.Combine(loadPath, "OpenCVForUnity/camera_parameters640x480.xml");

        if (useStoredCameraParameters && File.Exists(loadPath))
        {
            // Read camera parameters from file
            CameraParameters param;
            XmlSerializer serializer = new XmlSerializer(typeof(CameraParameters));
            using (var stream = new FileStream(loadPath, FileMode.Open))
            {
                param = (CameraParameters)serializer.Deserialize(stream);
            }

            camMatrix = param.GetCameraMatrix();
            distCoeffs = new MatOfDouble(param.GetDistortionCoefficients());

            fx = param.camera_matrix[0];
            fy = param.camera_matrix[4];
            cx = param.camera_matrix[2];
            cy = param.camera_matrix[5];
        }
        else
        {
            // Create dummy camera parameters
            int max_d = (int)Mathf.Max(width, height);
            fx = max_d;
            fy = max_d;
            cx = width / 2.0f;
            cy = height / 2.0f;

            camMatrix = new Mat(3, 3, CvType.CV_64FC1);
            camMatrix.put(0, 0, fx);
            camMatrix.put(0, 1, 0);
            camMatrix.put(0, 2, cx);
            camMatrix.put(1, 0, 0);
            camMatrix.put(1, 1, fy);
            camMatrix.put(1, 2, cy);
            camMatrix.put(2, 0, 0);
            camMatrix.put(2, 1, 0);
            camMatrix.put(2, 2, 1.0f);

            distCoeffs = new MatOfDouble(0, 0, 0, 0);
        }

        // Set camera calibration matrix values
        float imageSizeScale = 1.0f;
        Size imageSize = new Size(width * imageSizeScale, height * imageSizeScale);
        double apertureWidth = 0;
        double apertureHeight = 0;
        double[] fovx = new double[1];
        double[] fovy = new double[1];
        double[] focalLength = new double[1];
        Point principalPoint = new Point(0, 0);
        double[] aspectratio = new double[1];

        Calib3d.calibrationMatrixValues(camMatrix, imageSize, apertureWidth, apertureHeight, fovx, fovy, focalLength, principalPoint, aspectratio);

        // Initialize variables
        rgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
        rgbMat = new Mat(rgbaMat.rows(), rgbaMat.cols(), CvType.CV_8UC3);
        undistortedRgbMat = new Mat();
        ids = new Mat();
        corners = new List<Mat>();
        rejectedCorners = new List<Mat>();
        dictionary = Objdetect.getPredefinedDictionary((int)dictionaryId);
        colors = new Color32[width * height];

        DetectorParameters detectorParams = new DetectorParameters();
        detectorParams.set_minDistanceToBorder(3);
        detectorParams.set_useAruco3Detection(true);
        detectorParams.set_cornerRefinementMethod(Objdetect.CORNER_REFINE_SUBPIX);
        detectorParams.set_minSideLengthCanonicalImg(16);
        detectorParams.set_errorCorrectionRate(0.8);
        RefineParameters refineParameters = new RefineParameters(10f, 3f, true);
        arucoDetector = new ArucoDetector(dictionary, detectorParams, refineParameters);
    }

    public enum ArUcoDictionary
    {
        DICT_4X4_50 = Objdetect.DICT_4X4_50,
        DICT_4X4_100 = Objdetect.DICT_4X4_100,
        DICT_4X4_250 = Objdetect.DICT_4X4_250,
        DICT_4X4_1000 = Objdetect.DICT_4X4_1000,
        DICT_5X5_50 = Objdetect.DICT_5X5_50,
        DICT_5X5_100 = Objdetect.DICT_5X5_100,
        DICT_5X5_250 = Objdetect.DICT_5X5_250,
        DICT_5X5_1000 = Objdetect.DICT_5X5_1000,
        DICT_6X6_50 = Objdetect.DICT_6X6_50,
        DICT_6X6_100 = Objdetect.DICT_6X6_100,
        DICT_6X6_250 = Objdetect.DICT_6X6_250,
        DICT_6X6_1000 = Objdetect.DICT_6X6_1000,
        DICT_7X7_50 = Objdetect.DICT_7X7_50,
        DICT_7X7_100 = Objdetect.DICT_7X7_100,
        DICT_7X7_250 = Objdetect.DICT_7X7_250,
        DICT_7X7_1000 = Objdetect.DICT_7X7_1000,
        DICT_ARUCO_ORIGINAL = Objdetect.DICT_ARUCO_ORIGINAL,
    }

    [System.Serializable]
    public struct CameraParameters
    {
        public string calibration_date;
        public int frames_count;
        public int image_width;
        public int image_height;
        public int calibration_flags;
        public double[] camera_matrix;
        public double[] distortion_coefficients;
        public double avg_reprojection_error;

        public CameraParameters(int frames_count, int image_width, int image_height, int calibration_flags, double[] camera_matrix, double[] distortion_coefficients, double avg_reprojection_error)
        {
            this.calibration_date = DateTime.Now.ToString();
            this.frames_count = frames_count;
            this.image_width = image_width;
            this.image_height = image_height;
            this.calibration_flags = calibration_flags;
            this.camera_matrix = camera_matrix;
            this.distortion_coefficients = distortion_coefficients;
            this.avg_reprojection_error = avg_reprojection_error;
        }

        public CameraParameters(int frames_count, int image_width, int image_height, int calibration_flags, Mat camera_matrix, Mat distortion_coefficients, double avg_reprojection_error)
        {
            double[] camera_matrixArr = new double[camera_matrix.total()];
            camera_matrix.get(0, 0, camera_matrixArr);

            double[] distortion_coefficientsArr = new double[distortion_coefficients.total()];
            distortion_coefficients.get(0, 0, distortion_coefficientsArr);

            this.calibration_date = DateTime.Now.ToString();
            this.frames_count = frames_count;
            this.image_width = image_width;
            this.image_height = image_height;
            this.calibration_flags = calibration_flags;
            this.camera_matrix = camera_matrixArr;
            this.distortion_coefficients = distortion_coefficientsArr;
            this.avg_reprojection_error = avg_reprojection_error;
        }

        public Mat GetCameraMatrix()
        {
            Mat m = new Mat(3, 3, CvType.CV_64FC1);
            m.put(0, 0, camera_matrix);
            return m;
        }

        public Mat GetDistortionCoefficients()
        {
            Mat m = new Mat(distortion_coefficients.Length, 1, CvType.CV_64FC1);
            m.put(0, 0, distortion_coefficients);
            return m;
        }
    }
}
