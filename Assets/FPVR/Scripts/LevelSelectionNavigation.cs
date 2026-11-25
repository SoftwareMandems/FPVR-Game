using UnityEngine;

namespace FPVR
{
    // Pass single-use destinations through the scene changes between the menu and a flight session.
    static class LevelSelectionNavigation
    {
        static (string scenePath, string courseId, string menuPath) pendingCourse;
        static string returnPath;

        public static void QueueCourse(string scenePath, string courseId, string menuPath)
        {
            pendingCourse = (scenePath, courseId, menuPath);
        }

        public static bool TryTakeCourse(string scenePath, out string courseId, out string menuPath)
        {
            courseId = null;
            menuPath = null;
            if (pendingCourse.scenePath != scenePath) return false;

            (_, courseId, menuPath) = pendingCourse;
            pendingCourse = default;
            return true;
        }

        public static void QueueReturn(string menuPath) => returnPath = menuPath;

        public static bool TakeReturn(string menuPath)
        {
            if (returnPath != menuPath) return false;

            returnPath = null;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear()
        {
            pendingCourse = default;
            returnPath = null;
        }
    }
}
