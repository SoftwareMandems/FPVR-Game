using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace FPVR
{
    [DefaultExecutionOrder(-200)]
    internal sealed class FlightSettingsMenu : MonoBehaviour
    {
        const int SensitivityStep = 5;
        static FlightSettingsMenu instance;

        FlightSettingsView view;
        InputAction menuAction, leftStick, rightStick, confirm;
        EventSystem previousEventSystem;
        bool open, previousToggle, previousConfirm, navigationReady;
        float previousTimeScale, repeatAt;
        int closedFrame = -1;
        Vector2Int previousDirection;

        public static bool IsBlockingInput => instance && (instance.open || instance.closedFrame == Time.frameCount);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var root = new GameObject("Flight Settings");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<FlightSettingsMenu>();
        }

        void Awake()
        {
            menuAction = CreateAction("Settings menu", InputActionType.Button,
                "<XRController>/{MenuButton}", "<XRController>/menuButton");
            leftStick = CreateAction("Settings left thumbstick", InputActionType.Value,
                "<XRController>{LeftHand}/{Primary2DAxis}", "<XRController>{LeftHand}/thumbstick");
            rightStick = CreateAction("Settings right thumbstick", InputActionType.Value,
                "<XRController>{RightHand}/{Primary2DAxis}", "<XRController>{RightHand}/thumbstick");
            confirm = CreateAction("Settings confirm", InputActionType.Button,
                "<XRController>{RightHand}/{PrimaryButton}", "<XRController>{RightHand}/primaryButton");
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        static InputAction CreateAction(string name, InputActionType type, params string[] bindings)
        {
            var action = new InputAction(name, type);
            foreach (var binding in bindings) action.AddBinding(binding);
            action.Enable();
            return action;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool leftMenu);
            right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool rightMenu);
            bool toggle = (keyboard?.escapeKey.isPressed ?? false) || menuAction.IsPressed() || leftMenu || rightMenu;
            bool togglePressed = toggle && !previousToggle;
            previousToggle = toggle;

            right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool rightPrimary);
            bool accept = (keyboard?.enterKey.isPressed ?? false) || (gamepad?.buttonSouth.isPressed ?? false) ||
                confirm.IsPressed() || rightPrimary;
            bool acceptPressed = accept && !previousConfirm;
            previousConfirm = accept;

            if (togglePressed)
            {
                if (open) Close();
                else Open();
                return;
            }
            if (!open) return;

            var headCamera = Camera.main;
            bool headset = XRSettings.isDeviceActive && headCamera;
            if (view.IsVR != headset) view.SetPresentation(headset ? headCamera : null);

            Vector2 direction = Larger(DeviceStick(left, leftStick), DeviceStick(right, rightStick));
            if (gamepad != null) direction = Larger(direction, gamepad.dpad.ReadValue());
            if (keyboard != null)
            {
                var arrows = new Vector2(
                    (keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0),
                    (keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.downArrowKey.isPressed ? 1 : 0));
                direction = Larger(direction, arrows);
            }
            Navigate(direction);
            if (acceptPressed) view.Confirm();
        }

        void Navigate(Vector2 axes)
        {
            Vector2Int direction = Vector2Int.zero;
            if (Mathf.Abs(axes.y) >= Mathf.Abs(axes.x) && Mathf.Abs(axes.y) > .55f)
                direction.y = axes.y > 0 ? -1 : 1;
            else if (Mathf.Abs(axes.x) > .55f)
                direction.x = axes.x > 0 ? 1 : -1;

            if (direction == Vector2Int.zero) navigationReady = true;
            else if (navigationReady && (direction != previousDirection || Time.unscaledTime >= repeatAt))
            {
                if (direction.y != 0) view.MoveSelection(direction.y);
                else view.Adjust(direction.x * SensitivityStep);
                repeatAt = Time.unscaledTime + (direction != previousDirection ? .30f : .12f);
            }
            previousDirection = direction;
        }

        static Vector2 DeviceStick(UnityEngine.XR.InputDevice device, InputAction action)
        {
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axes);
            return Larger(axes, action.ReadValue<Vector2>());
        }

        static Vector2 Larger(Vector2 a, Vector2 b) => a.sqrMagnitude >= b.sqrMagnitude ? a : b;

        void Open()
        {
            var camera = Camera.main;
            if (!camera) return;
            if (view == null) view = new FlightSettingsView(transform, Close);
            if (!previousEventSystem && EventSystem.current)
            {
                previousEventSystem = EventSystem.current;
                previousEventSystem.enabled = false;
            }

            open = true;
            navigationReady = false;
            previousDirection = Vector2Int.zero;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            view.Open(XRSettings.isDeviceActive ? camera : null);
        }

        void Close()
        {
            if (!open) return;
            open = false;
            closedFrame = Time.frameCount;
            view.Close();
            Time.timeScale = previousTimeScale;
            FlightSensitivity.Save();
        }

        void LateUpdate()
        {
            if (!open && Time.frameCount > closedFrame) RestoreEventSystem();
        }

        void RestoreEventSystem()
        {
            if (previousEventSystem) previousEventSystem.enabled = true;
            previousEventSystem = null;
        }

        void OnActiveSceneChanged(Scene before, Scene after) => Close();
        void OnApplicationPause(bool paused) { if (paused) FlightSensitivity.Save(); }

        void OnDestroy()
        {
            Close();
            RestoreEventSystem();
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            menuAction.Dispose();
            leftStick.Dispose();
            rightStick.Dispose();
            confirm.Dispose();
            view?.Dispose();
        }
    }
}
