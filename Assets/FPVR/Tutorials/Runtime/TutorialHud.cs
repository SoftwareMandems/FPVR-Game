using System;
using System.Text;
using UnityEngine;

namespace FPVR.Tutorials
{
    // One presentation model for a desktop panel and a stationary, head-visible VR panel.
    public sealed class TutorialHud : MonoBehaviour
    {
        public string Title = "FPVR Tutorials", Body = "";
        public string[] Options = Array.Empty<string>();
        public int Selected;
        public bool Flying, IsVR;
        public Action<int> Choose;
        TextMesh text;
        Transform panel;
        Material backdrop;
        Material lettering;
        GUIStyle heading, body, button;

        public void Attach(Camera camera)
        {
            var root = new GameObject("Tutorial headset instructions");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = new Vector3(0, 0, 2.3f);
            panel = root.transform;
            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Readable backdrop"; background.transform.SetParent(panel, false);
            background.transform.localPosition = new Vector3(0, 0, .02f);
            background.transform.localScale = new Vector3(1.95f, 1.65f, 1);
            background.GetComponent<Collider>().enabled = false; Destroy(background.GetComponent<Collider>());
            // A Resources material keeps this shader available in stripped Quest/player builds.
            backdrop = new Material(Resources.Load<Material>("TutorialPanel"));
            backdrop.renderQueue = 3998;
            backdrop.color = new Color(.025f, .045f, .065f);
            background.GetComponent<Renderer>().sharedMaterial = backdrop;
            text = new GameObject("Instructions and menu").AddComponent<TextMesh>();
            text.transform.SetParent(panel, false); text.transform.localPosition = new Vector3(-.88f, .73f, 0);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lettering = new Material(backdrop) { mainTexture = text.font.material.mainTexture, color = Color.white, renderQueue = 3999 };
            text.GetComponent<Renderer>().sharedMaterial = lettering;
            text.anchor = TextAnchor.UpperLeft; text.fontSize = 64; text.characterSize = .047f;
            text.color = Color.white;
        }
        void LateUpdate() => RefreshHeadsetPanel();
        public void RefreshHeadsetPanel()
        {
            if (!panel) return;
            bool visible = IsVR && !FlightSettingsMenu.IsBlockingInput;
            panel.gameObject.SetActive(visible);
            if (!visible) return;
            panel.localPosition = new Vector3(0, Flying ? -.7f : 0, 2.3f);
            panel.GetChild(0).localScale = new Vector3(1.95f, Flying ? .62f : 1.65f, 1);
            text.transform.localPosition = new Vector3(-.88f, Flying ? .24f : .73f, 0);
            var content = new StringBuilder(Title).Append("\n\n").Append(Wrap(Body, 66));
            if (Options.Length > 0)
            {
                content.Append("\n\n");
                for (int i = 0; i < Options.Length; i++) content.Append(i == Selected ? "> " : "  ").Append(Options[i]).Append('\n');
                content.Append("\nLeft stick: choose   A: select   B: pause/back");
            }
            text.text = content.ToString();
            text.font.RequestCharactersInTexture(text.text, text.fontSize);
            lettering.mainTexture = text.font.material.mainTexture;
            // TextMesh's character units differ from world metres. Fit measured glyph
            // bounds rather than relying on a guessed character size in the headset.
            var bounds = text.GetComponent<Renderer>().localBounds;
            float size = Mathf.Min(1, 1.76f / Mathf.Max(.001f, bounds.size.x),
                (Flying ? .51f : 1.46f) / Mathf.Max(.001f, bounds.size.y));
            text.transform.localScale = Vector3.one * size;
        }
        internal static string Wrap(string source, int width)
        {
            var result = new StringBuilder(); int column = 0;
            foreach (var line in source.Split('\n'))
            {
                foreach (var word in line.Split(' '))
                {
                    if (column + word.Length + 1 > width) { result.Append('\n'); column = 0; }
                    if (column > 0) { result.Append(' '); column++; }
                    result.Append(word); column += word.Length;
                }
                result.Append('\n'); column = 0;
            }
            return result.ToString().TrimEnd();
        }
        void OnGUI()
        {
            if (IsVR || FlightSettingsMenu.IsBlockingInput) return;
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 800f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, wordWrap = true };
                heading.normal.textColor = new Color(.35f, .94f, 1);
                body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
                body.normal.textColor = Color.white;
                button = new GUIStyle(GUI.skin.button) { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            }
            float height = Flying ? 220 : 720;
            GUILayout.BeginArea(new Rect(22, Flying ? Screen.height / scale - height - 20 : 25, 650, height), GUI.skin.box);
            GUILayout.Space(10); GUILayout.Label(Title, heading); GUILayout.Space(8); GUILayout.Label(Body, body);
            GUILayout.Space(12);
            for (int i = 0; i < Options.Length; i++)
                if (GUILayout.Button((i == Selected ? "> " : "   ") + Options[i], button, GUILayout.Height(42))) Choose?.Invoke(i);
            if (!Flying) GUILayout.Label("Arrows / D-pad: choose   Enter / gamepad A: select\nP / Start: pause or back   Esc: settings", body);
            GUILayout.EndArea(); GUI.matrix = previous;
        }
        void OnDestroy() { if (panel) Destroy(panel.gameObject); if (backdrop) Destroy(backdrop); if (lettering) Destroy(lettering); }
    }
}
