using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPVR.Tutorials.EditorTools
{
    [InitializeOnLoad]
    public static class TutorialEditor
    {
        public const string ScenePath = "Assets/FPVR/Tutorials/Scenes/FPVR_Tutorials.unity";
        const string PreviousStartKey = "FPVR.TutorialPreviousStart";
        static TutorialEditor()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(PreviousStartKey + ".active", false)) return;
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousStartKey, ""));
                SessionState.EraseBool(PreviousStartKey + ".active");
            };
        }

        [MenuItem("FPVR/Tutorials/Open Tutorial Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("FPVR/Tutorials/Play Tutorial Scene")]
        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(PreviousStartKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(PreviousStartKey + ".active", true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorApplication.isPlaying = true;
        }

        // Opt-in, so the tutorial branch doesn't rewrite the menu owner's build/startup settings.
        [MenuItem("FPVR/Tutorials/Add Tutorial Scene to Build List")]
        public static void RegisterScene()
        {
            var scenes = EditorBuildSettings.scenes;
            int index = Array.FindIndex(scenes, scene => scene.path == ScenePath);
            if (index >= 0) { scenes[index].enabled = true; EditorBuildSettings.scenes = scenes; }
            else EditorBuildSettings.scenes = scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        }

        // Batch entry point used to generate the committed scene from the existing drone/rig.
        // It never saves or edits the demo scene.
        public static void BuildScene()
        {
            const string panelPath = "Assets/FPVR/Tutorials/Resources/TutorialPanel.mat";
            Directory.CreateDirectory("Assets/FPVR/Tutorials/Resources");
            var material = AssetDatabase.LoadAssetAtPath<Material>(panelPath);
            if (!material)
            {
                material = new Material(Shader.Find("FPVR/Tutorial Overlay"));
                AssetDatabase.CreateAsset(material, panelPath);
            }
            material.shader = Shader.Find("FPVR/Tutorial Overlay");
            material.color = new Color(.025f, .045f, .065f);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/FPVR/Scenes/FPVR_Demo.unity");
            var demo = UnityEngine.Object.FindAnyObjectByType<TrainingDemo>();
            if (!demo) throw new InvalidOperationException("Demo rig template is missing.");
            var drone = demo.drone; var pilot = demo.pilot; var rotors = demo.rotors;
            UnityEngine.Object.DestroyImmediate(demo.gateContainer.gameObject);
            if (demo.vrHud) UnityEngine.Object.DestroyImmediate(demo.vrHud.gameObject);
            UnityEngine.Object.DestroyImmediate(demo.gameObject);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Environment") UnityEngine.Object.DestroyImmediate(root);
            drone.transform.SetPositionAndRotation(TutorialCourseCatalog.All[0].Start, Quaternion.identity);
            drone.gameObject.AddComponent<TutorialContact>();
            var session = new GameObject("Tutorial Session").AddComponent<TutorialSession>();
            session.drone = drone; session.pilot = pilot; session.rotors = rotors;
            Directory.CreateDirectory("Assets/FPVR/Tutorials/Scenes");
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath)) throw new IOException("Could not save tutorial scene");
            AssetDatabase.Refresh();
            Validate();
        }

        [MenuItem("FPVR/Tutorials/Validate Tutorial Content")]
        public static void Validate()
        {
            TutorialModelChecks.Run();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TutorialSession>()).ToArray();
                if (sessions.Length != 1 || !sessions[0].drone || !sessions[0].pilot || !sessions[0].pilot.view)
                    throw new InvalidOperationException("Flight scene references are incomplete.");
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                            throw new InvalidOperationException("Missing script: " + transform.name);
                if (scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<TrainingDemo>()))
                    throw new InvalidOperationException("Flight scene must have exactly one coordinator.");
                Debug.Log("TUTORIAL SCENE VALIDATION PASSED");
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
