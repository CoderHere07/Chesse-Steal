using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public static class DynamicMazeSetupHelper
{
    [MenuItem("Tools/Setup Dynamic Maze System", false, -100)]
    [MenuItem("Window/Cheese Steal/Setup Dynamic Maze System", false, 0)]
    [MenuItem("GameObject/Cheese Steal/Setup Dynamic Maze System", false, 0)]
    public static void ExecuteSetup()
    {
        Debug.Log("[DynamicMazeSetup] Starting automated setup...");

        // 1. Ensure 'Generated' Tag exists in TagManager
        EnsureGeneratedTag();

        // 2. Ensure Assets/Resources/Prefabs folder exists
        string prefabsFolder = "Assets/Resources/Prefabs";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(prefabsFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

        // Load Materials
        Material wallMat  = AssetDatabase.LoadAssetAtPath<Material>("Assets/Texture/Materials/medieval_blocks_03_diff_4k.mat");
        Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Texture/Materials/laminate_floor_02_diff_4k.mat");

        // Fallbacks if material paths changed
        if (wallMat == null)  wallMat  = new Material(Shader.Find("Standard"));
        if (floorMat == null) floorMat = new Material(Shader.Find("Standard"));

        // Create Trap / Spike Material
        Material spikeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Prefabs/SpikeMat.mat");
        if (spikeMat == null)
        {
            spikeMat = new Material(Shader.Find("Standard"));
            spikeMat.color = new Color(0.85f, 0.15f, 0.15f); // Red
            AssetDatabase.CreateAsset(spikeMat, "Assets/Resources/Prefabs/SpikeMat.mat");
        }

        // Create Cheese Material
        Material cheeseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Prefabs/CheeseMat.mat");
        if (cheeseMat == null)
        {
            cheeseMat = new Material(Shader.Find("Standard"));
            cheeseMat.color = new Color(1f, 0.85f, 0.1f); // Yellow
            AssetDatabase.CreateAsset(cheeseMat, "Assets/Resources/Prefabs/CheeseMat.mat");
        }

        // 3. Create Prefabs
        GameObject wallTilePrefab = CreateOrLoadPrefab("WallTile", PrimitiveType.Cube, (go) =>
        {
            go.transform.localScale = Vector3.one;
            if (wallMat != null) go.GetComponent<Renderer>().sharedMaterial = wallMat;
            if (go.GetComponent<BoxCollider>() == null) go.AddComponent<BoxCollider>();
        });

        GameObject floorTilePrefab = CreateOrLoadPrefab("FloorTile", PrimitiveType.Cube, (go) =>
        {
            go.transform.localScale = Vector3.one;
            if (floorMat != null) go.GetComponent<Renderer>().sharedMaterial = floorMat;
            if (go.GetComponent<BoxCollider>() == null) go.AddComponent<BoxCollider>();
        });

        GameObject upDownTrapPrefab = CreateOrLoadPrefab("DynamicUpDownTrapPrefab", PrimitiveType.Cube, (go) =>
        {
            go.transform.localScale = new Vector3(4.5f, 3f, 4.5f);
            if (spikeMat != null) go.GetComponent<Renderer>().sharedMaterial = spikeMat;
            if (go.GetComponent<BoxCollider>() == null) go.AddComponent<BoxCollider>();
            if (go.GetComponent<DynamicUpDownTrap>() == null) go.AddComponent<DynamicUpDownTrap>();
        });

        GameObject crushWallPrefab = CreateOrLoadPrefab("DynamicCrushWallPrefab", PrimitiveType.Cube, (go) =>
        {
            go.transform.localScale = new Vector3(5.8f, 34.5f, 0.5f);
            if (wallMat != null) go.GetComponent<Renderer>().sharedMaterial = wallMat;
            if (go.GetComponent<BoxCollider>() == null) go.AddComponent<BoxCollider>();
            
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            if (go.GetComponent<DynamicCrushTrap>() == null) go.AddComponent<DynamicCrushTrap>();
        });

        GameObject cheesePrefab = CreateOrLoadPrefab("CheesePrefab", PrimitiveType.Sphere, (go) =>
        {
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            if (cheeseMat != null) go.GetComponent<Renderer>().sharedMaterial = cheeseMat;
            
            // Remove any box collider if present
            BoxCollider bc = go.GetComponent<BoxCollider>();
            if (bc != null) UnityEngine.Object.DestroyImmediate(bc);

            SphereCollider sc = go.GetComponent<SphereCollider>();
            if (sc == null) sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;

            if (go.GetComponent<CheeseCollectible>() == null) go.AddComponent<CheeseCollectible>();
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Disable static scene maze objects to prevent overlap (deep scan)
        GameObject[] allSceneObjs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allSceneObjs)
        {
            if (go.CompareTag("Generated")) continue;
            string n = go.name.ToLower();
            if (n == "walls" || n == "ground" || n.StartsWith("trap trigger") || (n == "cheese" && go.transform.parent == null))
            {
                Undo.RecordObject(go, "Disable Static Scene Object");
                go.SetActive(false);
                Debug.Log($"[DynamicMazeSetup] Disabled static scene object '{go.name}'");
            }
        }

        // 4. Scene Setup — MazeGenerator GameObject
        MazeGenerator generator = UnityEngine.Object.FindAnyObjectByType<MazeGenerator>();
        if (generator == null)
        {
            GameObject genGO = new GameObject("MazeGenerator");
            generator = genGO.AddComponent<MazeGenerator>();
            Undo.RegisterCreatedObjectUndo(genGO, "Create MazeGenerator");
        }
        generator.wallPrefab  = wallTilePrefab;
        generator.floorPrefab = floorTilePrefab;
        EditorUtility.SetDirty(generator);

        // 5. Scene Setup — TrapSpawner GameObject
        TrapSpawner spawner = UnityEngine.Object.FindAnyObjectByType<TrapSpawner>();
        if (spawner == null)
        {
            GameObject spawnerGO = new GameObject("TrapSpawner");
            spawner = spawnerGO.AddComponent<TrapSpawner>();
            Undo.RegisterCreatedObjectUndo(spawnerGO, "Create TrapSpawner");
        }
        spawner.cheesePrefab     = cheesePrefab;
        spawner.upDownTrapPrefab = upDownTrapPrefab;
        spawner.crushWallPrefab  = crushWallPrefab;
        EditorUtility.SetDirty(spawner);

        // 6. Scene Setup — Attach TrapProximityDetector to Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            if (player.GetComponent<TrapProximityDetector>() == null)
            {
                Undo.AddComponent<TrapProximityDetector>(player);
                Debug.Log("[DynamicMazeSetup] Added TrapProximityDetector to Player.");
            }
        }
        else
        {
            Debug.LogWarning("[DynamicMazeSetup] No GameObject with tag 'Player' found in the active scene! Please ensure your player has the 'Player' tag.");
        }

        // Save scene modifications
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[DynamicMazeSetup] ✅ Setup completed successfully!");
        EditorUtility.DisplayDialog("Dynamic Maze Setup", 
            "Dynamic Maze System setup completed successfully!\n\n" +
            "• Prefabs created in Assets/Resources/Prefabs/\n" +
            "• MazeGenerator & TrapSpawner configured in scene\n" +
            "• 'Generated' tag registered\n" +
            "• Player proximity detector attached\n\n" +
            "Press PLAY to test your dynamic maze!", "OK");
    }

    private static GameObject CreateOrLoadPrefab(string name, PrimitiveType primitiveType, System.Action<GameObject> configure)
    {
        string path = $"Assets/Resources/Prefabs/{name}.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefabAsset != null) return prefabAsset;

        // Create temporary primitive
        GameObject tempObj = GameObject.CreatePrimitive(primitiveType);
        tempObj.name = name;
        configure?.Invoke(tempObj);

        // Save as prefab
        prefabAsset = PrefabUtility.SaveAsPrefabAsset(tempObj, path);
        UnityEngine.Object.DestroyImmediate(tempObj);

        Debug.Log($"[DynamicMazeSetup] Created prefab: {path}");
        return prefabAsset;
    }

    private static void EnsureGeneratedTag()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        bool found = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue.Equals("Generated"))
            {
                found = true;
                break;
            }
        }
        if (!found)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            SerializedProperty n = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
            n.stringValue = "Generated";
            tagManager.ApplyModifiedProperties();
            Debug.Log("[DynamicMazeSetup] Added 'Generated' tag to ProjectSettings.");
        }
    }
}
#endif
