using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Blackjack.Editor
{
    /// <summary>
    /// One-click setup for URP post-processing Bloom on the Blackjack scene.
    /// Run via Tools > Blackjack > Setup Bloom.
    /// What it does:
    ///   1. Creates a VolumeProfile with a Bloom override at Assets/Settings/BlackjackBloom.asset.
    ///   2. Adds a Global Volume GameObject to the active scene using that profile.
    ///   3. Enables renderPostProcessing on the Main Camera.
    ///   4. Switches the main Canvas from Screen Space Overlay to Screen Space Camera.
    /// </summary>
    public static class BloomSetup
    {
        private const string ProfilePath = "Assets/Settings/BlackjackBloom.asset";
        private const string VolumeName  = "Bloom Volume";

        [MenuItem("Tools/Blackjack/Setup Bloom")]
        public static void Setup()
        {
            CreateVolumeProfile();
            SetupScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[BloomSetup] URP Bloom setup complete.");
        }

        // ── Volume Profile ────────────────────────────────────────────────────────

        private static void CreateVolumeProfile()
        {
            System.IO.Directory.CreateDirectory(
                System.IO.Path.Combine(Application.dataPath, "Settings"));

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            if (!profile.TryGet<Bloom>(out var bloom))
                bloom = profile.Add<Bloom>();

            bloom.active                 = true;
            bloom.intensity.Override(1.5f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(true);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        // ── Scene ─────────────────────────────────────────────────────────────────

        private static void SetupScene()
        {
            Camera mainCam = Camera.main;

            // 1. Enable post-processing on the Main Camera
            if (mainCam != null)
            {
                var camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                    EditorUtility.SetDirty(camData);
                }
            }

            // 2. Switch Canvas to Screen Space Camera so post-processing applies to it
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null && mainCam != null)
            {
                canvas.renderMode    = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera   = mainCam;
                canvas.planeDistance = 1f;
                EditorUtility.SetDirty(canvas);
            }

            // 3. Create a global Volume in the scene if none exists yet
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Volume existing = Object.FindFirstObjectByType<Volume>();

            if (existing == null)
            {
                GameObject volumeGO = new GameObject(VolumeName);
                Volume     volume   = volumeGO.AddComponent<Volume>();
                volume.isGlobal     = true;
                volume.profile      = profile;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            else if (existing.profile == null)
            {
                existing.profile = profile;
                EditorUtility.SetDirty(existing);
            }
        }
    }
}
