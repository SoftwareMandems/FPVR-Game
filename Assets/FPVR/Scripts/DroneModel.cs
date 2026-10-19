using System.Collections.Generic;
using UnityEngine;

namespace FPVR
{
    // Swaps the drone's placeholder box visuals for an imported 3D model at runtime.
    // Only the model's meshes are kept: any scripts, rigidbodies, colliders, cameras or lines that came
    // with the asset are stripped so they can't fight our flight physics. The model is scaled and
    // centred to fit where the old visuals were, so the collider and FPV camera still line up.
    public static class DroneModel
    {
        public struct Result { public Transform[] rotors; public float[] spin; }

        public static Result Apply(DroneFlight drone, GameObject prefab, float sizeMultiplier, string propellerPrefix)
        {
            Transform visuals = drone.transform.Find("Visuals");
            Bounds before = WorldBounds(visuals ? visuals : drone.transform);

            // Instantiate under an inactive holder so none of the asset's own scripts ever wake up.
            var holder = new GameObject("Model Holder");
            holder.SetActive(false);
            GameObject model = Object.Instantiate(prefab, holder.transform);
            model.name = "Model";
            Strip(model);
            model.transform.SetParent(drone.transform, false);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Object.Destroy(holder);
            if (visuals) visuals.gameObject.SetActive(false);

            // Fit the model's footprint to the old visuals, then centre it on them.
            Bounds after = WorldBounds(model.transform);
            float oldSize = Mathf.Max(before.size.x, before.size.z), newSize = Mathf.Max(after.size.x, after.size.z);
            if (newSize > .0001f && oldSize > .0001f)
                model.transform.localScale *= oldSize / newSize * sizeMultiplier;
            after = WorldBounds(model.transform);
            model.transform.position += before.center - after.center;

            var rotors = new List<Transform>(); var spin = new List<float>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(propellerPrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    rotors.Add(t);
                    // On a "+" quad, the front/back pair spins one way and the side pair the other.
                    Vector3 local = drone.transform.InverseTransformPoint(Centre(t));
                    spin.Add(Mathf.Abs(local.z) >= Mathf.Abs(local.x) ? 1 : -1);
                }
            return new Result { rotors = rotors.ToArray(), spin = spin.ToArray() };
        }

        // Centre of a propeller's mesh, which is its hub even if the mesh pivot is somewhere else.
        public static Vector3 Centre(Transform t)
        {
            var r = t.GetComponentInChildren<Renderer>();
            return r ? r.bounds.center : t.position;
        }

        static void Strip(GameObject root)
        {
            // Scripts first (they may require the physics components), then joints, then everything else.
            for (int pass = 0; pass < 3; pass++)
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (!c || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer) continue;
                    bool now = pass == 0 ? c is MonoBehaviour : pass == 1 ? c is Joint : true;
                    if (now) Object.DestroyImmediate(c);
                }
        }

        static Bounds WorldBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
