using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FPVR
{
    internal sealed class FlightSettingsView
    {
        const int RestoreIndex = FlightSensitivity.AxisCount;
        const int CloseIndex = RestoreIndex + 1;

        static readonly Color Surface = new Color32(16, 26, 39, 255);
        static readonly Color Row = new Color32(26, 40, 56, 255);
        static readonly Color Selected = new Color32(43, 70, 88, 255);
        static readonly Color Accent = new Color32(111, 228, 242, 255);
        static readonly Color MutedText = new Color32(180, 204, 218, 255);

        readonly Canvas canvas;
        readonly GraphicRaycaster raycaster;
        readonly GameObject desktopInput;
        readonly Image dimmer;
        readonly Text help;
        readonly Font font;
        readonly Material headsetMaterial;
        readonly List<Graphic> graphics = new();
        readonly Image[] highlights = new Image[CloseIndex + 1];
        readonly Slider[] sliders = new Slider[FlightSensitivity.AxisCount];
        readonly Text[] values = new Text[FlightSensitivity.AxisCount];
        readonly Action onClose;
        int selection;

        public bool IsVR { get; private set; }

        public FlightSettingsView(Transform parent, Action onClose)
        {
            this.onClose = onClose;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headsetMaterial = new Material(Resources.Load<Material>("TutorialPanel")) { color = Color.white };

            var root = new GameObject("Settings Canvas", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            canvas = root.AddComponent<Canvas>();
            canvas.sortingOrder = 100;
            raycaster = root.AddComponent<GraphicRaycaster>();
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            desktopInput = new GameObject("Settings Desktop Input");
            desktopInput.SetActive(false);
            desktopInput.transform.SetParent(parent, false);
            desktopInput.AddComponent<EventSystem>().sendNavigationEvents = false;
            desktopInput.AddComponent<InputSystemUIInputModule>();

            dimmer = ImageAt(RectAt("Backdrop", root.transform, 0, 0, 0, 0), new Color(0, 0, 0, .65f), true);
            dimmer.rectTransform.anchorMin = Vector2.zero;
            dimmer.rectTransform.anchorMax = Vector2.one;
            dimmer.rectTransform.offsetMin = dimmer.rectTransform.offsetMax = Vector2.zero;
            var panel = RectAt("Settings Panel", root.transform, 0, 0, 720, 516);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            ImageAt(panel, Surface, true);

            TextAt(panel, "Flight Settings", 36, 24, 648, 48, 39, Color.white, FontStyle.Bold);
            TextAt(panel, "Drone control sensitivity", 38, 76, 644, 34, 25, Accent);
            for (int index = 0; index < FlightSensitivity.AxisCount; index++) AddSlider(panel, index);

            AddButton(panel, "Restore defaults", RestoreIndex, 36, 260, Restore);
            AddButton(panel, "Close settings", CloseIndex, 444, 240, onClose);
            help = TextAt(panel, "", 38, 442, 644, 57, 20, MutedText);
        }

        public void Open(Camera headsetCamera)
        {
            for (int index = 0; index < sliders.Length; index++)
                ShowValue(index, FlightSensitivity.Get((FlightSensitivity.Axis)index));
            Select(0);
            SetPresentation(headsetCamera);
            canvas.gameObject.SetActive(true);
        }

        public void SetPresentation(Camera headsetCamera)
        {
            IsVR = headsetCamera != null;
            desktopInput.SetActive(!IsVR);
            raycaster.enabled = !IsVR;
            dimmer.gameObject.SetActive(!IsVR);
            foreach (var graphic in graphics) graphic.material = IsVR ? headsetMaterial : null;
            help.text = IsVR
                ? "Thumbstick up/down selects. Left/right adjusts. A activates buttons.\nPress the controller menu button to close."
                : "Drag a slider or use the arrow keys. Enter activates buttons.\nPress Esc to close.";

            if (IsVR)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = headsetCamera;
                var head = headsetCamera.transform;
                var rect = (RectTransform)canvas.transform;
                rect.sizeDelta = new Vector2(720, 516);
                rect.localScale = Vector3.one * .0018f;
                rect.SetPositionAndRotation(head.position + head.forward * 1.75f, head.rotation);
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.transform.localScale = Vector3.one;
            }
        }

        public void Close()
        {
            desktopInput.SetActive(false);
            canvas.gameObject.SetActive(false);
        }

        public void Dispose() => UnityEngine.Object.Destroy(headsetMaterial);

        public void MoveSelection(int amount) => Select((selection + amount + highlights.Length) % highlights.Length);

        public void Adjust(int amount)
        {
            if (selection < sliders.Length) sliders[selection].value += amount;
        }

        public void Confirm()
        {
            if (selection == RestoreIndex) Restore();
            else if (selection == CloseIndex) onClose();
        }

        void Select(int index)
        {
            highlights[selection].color = Row;
            selection = index;
            highlights[selection].color = Selected;
        }

        void Restore()
        {
            for (int index = 0; index < sliders.Length; index++)
            {
                FlightSensitivity.Set((FlightSensitivity.Axis)index, FlightSensitivity.Default);
                ShowValue(index, FlightSensitivity.Default);
            }
        }

        void ShowValue(int index, int percent)
        {
            sliders[index].SetValueWithoutNotify(percent);
            values[index].text = percent + "%";
        }

        void AddSlider(RectTransform panel, int index)
        {
            var axis = (FlightSensitivity.Axis)index;
            var row = RectAt(axis + " Sensitivity", panel, 36, 118 + index * 65, 648, 57);
            highlights[index] = ImageAt(row, Row);
            TextAt(row, axis.ToString(), 15, 0, 150, 57, 25, Color.white);
            values[index] = TextAt(row, "", 560, 0, 72, 57, 24, Accent);
            values[index].alignment = TextAnchor.MiddleRight;

            var hitArea = RectAt(axis + " Slider", row, 174, 4, 367, 49);
            ImageAt(hitArea, Color.clear, true);
            var track = RectAt("Track", hitArea, 0, 20, 367, 9);
            ImageAt(track, Surface);
            var fill = ImageAt(RectAt("Fill", track, 0, 0, 0, 0), Accent);
            var handleArea = RectAt("Handle Area", hitArea, 0, 12, 367, 25);
            var handle = ImageAt(RectAt("Handle", handleArea, -9, 0, 18, 0), Color.white);

            var slider = hitArea.gameObject.AddComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = FlightSensitivity.Minimum;
            slider.maxValue = FlightSensitivity.Maximum;
            slider.wholeNumbers = true;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.onValueChanged.AddListener(newValue =>
            {
                int percent = (int)newValue;
                FlightSensitivity.Set(axis, percent);
                values[index].text = percent + "%";
                Select(index);
            });
            sliders[index] = slider;
        }

        void AddButton(RectTransform panel, string label, int index, int x, int width, Action onClick)
        {
            var rect = RectAt(label, panel, x, 383, width, 50);
            highlights[index] = ImageAt(rect, Row, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.targetGraphic = highlights[index];
            button.onClick.AddListener(() =>
            {
                Select(index);
                onClick();
            });
            var text = TextAt(rect, label, 0, 0, width, 50, 22, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
        }

        static RectTransform RectAt(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        Image ImageAt(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            graphics.Add(image);
            return image;
        }

        Text TextAt(Transform parent, string label, float x, float y, float width, float height, int size,
            Color color, FontStyle style = FontStyle.Normal)
        {
            var text = RectAt(label, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            graphics.Add(text);
            return text;
        }
    }
}
