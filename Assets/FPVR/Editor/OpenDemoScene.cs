using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FPVR.EditorTools
{
    // Pressing Play always starts the demo scene, and an empty startup scene is swapped for it.
    [InitializeOnLoad]
    static class OpenDemoScene
    {
        const string ScenePath = "Assets/FPVR/Scenes/FPVR_Demo.unity";
        static OpenDemoScene()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Scene active = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty && SceneManager.sceneCount == 1)
                    EditorSceneManager.OpenScene(ScenePath);
            };
        }
    }
}
