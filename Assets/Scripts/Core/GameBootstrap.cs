using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReadyCheck
{
    /// <summary>
    /// Builds the whole first-iteration game from code when you press Play in the Main scene
    /// (or an empty, untitled scene). No prefabs or scene setup needed yet.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string scene = SceneManager.GetActiveScene().name;
            bool supportedScene = scene == "Main" || scene == "SampleScene" || string.IsNullOrEmpty(scene);
            if (!supportedScene) return;
            if (Object.FindFirstObjectByType<EncounterManager>() != null) return;

            Application.targetFrameRate = 60;
            TimeController.SetSlowMo(false);

            SetupCamera();
            Arena.SetupLighting();
            Arena.Build();

            var root = new GameObject("ReadyCheck");
            root.AddComponent<GameInput>();
            root.AddComponent<CommandSystem>();
            root.AddComponent<Barks>();
            root.AddComponent<HUD>();
            root.AddComponent<EncounterManager>();
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.05f, 0.1f);
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            // Sort transparent sprites back-to-front along the world Z axis (good for a fixed 3/4 camera).
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, 0f, 1f);

            if (cam.GetComponent<CameraRig>() == null) cam.gameObject.AddComponent<CameraRig>();
        }
    }
}
