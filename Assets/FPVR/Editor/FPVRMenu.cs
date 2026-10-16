using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPVR.Editor
{
    public static class FPVRMenu
    {
        [MenuItem("FPVR/Open Demo Scene")]
        public static void Open()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/FPVR/Scenes/FPVR_Demo.unity");
        }
        [MenuItem("FPVR/Open Quick Start")]
        public static void Help() => Application.OpenURL("file://" + System.IO.Path.GetFullPath("START_HERE.md"));
    }
}
