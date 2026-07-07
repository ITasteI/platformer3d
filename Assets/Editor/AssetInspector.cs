using UnityEditor;
using UnityEngine;

public static class AssetInspector
{
    [MenuItem("Tools/Inspect Character Asset")]
    public static void InspectCharacter()
    {
        string path = "Assets/KenneyKit/character-oobi.fbx";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var asset in assets)
        {
            Debug.Log("Asset: " + asset.name + " | Type: " + asset.GetType());
        }

        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null)
        {
            Debug.Log("Animation type: " + importer.animationType);
            foreach (var clip in importer.clipAnimations)
                Debug.Log("Clip: " + clip.name);
            Debug.Log("Default clip count: " + importer.defaultClipAnimations.Length);
            foreach (var clip in importer.defaultClipAnimations)
                Debug.Log("Default Clip: " + clip.name);
        }
    }

    [MenuItem("Tools/Inspect Nature Asset")]
    public static void InspectNature()
    {
        string[] testAssets = { "Assets/NatureKit/tree_default.fbx", "Assets/NatureKit/rock_largeA.fbx", "Assets/NatureKit/cliff_large_rock.fbx" };
        foreach (string path in testAssets)
        {
            Debug.Log("=== " + path + " ===");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.Log("NOT FOUND");
                continue;
            }

            foreach (var rend in prefab.GetComponentsInChildren<Renderer>())
            {
                Debug.Log("Renderer: " + rend.name + " matCount=" + rend.sharedMaterials.Length);
                foreach (var mat in rend.sharedMaterials)
                {
                    if (mat == null) { Debug.Log("  material: NULL"); continue; }
                    Debug.Log("  material=" + mat.name + " shader=" + mat.shader.name + " color=" + mat.color + " mainTex=" + (mat.mainTexture != null ? mat.mainTexture.name : "none"));
                }
            }

            Mesh mesh = prefab.GetComponentInChildren<MeshFilter>()?.sharedMesh;
            if (mesh != null)
                Debug.Log("Mesh vertex colors: " + (mesh.colors != null && mesh.colors.Length > 0));
        }
    }
}
