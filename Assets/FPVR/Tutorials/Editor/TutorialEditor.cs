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
        public const string ObstacleScenePath = "Assets/FPVR/Tutorials/Scenes/FPVR_ObstacleCourses.unity";
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
        public static void Open() => OpenScene(ScenePath);

        [MenuItem("FPVR/Courses/Open Obstacle Courses")]
        public static void OpenObstacleCourses() => OpenScene(ObstacleScenePath);

        static void OpenScene(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        [MenuItem("FPVR/Tutorials/Play Tutorial Scene")]
        public static void Play() => PlayScene(ScenePath);

        [MenuItem("FPVR/Courses/Play Obstacle Courses")]
        public static void PlayObstacleCourses() => PlayScene(ObstacleScenePath);

        static void PlayScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(PreviousStartKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(PreviousStartKey + ".active", true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            EditorApplication.isPlaying = true;
        }

        // Opt-in, so the tutorial branch doesn't rewrite the menu owner's build/startup settings.
        [MenuItem("FPVR/Tutorials/Add Tutorial Scene to Build List")]
        public static void RegisterScene() => RegisterScene(ScenePath);

        [MenuItem("FPVR/Courses/Add Obstacle Courses to Build List")]
        public static void RegisterObstacleScene() => RegisterScene(ObstacleScenePath);

        static void RegisterScene(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            int index = Array.FindIndex(scenes, scene => scene.path == path);
            if (index >= 0) { scenes[index].enabled = true; EditorBuildSettings.scenes = scenes; }
            else EditorBuildSettings.scenes = scenes.Concat(new[] { new EditorBuildSettingsScene(path, true) }).ToArray();
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
            BuildFlightScene(ScenePath, false);
            Validate();
        }

        public static void BuildObstacleScene()
        {
            BuildFlightScene(ObstacleScenePath, true);
            ValidateObstacleCourses();
        }

        static void BuildFlightScene(string path, bool regularCourses)
        {
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
            var session = new GameObject(regularCourses ? "Obstacle Course Session" : "Tutorial Session").AddComponent<TutorialSession>();
            session.drone = drone; session.pilot = pilot; session.rotors = rotors;
            session.regularCourses = regularCourses;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path)) throw new IOException("Could not save " + path);
            AssetDatabase.Refresh();
        }

        [MenuItem("FPVR/Tutorials/Validate Tutorial Content")]
        public static void Validate()
        {
            TutorialModelChecks.Run();
            ValidateScene(ScenePath, false);
            Debug.Log("TUTORIAL SCENE VALIDATION PASSED");
        }

        [MenuItem("FPVR/Courses/Validate Obstacle Courses")]
        public static void ValidateObstacleCourses()
        {
            TutorialModelChecks.CheckObstacleCatalog();
            ValidateScene(ObstacleScenePath, true);
            Debug.Log("OBSTACLE COURSE SCENE VALIDATION PASSED");
        }

        static void ValidateScene(string path, bool regularCourses)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TutorialSession>()).ToArray();
                if (sessions.Length != 1 || !sessions[0].drone || !sessions[0].pilot || !sessions[0].pilot.view)
                    throw new InvalidOperationException("Flight scene references are incomplete.");
                if (sessions[0].regularCourses != regularCourses)
                    throw new InvalidOperationException("Flight scene has the wrong activity catalog.");
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                            throw new InvalidOperationException("Missing script: " + transform.name);
                if (scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<TrainingDemo>()))
                    throw new InvalidOperationException("Flight scene must have exactly one coordinator.");
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
