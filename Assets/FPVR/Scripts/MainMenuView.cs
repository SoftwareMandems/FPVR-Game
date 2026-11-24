using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace FPVR
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [Serializable]
        struct ControllerPointer
        {
            public TrackedPoseDriver pose;
            public GameObject ray;
        }

        [SerializeField] RectTransform panel;
        [SerializeField] XROrigin origin;
        [SerializeField] TrackedPoseDriver headPose;
        [SerializeField] GameObject controllerPointers;
        [SerializeField] ControllerPointer[] pointers;
        [SerializeField, Min(0.5f)] float headsetDistance = 1.85f;

        const InputTrackingState FullPose = InputTrackingState.Position | InputTrackingState.Rotation;

        float desktopAspect;
        bool aligned;

        void LateUpdate()
        {
            if (!XRSettings.isDeviceActive)
            {
                if (headPose.enabled)
                {
                    headPose.enabled = false;
                    controllerPointers.SetActive(false);
                    foreach (var pointer in pointers) pointer.ray.SetActive(false);
                    aligned = false;
                    desktopAspect = 0f;
                }

                FitDesktopView();
                return;
            }

            if (!headPose.enabled)
            {
                headPose.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                headPose.enabled = true;
                return;
            }

            // Align the tracking origin once. The panel stays at its saved world pose.
            if (!aligned)
            {
                if (!HasFullPose(headPose)) return;

                origin.MatchOriginUpCameraForward(Vector3.up, panel.forward);
                origin.MoveCameraToWorldLocation(panel.position - panel.forward * headsetDistance + Vector3.up * 0.06f);
                controllerPointers.SetActive(true);
                aligned = true;
            }

            foreach (var pointer in pointers)
            {
                bool tracked = HasFullPose(pointer.pose);
                if (pointer.ray.activeSelf != tracked) pointer.ray.SetActive(tracked);
            }
        }

        void FitDesktopView()
        {
            Camera view = origin.Camera;
            float aspect = view.aspect;
            if (aspect <= 0f || Mathf.Approximately(desktopAspect, aspect)) return;
            desktopAspect = aspect;

            float width = panel.rect.width * panel.lossyScale.x;
            float height = panel.rect.height * panel.lossyScale.y;
            float fittedHeight = Mathf.Max(height / 0.84f, width / (aspect * 0.88f));
            float distance = fittedHeight / (2f * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f));
            view.transform.SetPositionAndRotation(panel.position - panel.forward * distance, panel.rotation);
        }

        static bool HasFullPose(TrackedPoseDriver driver)
        {
            var state = (InputTrackingState)driver.trackingStateInput.action.ReadValue<int>();
            return (state & FullPose) == FullPose;
        }
    }
}
