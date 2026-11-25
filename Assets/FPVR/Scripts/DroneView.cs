using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace FPVR
{
    public class DroneView : MonoBehaviour
    {
        public DroneFlight drone;
        public Camera view;
        public Transform trackingOffset;
        [Tooltip("FPV camera mount in the drone's local axes: X right, Y up, Z forward.")]
        public Vector3 cameraOffset = new Vector3(0, .18f, .24f);
        public Vector3 chaseOffset = new Vector3(0, 2.2f, -4);
        TrackedPoseDriver tracking;
        bool xr, chase;
        public bool IsVR => xr;
        void Start()
        {
            if (!drone || !view || !trackingOffset)
            { Debug.LogError("Pilot Rig requires Drone, View and Tracking Offset references.", this); enabled = false; return; }
            tracking = view.GetComponent<TrackedPoseDriver>();
            if (!tracking) tracking = view.gameObject.AddComponent<TrackedPoseDriver>();
            tracking.enabled = false;
            tracking.positionInput = new InputActionProperty(new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
            tracking.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            tracking.ignoreTrackingState = true;
        }
        void LateUpdate()
        {
            if (!tracking) return;
            bool active = XRSettings.isDeviceActive;
            if (active != xr)
            {
                xr = active; tracking.enabled = xr;
                if (xr) { Recenter(); Invoke(nameof(Recenter), .5f); }
                else
                {
                    trackingOffset.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    view.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }
            var k = Keyboard.current;
            if (k != null && k.cKey.wasPressedThisFrame) chase = !chase;
            if (k != null && k.vKey.wasPressedThisFrame && xr) Recenter();
            if (!xr && chase)
            {
                Quaternion yaw = Quaternion.Euler(0, drone.transform.eulerAngles.y, 0);
                transform.position = drone.transform.position + yaw * chaseOffset;
                transform.LookAt(drone.transform.position + Vector3.up * .1f);
            }
            else
            {
                // Follow the full drone attitude, including pitch, roll and inverted flight.
                // VR head tracking remains relative to this mount through its child transforms.
                transform.SetPositionAndRotation(drone.transform.TransformPoint(cameraOffset), drone.transform.rotation);
            }
        }
        public void Recenter()
        {
            if (!xr || !trackingOffset) return;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position)) return;
            if (!head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation)) return;
            trackingOffset.localRotation = Quaternion.Euler(0, -rotation.eulerAngles.y, 0);
            trackingOffset.localPosition = -(trackingOffset.localRotation * position);
        }
    }
}
