﻿using System;
using UnityEngine;
using Valve.VR;

namespace KerbalVR
{
    public class Manipulator : MonoBehaviour
    {
        // TODO: think about these fields
        public Color originalColor = Color.white;
        public Color activeColor = Color.black;
        public float movementVelocity = 1f;

        public ETrackedControllerRole role;

        private MeshRenderer meshRenderer;

        void Awake() {
            // Utils.Log("Manipulator alive.");
            meshRenderer = GetComponent<MeshRenderer>();
        }

        void Update() {
#if DEBUG
            meshRenderer.enabled = KerbalVR.HmdIsEnabled;
#endif
        }

        public void UpdateState(SteamVR_Utils.RigidTransform pose, SteamVR_Controller.Device state) {
            // position the controller object
            transform.position = Scene.DevicePoseToWorld(pose.pos);
            transform.rotation = Scene.DevicePoseToWorld(pose.rot);

            // set the layer to render to
            gameObject.layer = Scene.RenderLayer;

            // update individual manipulator states (hand controls)
            if (role == ETrackedControllerRole.LeftHand) {
                UpdateStateLeft(pose, state);
            } else if (role == ETrackedControllerRole.RightHand) {
                UpdateStateRight(pose, state);
            } else {
                throw new Exception("Unsupported controller role: " + role);
            }
        }

        void UpdateStateLeft(SteamVR_Utils.RigidTransform pose, SteamVR_Controller.Device state) {
            if (state.GetPress(EVRButtonId.k_EButton_SteamVR_Touchpad)) {
                Vector2 touchAxis = state.GetAxis(EVRButtonId.k_EButton_SteamVR_Touchpad);

                Vector3 upDisplacement = Vector3.up * (movementVelocity * touchAxis.y) * Time.deltaTime;

                Vector3 newPosition = Scene.CurrentPosition + upDisplacement;
                if (newPosition.y < 0f) newPosition.y = 0f;

                Scene.CurrentPosition = newPosition;
            }
        }

        void UpdateStateRight(SteamVR_Utils.RigidTransform pose, SteamVR_Controller.Device state) {
            if (state.GetPress(EVRButtonId.k_EButton_SteamVR_Touchpad)) {
                Vector2 touchAxis = state.GetAxis(EVRButtonId.k_EButton_SteamVR_Touchpad);

                Vector3 fwdDirection = Scene.HmdRotation * Vector3.forward;
                fwdDirection.y = 0f; // allow only planar movement
                Vector3 fwdDisplacement = fwdDirection.normalized * (movementVelocity * touchAxis.y) * Time.deltaTime;

                Vector3 rightDirection = Scene.HmdRotation * Vector3.right;
                rightDirection.y = 0f; // allow only planar movement
                Vector3 rightDisplacement = rightDirection.normalized * (movementVelocity * touchAxis.x) * Time.deltaTime;

                Scene.CurrentPosition += fwdDisplacement + rightDisplacement;
            }

            if (state.GetPressDown(EVRButtonId.k_EButton_ApplicationMenu)) {
                KerbalVR.ResetInitialHmdPosition();
            }
        }

        void OnDestroy() {
            // Utils.Log("Manipulator " + role + " is being destroyed!");
        }

        void OnTriggerStay(Collider other) {
            // Utils.Log("OnTriggerEnter! " + other.name);

            MeshRenderer r = gameObject.GetComponent<MeshRenderer>();
            r.sharedMaterial.color = activeColor;
        }

        void OnTriggerExit(Collider other) {
            // Utils.Log("Manipulator OnTriggerExit " + other.name);

            MeshRenderer r = gameObject.GetComponent<MeshRenderer>();
            r.sharedMaterial.color = originalColor;
        }
    }
}
