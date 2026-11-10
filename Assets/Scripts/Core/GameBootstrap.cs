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
            cam.backgroundColor = new Color(0.086f, 0.07f, 0.11f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            // Everything is a flat sprite; draw order comes from each renderer's sortingOrder.
            cam.transparencySortMode = TransparencySortMode.Orthographic;

            if (cam.GetComponent<CameraRig>() == null) cam.gameObject.AddComponent<CameraRig>();
        }
    }
}
