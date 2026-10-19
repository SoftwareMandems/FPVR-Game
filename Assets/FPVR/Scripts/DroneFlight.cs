using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FPVR
{
    [RequireComponent(typeof(Rigidbody))]
    public class DroneFlight : MonoBehaviour
    {
        public float maxTilt = 23f, climbSpeed = 3f, yawSpeed = 85f;
        [Tooltip("How quickly keyboard input ramps up and down (per second). Lower is smoother.")]
        public float keyResponse = 5f;
        [Tooltip("How strongly the drone brakes when you let go of the movement controls.")]
        public float brake = 2.5f;
        [Tooltip("Off: game style (left stick moves, right stick turns and climbs). On: real drone Mode 2 (left stick throttle and yaw, right stick tilts).")]
        public bool droneStyleSticks;
        public bool armed, crashed;
        public Rigidbody Body { get; private set; }
        Vector3 spawn; Quaternion spawnRotation;
        [Tooltip("Crash speed in metres per second")] public float crashSpeed = 3.5f;
        public System.Action ResetCourse;
        Vector2 tilt, keyMove; float climb, turn, heading, keyClimb, keyTurn;
        bool previousReset;
        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            spawn = transform.position; spawnRotation = transform.rotation; heading = transform.eulerAngles.y;
            Body.maxAngularVelocity = 8;
        }
        static float Dead(float value) => Mathf.Abs(value) < 0.12f ? 0 : value;
        static float Keys(bool positive, bool negative) => (positive ? 1 : 0) - (negative ? 1 : 0);
        void Update()
        {
            bool reset = false;
            // Keyboard: W/S forward and back, A/D turn, Q/E strafe, Space up, Shift or Ctrl down.
            // Keys ramp in and out so taps feel smooth instead of jerky.
            Vector2 keyTarget = Vector2.zero; float climbTarget = 0, turnTarget = 0;
            var k = Keyboard.current;
            if (k != null)
            {
                keyTarget = new Vector2(Keys(k.eKey.isPressed, k.qKey.isPressed),
                    Keys(k.wKey.isPressed || k.upArrowKey.isPressed, k.sKey.isPressed || k.downArrowKey.isPressed));
                climbTarget = Keys(k.spaceKey.isPressed, k.leftShiftKey.isPressed || k.leftCtrlKey.isPressed);
                turnTarget = Keys(k.dKey.isPressed || k.rightArrowKey.isPressed, k.aKey.isPressed || k.leftArrowKey.isPressed);
                reset = k.rKey.isPressed;
            }
            float step = keyResponse * Time.deltaTime;
            keyMove = Vector2.MoveTowards(keyMove, Vector2.ClampMagnitude(keyTarget, 1), step);
            keyClimb = Mathf.MoveTowards(keyClimb, climbTarget, step);
            keyTurn = Mathf.MoveTowards(keyTurn, turnTarget, step);
            tilt = keyMove; climb = keyClimb; turn = keyTurn;
            bool input = keyTarget != Vector2.zero || climbTarget != 0 || turnTarget != 0;

            var g = Gamepad.current;
            if (g != null)
            {
                input |= ApplySticks(g.leftStick.ReadValue(), g.rightStick.ReadValue(), g.rightTrigger.ReadValue() - g.leftTrigger.ReadValue());
                reset |= g.buttonNorth.isPressed;
            }
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 ls);
            right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 rs);
            left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float lt);
            right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float rt);
            input |= ApplySticks(ls, rs, Dead(rt) - Dead(lt));
            if (left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool x)) reset |= x;

            if (reset && !previousReset) ResetDrone();
            // Motors start automatically the moment you touch any flight control.
            else if (input && !armed && !crashed) armed = true;
            previousReset = reset;
            if (transform.position.y < -5 || transform.position.y > 65 || Mathf.Abs(transform.position.x) > 95 || Mathf.Abs(transform.position.z) > 95)
                ResetDrone();
        }
        // Applies a pair of thumbsticks (gamepad or VR) on top of the keyboard. Returns true if any were used.
        bool ApplySticks(Vector2 leftStick, Vector2 rightStick, float extraClimb)
        {
            leftStick = new Vector2(Dead(leftStick.x), Dead(leftStick.y));
            rightStick = new Vector2(Dead(rightStick.x), Dead(rightStick.y));
            Vector2 move; float lift, yaw;
            if (droneStyleSticks) { move = rightStick; lift = leftStick.y; yaw = leftStick.x; }
            else { move = leftStick; lift = rightStick.y; yaw = rightStick.x; }
            lift = Mathf.Clamp(lift + extraClimb, -1, 1);
            if (move != Vector2.zero) tilt = Vector2.ClampMagnitude(move, 1);
            if (lift != 0) climb = lift;
            if (yaw != 0) turn = yaw;
            return move != Vector2.zero || lift != 0 || yaw != 0;
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
            // Assisted braking when no movement input is held. This is not acro flight.
            if (tilt.sqrMagnitude < .02f)
                Body.AddForce(-Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up) * brake, ForceMode.Acceleration);
        }
        public void ResetDrone()
        {
            Body.isKinematic = false;
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            Body.position = spawn; Body.rotation = spawnRotation;
            transform.SetPositionAndRotation(spawn, spawnRotation);
            heading = spawnRotation.eulerAngles.y; armed = crashed = false;
            keyMove = Vector2.zero; keyClimb = keyTurn = 0;
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
