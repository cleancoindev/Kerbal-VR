﻿using System;
using UnityEngine;
using KerbalVR.Components;

namespace KerbalVR.Modules
{
    public class KVR_ToggleSwitchCover : InternalModule, IActionableCollider
    {

        #region Types
        public enum CoverState
        {
            Closed,
            Open,
        }

        public enum CoverStateInput
        {
            ColliderEnter,
            FinishedAction,
        }

        public enum CoverFSMState
        {
            IsClosed,
            IsOpening,
            IsOpen,
            IsClosing,
        }

        public enum SwitchState
        {
            Up,
            Down,
        }
        #endregion


        #region KSP Config Fields
        [KSPField]
        public string coverAnimationName = string.Empty;
        [KSPField]
        public string transformCoverCollider = string.Empty;
        [KSPField]
        public string switchAnimationName = string.Empty;
        [KSPField]
        public string transformSwitchCollider = string.Empty;
        #endregion


        #region Properties
        public CoverState CurrentCoverState { get; private set; }
        public SwitchState CurrentSwitchState { get; private set; }
        #endregion


        #region Private Members
        private ConfigNode moduleConfigNode;

        private Animation coverAnimation;
        private AnimationState coverAnimationState;
        private GameObject coverGameObject;
        private CoverFSMState coverFSMState;
        private float targetCoverAnimationEndTime;
        private bool isCoverAnimationPlayingPrev;

        private Animation switchAnimation;
        private AnimationState switchAnimationState;
        private GameObject switchGameObject;
        private float targetSwitchAnimationEndTime;
        private bool isSwitchAnimationPlayingPrev;
        #endregion


        void Start() {
            // no setup needed in editor mode
            if (HighLogic.LoadedScene == GameScenes.EDITOR) return;

            // retrieve the animations
            Animation[] animations = internalProp.FindModelAnimators(coverAnimationName);
            if (animations.Length > 0) {
                coverAnimation = animations[0];
                coverAnimationState = coverAnimation[coverAnimationName];
                coverAnimationState.wrapMode = WrapMode.Once;
            } else {
                Utils.LogWarning("KVR_PushButton (" + gameObject.name + ") has no animation \"" + coverAnimationName + "\"");
            }

            animations = internalProp.FindModelAnimators(switchAnimationName);
            if (animations.Length > 0) {
                switchAnimation = animations[0];
                switchAnimationState = switchAnimation[switchAnimationName];
                switchAnimationState.wrapMode = WrapMode.Once;
            } else {
                Utils.LogWarning("KVR_PushButton (" + gameObject.name + ") has no animation \"" + switchAnimationName + "\"");
            }

            // retrieve the collider GameObjects
            Transform colliderTransform = internalProp.FindModelTransform(transformCoverCollider);
            if (colliderTransform != null) {
                coverGameObject = colliderTransform.gameObject;
                coverGameObject.AddComponent<KVR_ActionableCollider>().module = this;
            } else {
                Utils.LogWarning("KVR_PushButton (" + gameObject.name + ") has no cover collider \"" + transformCoverCollider + "\"");
            }

            colliderTransform = internalProp.FindModelTransform(transformSwitchCollider);
            if (colliderTransform != null) {
                switchGameObject = colliderTransform.gameObject;
                switchGameObject.AddComponent<KVR_ActionableCollider>().module = this;
            } else {
                Utils.LogWarning("KVR_PushButton (" + gameObject.name + ") has no switch collider \"" + transformSwitchCollider + "\"");
            }

            // set initial state
            isCoverAnimationPlayingPrev = false;
            coverFSMState = CoverFSMState.IsClosed;
            targetCoverAnimationEndTime = 0f;
            GoToCoverState(CoverState.Closed);

            isSwitchAnimationPlayingPrev = false;
            targetSwitchAnimationEndTime = 0f;
            GoToSwitchState(SwitchState.Down);

            // create labels
            CreateLabels();
        }

        public override void OnUpdate() {
            bool isCoverAnimationPlaying = coverAnimation.isPlaying;
            bool isSwitchAnimationPlaying = switchAnimation.isPlaying;

            if (!isCoverAnimationPlaying && isCoverAnimationPlayingPrev) {
                UpdateCoverFSM(CoverStateInput.FinishedAction);
            }

            isCoverAnimationPlayingPrev = isCoverAnimationPlaying;
            isSwitchAnimationPlayingPrev = isSwitchAnimationPlaying;
        }

        private void UpdateCoverFSM(CoverStateInput input) {
            switch (coverFSMState) {
                case CoverFSMState.IsClosed:
                    if (input == CoverStateInput.ColliderEnter) {
                        coverFSMState = CoverFSMState.IsOpening;
                        PlayToCoverState(CoverState.Open);
                    }
                    break;

                case CoverFSMState.IsOpening:
                    if (input == CoverStateInput.FinishedAction) {
                        coverFSMState = CoverFSMState.IsOpen;
                    }
                    break;

                case CoverFSMState.IsOpen:
                    if (input == CoverStateInput.ColliderEnter) {
                        coverFSMState = CoverFSMState.IsClosing;
                        PlayToCoverState(CoverState.Closed);
                    }
                    break;

                case CoverFSMState.IsClosing:
                    if (input == CoverStateInput.FinishedAction) {
                        coverFSMState = CoverFSMState.IsClosed;
                    }
                    break;

                default:
                    break;
            }
        }

        public void OnColliderEntered(Collider thisObject, Collider otherObject) {
            if (DeviceManager.IsManipulator(otherObject.gameObject)) {

                if (thisObject.gameObject == coverGameObject) {
                    // when cover is closed, can only be opened from the
                    // bottom edge of the collider on the top side.
                    // when cover is open, can only be opened from the top
                    // side of the collider.
                    Vector3 manipulatorDeltaPos = coverGameObject.transform.InverseTransformPoint(
                        otherObject.transform.position);
                    if ((CurrentCoverState == CoverState.Closed &&
                        manipulatorDeltaPos.z > 0f) ||
                        (CurrentCoverState == CoverState.Open &&
                        manipulatorDeltaPos.y > 0f))
                        UpdateCoverFSM(CoverStateInput.ColliderEnter);

                } else if (CurrentCoverState == CoverState.Open &&
                    thisObject.gameObject == switchGameObject) {
                    // switch is pressed while cover is OPEN, and collider
                    // has entered from the top side of the switch
                    Vector3 manipulatorDeltaPos = switchGameObject.transform.InverseTransformPoint(
                        otherObject.transform.position);

                    if (manipulatorDeltaPos.z > 0f) {
                        PlayToSwitchState(SwitchState.Up);
                    }
                }
            }
        }

        public void OnColliderExited(Collider thisObject, Collider otherObject) {
            if (DeviceManager.IsManipulator(otherObject.gameObject)) {
                if (thisObject.gameObject == switchGameObject) {
                    PlayToSwitchState(SwitchState.Down);
                }
            }
        }

        public void SetCoverState(CoverState state) {
            CurrentCoverState = state;
        }

        private void GoToCoverState(CoverState state) {
            SetCoverState(state);

            // switch to animation state instantly
            coverAnimationState.normalizedTime = GetNormalizedTimeForCoverState(state);
            coverAnimationState.speed = 0f;
            coverAnimation.Play(coverAnimationName);
        }

        private void PlayToCoverState(CoverState state) {

            // set the animation time that we want to play to
            targetCoverAnimationEndTime = GetNormalizedTimeForCoverState(state);

            // note that the normalizedTime always resets to zero after finishing the clip.
            // so if cover was at Open and it was already done playing, its normalizedTime is
            // 0f, even though the Open state corresponds to a time of 1f. so, for this special
            // case, force it to 1f.
            if (CurrentCoverState == CoverState.Open &&
                coverAnimationState.normalizedTime == 0f &&
                !coverAnimation.isPlaying) {
                coverAnimationState.normalizedTime = 1f;
            }

            // move either up or down depending on where the switch is right now
            coverAnimationState.speed =
                Mathf.Sign(targetCoverAnimationEndTime - coverAnimationState.normalizedTime) * 1f;

            // play animation and actuate switch
            coverAnimation.Play(coverAnimationName);
            SetCoverState(state);
        }

        public void SetSwitchState(SwitchState state) {
            CurrentSwitchState = state;
        }

        private void GoToSwitchState(SwitchState state) {
            SetSwitchState(state);

            // switch to animation state instantly
            switchAnimationState.normalizedTime = GetNormalizedTimeForSwitchState(state);
            switchAnimationState.speed = 0f;
            switchAnimation.Play(switchAnimationName);
        }

        private void PlayToSwitchState(SwitchState state) {

            // set the animation time that we want to play to
            targetSwitchAnimationEndTime = GetNormalizedTimeForSwitchState(state);

            // note that the normalizedTime always resets to zero after finishing the clip.
            // so if button was at Pressed and it was already done playing, its normalizedTime is
            // 0f, even though the Pressed state corresponds to a time of 1f. so, for this special
            // case, force it to 1f.
            if (CurrentSwitchState == SwitchState.Up &&
                switchAnimationState.normalizedTime == 0f &&
                !switchAnimation.isPlaying) {
                switchAnimationState.normalizedTime = 1f;
            }

            // move either up or down depending on where the switch is right now
            switchAnimationState.speed =
                Mathf.Sign(targetSwitchAnimationEndTime - switchAnimationState.normalizedTime) * 1f;

            // play animation and actuate switch
            switchAnimation.Play(switchAnimationName);
            SetSwitchState(state);
        }

        private float GetNormalizedTimeForCoverState(CoverState state) {
            float targetTime = 0f;
            switch (state) {
                case CoverState.Open:
                    targetTime = 1f;
                    break;
                case CoverState.Closed:
                    targetTime = 0f;
                    break;
            }
            return targetTime;
        }

        private float GetNormalizedTimeForSwitchState(SwitchState state) {
            float targetTime = 0f;
            switch (state) {
                case SwitchState.Up:
                    targetTime = 1f;
                    break;
                case SwitchState.Down:
                    targetTime = 0f;
                    break;
            }
            return targetTime;
        }

        private void CreateLabels() {
            // create labels from the module configuration
            moduleConfigNode = ConfigUtils.GetModuleConfigNode(internalProp.name, moduleID);
            ConfigNode[] labelNodes = moduleConfigNode.GetNodes("KVR_LABEL");
            for (int i = 0; i < labelNodes.Length; i++) {
                CreateLabelFromConfig(labelNodes[i], i);
            }
        }

        private void CreateLabelFromConfig(ConfigNode cfgLabelNode, int id = 0) {
            // label text
            string labelText = "";
            bool success = cfgLabelNode.TryGetValue("text", ref labelText);

            // label transform (where to place the label)
            string cfgLabelTransform = "";
            success = cfgLabelNode.TryGetValue("transform", ref cfgLabelTransform);
            if (!success) throw new ArgumentException("Transform not specified for label " + labelText + " (" + cfgLabelNode.id + ")");

            Transform labelTransform = internalProp.FindModelTransform(cfgLabelTransform);
            if (labelTransform == null) throw new ArgumentException("Transform not found for label " + labelText + " (prop: " + internalProp.name + ")");

            // position offset from the transform
            Vector3 labelPositionOffset = Vector3.zero;
            success = cfgLabelNode.TryGetValue("transformPositionOffset", ref labelPositionOffset);

            // rotation offset from the transform
            Quaternion labelRotationOffset = Quaternion.identity;
            success = cfgLabelNode.TryGetValue("transformRotationOffset", ref labelRotationOffset);

            // font size
            float labelFontSize = 0.14f;
            success = cfgLabelNode.TryGetValue("fontSize", ref labelFontSize);

            // font style (bold, italics, etc)
            TMPro.FontStyles labelFontStyle = TMPro.FontStyles.Normal;
            success = cfgLabelNode.TryGetEnum("fontStyle", ref labelFontStyle, TMPro.FontStyles.Normal);

            // font alignment
            TMPro.TextAlignmentOptions labelAlignment = TMPro.TextAlignmentOptions.Center;
            success = cfgLabelNode.TryGetEnum("alignment", ref labelAlignment, TMPro.TextAlignmentOptions.Center);

            // canvas pivot (anchor point on the canvas)
            Vector2 labelPivot = new Vector2(0.5f, 0.5f);
            success = cfgLabelNode.TryGetValue("pivot", ref labelPivot);

            // size of the label canvas
            Vector2 labelSize = new Vector2(0.2f, 0.2f);
            success = cfgLabelNode.TryGetValue("canvasSize", ref labelSize);

            CreateLabel(id, labelText, labelFontSize, labelFontStyle,
                labelAlignment, labelTransform, labelPositionOffset,
                labelRotationOffset, labelPivot, labelSize);
        }

        private GameObject CreateLabel(
            int id,
            string text,
            float fontSize,
            TMPro.FontStyles fontStyle,
            TMPro.TextAlignmentOptions alignment,
            Transform labelTransform,
            Vector3 labelPositionOffset,
            Quaternion labelRotationOffset,
            Vector2 rectPivot,
            Vector2 rectSize) {

            string labelName = internalProp.name + "-" + internalProp.propID + "-" + id;

            GameObject labelGameObject = new GameObject(labelName);
            labelGameObject.layer = 20;
            labelGameObject.transform.SetParent(labelTransform);

            TMPro.TextMeshPro tmpLabel = labelGameObject.AddComponent<TMPro.TextMeshPro>();
            tmpLabel.SetText(text);
            tmpLabel.fontSize = fontSize;
            tmpLabel.fontStyle = fontStyle;
            tmpLabel.alignment = alignment;

            tmpLabel.rectTransform.pivot = rectPivot;
            tmpLabel.rectTransform.localPosition = labelPositionOffset;
            tmpLabel.rectTransform.localRotation = labelRotationOffset;
            tmpLabel.rectTransform.sizeDelta = rectSize;

            // find and set font
            TMPro.TMP_FontAsset font = Resources.Load("Font/LiberationSans SDF", typeof(TMPro.TMP_FontAsset)) as TMPro.TMP_FontAsset;
            if (font != null) {
                tmpLabel.font = font;
            }

            return labelGameObject;
        }
    }
}
