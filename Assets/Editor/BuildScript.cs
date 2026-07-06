using UnityEditor;
using UnityEngine;

public static class BuildScript
{
    [MenuItem("Tools/Build Windows EXE")]
    public static void BuildWindows()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainScene.unity" },
            locationPathName = "Builds/Windows/Platformer3D.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Build result: " + report.summary.result + " | Size: " + report.summary.totalSize + " bytes");
    }
}
