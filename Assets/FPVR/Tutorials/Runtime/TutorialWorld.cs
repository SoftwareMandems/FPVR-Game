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
        Renderer[] activeGateLabels;

        public void Load(TutorialCourse course)
        {
            if (layout) { layout.gameObject.SetActive(false); Destroy(layout.gameObject); }
            foreach (var material in materials) Destroy(material);
            materials.Clear();
            layout = new GameObject(course.Environment).transform;
            layout.SetParent(transform, false);
            bool hangar = course.Environment == "Practice Hangar";
            bool canyon = course.Environment == "Canyon";
            bool regular = course.Layout != null;
            ground = Paint(canyon ? new Color(.62f, .40f, .24f) : hangar ? new Color(.18f, .23f, .29f) : new Color(.28f, .45f, .23f));
            frame = Paint(new Color(.08f, .65f, .75f));
            white = Paint(new Color(.93f, .95f, .9f));
            dark = Paint(new Color(.07f, .12f, .18f));
            passed = Paint(new Color(.25f, .9f, .5f));
            Box("Ground", new Vector3(0, -.25f, 20), new Vector3(240, .5f, 240), ground);
            // Environment decoration stays outside the flight corridor. Only catalog obstacles
            // and gate frames introduce challenges along the route.
            if (hangar)
            {
                float centre = regular ? 40 : 22;
                float length = regular ? 120 : 80;
                Box("Hangar left wall", new Vector3(-23, 7, centre), new Vector3(1, 14, length), dark);
                Box("Hangar right wall", new Vector3(23, 7, centre), new Vector3(1, 14, length), dark);
                Box("Hangar roof", new Vector3(0, 14, centre), new Vector3(47, .5f, length), ground);
                for (int z = -8; z <= (regular ? 96 : 52); z += 12)
                    Box("Roof light", new Vector3(0, 13.6f, z), new Vector3(12, .1f, .5f), white, false);
                if (regular)
                {
                    for (int z = -8; z <= 88; z += 12)
                    {
                        Box("Hangar left rib", new Vector3(-22.35f, 7, z), new Vector3(.3f, 14, .5f), frame);
                        Box("Hangar right rib", new Vector3(22.35f, 7, z), new Vector3(.3f, 14, .5f), frame);
                    }
                    Box("Hangar left route line", new Vector3(-20, .01f, 40), new Vector3(.12f, .02f, 115), white, false);
                    Box("Hangar right route line", new Vector3(20, .01f, 40), new Vector3(.12f, .02f, 115), white, false);
                }
            }
            else if (canyon)
            {
                var sandstone = Paint(new Color(.58f, .27f, .17f));
                var paleRock = Paint(new Color(.76f, .48f, .28f));
                // Deterministic stepped formations: nearest faces remain beyond x +/-22,
                // leaving the catalog's x +/-18 route and checkpoint approaches clear.
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 9; i++)
                    {
                        float z = -10 + i * 13;
                        float height = 9 + (i * 7 + (side + 1)) % 8;
                        float x = side * (28 + i % 3);
                        Box("Canyon formation", new Vector3(x, height * .5f, z), new Vector3(10, height, 11), sandstone);
                        Box("Canyon upper ledge", new Vector3(x + side, height + 1.5f, z + 1), new Vector3(7, 3, 8), paleRock);
                        Box("Canyon rock layer", new Vector3(x - side * .2f, height * .5f, z), new Vector3(10.4f, .55f, 11.2f), paleRock);
                    }
                for (int side = -1; side <= 1; side += 2)
                    Box("Canyon route border", new Vector3(side * 20, .012f, 40), new Vector3(.12f, .02f, 118), white, false);
            }
            else
            {
                for (int z = -10; z <= (regular ? 95 : 55); z += 5)
                    Box("Field stripe", new Vector3(0, .006f, z), new Vector3(36, .01f, .06f), white, false);
                for (int x = -18; x <= 18; x += 36)
                    Box("Field border", new Vector3(x, .01f, regular ? 40 : 22), new Vector3(.08f, .02f, regular ? 115 : 65), white, false);
                if (regular)
                    for (int z = 0; z <= 90; z += 15)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Box("Meadow boundary post", new Vector3(side * 22, .6f, z), new Vector3(.3f, 1.2f, .3f), dark);
                            Box("Meadow boundary cap", new Vector3(side * 22, 1.25f, z), new Vector3(.55f, .1f, .55f), white, false);
                        }
            }
            if (regular)
            {
                var obstacleMaterial = Paint(canyon ? new Color(.30f, .16f, .11f) : new Color(.85f, .30f, .10f));
                foreach (var obstacle in course.Layout.Obstacles)
                    Box(obstacle.Name, obstacle.Position, obstacle.Size, obstacleMaterial);
            }
            Box("Launch pad", new Vector3(0, .025f, 0), new Vector3(3, .05f, 3), frame);
            Label("START", new Vector3(0, .06f, -1), Quaternion.Euler(90, 0, 0), .14f);
            var opening = regular ? course.Layout.Opening : new Vector2(3.2f, 2.5f);
            Gates = new TrainingGate[course.Checkpoints.Length];
            activeGateLabels = regular ? new Renderer[Gates.Length + 1] : null;
            for (int i = 0; i < Gates.Length; i++)
            {
                Gates[i] = Gate(course.Checkpoints[i], "CHECKPOINT " + (i + 1), frame, opening, out var label);
                if (regular) activeGateLabels[i] = label;
            }
            Finish = null;
            if (!course.FreeFlight)
            {
                Finish = Gate(course.Finish, "FINISH", passed, opening, out var label);
                if (regular) activeGateLabels[Gates.Length] = label;
            }
            RenderSettings.fog = true;
            RenderSettings.fogColor = canyon ? new Color(.84f, .58f, .40f) : hangar ? new Color(.16f, .22f, .30f) : new Color(.64f, .80f, .90f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 130;
            RenderSettings.ambientLight = canyon ? new Color(.88f, .70f, .53f) : hangar ? new Color(.65f, .72f, .85f) : new Color(.75f, .8f, .75f);
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

        TrainingGate Gate(Vector3 position, string name, Material material, Vector2 opening, out Renderer label)
        {
            var root = new GameObject(name); root.transform.SetParent(layout, false);
            root.transform.position = position;
            var gate = root.AddComponent<TrainingGate>();
            gate.opening = opening;
            const float thickness = .3f;
            float side = opening.x + thickness * .5f, top = opening.y + thickness * .5f;
            Box("Left frame", new Vector3(-side, 0, 0), new Vector3(thickness, opening.y * 2 + thickness * 2, thickness), material, true, root.transform);
            Box("Right frame", new Vector3(side, 0, 0), new Vector3(thickness, opening.y * 2 + thickness * 2, thickness), material, true, root.transform);
            Box("Top frame", new Vector3(0, top, 0), new Vector3(opening.x * 2 + thickness * 2, thickness, thickness), material, true, root.transform);
            Box("Bottom frame", new Vector3(0, -top, 0), new Vector3(opening.x * 2 + thickness * 2, thickness, thickness), material, true, root.transform);
            label = Label(name + "  >", position + new Vector3(0, opening.y + .65f, 0), Quaternion.identity, .16f, root.transform);
            return gate;
        }

        Renderer Label(string text, Vector3 position, Quaternion rotation, float size, Transform parent = null)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.transform.SetParent(parent ? parent : layout, false);
            label.transform.SetPositionAndRotation(position, rotation);
            label.text = text; label.fontSize = 64; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var renderer = label.GetComponent<Renderer>();
            renderer.sharedMaterial = label.font.material;
            return renderer;
        }

        public void Highlight(int next)
        {
            for (int i = 0; i < Gates.Length; i++)
                Gates[i].Highlight(i < next ? 2 : i == next ? 1 : 0, new Color(1, .65f, .12f), Color.green);
            if (Finish) Finish.Highlight(next == Gates.Length ? 1 : 0, new Color(1, .65f, .12f), Color.green);
            // In the longer courses, distant gate labels overlap in the FPV view.
            if (activeGateLabels != null)
                for (int i = 0; i < activeGateLabels.Length; i++) activeGateLabels[i].enabled = i == next;
        }

        void OnDestroy() { foreach (var material in materials) if (material) Destroy(material); }
    }
}
