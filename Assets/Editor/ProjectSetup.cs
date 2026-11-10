using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReadyCheck.EditorTools
{
    /// <summary>
    /// One-time project setup that runs when the project is first opened: creates the (empty) Main scene,
    /// opens it and adds it to the build. The game itself builds from code at runtime (see GameBootstrap).
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string SceneDir = "Assets/Scenes";
        const string ScenePath = SceneDir + "/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath))
            {
                CreateOrOpenMainScene();
                Debug.Log("[Ready Check] Created Assets/Scenes/Main.unity. Press Play to start the fight.");
            }
            EnsureInBuildSettings();
        }

        [MenuItem("Ready Check/Open Main Scene")]
        public static void CreateOrOpenMainScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(SceneDir);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            else
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
            EnsureInBuildSettings();
        }

        [MenuItem("Ready Check/Reset Saved Progress")]
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Ready Check] Saved progress (attempts, familiarity) cleared.");
        }

        static void EnsureInBuildSettings()
        {
            if (!File.Exists(ScenePath)) return;
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == ScenePath)) return;
            EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
        }
    }
}
