using System.Collections.Generic;
using UGG.Environment;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ProximityToppleObstacleStaticCleaner : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    [MenuItem("Tools/Topple/Clear Static Flags In Build Scenes")]
    private static void ClearStaticFlagsInBuildScenesMenu()
    {
        int componentCount = ClearStaticFlagsInBuildScenes();
        Debug.Log($"[ProximityToppleObstacle] Cleared Static flags for topple hierarchies in build scenes. Components scanned: {componentCount}.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        int componentCount = ClearStaticFlagsInBuildScenes();
        Debug.Log($"[ProximityToppleObstacle] Pre-build static cleanup complete. Components scanned: {componentCount}.");
    }

    private static int ClearStaticFlagsInBuildScenes()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        int totalComponentCount = 0;

        try
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                EditorBuildSettingsScene buildScene = buildScenes[i];
                if (!buildScene.enabled)
                {
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                totalComponentCount += ClearStaticFlagsInLoadedScenes();
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return totalComponentCount;
    }

    private static int ClearStaticFlagsInLoadedScenes()
    {
        ProximityToppleObstacle[] obstacles = Object.FindObjectsByType<ProximityToppleObstacle>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var processedScenes = new HashSet<string>();
        for (int i = 0; i < obstacles.Length; i++)
        {
            ProximityToppleObstacle obstacle = obstacles[i];
            if (obstacle == null)
            {
                continue;
            }

            obstacle.ClearStaticFlagsInEditor();

            string scenePath = obstacle.gameObject.scene.path;
            if (!string.IsNullOrEmpty(scenePath) && processedScenes.Add(scenePath))
            {
                EditorSceneManager.MarkSceneDirty(obstacle.gameObject.scene);
            }
        }

        return obstacles.Length;
    }
}
