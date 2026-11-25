using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ParticleReview {
    [Serializable] class Entry {public string key,main,mask,ramp,cut;public float[] tint,maskRGBA,uv,second;public float threshold,blend;public bool usable;}
    [Serializable] class Table {public Entry[] entries;}
    static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
    static Texture2D Load(string path){if(textures.TryGetValue(path,out var cached))return cached;var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(path));image.wrapMode=TextureWrapMode.Clamp;textures[path]=image;return image;}
    public static void Run() {
        try {
            string project=Directory.GetParent(Application.dataPath).FullName;
            string output=Path.Combine(project,"fx-frames");
            var entries=JsonUtility.FromJson<Table>(File.ReadAllText(Path.Combine(project,"fx-input.json"))).entries.GroupBy(x=>x.key).ToDictionary(g=>g.Key,g=>g.First());
            var bundles=new Dictionary<string, AssetBundle>();
            foreach(string file in Directory.GetFiles(Path.Combine(project,"fx-bundles"),"*.ab",SearchOption.AllDirectories)){
                var bundle=AssetBundle.LoadFromFile(file);
                if(bundle!=null)bundles[Path.GetFileNameWithoutExtension(file)]=bundle;
            }
            var shader=Shader.Find("Qingyun/ParticleReview");if(!shader||!shader.isSupported)throw new Exception("Review shader unavailable");
            var camera=new GameObject("Capture").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.transform.position=new Vector3(0,6,7);camera.transform.LookAt(Vector3.zero);camera.nearClipPlane=.01f;camera.farClipPlane=35;
            var rt=new RenderTexture(384,384,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
            var image=new Texture2D(384,384,TextureFormat.RGBA32,false);
            var selected=Environment.GetEnvironmentVariable("QY_REVIEW_GROUP");
            foreach(var group in (String.IsNullOrEmpty(selected)?new[]{"pugongjian","pokongjianfa","liuxingjianyu"}:new[]{selected})){
                var ab=bundles[group];
                foreach(var path in ab.GetAllAssetNames().Where(n=>n.EndsWith("/vfx_cha_buff_xinfa_hudun.prefab")||n.EndsWith("_hit.prefab")||n.EndsWith("_shifa01.prefab")||n.EndsWith("_trail01.prefab")||n.EndsWith("_shifa.prefab")||n.EndsWith("_trail.prefab"))){
                    string name=Path.GetFileNameWithoutExtension(path);string dir=Path.Combine(output,group=="buff"?"hudun":group,name);Directory.CreateDirectory(dir);
                    var actor=UnityEngine.Object.Instantiate(ab.LoadAsset<GameObject>(path));actor.SetActive(true);
                    var systems=actor.GetComponentsInChildren<ParticleSystem>(true);
                    var roots=systems.Where(ps=>!systems.Any(other=>other!=ps&&ps.transform.IsChildOf(other.transform))).ToArray();
                    foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true)){
                        var portable=new List<Material>();
                        foreach(var original in renderer.sharedMaterials){
                            var material=new Material(shader);
                            string key=name+"/"+renderer.gameObject.name.Replace("(Clone)","")+"/"+portable.Count;
                            if(entries.TryGetValue(key,out var e)&&e.usable){
                                material.mainTexture=Load(e.main);material.mainTextureScale=new Vector2(e.uv[0],e.uv[1]);material.mainTextureOffset=new Vector2(e.uv[2],e.uv[3]);
                                material.SetColor("_Tint", new Color(e.tint[0],e.tint[1],e.tint[2],e.tint[3]));material.SetFloat("_Dst",e.blend);
                                if(e.mask.Length>0){material.SetTexture("_Mask",Load(e.mask));material.SetVector("_MaskChannels",new Vector4(e.maskRGBA[0],e.maskRGBA[1],e.maskRGBA[2],e.maskRGBA[3]));material.SetFloat("_HasMask",1);}
                                material.SetColor("_Second",new Color(e.second[0],e.second[1],e.second[2],e.second[3]));
                                if(e.cut.Length>0){material.SetTexture("_Cut",Load(e.cut));material.SetFloat("_Threshold",e.threshold);material.SetFloat("_HasCut",1);}
                                if(e.ramp.Length>0){material.SetTexture("_Ramp",Load(e.ramp));material.SetFloat("_HasRamp",1);}
                            }else{Debug.LogWarning("Missing FX texture "+key);material.SetColor("_Tint",Color.clear);}
                            portable.Add(material);
                        }
                        renderer.sharedMaterials=portable.ToArray();
                    }
                    foreach(var ps in systems){ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(17+Array.IndexOf(systems,ps));}
                    for(int i=0;i<32;i++){
                        foreach(var ps in roots)ps.Simulate((i+.3f)/24f,true,true,false);
                        camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,384,384),0,0);image.Apply();
                        File.WriteAllBytes(Path.Combine(dir,i.ToString("D2")+".png"),image.EncodeToPNG());
                    }
                    Debug.Log("FX_REVIEW "+name+" emitters="+systems.Length);UnityEngine.Object.DestroyImmediate(actor);
                }
            }
            EditorApplication.Exit(0);
        }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
