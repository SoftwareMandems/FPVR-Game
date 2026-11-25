using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPVR
{
    internal sealed class VrFpvDisplay : IDisposable
    {
        const int FeedWidth = 1600;
        const int FeedHeight = 1000;
        const float ScreenDistance = 2f;
        const float ScreenWidth = 2.9f;
        const float ComfortBorderStrength = .55f;
        const int HeadsetRendererIndex = 1;
        static readonly int BorderStrength = Shader.PropertyToID("_BorderStrength");

        readonly Camera feed;
        readonly UniversalAdditionalCameraData feedData;
        readonly RenderTexture previousTarget;
        readonly int previousMask;
        readonly bool previousXrRendering;
        readonly RenderTexture feedTexture;
        readonly GameObject headset;
        readonly Material screenMaterial;
        readonly InputAction headPosition, headRotation, trackingState;
        bool borderEnabled;

        public VrFpvDisplay(Camera feed)
        {
            this.feed = feed;
            feedData = feed.GetUniversalAdditionalCameraData();
            previousTarget = feed.targetTexture;
            previousMask = feed.cullingMask;
            previousXrRendering = feedData.allowXRRendering;

            int displayLayer = LayerMask.NameToLayer("FPV Headset Display");
            int displayMask = 1 << displayLayer;
            feedTexture = new RenderTexture(FeedWidth, FeedHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "FPV Drone Feed",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // Keep the flight camera and its HUD on a single drone-mounted feed.
            feedData.allowXRRendering = false;
            feed.targetTexture = feedTexture;
            feed.cullingMask &= ~displayMask;

            headset = new GameObject("FPV Headset Camera");
            headset.SetActive(false);
            SceneManager.MoveGameObjectToScene(headset, feed.gameObject.scene);
            var camera = headset.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.02f, .02f, .02f);
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 10f;
            camera.cullingMask = displayMask;
            camera.useOcclusionCulling = false;
            camera.depth = feed.depth + 1f;
            camera.allowHDR = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.allowXRRendering = true;
            cameraData.renderShadows = false;
            cameraData.SetRenderer(HeadsetRendererIndex);

            var tracker = headset.AddComponent<TrackedPoseDriver>();
            headPosition = new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition");
            headRotation = new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
            trackingState = new InputAction("Head Tracking State", InputActionType.Value, "<XRHMD>/trackingState");
            tracker.positionInput = new InputActionProperty(headPosition);
            tracker.rotationInput = new InputActionProperty(headRotation);
            tracker.trackingStateInput = new InputActionProperty(trackingState);

            // A headset-relative screen displays the same FPV image to both eyes.
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "FPV Screen";
            screen.layer = displayLayer;
            screen.transform.SetParent(headset.transform, false);
            screen.transform.localPosition = Vector3.forward * ScreenDistance;
            screen.transform.localScale = new Vector3(ScreenWidth, ScreenWidth * FeedHeight / FeedWidth, 1f);
            var collider = screen.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            screenMaterial = new Material(Resources.Load<Shader>("FpvHeadsetDisplay")) { mainTexture = feedTexture };
            screen.GetComponent<MeshRenderer>().sharedMaterial = screenMaterial;

            headset.SetActive(true);
        }

        public void SetComfortBorder(bool enabled)
        {
            if (borderEnabled == enabled) return;
            borderEnabled = enabled;
            screenMaterial.SetFloat(BorderStrength, enabled ? ComfortBorderStrength : 0f);
        }

        public void Dispose()
        {
            if (feed)
            {
                feed.targetTexture = previousTarget;
                feed.cullingMask = previousMask;
            }
            if (feedData) feedData.allowXRRendering = previousXrRendering;
            if (headset)
            {
                headset.SetActive(false);
                Object.Destroy(headset);
            }
            trackingState.Dispose();
            headRotation.Dispose();
            headPosition.Dispose();
            Object.Destroy(screenMaterial);
            Object.Destroy(feedTexture);
        }
    }
}
