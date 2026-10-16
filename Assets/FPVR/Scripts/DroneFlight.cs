using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FPVR
{
    [RequireComponent(typeof(Rigidbody))]
    public class DroneFlight : MonoBehaviour
    {
        public float maxTilt = 23f, climbSpeed = 3f, yawSpeed = 85f;
        public bool armed, crashed;
        public Rigidbody Body { get; private set; }
        Vector3 spawn; Quaternion spawnRotation;
        [Tooltip("Crash speed in metres per second")] public float crashSpeed = 3.5f;
        public System.Action ResetCourse;
        Vector2 tilt; float climb, turn, heading;
        bool previousArm, previousReset;
        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            spawn = transform.position; spawnRotation = transform.rotation; heading = transform.eulerAngles.y;
            Body.maxAngularVelocity = 8;
        }
        static float Dead(float value) => Mathf.Abs(value) < 0.12f ? 0 : value;
        void Update()
        {
            tilt = Vector2.zero; climb = turn = 0;
            bool arm = false, reset = false;
            var k = Keyboard.current;
            if (k != null)
            {
                tilt = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                    (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                climb = (k.eKey.isPressed ? 1 : 0) - (k.qKey.isPressed ? 1 : 0);
                turn = (k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0);
                arm = k.spaceKey.isPressed; reset = k.rKey.isPressed;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                if (g.rightStick.ReadValue().magnitude > .12f) tilt = g.rightStick.ReadValue();
                if (Mathf.Abs(g.leftStick.y.ReadValue()) > .12f) climb = g.leftStick.y.ReadValue();
                if (Mathf.Abs(g.leftStick.x.ReadValue()) > .12f) turn = g.leftStick.x.ReadValue();
                arm |= g.buttonSouth.isPressed; reset |= g.buttonNorth.isPressed;
            }
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 ls))
            { climb = Dead(ls.y); turn = Dead(ls.x); }
            if (right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 rs))
                tilt = new Vector2(Dead(rs.x), Dead(rs.y));
            if (right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool a)) arm |= a;
            if (left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool x)) reset |= x;
            if (reset && !previousReset) ResetDrone();
            else if (arm && !previousArm && !crashed) armed = !armed;
            previousArm = arm; previousReset = reset;
            if (transform.position.y < -5 || transform.position.y > 65 || Mathf.Abs(transform.position.x) > 95 || Mathf.Abs(transform.position.z) > 95)
                ResetDrone();
        }
        void FixedUpdate()
        {
            if (!armed || crashed) return;
            heading += turn * yawSpeed * Time.fixedDeltaTime;
            Quaternion target = Quaternion.Euler(0, heading, 0) * Quaternion.Euler(tilt.y * maxTilt, 0, -tilt.x * maxTilt);
            Quaternion error = target * Quaternion.Inverse(Body.rotation);
            if (error.w < 0) error = new Quaternion(-error.x, -error.y, -error.z, -error.w);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (axis.sqrMagnitude > .001f && !float.IsNaN(axis.x))
                Body.AddTorque(axis.normalized * (angle * Mathf.Deg2Rad * 22f) - Body.angularVelocity * 8f, ForceMode.Acceleration);
            float verticalAcceleration = Mathf.Clamp(9.81f + (climb * climbSpeed - Body.linearVelocity.y) * 5f, 0, 23);
            float upright = Mathf.Max(.4f, Vector3.Dot(transform.up, Vector3.up));
            Body.AddForce(transform.up * (verticalAcceleration / upright), ForceMode.Acceleration);
            // Assisted braking when the right stick is centred. This is not acro flight.
            if (tilt.sqrMagnitude < .02f)
                Body.AddForce(-Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up) * 1.5f, ForceMode.Acceleration);
        }
        public void ResetDrone()
        {
            Body.isKinematic = false;
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            Body.position = spawn; Body.rotation = spawnRotation;
            transform.SetPositionAndRotation(spawn, spawnRotation);
            heading = spawnRotation.eulerAngles.y; armed = crashed = false;
            ResetCourse?.Invoke();
        }
        void OnCollisionEnter(Collision c)
        {
            if (armed && c.relativeVelocity.magnitude > crashSpeed)
            {
                crashed = true; armed = false;
                Body.linearVelocity = Body.angularVelocity = Vector3.zero;
                Body.isKinematic = true;
            }
        }
        void OnApplicationFocus(bool focus) { if (!focus) armed = false; }
    }
}
