using UnityEngine;

namespace FPVR
{
    public class TrainingGate : MonoBehaviour
    {
        [Tooltip("Opening half-size in local units, measured to the inside frame edges.")]
        public Vector2 opening = new Vector2(2.19f, 2.19f);
        Renderer[] frame;
        MaterialPropertyBlock properties;
        public bool Crossed(Vector3 previous, Vector3 current, float droneRadius)
        {
            Vector3 a = transform.InverseTransformPoint(previous), b = transform.InverseTransformPoint(current);
            if (a.z >= 0 || b.z < 0) return false;
            Vector3 p = Vector3.Lerp(a, b, -a.z / (b.z - a.z));
            float x = opening.x - droneRadius / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x));
            float y = opening.y - droneRadius / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.y));
            return Mathf.Abs(p.x) < x && Mathf.Abs(p.y) < y;
        }
        public void Highlight(int state, Color active, Color passed)
        {
            if (frame == null) frame = GetComponentsInChildren<Renderer>();
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.Clear();
            if (state != 0)
            {
                properties.SetColor("_BaseColor", state == 1 ? active : passed);
                properties.SetColor("_Color", state == 1 ? active : passed);
                // HDR emission so the next gate glows (and blooms) and cleared gates glow softly.
                properties.SetColor("_EmissionColor", state == 1 ? active * 2.6f : passed * 1.2f);
            }
            foreach (var r in frame) if (!(r is MeshRenderer && r.GetComponent<TextMesh>() != null))
                r.SetPropertyBlock(properties);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(opening.x * 2, opening.y * 2, .05f));
            Gizmos.DrawLine(Vector3.back * 2, Vector3.forward * 2);
            Gizmos.DrawLine(Vector3.forward * 2, new Vector3(.35f, 0, 1.4f));
            Gizmos.DrawLine(Vector3.forward * 2, new Vector3(-.35f, 0, 1.4f));
        }
    }
}
