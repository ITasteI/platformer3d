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
}
