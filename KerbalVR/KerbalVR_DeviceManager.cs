using UnityEngine;
using Valve.VR;

namespace KerbalVR
{
    public class DeviceManager : MonoBehaviour
    {
        #region Properties
        // Manipulator Game Objects
        public GameObject ManipulatorLeft { get; private set; }
        public GameObject ManipulatorRight { get; private set; }

        // keep aliases of controller indices
        public uint ControllerIndexLeft { get; private set; }
        public uint ControllerIndexRight { get; private set; }
        #endregion

        // keep track of devices that are connected
        private bool[] isDeviceConnected = new bool[OpenVR.k_unMaxTrackedDeviceCount];
        

        #region Singleton
        // this is a singleton class, and there must be one EventManager in the scene
        private static DeviceManager _instance;
        public static DeviceManager Instance {
            get {
                if (_instance == null) {
                    _instance = FindObjectOfType<DeviceManager>();
                    if (_instance == null) {
                        Utils.LogError("The scene needs to have one active GameObject with a DeviceManager script attached!");
                    } else {
                        _instance.Initialize();
                    }
                }
                return _instance;
            }
        }

        // first-time initialization for this singleton class
        private void Initialize() {
            if (isDeviceConnected == null) {
                isDeviceConnected = new bool[OpenVR.k_unMaxTrackedDeviceCount];
            }

            ControllerIndexLeft = OpenVR.k_unTrackedDeviceIndexInvalid;
            ControllerIndexRight = OpenVR.k_unTrackedDeviceIndexInvalid;
        }
        #endregion

        void OnEnable() {
            SteamVR_Events.NewPoses.Listen(OnDevicePosesReady);
            SteamVR_Events.DeviceConnected.Listen(OnDeviceConnected);
            SteamVR_Events.System(EVREventType.VREvent_TrackedDeviceRoleChanged).Listen(OnTrackedDeviceRoleChanged);
        }

        void OnDisable() {
            SteamVR_Events.NewPoses.Remove(OnDevicePosesReady);
            SteamVR_Events.DeviceConnected.Remove(OnDeviceConnected);
            SteamVR_Events.System(EVREventType.VREvent_TrackedDeviceRoleChanged).Remove(OnTrackedDeviceRoleChanged);
        }

        private void OnDevicePosesReady(TrackedDevicePose_t[] devicePoses) {
            // detect devices that have (dis)connected
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++) {
                bool isConnected = devicePoses[i].bDeviceIsConnected;
                if (isDeviceConnected[i] != isConnected) {
                    SteamVR_Events.DeviceConnected.Send((int)i, isConnected);
                }
                isDeviceConnected[i] = isConnected;
            }

            // update poses for tracked devices
            SteamVR_Controller.Update();

            // update Manipulator objects' state
            if (DeviceIndexIsValid(ControllerIndexLeft)) {
                SteamVR_Utils.RigidTransform controllerPose = new SteamVR_Utils.RigidTransform(
                    devicePoses[ControllerIndexLeft].mDeviceToAbsoluteTracking);
                SteamVR_Controller.Device controllerState = 
                    SteamVR_Controller.Input((int)ControllerIndexLeft);

                ManipulatorLeft.GetComponent<Manipulator>().UpdateState(controllerPose, controllerState);
            }

            if (DeviceIndexIsValid(ControllerIndexRight)) {
                SteamVR_Utils.RigidTransform controllerPose = new SteamVR_Utils.RigidTransform(
                    devicePoses[ControllerIndexRight].mDeviceToAbsoluteTracking);
                SteamVR_Controller.Device controllerState =
                    SteamVR_Controller.Input((int)ControllerIndexRight);
                
                ManipulatorRight.GetComponent<Manipulator>().UpdateState(controllerPose, controllerState);
            }
        }

        private void OnTrackedDeviceRoleChanged(VREvent_t vrEvent) {
            // re-check controller indices
            ControllerIndexLeft = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            ControllerIndexRight = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);

            ManageManipulators();
        }

        private void OnDeviceConnected(int deviceIndex, bool isConnected) {
            Utils.Log("Device " + deviceIndex + " (" +
                OpenVR.System.GetTrackedDeviceClass((uint)deviceIndex) +
                ") is " + (isConnected ? "connected" : "disconnected"));
        }

        private bool DeviceIndexIsValid(uint deviceIndex) {
            // return deviceIndex < OpenVR.k_unMaxTrackedDeviceCount;
            return deviceIndex != OpenVR.k_unTrackedDeviceIndexInvalid;
        }

        private GameObject CreateManipulator(ETrackedControllerRole role) {
            // create new GameObject
            GameObject manipulator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            manipulator.name = "VR_Manipulator_" + role.ToString();
            DontDestroyOnLoad(manipulator);

            // define the render model
            manipulator.transform.localScale = Vector3.one *
                ((role == ETrackedControllerRole.RightHand) ? 0.02f : 0.08f);
            Color manipulatorColor = (role == ETrackedControllerRole.RightHand) ? Color.green : Color.red;
            MeshRenderer manipulatorRenderer = manipulator.GetComponent<MeshRenderer>();
            manipulatorRenderer.material.color = manipulatorColor;

            // define the collider
            Rigidbody manipulatorRigidbody = manipulator.AddComponent<Rigidbody>();
            manipulatorRigidbody.isKinematic = true;
            SphereCollider manipulatorCollider = manipulator.GetComponent<SphereCollider>();
            manipulatorCollider.isTrigger = true;

            // define the Manipulator component
            Manipulator manipulatorComponent = manipulator.AddComponent<Manipulator>();
            manipulatorComponent.role = role;
            manipulatorComponent.originalColor = manipulatorColor;
            manipulatorComponent.activeColor = Color.yellow;

#if DEBUG
            manipulatorRenderer.enabled = true;
#else
            manipulatorRenderer.enabled = false;
#endif

            return manipulator;
        }

        private void ManageManipulators() {
            if (DeviceIndexIsValid(ControllerIndexLeft) && ManipulatorLeft == null) {
                ManipulatorLeft = CreateManipulator(ETrackedControllerRole.LeftHand);
            } else if (!DeviceIndexIsValid(ControllerIndexLeft) && ManipulatorLeft != null) {
                Destroy(ManipulatorLeft);
            }

            if (DeviceIndexIsValid(ControllerIndexRight) && ManipulatorRight == null) {
                ManipulatorRight = CreateManipulator(ETrackedControllerRole.RightHand);
            } else if (!DeviceIndexIsValid(ControllerIndexRight) && ManipulatorRight != null) {
                Destroy(ManipulatorRight);
            }
        }
    }
}
