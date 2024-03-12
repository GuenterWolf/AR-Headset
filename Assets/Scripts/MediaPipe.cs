#region " Using statements "

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnityExample.DnnModel;
using OpenCVRange = OpenCVForUnity.CoreModule.Range;

#endregion

// MediaPipe hand poses estimation
public class MediaPipe : MonoBehaviour
{
    #region " Variables definitions "

    // "Victory" and "thumb down" hand gesture variables
    public static bool victoryFound = false;
    public static bool thumbDownFound = false;

    // OpenCV variables
    Mat bgrMat;
    Mat palms;
    Mat handpose;
    List<Mat> hands;
    Mat hands_col4_67_21x3;
    float[] handLM;

    // MediaPipe variables
    MediaPipePalmDetector palmDetector;
    MediaPipeHandPoseEstimator handPoseEstimator;
    protected static readonly string PALM_DETECTION_MODEL_FILENAME = "OpenCVForUnity/dnn/palm_detection_mediapipe_2023feb.onnx";
    string palmDetectionModelFilepath;
    protected static readonly string HANDPOSE_ESTIMATION_MODEL_FILENAME = "OpenCVForUnity/dnn/handpose_estimation_mediapipe_2023feb.onnx";
    string handposeEstimationModelFilepath;

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        try
        {
            // Initialize MediaPipe variables
            palmDetectionModelFilepath = Utils.getFilePath(PALM_DETECTION_MODEL_FILENAME);
            handposeEstimationModelFilepath = Utils.getFilePath(HANDPOSE_ESTIMATION_MODEL_FILENAME);
            palmDetector = new MediaPipePalmDetector(palmDetectionModelFilepath, 0.3f, 0.6f);
            handPoseEstimator = new MediaPipeHandPoseEstimator(handposeEstimationModelFilepath, 0.9f);

            // Start the handposes estimation thread
            Thread estimateHandPoses;
            estimateHandPoses = new Thread(EstimateHandPoses);
            estimateHandPoses.Start();
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }
    }

    // Estimate hand poses using MediaPipe
    public void EstimateHandPoses()
    {
        // Run continuously
        while (true)
        {
            try
            {
                // Only continue if Anry Birds game is running
                if (GameObjectManager.angryBirdsRunning == true)
                {
                    // This algorithm needs much ressources, therefore run it only 2x per second
                    Thread.Sleep(500);

                    if (GetArucoData.rgbaMat != null && bgrMat == null)
                    {
                        bgrMat = new Mat(GetArucoData.rgbaMat.rows(), GetArucoData.rgbaMat.cols(), CvType.CV_8UC3);
                    }

                    if (GetArucoData.rgbaMat != null && bgrMat != null && palmDetector != null && handPoseEstimator != null)
                    {
                        Imgproc.cvtColor(GetArucoData.rgbaMat, bgrMat, Imgproc.COLOR_RGBA2BGR);
                        palms = palmDetector.Infer(bgrMat);
                        hands = new List<Mat>();

                        // Estimate the pose of each hand
                        for (int i = 0; i < palms.rows(); ++i)
                        {
                            // Handpose estimator inference
                            handpose = handPoseEstimator.Infer(bgrMat, palms.row(i));

                            if (!handpose.empty())
                                hands.Add(handpose);
                        }

                        // Check for "victory" and "thumb down" hand gestures
                        victoryFound = false;
                        thumbDownFound = false;

                        if (hands.Count > 0 && hands[0].rows() >= 132)
                        {

                            // Only evaluate the first hand found
                            hands_col4_67_21x3 = hands[0].rowRange(new OpenCVRange(4, 67)).reshape(1, 21);
                            handLM = new float[42];
                            hands_col4_67_21x3.colRange(new OpenCVRange(0, 2)).get(0, 0, handLM);

                            // "Victory" hand gesture is found if the following hand landmarks are detected:
                            // indexFingerTipY < thumbFingerTipY and indexFingerTipY < ringFingerTipY and indexFingerTipY < pinkyFingerTipY and middleFingerTipY < thumbFingerTipY and
                            // middleFingerTipY < ringFingerTipY and middleFingerTipY < pinkyFingerTipY and ringFingerMcpY < ringFingerTipY and pinkyFingerMcpY < pinkyFingerTipY
                            //
                            // "Thumb down" hand gesture is found if the following hand landmark is detected:
                            // thumbFingerCmcY < thumbFingerTipY

                            if (handLM[17] < handLM[9] && handLM[17] < handLM[33] && handLM[17] < handLM[41] && handLM[25] < handLM[9] && handLM[25] < handLM[33] && handLM[25] < handLM[41] && handLM[27] < handLM[33] && handLM[35] < handLM[41])
                                victoryFound = true;
                            else if (handLM[3] < handLM[9])
                                thumbDownFound = true;
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
}
