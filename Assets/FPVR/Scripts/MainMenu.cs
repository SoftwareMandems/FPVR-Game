using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPVR
{
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] string tutorialScenePath;
        [SerializeField] string obstacleScenePath;
        [SerializeField] string demoScenePath;
        [SerializeField] GameObject menuContent;
        [SerializeField] Font menuFont;

        LevelSelectionView levelSelection;
        bool loading;

        void Start()
        {
            if (LevelSelectionNavigation.TakeReturn(gameObject.scene.path)) Play();
        }

        public void Play()
        {
            if (loading) return;

            levelSelection ??= new LevelSelectionView((RectTransform)transform, menuFont,
                course => LoadLevel(tutorialScenePath, course.Id),
                course => LoadLevel(obstacleScenePath, course.Id), () => LoadLevel(demoScenePath), ShowMainMenu);
            menuContent.SetActive(false);
            levelSelection.Show(true);
            ResizeLevelSelection();
        }

        void ShowMainMenu()
        {
            if (loading) return;

            levelSelection.Show(false);
            menuContent.SetActive(true);
        }

        void OnRectTransformDimensionsChange() => ResizeLevelSelection();

        void ResizeLevelSelection()
        {
            if (levelSelection != null) levelSelection.Fit(((RectTransform)transform).rect.size);
        }

        void LoadLevel(string scenePath, string courseId = null)
        {
            if (loading) return;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (buildIndex < 0)
            {
                levelSelection.SetStatus("This level is unavailable in this build.");
                Debug.LogError($"Enable the FPVR level scene in the build scene list: {scenePath}", this);
                return;
            }

            loading = true;
            levelSelection.SetStatus("Loading level…", loading: true);
            if (courseId != null) LevelSelectionNavigation.QueueCourse(scenePath, courseId, gameObject.scene.path);
            SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        }
    }
}
