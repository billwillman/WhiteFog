using UnityEditor;
using UnityEditor.Build;

// The standard build window and the MiniGame exporter both read EditorBuildSettings.scenes.
public sealed class FogDemoBuildScenes : IActiveBuildTargetChanged
{
    const string Desktop = "Assets/Scenes/WhiteFog.unity";
    const string Mobile = "Assets/Scenes/WhiteFog_WeChat.unity";

    public int callbackOrder => 0;
    public void OnActiveBuildTargetChanged(BuildTarget previous, BuildTarget current) => Select(current);

    [InitializeOnLoadMethod]
    static void OnLoad() => Select(EditorUserBuildSettings.activeBuildTarget);

    static void Select(BuildTarget target)
    {
        bool mobile = target == BuildTarget.MiniGame || target == BuildTarget.WebGL;
        var scenes = EditorBuildSettings.scenes;
        bool changed = false;
        foreach (var scene in scenes)
        {
            if (scene.path != Desktop && scene.path != Mobile) continue;
            bool enabled = scene.path == (mobile ? Mobile : Desktop);
            if (scene.enabled == enabled) continue;
            scene.enabled = enabled;
            changed = true;
        }
        if (changed) EditorBuildSettings.scenes = scenes;
    }
}
