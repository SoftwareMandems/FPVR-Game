using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace FPVR.EditorTools
{
    // Sets the project up for Meta Quest, for PC (Quest Link) and Android (standalone Quest):
    //  1. OpenXR is the XR plug-in and starts when you press Play, so the game runs in the headset.
    //  2. The Quest controller interaction profiles are on. Without one, OpenXR sends no controller input.
    [InitializeOnLoad]
    static class EnableQuestControllers
    {
        static readonly BuildTargetGroup[] Targets = { BuildTargetGroup.Standalone, BuildTargetGroup.Android };

        static EnableQuestControllers() => EditorApplication.delayCall += Apply;

        [MenuItem("FPVR/Set Up Quest VR")]
        static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnableOpenXR();
            foreach (var group in Targets)
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (!settings) continue;
                bool changed = Enable<OculusTouchControllerProfile>(settings) | Enable<MetaQuestTouchPlusControllerProfile>(settings)
                    | Enable<MetaQuestTouchProControllerProfile>(settings);
                if (changed)
                {
                    EditorUtility.SetDirty(settings);
                    UnityEngine.Debug.Log("FPVR: enabled Meta Quest controller profiles for " + group + ".");
                }
            }
            AssetDatabase.SaveAssets();
        }

        static void EnableOpenXR()
        {
            // The XR settings asset is normally created the first time someone opens XR Plug-in Management.
            var create = typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate", BindingFlags.NonPublic | BindingFlags.Static);
            var perTarget = create?.Invoke(null, null) as XRGeneralSettingsPerBuildTarget;
            if (!perTarget)
            {
                UnityEngine.Debug.LogWarning("FPVR: couldn't create XR settings. Open Edit > Project Settings > XR Plug-in Management and tick OpenXR.");
                return;
            }
            foreach (var group in Targets)
            {
                if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                XRGeneralSettings general = perTarget.SettingsForBuildTarget(group);
                XRManagerSettings manager = general ? general.AssignedSettings : null;
                if (!manager) continue;
                general.InitManagerOnStart = true;
                EditorUtility.SetDirty(general);
                if (manager.activeLoaders.Any(l => l is OpenXRLoader)) continue;
                if (XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, group))
                    UnityEngine.Debug.Log("FPVR: enabled OpenXR for " + group + ".");
                EditorUtility.SetDirty(manager);
            }
        }

        static bool Enable<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            if (!feature || feature.enabled) return false;
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
            return true;
        }
    }
}
