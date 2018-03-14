using System;
using UnityEngine;
using Valve.VR;

namespace KerbalVR
{
    public class Scene
    {
        #region Constants
        public static readonly string[] FLIGHT_SCENE_CAMERAS = {
            "GalaxyCamera",
            "Camera ScaledSpace",
            "Camera 01",
            "Camera 00",
            "InternalCamera",
        };

        public static readonly string[] SPACECENTER_SCENE_CAMERAS = {
            "GalaxyCamera",
            "Camera ScaledSpace",
            "Camera 01",
            "Camera 00",
        };

        public static readonly string[] EDITOR_SCENE_CAMERAS = {
            "GalaxyCamera",
            "sceneryCam",
            "Main Camera",
            "markerCam",
        };
        #endregion


        #region Properties

        // The list of cameras to render for the current scene.
        public static Types.CameraData[] VRCameras { get; private set; }
        public static int NumVRCameras { get; private set; }

        // The initial world position of the cameras for the current scene. This
        // position corresponds to the origin in the real world physical device
        // coordinate system.
        public static Vector3 InitialPosition { get; private set; }
        public static Quaternion InitialRotation { get; private set; }

        #endregion

        
        /// <summary>
        /// Set up the list of cameras to render for this scene and the initial position
        /// corresponding to the origin in the real world device coordinate system.
        /// </summary>
        public static void SetupScene() {
            switch (HighLogic.LoadedScene) {
                case GameScenes.FLIGHT:
                    SetupFlightScene();
                    break;

                case GameScenes.EDITOR:
                    SetupEditorScene();
                    break;

                default:
                    throw new Exception("Cannot setup VR scene, current scene \"" +
                        HighLogic.LoadedScene + "\" is invalid.");
            }

            Utils.LogInfo("Scene: " + HighLogic.LoadedScene);
            Utils.LogInfo("InitialPosition = " + InitialPosition.ToString("F3"));
        }

        private static void SetupFlightScene() {
            // generate list of cameras to render
            PopulateCameraList(FLIGHT_SCENE_CAMERAS);

            // set inital scene position
            InitialPosition = InternalCamera.Instance.transform.position;
            InitialRotation = InternalCamera.Instance.transform.rotation;
        }

        private static void SetupEditorScene() {
            // generate list of cameras to render
            PopulateCameraList(EDITOR_SCENE_CAMERAS);

            // set inital scene position
            InitialPosition = EditorCamera.Instance.transform.position;
            InitialRotation = EditorCamera.Instance.transform.rotation;
        }

        /// <summary>
        /// Updates the game cameras to the correct position, according to the given HMD eye pose.
        /// </summary>
        /// <param name="eyePosition">Position of the HMD eye, in the device space coordinate system</param>
        /// <param name="eyeRotation">Rotation of the HMD eye, in the device space coordinate system</param>
        public static void UpdateScene(Vector3 eyePosition, Quaternion eyeRotation) {
            switch (HighLogic.LoadedScene) {
                case GameScenes.FLIGHT:
                    UpdateFlightScene(eyePosition, eyeRotation);
                    break;

                case GameScenes.EDITOR:
                    UpdateEditorScene(eyePosition, eyeRotation);
                    break;

                default:
                    throw new Exception("Cannot setup VR scene, current scene \"" +
                        HighLogic.LoadedScene + "\" is invalid.");
            }
        }

        private static void UpdateFlightScene(Vector3 eyePosition, Quaternion eyeRotation) {
            InternalCamera.Instance.transform.position = InitialPosition + InitialRotation * eyePosition;
            InternalCamera.Instance.transform.rotation = InitialRotation * eyeRotation;

            FlightCamera.fetch.transform.position = InternalSpace.InternalToWorld(InternalCamera.Instance.transform.position);
            FlightCamera.fetch.transform.rotation = InternalSpace.InternalToWorld(InternalCamera.Instance.transform.rotation);
        }

        private static void UpdateEditorScene(Vector3 eyePosition, Quaternion eyeRotation) {
            EditorCamera.Instance.transform.position = InitialPosition + InitialRotation * eyePosition;
            EditorCamera.Instance.transform.rotation = InitialRotation * eyeRotation;
        }

        /// <summary>
        /// Resets game cameras back to their original settings
        /// </summary>
        public static void CloseScene() {
            // reset cameras to their original settings
            if (VRCameras != null) {
                for (int i = 0; i < VRCameras.Length; i++) {
                    VRCameras[i].camera.targetTexture = null;
                    VRCameras[i].camera.projectionMatrix = VRCameras[i].originalProjectionMatrix;
                    VRCameras[i].camera.enabled = true;
                }
            }
        }

        /// <summary>
        /// Populates the list of cameras according to the cameras that should be used for
        /// the current game scene.
        /// </summary>
        /// <param name="cameraNames">An array of camera names to use for this VR scene.</param>
        private static void PopulateCameraList(string[] cameraNames) {
            // search for the cameras to render
            NumVRCameras = cameraNames.Length;
            VRCameras = new Types.CameraData[NumVRCameras];
            for (int i = 0; i < NumVRCameras; i++) {
                Camera foundCamera = Array.Find(Camera.allCameras, cam => cam.name.Equals(cameraNames[i]));
                if (foundCamera == null) {
                    Utils.LogError("Could not find camera \"" + cameraNames[i] + "\" in the scene!");

                } else {
                    // determine clip plane and new projection matrices
                    float nearClipPlane = (foundCamera.name.Equals("Camera 01")) ? 0.05f : foundCamera.nearClipPlane;
                    HmdMatrix44_t projectionMatrixL = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Left, nearClipPlane, foundCamera.farClipPlane);
                    HmdMatrix44_t projectionMatrixR = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Right, nearClipPlane, foundCamera.farClipPlane);

                    // store information about the camera
                    VRCameras[i].camera = foundCamera;
                    VRCameras[i].originalProjectionMatrix = foundCamera.projectionMatrix;
                    VRCameras[i].hmdProjectionMatrixL = MathUtils.Matrix4x4_OpenVr2UnityFormat(ref projectionMatrixL);
                    VRCameras[i].hmdProjectionMatrixR = MathUtils.Matrix4x4_OpenVr2UnityFormat(ref projectionMatrixR);

                    // disable the camera so we can call Render directly
                    foundCamera.enabled = false;
                }
            }
        }

        public static bool SceneAllowsVR() {
            bool allowed;
            switch (HighLogic.LoadedScene) {
                case GameScenes.FLIGHT:
                    allowed = (CameraManager.Instance != null) &&
                        (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA);
                    break;

                case GameScenes.EDITOR:
                    allowed = true;
                    break;

                default:
                    allowed = false;
                    break;
            }
            return allowed;
        }

        /// <summary>
        /// Convert a device position to Unity world coordinates for this scene.
        /// </summary>
        /// <param name="devicePosition">Device position in the device space coordinate system.</param>
        /// <returns>Unity world position corresponding to the device position.</returns>
        public static Vector3 DevicePoseToWorld(Vector3 devicePosition) {
            return InitialPosition + InitialRotation * devicePosition;
        }

        /// <summary>
        /// Convert a device rotation to Unity world coordinates for this scene.
        /// </summary>
        /// <param name="deviceRotation">Device rotation in the device space coordinate system.</param>
        /// <returns>Unity world rotation corresponding to the device rotation.</returns>
        public static Quaternion DevicePoseToWorld(Quaternion deviceRotation) {
            return InitialRotation * deviceRotation;
        }
    }
}