using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FPVR
{
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] string trainingScenePath;
        [SerializeField] Button playButton;
        [SerializeField] Text playLabel;

        bool loading;

        public void Play()
        {
            if (loading) return; // Prevent double-clicking the button while the scene is loading. ignores clicks after

            int trainingIndex = SceneUtility.GetBuildIndexByScenePath(trainingScenePath);
            if (trainingIndex < 0)
            {
                Debug.LogError($"Enable the FPVR training scene in the build scene list: {trainingScenePath}", this);
                return;
            }

            loading = true;
            playButton.interactable = false;
            playLabel.text = "Loading…";
            SceneManager.LoadSceneAsync(trainingIndex, LoadSceneMode.Single);
        }
    }
}
