﻿using System.Collections;
using UnityEngine;

namespace KerbalVR.Modules
{
    /// <summary>
    /// For a Toggle Switch, use a Finite-State Machine (FSM) to determine how
    /// to define its current operating state and how to animate the switch
    /// being flipped.
    /// 
    /// Basic operation:
    /// Assume two colliders, a switch "up" collider, and switch "down" collider.
    /// 
    /// When the up-collider is entered, followed by entering the down-collider,
    /// trigger the switch to flip downwards.
    /// 
    /// When the down-collider is entered, followed by entering the up-collider,
    /// trigger the switch to flip upwards.
    /// 
    /// This captures the gesture of swiping up/down to flip the switch.
    /// </summary>
    public class KVR_ToggleSwitchDouble : InternalModule {
        #region Types
        public enum SwitchState {
            Up,
            Down,
        }

        public enum SwitchStateInput {
            ColliderUpEnter,
            ColliderUpExit,
            ColliderDownEnter,
            ColliderDownExit,
        }

        public enum SwitchFSMState {
            IsUp,
            IsWaitingForDown,
            IsDown,
            IsWaitingForUp,
        }
        #endregion

        #region KSP Config Fields
        [KSPField]
        public string animationName = string.Empty;
        [KSPField]
        public string transformSwitchColliderUp = string.Empty;
        [KSPField]
        public string transformSwitchColliderDown = string.Empty;
        #endregion

        #region Properties
        public SwitchState switchState { get; private set; }
        #endregion

        #region Private Members
        private Animation switchAnimation;
        private AnimationState switchAnimationState;
        private GameObject switchUpGameObject;
        private GameObject switchDownGameObject;
        private SwitchFSMState switchFSMState;
        private float targetAnimationEndTime;
        #endregion

        /// <summary>
        /// Loads the animations and hooks into the colliders for this toggle switch.
        /// </summary>
        void Start() {
            // no setup needed in editor mode
            if (HighLogic.LoadedScene == GameScenes.EDITOR) return;

            // retrieve the animation
            Animation[] animations = internalProp.FindModelAnimators(animationName);
            if (animations.Length > 0) {
                switchAnimation = animations[0];
                switchAnimationState = switchAnimation[animationName];
                switchAnimationState.wrapMode = WrapMode.Once;
            } else {
                Utils.LogWarning("KVR_ToggleSwitchDouble (" + gameObject.name + ") has no animation \"" + animationName + "\"");
            }

            // retrieve the collider GameObjects
            Transform switchTransform = internalProp.FindModelTransform(transformSwitchColliderUp);
            if (switchTransform != null) {
                switchUpGameObject = switchTransform.gameObject;
                switchUpGameObject.AddComponent<KVR_ToggleSwitchCollider>().toggleSwitchComponent = this;
            } else {
                Utils.LogWarning("KVR_ToggleSwitchDouble (" + gameObject.name + ") has no switch collider \"" + transformSwitchColliderUp + "\"");
            }

            switchTransform = internalProp.FindModelTransform(transformSwitchColliderDown);
            if (switchTransform != null) {
                switchDownGameObject = switchTransform.gameObject;
                switchDownGameObject.AddComponent<KVR_ToggleSwitchCollider>().toggleSwitchComponent = this;
            } else {
                Utils.LogWarning("KVR_ToggleSwitchDouble (" + gameObject.name + ") has no switch collider \"" + transformSwitchColliderDown + "\"");
            }

            // set initial state
            targetAnimationEndTime = 0f;
            switchFSMState = SwitchFSMState.IsDown;
            SetState(SwitchState.Down);
        }

        void Update() {
            if (switchAnimation.isPlaying &&
                ((switchAnimationState.speed > 0f && switchAnimationState.normalizedTime >= targetAnimationEndTime) ||
                (switchAnimationState.speed < 0f && switchAnimationState.normalizedTime <= targetAnimationEndTime))) {

                switchAnimation.Stop();
            }
        }

        private void UpdateSwitchFSM(SwitchStateInput colliderInput) {
            switch (switchFSMState) {
                case SwitchFSMState.IsUp:
                    if (colliderInput == SwitchStateInput.ColliderUpEnter) {
                        switchFSMState = SwitchFSMState.IsWaitingForDown;
                    }
                    break;

                case SwitchFSMState.IsWaitingForDown:
                    if (colliderInput == SwitchStateInput.ColliderUpExit) {
                        switchFSMState = SwitchFSMState.IsUp;
                    } else if (colliderInput == SwitchStateInput.ColliderDownEnter) {
                        switchFSMState = SwitchFSMState.IsDown;
                        PlayToState(SwitchState.Down);
                    }
                    break;

                case SwitchFSMState.IsDown:
                    if (colliderInput == SwitchStateInput.ColliderDownEnter) {
                        switchFSMState = SwitchFSMState.IsWaitingForUp;
                    }
                    break;

                case SwitchFSMState.IsWaitingForUp:
                    if (colliderInput == SwitchStateInput.ColliderDownExit) {
                        switchFSMState = SwitchFSMState.IsDown;
                    } else if (colliderInput == SwitchStateInput.ColliderUpEnter) {
                        switchFSMState = SwitchFSMState.IsUp;
                        PlayToState(SwitchState.Up);
                    }
                    break;

                default:
                    break;
            }
        }

        public void SwitchColliderEntered(GameObject colliderObject) {
            if (colliderObject == switchUpGameObject) {
                UpdateSwitchFSM(SwitchStateInput.ColliderUpEnter);
            } else if (colliderObject == switchDownGameObject) {
                UpdateSwitchFSM(SwitchStateInput.ColliderDownEnter);
            }
        }

        public void SwitchColliderExited(GameObject colliderObject) {
            if (colliderObject == switchUpGameObject) {
                UpdateSwitchFSM(SwitchStateInput.ColliderUpExit);
            } else if (colliderObject == switchDownGameObject) {
                UpdateSwitchFSM(SwitchStateInput.ColliderDownExit);
            }
        }

        private void SetState(SwitchState state) {
            switchState = state;
            switchAnimationState.normalizedTime = GetNormalizedTimeForState(state);
            switchAnimationState.speed = 0f;
            switchAnimation.Play(animationName);
        }

        private void PlayToState(SwitchState state) {

            // set the animation time that we want to play to
            targetAnimationEndTime = GetNormalizedTimeForState(state);

            // note that the normalizedTime always resets to zero after finishing the clip.
            // so if switch was at Up and it was already done playing, its normalizedTime is
            // 0f, even though the Up state corresponds to a time of 1f. so, for this special
            // case, force set it to 1f.
            if (switchState == SwitchState.Up &&
                switchAnimationState.normalizedTime == 0f &&
                !switchAnimation.isPlaying) {
                switchAnimationState.normalizedTime = 1f;
            }

            // move either up or down depending on where the switch is right now
            switchAnimationState.speed =
                Mathf.Sign(targetAnimationEndTime - switchAnimationState.normalizedTime) * 1f;

            /*Utils.Log("Play to state " + state + ", current state = " + switchState +
                ", start = " + switchAnimationState.normalizedTime.ToString("F2") +
                ", end = " + targetAnimationEndTime.ToString("F1") +
                ", speed = " + switchAnimationState.speed.ToString("F1"));*/
            switchAnimation.Play(animationName);

            switchState = state;
        }

        private float GetNormalizedTimeForState(SwitchState state) {
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
    }
}
