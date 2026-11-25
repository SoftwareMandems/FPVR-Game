using System.Collections.Generic;
using UnityEngine;

namespace FPVR.Tutorials
{
    // Owns course geometry only. The session owns passage detection and progression.
    public sealed class TutorialWorld : MonoBehaviour
    {
        public TrainingGate[] Gates { get; private set; }
        public TrainingGate Finish { get; private set; }
        readonly List<Material> materials = new List<Material>();
        Transform layout;
        Material ground, frame, white, dark, passed;

        public void Load(TutorialCourse course)
        {
            if (layout) { layout.gameObject.SetActive(false); Destroy(layout.gameObject); }
            foreach (var material in materials) Destroy(material);
            materials.Clear();
            layout = new GameObject(course.Environment).transform;
            layout.SetParent(transform, false);
            bool hangar = course.Environment == "Practice Hangar";
            ground = Paint(hangar ? new Color(.18f, .23f, .29f) : new Color(.28f, .45f, .23f));
            frame = Paint(new Color(.08f, .65f, .75f));
            white = Paint(new Color(.93f, .95f, .9f));
            dark = Paint(new Color(.07f, .12f, .18f));
            passed = Paint(new Color(.25f, .9f, .5f));
            Box("Ground", new Vector3(0, -.25f, 20), new Vector3(240, .5f, 240), ground);
            // Both environments have a broad, empty flight corridor. Hangar walls stay well clear.
            if (hangar)
            {
                Box("Hangar left wall", new Vector3(-23, 7, 22), new Vector3(1, 14, 80), dark);
                Box("Hangar right wall", new Vector3(23, 7, 22), new Vector3(1, 14, 80), dark);
                Box("Hangar roof", new Vector3(0, 14, 22), new Vector3(47, .5f, 80), ground);
                for (int z = -8; z <= 52; z += 12)
                    Box("Roof light", new Vector3(0, 13.6f, z), new Vector3(12, .1f, .5f), white, false);
            }
            else
            {
                for (int z = -10; z <= 55; z += 5)
                    Box("Field stripe", new Vector3(0, .006f, z), new Vector3(36, .01f, .06f), white, false);
                for (int x = -18; x <= 18; x += 36)
                    Box("Field border", new Vector3(x, .01f, 22), new Vector3(.08f, .02f, 65), white, false);
            }
            Box("Launch pad", new Vector3(0, .025f, 0), new Vector3(3, .05f, 3), frame);
            Label("START", new Vector3(0, .06f, -1), Quaternion.Euler(90, 0, 0), .14f);
            Gates = new TrainingGate[course.Checkpoints.Length];
            for (int i = 0; i < Gates.Length; i++) Gates[i] = Gate(course.Checkpoints[i], "CHECKPOINT " + (i + 1), frame);
            Finish = course.FreeFlight ? null : Gate(course.Finish, "FINISH", passed);
            RenderSettings.fog = true;
            RenderSettings.fogColor = hangar ? new Color(.16f, .22f, .30f) : new Color(.64f, .80f, .90f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 130;
            RenderSettings.ambientLight = hangar ? new Color(.65f, .72f, .85f) : new Color(.75f, .8f, .75f);
            var camera = Camera.main;
            if (camera) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor; }
        }

        Material Paint(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            materials.Add(material); return material;
        }

        GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool solid = true, Transform parent = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name; box.transform.SetParent(parent ? parent : layout, false);
            box.transform.localPosition = position; box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { box.GetComponent<Collider>().enabled = false; Destroy(box.GetComponent<Collider>()); }
            return box;
        }

        TrainingGate Gate(Vector3 position, string name, Material material)
        {
            var root = new GameObject(name); root.transform.SetParent(layout, false);
            root.transform.position = position;
            var gate = root.AddComponent<TrainingGate>();
            gate.opening = new Vector2(3.2f, 2.5f);
            Box("Left frame", new Vector3(-3.35f, 0, 0), new Vector3(.3f, 5.6f, .3f), material, true, root.transform);
            Box("Right frame", new Vector3(3.35f, 0, 0), new Vector3(.3f, 5.6f, .3f), material, true, root.transform);
            Box("Top frame", new Vector3(0, 2.65f, 0), new Vector3(7, .3f, .3f), material, true, root.transform);
            Box("Bottom frame", new Vector3(0, -2.65f, 0), new Vector3(7, .3f, .3f), material, true, root.transform);
            Label(name + "  >", position + new Vector3(0, 3.15f, 0), Quaternion.identity, .16f, root.transform);
            return gate;
        }

        void Label(string text, Vector3 position, Quaternion rotation, float size, Transform parent = null)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.transform.SetParent(parent ? parent : layout, false);
            label.transform.SetPositionAndRotation(position, rotation);
            label.text = text; label.fontSize = 64; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
        }

        public void Highlight(int next)
        {
            for (int i = 0; i < Gates.Length; i++)
                Gates[i].Highlight(i < next ? 2 : i == next ? 1 : 0, new Color(1, .65f, .12f), Color.green);
            if (Finish) Finish.Highlight(next == Gates.Length ? 1 : 0, new Color(1, .65f, .12f), Color.green);
        }

        void OnDestroy() { foreach (var material in materials) if (material) Destroy(material); }
    }
}
