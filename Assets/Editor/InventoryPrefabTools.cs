using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InventoryPrefabTools
{
    private const string SourceScenePath = "Assets/1_GameScene.unity";
    private const string PrefabFolderPath = "Assets/Prefabs/Inventory";
    private const string BagRootName = "Bag";
    private const string BagBarName = "BagInventoryBar";
    private static readonly string[] PickupNames = { "MonstourRed", "MonstourBlue" };

    [MenuItem("Tools/Inventory/Export Inventory Kit Prefabs From 1_GameScene")]
    public static void ExportInventoryKitPrefabs()
    {
        if (!File.Exists(SourceScenePath))
        {
            Debug.LogError($"[InventoryPrefabTools] Source scene not found: {SourceScenePath}");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene activeSceneBeforeExport = SceneManager.GetActiveScene();
        string restoreScenePath = activeSceneBeforeExport.path;

        try
        {
            EditorSceneManager.OpenScene(SourceScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            EnsureFolderExists(PrefabFolderPath);

            ExportNamedRoot(BagRootName);
            for (int i = 0; i < PickupNames.Length; i++)
            {
                ExportNamedRoot(PickupNames[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[InventoryPrefabTools] Exported inventory kit prefabs to '{PrefabFolderPath}'.");
        }
        finally
        {
            if (!string.IsNullOrEmpty(restoreScenePath) && File.Exists(restoreScenePath))
            {
                EditorSceneManager.OpenScene(restoreScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
        }
    }

    [MenuItem("Tools/Inventory/Instantiate Inventory Kit In Active Scene")]
    public static void InstantiateInventoryKitInActiveScene()
    {
        EnsurePrefabsExist();

        GameObject bagPrefab = LoadPrefab(BagRootName);
        if (bagPrefab == null)
        {
            return;
        }

        if (GameObject.Find(BagRootName) == null)
        {
            GameObject bagInstance = (GameObject)PrefabUtility.InstantiatePrefab(bagPrefab);
            bagInstance.name = BagRootName;
            Undo.RegisterCreatedObjectUndo(bagInstance, "Instantiate Bag Prefab");
        }

        for (int i = 0; i < PickupNames.Length; i++)
        {
            if (GameObject.Find(PickupNames[i]) != null)
            {
                continue;
            }

            GameObject pickupPrefab = LoadPrefab(PickupNames[i]);
            if (pickupPrefab == null)
            {
                continue;
            }

            GameObject pickupInstance = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab);
            pickupInstance.name = PickupNames[i];
            Undo.RegisterCreatedObjectUndo(pickupInstance, $"Instantiate {PickupNames[i]} Prefab");
        }

        Scene activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log("[InventoryPrefabTools] Instantiated inventory kit prefabs in the active scene.");
    }

    [MenuItem("Tools/Inventory/Select Exported Inventory Prefab Folder")]
    public static void SelectExportedInventoryPrefabFolder()
    {
        EnsureFolderExists(PrefabFolderPath);
        Object folder = AssetDatabase.LoadAssetAtPath<Object>(PrefabFolderPath);
        if (folder != null)
        {
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }

    private static void ExportNamedRoot(string rootName)
    {
        GameObject rootObject = GameObject.Find(rootName);
        if (rootObject == null)
        {
            Debug.LogWarning($"[InventoryPrefabTools] Could not find '{rootName}' in {SourceScenePath}.");
            return;
        }

        if (rootName == BagRootName)
        {
            ValidateBagHierarchy(rootObject);
        }

        string prefabPath = GetPrefabPath(rootName);
        PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
    }

    private static void ValidateBagHierarchy(GameObject bagRoot)
    {
        Transform bagBar = bagRoot.transform.Find(BagBarName);
        if (bagBar == null)
        {
            Debug.LogWarning($"[InventoryPrefabTools] '{BagRootName}' does not contain child '{BagBarName}'.");
            return;
        }

        for (int i = 1; i <= 5; i++)
        {
            string slotName = $"Slot{i}";
            if (bagBar.Find(slotName) == null)
            {
                Debug.LogWarning($"[InventoryPrefabTools] '{BagBarName}' is missing child '{slotName}'.");
            }
        }
    }

    private static void EnsurePrefabsExist()
    {
        bool missingPrefab = LoadPrefab(BagRootName) == null;
        for (int i = 0; i < PickupNames.Length && !missingPrefab; i++)
        {
            missingPrefab = LoadPrefab(PickupNames[i]) == null;
        }

        if (!missingPrefab)
        {
            return;
        }

        ExportInventoryKitPrefabs();
    }

    private static GameObject LoadPrefab(string prefabName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath(prefabName));
    }

    private static string GetPrefabPath(string prefabName)
    {
        return $"{PrefabFolderPath}/{prefabName}.prefab";
    }

    private static void EnsureFolderExists(string assetFolderPath)
    {
        string[] parts = assetFolderPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }
}
