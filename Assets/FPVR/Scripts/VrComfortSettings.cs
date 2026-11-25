using UnityEngine;

namespace FPVR
{
    internal static class VrComfortSettings
    {
        const string PreferenceKey = "FPVR.ReduceMotionSickness";
        public const bool DefaultEnabled = false;

        public static bool Enabled { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Load() => Enabled = PlayerPrefs.GetInt(PreferenceKey, DefaultEnabled ? 1 : 0) == 1;

        public static void SetEnabled(bool enabled)
        {
            if (Enabled == enabled) return;

            Enabled = enabled;
            PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
