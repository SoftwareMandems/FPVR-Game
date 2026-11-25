using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FPVR
{
    public class DroneView : MonoBehaviour
    {
        public DroneFlight drone;
        public Camera view;
        [Tooltip("FPV camera mount in the drone's local axes: X right, Y up, Z forward.")]
        public Vector3 cameraOffset = new Vector3(0, .18f, .24f);
        public Vector3 chaseOffset = new Vector3(0, 2.2f, -4);
        VrFpvDisplay headsetDisplay;
        bool chase;
        public bool IsVR => XRSettings.isDeviceActive;

        void Start()
        {
            if (drone && view) return;
            Debug.LogError("Pilot Rig requires Drone and View references.", this);
            enabled = false;
        }

        void LateUpdate()
        {
            bool xr = IsVR;
            if (xr && headsetDisplay == null) headsetDisplay = new VrFpvDisplay(view);
            else if (!xr) CloseHeadsetDisplay();
            headsetDisplay?.SetComfortBorder(VrComfortSettings.Enabled);

            var keyboard = Keyboard.current;
            if (keyboard != null && !FlightSettingsMenu.IsBlockingInput && keyboard.cKey.wasPressedThisFrame)
                chase = !chase;

            if (!xr && chase)
            {
                Quaternion yaw = Quaternion.Euler(0, drone.transform.eulerAngles.y, 0);
                transform.position = drone.transform.position + yaw * chaseOffset;
                transform.LookAt(drone.transform.position + Vector3.up * .1f);
            }
            else
            {
                // The headset displays this feed without changing the drone camera's pose.
                transform.SetPositionAndRotation(drone.transform.TransformPoint(cameraOffset), drone.transform.rotation);
            }
        }

        void OnDisable() => CloseHeadsetDisplay();

        void CloseHeadsetDisplay()
        {
            headsetDisplay?.Dispose();
            headsetDisplay = null;
        }
    }
}
