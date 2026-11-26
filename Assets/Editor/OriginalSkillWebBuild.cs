using System;
using System.IO;
using System.Linq;
using SoftKitty;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OriginalSkillWebBuild
{
    public static void Build()
    {
        const string sourceScene = "Assets/WhiteFog/SkillPreview/角色预览.unity";
        string output = Environment.GetEnvironmentVariable("QINGYUN_WEB_OUTPUT");
        if (string.IsNullOrEmpty(output)) throw new ArgumentException("Set QINGYUN_WEB_OUTPUT to the destination directory.");
        string tempScene = AssetDatabase.GenerateUniqueAssetPath("Assets/WhiteFog/SkillPreview/网页试放.unity");
        var compression = PlayerSettings.WebGL.compressionFormat;
        bool fallback = PlayerSettings.WebGL.decompressionFallback;
        Scene scene = default;
        if (!AssetDatabase.CopyAsset(sourceScene, tempScene)) throw new IOException(tempScene);
        try
        {
            scene = EditorSceneManager.OpenScene(tempScene, OpenSceneMode.Additive);
            var members = scene.GetRootGameObjects().Select(x => x.GetComponent<EntityComponent>()).Where(x => x).ToArray();
            var driver = new GameObject("QingyunBattle").AddComponent<OriginalSkillWebBattle>();
            SceneManager.MoveGameObjectToScene(driver.gameObject, scene);
            driver.hero = members.Single(x => x.name == "男角色 001");
            driver.enemy = members.Single(x => x.name == "小雕");
            driver.bird = driver.enemy.transform.Find("小雕 · 飞行");
            driver.skills = AssetDatabase.FindAssets("t:OriginalSkillGraph", new[]{"Assets/WhiteFog/Skills/宗师之上"})
                .Select(k => AssetDatabase.LoadAssetAtPath<OriginalSkillGraph>(AssetDatabase.GUIDToAssetPath(k))).ToArray();
            driver.editorSkillIds = OriginalSkillEditorExport.SkillIds();
            driver.editorSkills = driver.editorSkillIds.Select(id =>
                AssetDatabase.LoadAssetAtPath<GraphObject>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
            var camera = scene.GetRootGameObjects().Select(x => x.GetComponent<Camera>()).Single(x => x && x.CompareTag("MainCamera"));
            camera.transform.rotation = Quaternion.Euler(38,0,0);
            camera.transform.position = new Vector3(0,11.05f,20) - camera.transform.forward * 16;
            camera.orthographicSize = 4.86f;
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException(tempScene);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes = new[]{tempScene}, locationPathName = output, target = BuildTarget.WebGL,
                options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("WebGL " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
            Debug.Log("QINGYUN_WEB_BUILD_OK " + report.summary.totalSize);
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.DeleteAsset(tempScene);
        }
    }
}
