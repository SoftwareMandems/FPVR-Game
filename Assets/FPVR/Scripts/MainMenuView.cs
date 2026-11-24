using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
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
        [SerializeField] RawImage background;
        [SerializeField] RectTransform content;
        [SerializeField] XROrigin origin;
        [SerializeField] TrackedPoseDriver headPose;
        [SerializeField] GameObject controllerPointers;
        [SerializeField] ControllerPointer[] pointers;
        [SerializeField, Min(0.5f)] float headsetDistance = 1.85f;

        const InputTrackingState FullPose = InputTrackingState.Position | InputTrackingState.Rotation;

        Vector2 authoredPanelSize;
        Vector2 authoredContentAnchor;
        Vector3 authoredContentScale;
        Rect authoredBackgroundUv;
        float desktopAspect;
        bool aligned;

        void Awake()
        {
            authoredPanelSize = panel.rect.size;
            authoredContentAnchor = content.anchorMin;
            authoredContentScale = content.localScale;
            authoredBackgroundUv = background.uvRect;
        }

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
                RestoreWorldLayout();
                headPose.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                headPose.enabled = true;
                return;
            }

            // Align the tracking origin once. The artwork and UI stay at the saved world pose.
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

            float width = authoredPanelSize.y * aspect;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            // Shift the column above the drone as horizontal space runs out.
            float compact = Mathf.InverseLerp(1.30f, 1.0f, aspect);
            var anchor = new Vector2(authoredContentAnchor.x, Mathf.Lerp(authoredContentAnchor.y, 0.74f, compact));
            content.anchorMin = content.anchorMax = anchor;
            float columnWidth = width * Mathf.Lerp(0.36f, 0.79f, compact);
            float contentWidth = content.rect.width * authoredContentScale.x;
            content.localScale = authoredContentScale * Mathf.Min(1f, columnWidth / contentWidth);

            FitBackground(aspect, compact);

            float height = authoredPanelSize.y * panel.lossyScale.y;
            float distance = height / (2f * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f));
            view.transform.SetPositionAndRotation(panel.position - panel.forward * distance, panel.rotation);
        }

        void FitBackground(float aspect, float compact)
        {
            Texture texture = background.mainTexture;
            float imageAspect = (float)texture.width / texture.height;
            float width = Mathf.Min(1f, aspect / imageAspect);
            float height = Mathf.Min(1f, imageAspect / aspect);

            // Bias the crop toward the drone and keep the image scale uniform.
            float cropBias = Mathf.Lerp(0.88f, 0.72f, compact);
            background.uvRect = new Rect((1f - width) * cropBias, (1f - height) * 0.45f, width, height);
        }

        void RestoreWorldLayout()
        {
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, authoredPanelSize.x);
            content.anchorMin = content.anchorMax = authoredContentAnchor;
            content.localScale = authoredContentScale;
            background.uvRect = authoredBackgroundUv;
        }

        static bool HasFullPose(TrackedPoseDriver driver)
        {
            var state = (InputTrackingState)driver.trackingStateInput.action.ReadValue<int>();
            return (state & FullPose) == FullPose;
        }
    }
}
