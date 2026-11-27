using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

// Capture the restored particle prefabs from the same camera elevation as the local battle.
public static class LocalBattleEffectExport
{
    [Serializable] private class Sheet {
        public string image, light;
        public int rate = 30, cell = 320, cols = 8, count;
        public float metres;
        public bool depthCoverage;
        public float innerFraction;
    }

    // Reuse the particle shader's UV, dissolve and opacity calculations when recording visible depths.
    private sealed class ParticleDepth : IDisposable
    {
        private readonly List<(Renderer renderer, Material[] original, Material[] depth)> bindings = new();
        private readonly Dictionary<Material, Material> copies = new();
        public bool Available => bindings.Count > 0;

        public ParticleDepth(GameObject root, Camera camera)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            if (materials.Length == 0 || materials.Any(m => !m || m.shader.name != "WhiteFog/Original Skill Particle")) return;
            int always = (int)CompareFunction.Always;
            if (materials.All(m => m.GetFloat("_ZTest") == always)) return;
            float origin = camera.WorldToViewportPoint(root.transform.position).z;
            foreach (var source in materials)
            {
                var depth = new Material(source);
                depth.EnableKeyword("H5_PARTICLE_DEPTH_CAPTURE");
                depth.SetVector("_H5DepthCapture", new Vector4(origin, .5f / camera.orthographicSize,
                    source.GetFloat("_FxDstBlend") == (int)BlendMode.One ? 1 : 0, source.GetFloat("_ZTest") == always ? 1 : 0));
                depth.SetFloat("_FxSrcBlend", (int)BlendMode.One);
                depth.SetFloat("_FxDstBlend", (int)BlendMode.Zero);
                depth.SetFloat("_ZWrite", 1);
                copies.Add(source, depth);
            }
            foreach (var r in renderers)
            {
                var originals = r.sharedMaterials;
                bindings.Add((r, originals, originals.Select(m => copies[m]).ToArray()));
            }
        }

        public void Use(bool depth)
        {
            foreach (var binding in bindings) binding.renderer.sharedMaterials = depth ? binding.depth : binding.original;
        }
        public void Dispose()
        {
            foreach (var copy in copies.Values) UnityEngine.Object.DestroyImmediate(copy);
        }
    }

    [MenuItem("Tools/技能工作台/烘焙 H5 战斗粒子")]
    public static void Export()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var cameraObject = new GameObject("H5 粒子相机");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.scene = scene;
        camera.enabled = false;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.allowHDR = true;
        camera.transform.rotation = Quaternion.Euler(38,0,0);
        camera.transform.position = -camera.transform.forward * 30;
        var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QingyunSect/LoginDemo/battle-art/effects");
        Directory.CreateDirectory(folder);
        var captured = new HashSet<string>();
        try {
            foreach (var guid in AssetDatabase.FindAssets("t:OriginalSkillGraph", new[]{"Assets/WhiteFog/Skills/宗师之上"})) {
                var graph = AssetDatabase.LoadAssetAtPath<SoftKitty.OriginalSkillGraph>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var action in graph.RootSet.Actions) {
                    if (action.ClassName == "OriginalSkillVisual" && !SoftKitty.SkillWorkbench.IsSourceTime(action)) {
                        var cue = SoftKitty.OriginalSkillVisualAction.EffectFor(action,graph);
                        if (cue?.prefab) Capture(cue.prefab, graph.previewAsOpponent != cue.onTarget);
                    } else if (action.ClassName == "Projectile" && action.projectileSnap && action.projectileSnap.settings != null) {
                        var shot = action.projectileSnap.settings.WithOverrides(action);
                        Capture(shot.prefab,graph.previewAsOpponent);
                        Capture(shot.trail,graph.previewAsOpponent);
                        Capture(shot.impact,false);
                    }
                }
                void Capture(GameObject prefab,bool reversed) {
                    if (!prefab ||
                        !prefab.GetComponentInChildren<ParticleSystem>(true)) return;
                    var key = prefab.name + (reversed ? "-back" : "-front");
                    if (!captured.Add(key)) return;
                    var sheet = Render(prefab,reversed,scene,camera);
                    File.WriteAllText(Path.Combine(folder,key+".js"),"window.localBattleSheets ??= {};\nwindow.localBattleSheets[\"" + key +
                        "\"] = " + JsonUtility.ToJson(sheet) + ";\n",new System.Text.UTF8Encoding(false));
                    Debug.Log("LOCAL_FX " + key + " frames=" + sheet.count);
                }
            }
        } finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static Sheet Render(GameObject prefab, bool reversed, Scene scene, Camera camera, int extentPass = 0)
    {
        for (; extentPass <= 6; extentPass++)
        {
            var sheet = RenderAtExtent(prefab, reversed, scene, camera, extentPass);
            if (sheet != null) return sheet;
        }
        throw new InvalidDataException("特效图集超出取景范围 " + prefab.name);
    }

    private static Sheet RenderAtExtent(GameObject prefab, bool reversed, Scene scene, Camera camera, int extentPass)
    {
        var sheet = new Sheet();
        var go = UnityEngine.Object.Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(go,scene);
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,reversed?180:0,0));
        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        var roots = systems.Where(p => !p.transform.parent || !p.transform.parent.GetComponentInParent<ParticleSystem>()).ToArray();
        for (int i=0; i<systems.Length; i++)
        {
            var p = systems[i];
            p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            if (p.useAutoRandomSeed) p.randomSeed = (uint)i + 1;
        }
        sheet.cell = extentPass==0 ? 320 : 480;
        int n = sheet.cell;
        // Recover emitted light and background transmission before the runtime target clamps their sum.
        var saved = RenderTexture.active;
        camera.orthographicSize=2.4f*Mathf.Pow(1.65f,extentPass);
        sheet.metres=camera.orthographicSize*2;
        sheet.innerFraction = Mathf.Min(.75f, 12f / sheet.metres);
        int resolution = Mathf.Min(2048, Mathf.CeilToInt(n * 1.5f / sheet.innerFraction));
        var rt = RenderTexture.GetTemporary(resolution,resolution,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Bilinear;
        var cell = RenderTexture.GetTemporary(n,n,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
        var sampling = new Material(Shader.Find("Hidden/WhiteFog/H5 Battle Atlas Sampling"));
        sampling.SetFloat("_InnerFraction", sheet.innerFraction);
        var buffer = new Texture2D(n,n,TextureFormat.RGBAHalf,false,true);
        camera.targetTexture=rt;
        using var depth = new ParticleDepth(go, camera);
        sheet.depthCoverage = depth.Available;
        var frames = new List<Color32[]>();
        var lightFrames = new List<Color32[]>();
        bool touchesEdge=false;
        try {
            // Capture a black and a white plate so both additive light and dark alpha-blended ink survive.
            const float simulationStep = 1f / 60f;
            int simulatedSteps = 0;
            for (int i=0;i<sheet.rate*6;i++) {
                float age=(i+.5f)/sheet.rate;
                // A live system receives its first particle update on the frame it spawns.
                int targetSteps = 1 + Mathf.RoundToInt(age / simulationStep);
                while (simulatedSteps < targetSteps)
                {
                    foreach (var p in roots) p.Simulate(simulationStep, true, simulatedSteps == 0, false);
                    simulatedSteps++;
                }
                Color[] Plate(Color background) {
                    camera.backgroundColor=background;camera.Render();Graphics.Blit(rt,cell,sampling);RenderTexture.active=cell;
                    buffer.ReadPixels(new Rect(0,0,n,n),0,0,false);return buffer.GetPixels();
                }
                var emission=Plate(Color.black); var white=Plate(Color.white);
                Color[] depths = null;
                if (depth.Available)
                {
                    depth.Use(true);
                    depths = Plate(Color.clear);
                    depth.Use(false);
                }
                var black=new Color32[emission.Length];
                var light=new Color32[black.Length];
                bool visible=false;
                for (int j=0;j<black.Length;j++) {
                    var a=emission[j];var b=white[j];
                    float transmission = Mathf.Clamp01(Mathf.Max(b.r-a.r,Mathf.Max(b.g-a.g,b.b-a.b)));
                    int alpha=Mathf.RoundToInt(255*(1f-transmission));
                    float peak=Mathf.Max(a.r,Mathf.Max(a.g,a.b));
                    if(peak>.0001f){float Pack(float c)=>Mathf.Sqrt(Mathf.Max(0,c)/(1f+Mathf.Max(0,c)));
                        light[j]=new Color(Pack(a.r),Pack(a.g),Pack(a.b),1f);}
                    black[j]=depth.Available
                        ? (Color32)new Color(alpha/255f,depths[j].r,depths[j].g,1)
                        : new Color32(0,0,0,(byte)alpha);
                    visible |= peak>.002f || alpha>2;
                    if((peak>.01f || alpha>8) && (j<n || j>=black.Length-n || j%n==0 || j%n==n-1)) touchesEdge=true;
                }
                frames.Add(black);lightFrames.Add(light);
                if(visible)sheet.count=i+1;
                if(touchesEdge && extentPass<6)break;
                if(i>=sheet.rate && i-sheet.count>sheet.rate/2 && systems.All(p=>p.particleCount==0))break;
            }
            if(sheet.count==0)throw new InvalidDataException("特效未生成可见帧 "+prefab.name);
            if(touchesEdge && extentPass<6) {
                return null;
            }
            int rows=(sheet.count+sheet.cols-1)/sheet.cols;
            var atlas=new Texture2D(n*sheet.cols,n*rows,TextureFormat.RGBA32,false,true);
            try {
                atlas.SetPixels32(new Color32[atlas.width*atlas.height]);
                for(int i=0;i<sheet.count;i++) atlas.SetPixels32((i%sheet.cols)*n,(rows-1-i/sheet.cols)*n,n,n,frames[i]);
                atlas.Apply();sheet.image="data:image/png;base64,"+Convert.ToBase64String(atlas.EncodeToPNG());
                for(int i=0;i<sheet.count;i++) atlas.SetPixels32((i%sheet.cols)*n,(rows-1-i/sheet.cols)*n,n,n,lightFrames[i]);
                atlas.Apply();sheet.light="data:image/png;base64,"+Convert.ToBase64String(atlas.EncodeToPNG());
            } finally {UnityEngine.Object.DestroyImmediate(atlas);}
            return sheet;
        } finally {depth.Use(false);camera.targetTexture=null;RenderTexture.active=saved;RenderTexture.ReleaseTemporary(rt);
            RenderTexture.ReleaseTemporary(cell);UnityEngine.Object.DestroyImmediate(sampling);
            UnityEngine.Object.DestroyImmediate(buffer);UnityEngine.Object.DestroyImmediate(go);}
    }
}
