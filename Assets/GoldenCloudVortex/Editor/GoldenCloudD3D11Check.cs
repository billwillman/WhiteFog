using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Run in batch mode with -force-d3d11 -executeMethod GoldenCloudD3D11Check.Run.
public static class GoldenCloudD3D11Check
{
    const string Root = "Assets/GoldenCloudVortex/";
    const string MaterialPath = Root + "GoldenCloud.mat";
    const string Output = "Logs/GoldenVortex/MainProjectCheck";
    const int Width = 640, Height = 360;

    public static void Run()
    {
        if (!Application.isBatchMode || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
            throw new InvalidOperationException("Run in a D3D11 batch editor.");

        Debug.Log($"VORTEX_CHECK project={Application.dataPath} device={SystemInfo.graphicsDeviceName} API={SystemInfo.graphicsDeviceType}");
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(Root + "GoldenCloudVortex.unity");
        var demo = UnityEngine.Object.FindObjectOfType<GoldenCloudVortexDemo>();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (demo.clouds.sharedMaterial != material || AssetDatabase.GetAssetPath(material.shader) != Root + "GoldenCloudVortex.shader")
            throw new InvalidOperationException("Cloud scene did not resolve its material and shader.");

        ShaderUtil.allowAsyncCompilation = false;
        var inspector = Editor.CreateEditor(material);
        var thumb = inspector.RenderStaticPreview(MaterialPath, null, 128, 128);
        if (thumb == null) throw new InvalidOperationException("Material preview was empty.");
        File.WriteAllBytes(Path.Combine(Output, "preview.png"), thumb.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(inspector);
        ShaderUtil.CompilePass(material, 0, true);
        CheckCompiler(material.shader);
        Debug.Log("VORTEX_CHECK material preview rendered; pass 0 compiled.");

        var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        var camera = demo.viewCamera;
        var home = camera.transform.position;
        camera.targetTexture = target;
        camera.aspect = (float)Width / Height;
        foreach (bool fixedCamera in new[] { true, false })
        {
            demo.FixedCamera = fixedCamera;
            var mode = fixedCamera ? "fixed" : "travel";
            Color32[] previous = null;
            foreach (float seconds in new[] { 0f, 4.3f, 8f })
            {
                demo.SetTime(seconds, 512);
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                var pixels = image.GetPixels32();
                int gold = pixels.Count(p => p.r > p.b + 10 && p.g > p.b + 5);
                int magenta = pixels.Count(p => p.r > 245 && p.g < 10 && p.b > 245);
                if (gold < pixels.Length / 5 || magenta > pixels.Length / 100)
                    throw new InvalidOperationException($"{mode} frame: gold={gold}, magenta={magenta}.");
                if (previous != null && previous.SequenceEqual(pixels))
                    throw new InvalidOperationException($"{mode} returned a static frame.");
                previous = pixels;
                string name = $"{mode}-{Mathf.RoundToInt(seconds * 10)}";
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
                Debug.Log($"VORTEX_CHECK {name} gold={gold} magenta={magenta} pixels={pixels.Length}");
            }
            float distance = Vector3.Distance(home, camera.transform.position);
            if (Mathf.Abs(distance - (fixedCamera ? 0f : 40f)) > .01f)
                throw new InvalidOperationException($"{mode} camera travel was {distance}.");
        }
        CheckCompiler(material.shader);
        Debug.Log("VORTEX_CHECK PASS");
    }

    static void CheckCompiler(Shader shader)
    {
        var messages = ShaderUtil.GetShaderMessages(shader);
        if (ShaderUtil.ShaderHasError(shader) || !shader.isSupported)
            throw new InvalidOperationException(string.Join("\n", messages.Select(m => m.message)));
    }
}
