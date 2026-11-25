using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FPVR
{
    [RequireComponent(typeof(Rigidbody))]
    public class DroneFlight : MonoBehaviour
    {
        [Header("Quadrotor (metres, kilograms, seconds)")]
        [Min(0)] public float gravity = 9.81f;
        [Tooltip("Distance from the centre of mass to each motor in an X layout.")]
        [Min(.01f)] public float armLength = .22f;
        [Tooltip("Maximum thrust of ONE motor, in newtons.")]
        [Min(.01f)] public float maxMotorThrust = 6f;
        [Min(1)] public float maxMotorRpm = 24000f;
        [Tooltip("Motor/ESC time constant in seconds: RPM approaches its command exponentially.")]
        [Min(.001f)] public float motorTimeConstant = .035f;
        [Tooltip("Propeller reaction torque / thrust, in metres.")]
        [Min(0)] public float propellerTorqueRatio = .015f;
        [Tooltip("Axial inertia of ONE motor and propeller, in kg m².")]
        [Min(0)] public float rotorInertia = .0000015f;

        [Header("Air resistance")]
        [Min(0)] public float airDensity = 1.225f;
        [Tooltip("Drag coefficient times frontal area on local X/Y/Z, in m². Zero disables drag.")]
        public Vector3 dragArea = new Vector3(.018f, .035f, .018f);
        [Tooltip("Local X/Y/Z rotational drag coefficients, in N m / (rad/s)².")]
        public Vector3 angularDrag = new Vector3(.001f, .002f, .001f);

        [Header("Mode 2 / acro rate controller")]
        [Tooltip("Maximum body rotation rates in degrees/second: X pitch, Y yaw, Z roll.")]
        public Vector3 maxRates = new Vector3(240, 180, 240);
        [Range(0, 1)] public float stickExpo = .3f;
        [Range(0, .4f)] public float stickDeadzone = .08f;
        [Tooltip("Rate error to angular acceleration gain, in 1/seconds.")]
        [Min(0)] public float rateGain = 12f;
        [Tooltip("Integral rate-error gain, in 1/seconds². Corrects sustained aerodynamic loads.")]
        [Min(0)] public float rateIntegralGain = 4f;
        [Tooltip("Maximum accumulated rate error per axis, in radians. Prevents integral windup.")]
        [Min(0)] public float integralLimit = 1f;
        [Tooltip("Keyboard stick smoothing bandwidth, in 1/seconds.")]
        [Min(0)] public float keyResponse = 10f;
        [Tooltip("Crash speed in metres per second.")]
        [Min(0)] public float crashSpeed = 3.5f;

        public bool armed, crashed;
        public Rigidbody Body { get; private set; }
        public Vector2 Tilt => tilt;
        public float Climb => throttle;
        // Actual total thrust / level hover thrust; retained for the existing audio system.
        public float Thrust { get; private set; }
        // Actual motor-generated body torque, in N m (X pitch, Y yaw, Z roll).
        public Vector3 LocalTorque { get; private set; }
        public Vector2 LeftStick { get; private set; }
        public Vector2 RightStick { get; private set; }
        public System.Action ResetCourse;

        // Motor order: front-left, front-right, back-left, back-right.
        // Opposite diagonals counter-rotate, viewed along local +Y.
        static readonly float[] Spin = { -1, 1, 1, -1 };
        readonly float[] motorSpeed = new float[4]; // rad/s, magnitudes
        readonly float[] motorCommand = new float[4]; // newtons
        readonly float[] motorThrust = new float[4]; // actual mean thrust this physics step
        Vector3 spawn, rateIntegral;
        Quaternion spawnRotation;
        Vector2 tilt, keyTilt;
        float throttle, yaw, keyThrottle, keyYaw;
        bool previousReset, hasFocus = true, originalGravity;
        float originalLinearDamping, originalAngularDamping, originalMaxAngularVelocity;
        InputAction leftStickAction, rightStickAction, resetAction;

        public float GetMotorRpm(int index) => motorSpeed[index] * (60f / (2f * Mathf.PI));
        public float GetMotorThrust(int index) => motorThrust[index];

        static InputAction XRAction(string name, InputActionType type, params string[] bindings)
        {
            var action = new InputAction(name, type);
            foreach (var binding in bindings) action.AddBinding(binding);
            action.Enable();
            return action;
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            spawn = Body.position;
            spawnRotation = Body.rotation;
            originalGravity = Body.useGravity;
            originalLinearDamping = Body.linearDamping;
            originalAngularDamping = Body.angularDamping;
            originalMaxAngularVelocity = Body.maxAngularVelocity;
            ConfigureBody();
        }

        void ConfigureBody()
        {
            // Apply gravity once, explicitly world-down, without changing global Physics.gravity.
            // All other forces/torques are in physical units; collider geometry supplies inertia.
            Body.useGravity = false;
            Body.linearDamping = Body.angularDamping = 0;
            Body.maxAngularVelocity = 100f;
        }

        void OnEnable()
        {
            if (Body) ConfigureBody();
            leftStickAction = XRAction("Left Stick", InputActionType.Value,
                "<XRController>{LeftHand}/{Primary2DAxis}", "<XRController>{LeftHand}/thumbstick");
            rightStickAction = XRAction("Right Stick", InputActionType.Value,
                "<XRController>{RightHand}/{Primary2DAxis}", "<XRController>{RightHand}/thumbstick");
            resetAction = XRAction("Reset", InputActionType.Button,
                "<XRController>{LeftHand}/primaryButton", "<XRController>{LeftHand}/{PrimaryButton}");
        }

        void OnDisable()
        {
            leftStickAction?.Dispose(); rightStickAction?.Dispose(); resetAction?.Dispose();
            leftStickAction = rightStickAction = resetAction = null;
            armed = false;
            if (Body)
            {
                Body.useGravity = originalGravity;
                Body.linearDamping = originalLinearDamping;
                Body.angularDamping = originalAngularDamping;
                Body.maxAngularVelocity = originalMaxAngularVelocity;
            }
        }

        static Vector2 Larger(Vector2 a, Vector2 b) => a.sqrMagnitude >= b.sqrMagnitude ? a : b;
        static float Keys(bool positive, bool negative) => (positive ? 1 : 0) - (negative ? 1 : 0);
        float Dead(float value)
        {
            float magnitude = Mathf.Abs(value);
            return magnitude <= stickDeadzone ? 0 : Mathf.Sign(value) *
                Mathf.Clamp01((magnitude - stickDeadzone) / (1f - stickDeadzone));
        }

        void Update()
        {
            if (!hasFocus) return;
            bool reset = false;
            Vector2 targetTilt = Vector2.zero;
            float targetThrottle = 0, targetYaw = 0;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Mode 2 equivalents: Space/Shift throttle, A/D yaw, Q/E roll, W/S pitch.
                // Forward stick (W/up arrow) pitches the nose DOWN, as on an FPV transmitter.
                targetTilt = new Vector2(Keys(keyboard.eKey.isPressed, keyboard.qKey.isPressed),
                    Keys(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                        keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed));
                targetThrottle = Keys(keyboard.spaceKey.isPressed,
                    keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed || keyboard.leftCtrlKey.isPressed);
                targetYaw = Keys(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                    keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
                reset = keyboard.rKey.isPressed;
            }
            // Exact first-order response instead of frame-dependent increments; axes stay independent.
            float response = 1f - Mathf.Exp(-Mathf.Max(0, keyResponse) * Time.deltaTime);
            keyTilt = Vector2.Lerp(keyTilt, targetTilt, response);
            keyThrottle = Mathf.Lerp(keyThrottle, targetThrottle, response);
            keyYaw = Mathf.Lerp(keyYaw, targetYaw, response);
            tilt = keyTilt; throttle = keyThrottle; yaw = keyYaw;
            bool input = targetTilt != Vector2.zero || targetThrottle != 0 || targetYaw != 0;

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                input |= ApplySticks(gamepad.leftStick.ReadValue(), gamepad.rightStick.ReadValue());
                reset |= gamepad.buttonNorth.isPressed;
            }
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 ls);
            right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 rs);
            if (leftStickAction != null) ls = Larger(ls, leftStickAction.ReadValue<Vector2>());
            if (rightStickAction != null) rs = Larger(rs, rightStickAction.ReadValue<Vector2>());
            LeftStick = ls; RightStick = rs;
            input |= ApplySticks(ls, rs);
            if (left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool x)) reset |= x;
            reset |= resetAction != null && resetAction.IsPressed();

            if (reset)
            {
                if (!previousReset) ResetDrone();
            }
            else if (input && !armed && !crashed) armed = true;
            previousReset = reset;
        }

        bool ApplySticks(Vector2 left, Vector2 right)
        {
            left = new Vector2(Dead(left.x), Dead(left.y));
            right = new Vector2(Dead(right.x), Dead(right.y));
            if (right.x != 0) tilt.x = right.x;
            if (right.y != 0) tilt.y = right.y;
            if (left.y != 0) throttle = left.y;
            if (left.x != 0) yaw = left.x;
            return left != Vector2.zero || right != Vector2.zero;
        }

        float RateCurve(float value) => Mathf.Lerp(value, value * value * value, stickExpo);

        // Inertia maps local angular velocity/acceleration into local momentum/torque.
        Vector3 Inertia(Vector3 value)
        {
            Quaternion principalAxes = Body.inertiaTensorRotation;
            return principalAxes * Vector3.Scale(Body.inertiaTensor, Quaternion.Inverse(principalAxes) * value);
        }

        void FixedUpdate()
        {
            if (Body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            Quaternion rotation = Body.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 omega = inverse * Body.angularVelocity;
            Vector3 velocity = inverse * Body.linearVelocity;
            Body.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
            Vector3 drag = -.5f * airDensity * Vector3.Scale(dragArea, SquaredSigned(velocity));
            Body.AddForce(rotation * drag, ForceMode.Force);

            // Continuous control chain:
            // sticks -> body rates -> PI angular acceleration -> I*alpha -> four motor forces
            // -> RPM target -> dRPM/dt=(target-RPM)/tau -> F=kF*RPM² -> rigid-body motion.
            float hoverForce = Body.mass * gravity;
            float totalLimit = 4f * Mathf.Max(.01f, maxMotorThrust);
            float collective = throttle < 0 ? hoverForce * (1f + throttle) :
                Mathf.Lerp(hoverForce, totalLimit, throttle);
            collective = Mathf.Clamp(collective, 0, totalLimit);
            bool powered = armed && !crashed && collective > .0001f;
            if (powered)
            {
                Vector3 desiredRate = Vector3.Scale(maxRates, new Vector3(
                    RateCurve(tilt.y), RateCurve(yaw), -RateCurve(tilt.x))) * Mathf.Deg2Rad;
                Vector3 error = desiredRate - omega;
                Vector3 acceleration = error * rateGain + rateIntegral * rateIntegralGain;
                // Feed forward rigid-body gyroscopic coupling; the physical term below still runs
                // with motors OFF, so a freely tumbling body retains its angular momentum.
                Vector3 requestedTorque = Inertia(acceleration) + Vector3.Cross(omega, Inertia(omega));
                float authority = MixMotors(collective, requestedTorque);
                // Stop accumulating error when the motor allocator runs out of differential authority.
                if (authority >= .999f)
                    rateIntegral = ClampAxes(rateIntegral + error * dt, integralLimit);
            }
            else
            {
                rateIntegral = Vector3.zero;
                for (int i = 0; i < 4; i++) motorCommand[i] = 0;
            }

            Vector3 motorTorque = StepMotors(rotation, dt, out float rotorMomentum);
            // Euler's rigid-body equation: I*omegaDot = torque - omega x (I*omega + Hrotors).
            // Unity/PhysX integrates orientation as a quaternion; no Euler angles or angle targets.
            Vector3 gyroscopic = -Vector3.Cross(omega, Inertia(omega) + Vector3.up * rotorMomentum);
            Vector3 rotationalDrag = -Vector3.Scale(angularDrag, SquaredSigned(omega));
            Body.AddTorque(rotation * (gyroscopic + rotationalDrag), ForceMode.Force);
            LocalTorque = motorTorque;
            float actualForce = motorThrust[0] + motorThrust[1] + motorThrust[2] + motorThrust[3];
            Thrust = hoverForce > .0001f ? actualForce / hoverForce : 0;
        }

        // Keep total thrust set by throttle. Limit pitch/roll first, then fit yaw into the
        // remaining motor headroom; steering must never raise or lower collective thrust.
        float MixMotors(float collective, Vector3 torque)
        {
            float lever = Mathf.Max(.01f, armLength) / Mathf.Sqrt(2f);
            float pitch = torque.x / (4f * lever), roll = torque.z / (4f * lever);
            float turn = propellerTorqueRatio > .000001f ? torque.y / (4f * propellerTorqueRatio) : 0;
            float limit = Mathf.Max(.01f, maxMotorThrust);
            float common = Mathf.Clamp(collective * .25f, 0, limit);
            motorCommand[0] = -pitch - roll;
            motorCommand[1] = -pitch + roll;
            motorCommand[2] = pitch - roll;
            motorCommand[3] = pitch + roll;
            float tiltScale = 1f;
            for (int i = 0; i < 4; i++)
                tiltScale = Mathf.Min(tiltScale, MotorHeadroom(common, motorCommand[i], limit));
            for (int i = 0; i < 4; i++) motorCommand[i] = common + motorCommand[i] * tiltScale;

            float yawScale = 1f;
            for (int i = 0; i < 4; i++)
                yawScale = Mathf.Min(yawScale, MotorHeadroom(motorCommand[i], -Spin[i] * turn, limit));
            for (int i = 0; i < 4; i++)
                motorCommand[i] = Mathf.Clamp(motorCommand[i] - Spin[i] * turn * yawScale, 0, limit);
            return Mathf.Min(tiltScale, yawScale);
        }

        static float MotorHeadroom(float current, float differential, float limit) => differential > 0 ?
            Mathf.Clamp01((limit - current) / differential) : differential < 0 ?
            Mathf.Clamp01(current / -differential) : 1f;

        Vector3 MotorPosition(int index)
        {
            float lever = Mathf.Max(.01f, armLength) / Mathf.Sqrt(2f);
            return new Vector3(index % 2 == 0 ? -lever : lever, 0, index < 2 ? lever : -lever);
        }

        Vector3 StepMotors(Quaternion rotation, float dt, out float rotorMomentum)
        {
            float maxSpeed = Mathf.Max(1, maxMotorRpm) * (2f * Mathf.PI / 60f);
            float thrustCoefficient = Mathf.Max(.01f, maxMotorThrust) / (maxSpeed * maxSpeed);
            float tau = Mathf.Max(.001f, motorTimeConstant), decay = Mathf.Exp(-dt / tau);
            float meanDecay = tau * (1f - decay) / dt;
            float meanSquaredDecay = tau * (1f - decay * decay) / (2f * dt);
            Vector3 torque = Vector3.zero;
            rotorMomentum = 0;
            Vector3 up = rotation * Vector3.up;
            for (int i = 0; i < 4; i++)
            {
                float before = motorSpeed[i];
                float target = Mathf.Sqrt(motorCommand[i] / thrustCoefficient);
                float difference = before - target;
                float after = target + difference * decay;
                motorSpeed[i] = after;
                // Analytic mean RPM² over this step avoids a timestep-dependent thrust overshoot.
                float meanSquare = target * target + 2f * target * difference * meanDecay +
                    difference * difference * meanSquaredDecay;
                float force = thrustCoefficient * Mathf.Max(0, meanSquare);
                motorThrust[i] = force;
                Vector3 arm = MotorPosition(i);
                Body.AddForceAtPosition(up * force, Body.worldCenterOfMass + rotation * arm, ForceMode.Force);
                float reaction = -Spin[i] * (propellerTorqueRatio * force + rotorInertia * (after - before) / dt);
                Body.AddTorque(up * reaction, ForceMode.Force);
                torque += Vector3.Cross(arm, Vector3.up * force) + Vector3.up * reaction;
                rotorMomentum += Spin[i] * rotorInertia * (target + difference * meanDecay);
            }
            return torque;
        }

        static Vector3 SquaredSigned(Vector3 v) => new Vector3(v.x * Mathf.Abs(v.x), v.y * Mathf.Abs(v.y), v.z * Mathf.Abs(v.z));
        static Vector3 ClampAxes(Vector3 v, float limit) => new Vector3(
            Mathf.Clamp(v.x, -limit, limit), Mathf.Clamp(v.y, -limit, limit), Mathf.Clamp(v.z, -limit, limit));

        public void ResetDrone()
        {
            Body.isKinematic = false;
            Body.linearVelocity = Body.angularVelocity = Vector3.zero;
            Body.position = spawn; Body.rotation = spawnRotation;
            transform.SetPositionAndRotation(spawn, spawnRotation);
            armed = crashed = false;
            Thrust = 0; LocalTorque = rateIntegral = Vector3.zero;
            tilt = keyTilt = LeftStick = RightStick = Vector2.zero;
            throttle = yaw = keyThrottle = keyYaw = 0;
            for (int i = 0; i < 4; i++) motorSpeed[i] = motorCommand[i] = motorThrust[i] = 0;
            ResetCourse?.Invoke();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (armed && collision.relativeVelocity.magnitude > crashSpeed)
            {
                crashed = true; armed = false;
                // Cut motor commands, but let the physics solver handle impact and the wreck's fall.
                // RPM decays naturally; neither linear nor angular momentum is erased here.
            }
        }

        void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            if (!focus)
            {
                armed = false;
                tilt = keyTilt = Vector2.zero;
                throttle = yaw = keyThrottle = keyYaw = 0;
            }
        }
    }
}
