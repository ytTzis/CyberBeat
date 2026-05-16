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

    [MenuItem("Tools/Dynamic Objects/Clear Static Flags In Build Scenes")]
    private static void ClearStaticFlagsInBuildScenesMenu()
    {
        (int toppleCount, int doorCount) = ClearStaticFlagsInBuildScenes();
        Debug.Log($"[DynamicObjects] Cleared Static flags in build scenes. Topple components: {toppleCount}, door components: {doorCount}.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        (int toppleCount, int doorCount) = ClearStaticFlagsInBuildScenes();
        Debug.Log($"[DynamicObjects] Pre-build static cleanup complete. Topple components: {toppleCount}, door components: {doorCount}.");
    }

    private static (int toppleCount, int doorCount) ClearStaticFlagsInBuildScenes()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        int totalToppleCount = 0;
        int totalDoorCount = 0;

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
                (int toppleCount, int doorCount) = ClearStaticFlagsInLoadedScenes();
                totalToppleCount += toppleCount;
                totalDoorCount += doorCount;
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return (totalToppleCount, totalDoorCount);
    }

    private static (int toppleCount, int doorCount) ClearStaticFlagsInLoadedScenes()
    {
        ProximityToppleObstacle[] obstacles = Object.FindObjectsByType<ProximityToppleObstacle>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        ProximitySlidingDoubleDoor[] doors = Object.FindObjectsByType<ProximitySlidingDoubleDoor>(
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

            ClearStaticFlagsOnHostHierarchy(obstacle.transform);
            obstacle.ClearStaticFlagsInEditor();

            string scenePath = obstacle.gameObject.scene.path;
            if (!string.IsNullOrEmpty(scenePath) && processedScenes.Add(scenePath))
            {
                EditorSceneManager.MarkSceneDirty(obstacle.gameObject.scene);
            }
        }

        for (int i = 0; i < doors.Length; i++)
        {
            ProximitySlidingDoubleDoor door = doors[i];
            if (door == null)
            {
                continue;
            }

            ClearStaticFlagsOnHostHierarchy(door.transform);
            door.ClearStaticFlagsInEditor();

            string scenePath = door.gameObject.scene.path;
            if (!string.IsNullOrEmpty(scenePath) && processedScenes.Add(scenePath))
            {
                EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
            }
        }

        return (obstacles.Length, doors.Length);
    }

    private static void ClearStaticFlagsOnHostHierarchy(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform current = allTransforms[i];
            if (current == null)
            {
                continue;
            }

            GameObject currentObject = current.gameObject;
            if (currentObject == null || !currentObject.isStatic)
            {
                continue;
            }

            currentObject.isStatic = false;
            EditorUtility.SetDirty(currentObject);
        }
    }
}
