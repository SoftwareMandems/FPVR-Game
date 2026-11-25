using System;
using FPVR.Tutorials;
using UnityEngine;
using UnityEngine.UI;

namespace FPVR
{
    // Uses the startup menu's world-space canvas and its desktop and XR raycasters.
    sealed class LevelSelectionView
    {
        const float Width = 720f;
        const float Margin = 36f;
        const float RowHeight = 55f;
        const float RowGap = 8f;
        const float ContentWidth = Width - 2f * Margin;
        static readonly Color Ink = new Color32(237, 246, 250, 255);
        static readonly Color Accent = new Color32(140, 236, 240, 255);
        static readonly Color Detail = new Color32(203, 223, 233, 255);

        readonly RectTransform panel;
        readonly CanvasGroup controls;
        readonly Font font;
        readonly Text status;

        public LevelSelectionView(RectTransform canvas, Font font, Action<TutorialCourse> selectTutorial,
            Action<TutorialCourse> selectCourse, Action demo, Action back)
        {
            this.font = font;
            panel = CreateRect("Level Selection", canvas, typeof(Image), typeof(CanvasGroup));
            controls = panel.GetComponent<CanvasGroup>();
            var background = panel.GetComponent<Image>();
            background.color = new Color32(12, 27, 43, 249);
            background.raycastTarget = false;

            AddText(panel, "Title", "Select a level", 50, Ink, Margin, 22f, ContentWidth, 60f, FontStyle.Bold);

            float top = 98f;
            AddCourses("TUTORIALS", TutorialCourseCatalog.All, selectTutorial, ref top);
            top += 9f;
            AddCourses("REGULAR COURSES", ObstacleCourseCatalog.All, selectCourse, ref top);
            top += 9f;

            const float backWidth = 215f;
            var backButton = AddButton("Back to main menu", Margin, top, backWidth, 48f, back);
            AddText(backButton, "Label", "Back to main menu", 21, Accent, 14f, 0f, backWidth - 28f, 48f)
                .alignment = TextAnchor.MiddleCenter;

            const float demoWidth = 166f;
            var demoButton = AddButton("Demo course", Width - Margin - demoWidth, top, demoWidth, 48f, demo);
            AddText(demoButton, "Label", "Demo course", 21, Detail, 14f, 0f, demoWidth - 28f, 48f)
                .alignment = TextAnchor.MiddleCenter;
            status = AddText(panel, "Status", "", 19, Ink, Margin + backWidth + 16f, top,
                ContentWidth - backWidth - demoWidth - 32f, 48f);
            status.alignment = TextAnchor.MiddleCenter;
            panel.sizeDelta = new Vector2(Width, top + 74f);
        }

        public void Fit(Vector2 availableSize)
        {
            // Scale down on narrow windows and keep the full list visible.
            float scale = Mathf.Min(1f, (availableSize.x - 32f) / Width, (availableSize.y - 32f) / panel.sizeDelta.y);
            panel.localScale = Vector3.one * Mathf.Max(scale, .01f);
        }

        public void Show(bool visible)
        {
            if (visible) SetStatus("");
            panel.gameObject.SetActive(visible);
        }

        public void SetStatus(string message, bool loading = false)
        {
            status.text = message;
            controls.interactable = !loading;
        }

        void AddCourses(string heading, TutorialCourse[] courses, Action<TutorialCourse> select, ref float top)
        {
            AddText(panel, heading, heading, 21, Accent, Margin, top, ContentWidth, 31f, FontStyle.Bold);
            top += 36f;

            foreach (var course in courses)
            {
                var row = AddButton(course.Id, Margin, top, ContentWidth, RowHeight, () => select(course));
                AddText(row, "Name", course.Name, 28, Ink, 17f, 0f, 420f, RowHeight, FontStyle.Bold);
                var difficulty = AddText(row, "Difficulty", course.FreeFlight ? "Practice" : course.Difficulty.ToString(),
                    22, Detail, 449f, 0f, ContentWidth - 467f, RowHeight);
                difficulty.alignment = TextAnchor.MiddleRight;
                top += RowHeight + RowGap;
            }
        }

        RectTransform AddButton(string name, float left, float top, float width, float height, Action click)
        {
            var rect = CreateRect(name, panel, typeof(Image), typeof(Button));
            Place(rect, left, top, width, height);
            var button = rect.GetComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.colors = new ColorBlock
            {
                normalColor = new Color32(29, 57, 78, 255),
                highlightedColor = new Color32(40, 87, 104, 255),
                pressedColor = new Color32(32, 69, 89, 255),
                selectedColor = new Color32(29, 57, 78, 255),
                disabledColor = new Color32(22, 39, 54, 255),
                colorMultiplier = 1f,
                fadeDuration = .1f,
            };
            button.onClick.AddListener(() => click());
            return rect;
        }

        Text AddText(RectTransform parent, string name, string value, int size, Color color,
            float left, float top, float width, float height, FontStyle style = FontStyle.Normal)
        {
            var rect = CreateRect(name, parent, typeof(Text));
            Place(rect, left, top, width, height);
            var label = rect.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = color;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        static RectTransform CreateRect(string name, RectTransform parent, params Type[] components)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.layer = parent.gameObject.layer;
            var rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
            foreach (var component in components) item.AddComponent(component);
            return rect;
        }

        static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
