using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FPVR.EditorTools
{
    [InitializeOnLoad]
    static class OpenStartupScene
    {
        static OpenStartupScene()
        {
            SetPlayModeStartScene();
            EditorBuildSettings.sceneListChanged += SetPlayModeStartScene;
            EditorApplication.update += InitializeStartup;
        }

        static void SetPlayModeStartScene()
        {
            var first = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);
            EditorSceneManager.playModeStartScene = first == null ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(first.path);
        }

        static void InitializeStartup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= InitializeStartup;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            SetPlayModeStartScene();
            Scene active = SceneManager.GetActiveScene();
            var startup = EditorSceneManager.playModeStartScene;
            if (startup && string.IsNullOrEmpty(active.path) && !active.isDirty && SceneManager.sceneCount == 1)
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(startup));
        }
    }
}
