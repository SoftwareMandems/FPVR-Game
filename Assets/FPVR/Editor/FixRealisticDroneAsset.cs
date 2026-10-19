using System.IO;
using UnityEditor;
using UnityEngine;

namespace FPVR.EditorTools
{
    // One-click fixes for the "Realistic Drone" Asset Store package (not committed to the repo; see README):
    //  - converts its built-in Standard materials to URP Lit so they don't render pink, and
    //  - updates screen.cs, which uses an API removed in Unity 6 and otherwise stops every script compiling.
    static class FixRealisticDroneAsset
    {
        const string Folder = "Assets/Realistic Drone";

        [MenuItem("FPVR/Fix Realistic Drone Asset")]
        static void Fix()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                EditorUtility.DisplayDialog("Realistic Drone not found",
                    "Import \"Realistic Drone\" from the Unity Asset Store first (Window > Package Manager > My Assets).", "OK");
                return;
            }

            string screen = Path.Combine(Folder, "screen.cs");
            if (File.Exists(screen))
            {
                string code = File.ReadAllText(screen);
                string fixedCode = code.Replace("Application.CaptureScreenshot(", "ScreenCapture.CaptureScreenshot(");
                if (fixedCode != code) File.WriteAllText(screen, fixedCode);
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            int converted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material || !lit || material.shader == lit || material.shader.name != "Standard") continue;
                Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : .5f;
                Texture texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                // Pure black reads as a flat silhouette under URP lighting; use a very dark carbon grey instead.
                if (color.maxColorComponent < .02f) color = new Color(.035f, .035f, .04f, color.a);
                material.shader = lit;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                if (texture) material.SetTexture("_BaseMap", texture);
                EditorUtility.SetDirty(material);
                converted++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FPVR: Realistic Drone fixed (" + converted + " materials converted to URP).");
        }
    }
}
