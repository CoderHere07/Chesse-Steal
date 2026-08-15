using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class ApplyCheckerMaterial
{
    static ApplyCheckerMaterial()
    {
        EditorApplication.delayCall += DoApply;
    }

    static void DoApply()
    {
        if (EditorPrefs.GetBool("CheckerFloorApplied_v1", false)) return;
        EditorPrefs.SetBool("CheckerFloorApplied_v1", true);

        string matPath = "Assets/Tiles074_1K-JPG/CheckerFloorMaterial.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Tiles074_1K-JPG/Tiles074_1K-JPG_Color.jpg");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Tiles074_1K-JPG/Tiles074_1K-JPG_NormalGL.jpg");
        
        if (albedo != null) mat.SetTexture("_MainTex", albedo);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        
        // Ensure tiling is decent since a single cell is a big floor tile
        mat.mainTextureScale = new Vector2(1, 1);
        
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        var mazeGen = Object.FindObjectOfType<MazeGenerator>();
        if (mazeGen != null && mazeGen.floorPrefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(mazeGen.floorPrefab);
            if (instance == null) 
            {
                // Fallback if it's not a prefab
                instance = Object.Instantiate(mazeGen.floorPrefab);
            }

            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }

            string prefabPath = "Assets/CheckerFloorPrefab.prefab";
            GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            
            if (newPrefab != null)
            {
                mazeGen.floorPrefab = newPrefab;
                EditorUtility.SetDirty(mazeGen);
                EditorSceneManager.MarkSceneDirty(mazeGen.gameObject.scene);
                EditorSceneManager.SaveScene(mazeGen.gameObject.scene);
                Debug.Log("[CheckerFloor] Successfully applied Checker Floor Material and saved scene!");
            }
        }
    }
}
