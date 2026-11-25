using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FPVR.Tutorials
{
    public sealed class TutorialInput : System.IDisposable
    {
        readonly InputAction confirm = Action("Tutorial confirm", "<XRController>{RightHand}/primaryButton");
        readonly InputAction pause = Action("Tutorial pause", "<XRController>{RightHand}/secondaryButton");
        readonly InputAction restart = Action("Tutorial restart", "<XRController>{LeftHand}/primaryButton");
        readonly InputAction leftStick = Action("Tutorial navigation", "<XRController>{LeftHand}/thumbstick", InputActionType.Value);
        readonly InputAction rightStick = Action("Tutorial right stick", "<XRController>{RightHand}/thumbstick", InputActionType.Value);
        readonly InputAction leftTrigger = Action("Tutorial left trigger", "<XRController>{LeftHand}/trigger", InputActionType.Value);
        readonly InputAction rightTrigger = Action("Tutorial right trigger", "<XRController>{RightHand}/trigger", InputActionType.Value);
        bool wasConfirm, wasPause, wasRestart;
        float repeatAt;
        int lastDirection;
        public bool Confirm { get; private set; }
        public bool Pause { get; private set; }
        public bool Restart { get; private set; }
        public int Move { get; private set; }

        static InputAction Action(string name, string binding, InputActionType type = InputActionType.Button)
        {
            var action = new InputAction(name, type, binding); action.Enable(); return action;
        }
        static Vector2 Stick(XRNode node, InputAction action)
        {
            InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 value);
            var other = action.ReadValue<Vector2>(); return value.sqrMagnitude > other.sqrMagnitude ? value : other;
        }
        static bool Button(XRNode node, bool secondary)
        {
            InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(secondary ? UnityEngine.XR.CommonUsages.secondaryButton : UnityEngine.XR.CommonUsages.primaryButton, out bool value);
            return value;
        }
        public void Poll()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            bool c = (k != null && k.enterKey.isPressed) || (g != null && g.buttonSouth.isPressed) || confirm.IsPressed() || Button(XRNode.RightHand, false);
            bool p = (k != null && (k.escapeKey.isPressed || k.pKey.isPressed)) || (g != null && g.startButton.isPressed) || pause.IsPressed() || Button(XRNode.RightHand, true);
            bool r = (k != null && k.rKey.isPressed) || (g != null && g.buttonNorth.isPressed) || restart.IsPressed() || Button(XRNode.LeftHand, false);
            Confirm = c && !wasConfirm; Pause = p && !wasPause; wasConfirm = c; wasPause = p;
            Restart = r && !wasRestart; wasRestart = r;
            float y = Stick(XRNode.LeftHand, leftStick).y;
            if (g != null && Mathf.Abs(g.dpad.y.ReadValue()) > .5f) y = g.dpad.y.ReadValue();
            if (k != null && k.upArrowKey.isPressed) y = 1;
            if (k != null && k.downArrowKey.isPressed) y = -1;
            int direction = y > .55f ? -1 : y < -.55f ? 1 : 0;
            Move = 0;
            if (direction != 0 && (direction != lastDirection || Time.unscaledTime >= repeatAt))
            { Move = direction; repeatAt = Time.unscaledTime + .28f; }
            lastDirection = direction;
        }
        public bool FlightNeutral()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            if (k != null && (k.wKey.isPressed || k.sKey.isPressed || k.aKey.isPressed || k.dKey.isPressed || k.qKey.isPressed || k.eKey.isPressed || k.spaceKey.isPressed || k.leftShiftKey.isPressed || k.leftCtrlKey.isPressed || k.upArrowKey.isPressed || k.downArrowKey.isPressed || k.leftArrowKey.isPressed || k.rightArrowKey.isPressed || k.rKey.isPressed)) return false;
            if (g != null && (g.leftStick.ReadValue().magnitude > .1f || g.rightStick.ReadValue().magnitude > .1f || g.leftTrigger.ReadValue() > .1f || g.rightTrigger.ReadValue() > .1f || g.buttonNorth.isPressed)) return false;
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float lt);
            right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float rt);
            return Stick(XRNode.LeftHand, leftStick).magnitude <= .1f && Stick(XRNode.RightHand, rightStick).magnitude <= .1f
                && Mathf.Max(lt, leftTrigger.ReadValue<float>()) <= .1f && Mathf.Max(rt, rightTrigger.ReadValue<float>()) <= .1f
                && !Button(XRNode.LeftHand, false);
        }
        public static bool XRReady()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracked = head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool value) && value;
            bool motionControllers = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid && InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
            bool pcGamepad = Application.platform != RuntimePlatform.Android && Gamepad.current != null;
            return XRSettings.isDeviceActive && tracked && (motionControllers || pcGamepad);
        }
        public void Dispose()
        {
            confirm.Dispose(); pause.Dispose(); restart.Dispose(); leftStick.Dispose(); rightStick.Dispose(); leftTrigger.Dispose(); rightTrigger.Dispose();
        }
    }
}
