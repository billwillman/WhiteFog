using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Bake poses on the authoring machine so the H5 renderer needs neither an Animator nor a Unity player.
public static class LocalBattleActorExport
{
    private const string Art = "Assets/WhiteFog/OriginalSkills/";
    [Serializable] private class Actor { public List<Part> parts = new List<Part>(); public List<Clip> clips = new List<Clip>(); }
    [Serializable] private class Part { public string attachment, bone, uv, indices, texture; public float cutoff; }
    [Serializable] private class Anchor { public string name; public Vector3[] positions; }
    [Serializable] private class Clip { public Anchor[] anchors; public string name; public float duration; public int frames; public string[] vertices; }

    private static string Pack(short[] values)
    {
        byte[] bytes = new byte[values.Length * 2];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return Convert.ToBase64String(bytes);
    }

    // The hair texture stores colour regions. Render its Unity material into a diffuse plate.
    private static byte[] MaterialPixels(Material mat, Texture tex)
    {
        if (mat.shader.name != "WhiteFog/Original Hair Preview")
            return File.ReadAllBytes(AssetDatabase.GetAssetPath(tex));
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
        try {
            Graphics.Blit(tex,target,mat,0);
            RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);
            pixels.Apply();
            return pixels.EncodeToPNG();
        } finally {
            RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    [MenuItem("Tools/技能工作台/烘焙 H5 剑修战斗模型")]
    public static void Export()
    {
        Bake("男角色001", new[]{"chr_male_lod1@idle_yj","chr_male_001@jian_pokong_25090","SourceCombat/chr_male_001@jian_jichu_21020","SourceCombat/chr_male_001@jian_liuxing_25120","SourceCombat/Shaoyang/chr_male_001@zhi_shaoyang_22020","SourceCombat/补录/chr_male_001@jian_baihong_22030"}, true, "local-swordsman.js", "localSwordsman");
        Bake("SourceCombat/独臂木侠", new[]{"SourceCombat/boss_dubi@attidle","SourceCombat/boss_dubi@skill02_102412"}, false, "local-wooden-swordsman.js", "localWoodenSwordsman");
    }

    private static void Bake(string prefab, string[] animations, bool mounted, string file, string binding)
    {
        var actor = new Actor();
        var root = new GameObject("H5 骨骼采样");
        var mesh = new Mesh();
        try {
            GameObject Place(string name, Transform parent) => UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".prefab"), parent, false);
            var hero = Place(prefab, root.transform);
            var bones = hero.GetComponentsInChildren<Transform>(true);
            var anchors = bones.Where(b => new[] {"root","weapon_r","vfx_p_hit","Bip01 R Hand"}.Contains(b.name)).ToArray();
            var weapons = new List<GameObject>();
            if (mounted) {
                Place("御剑坐骑", root.transform);
                foreach (var bone in new[] {"Bip01 R Hand","weapon_r"})
                    weapons.Add(Place("eff_cha_JianPugong_Weapon", bones.Single(b => b.name == bone)));
            }
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            Mesh Source(Renderer r) => r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
            foreach (var r in renderers) {
                var src = Source(r);
                var mat = r.sharedMaterial;
                if (!src || !mat || src.subMeshCount != 1) throw new InvalidDataException("模型或材质缺失 " + r.name);
                var tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
                if (!tex) tex = mat.mainTexture;
                if (!tex) throw new InvalidDataException("缺少贴图 " + mat.name);
                actor.parts.Add(new Part {
                    attachment = weapons.Any(w => r.transform.IsChildOf(w.transform)) ? "eff_cha_JianPugong_Weapon" : "",
                    bone = weapons.FirstOrDefault(w => r.transform.IsChildOf(w.transform))?.transform.parent.name,
                    texture = "data:image/png;base64," + Convert.ToBase64String(MaterialPixels(mat,tex)),
                    cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : .01f,
                    uv = Pack(src.uv.SelectMany(v => new[]{(short)Mathf.RoundToInt(v.x*4096), (short)Mathf.RoundToInt(v.y*4096)}).ToArray()),
                    indices = Pack(src.triangles.Select(i => unchecked((short)checked((ushort)i))).ToArray())
                });
            }
            foreach (string name in animations) {
                var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + name + ".anim");
                int count = Mathf.CeilToInt(source.length * 24) + 1;
                var data = new Clip { name=source.name, anchors=anchors.Select(b => new Anchor {name=b.name, positions=new Vector3[count]}).ToArray(), frames=count, duration=source.length, vertices=new string[renderers.Length] };
                var streams = renderers.Select(r => new List<short>()).ToArray();
                for (int frame=0; frame<count; frame++) {
                    source.SampleAnimation(hero, source.length*frame/(count-1));
                    for (int a=0; a<anchors.Length; a++) data.anchors[a].positions[frame]=anchors[a].position;
                    for (int i=0; i<renderers.Length; i++) {
                        var r = renderers[i];
                        bool animated = r is SkinnedMeshRenderer || weapons.Any(w => r.transform.IsChildOf(w.transform));
                        if (frame > 0 && !animated) continue;
                        var src = Source(r);
                        if (r is SkinnedMeshRenderer sk) { sk.BakeMesh(mesh); src=mesh; }
                        foreach (var v in src.vertices) {
                            var p = r.transform.TransformPoint(v);
                            for (int axis=0; axis<3; axis++) streams[i].Add(checked((short)Mathf.RoundToInt(p[axis]*2048)));
                        }
                    }
                }
                for (int i=0; i<renderers.Length; i++) data.vertices[i]=Pack(streams[i].ToArray());
                actor.clips.Add(data);
            }
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QingyunSect/LoginDemo/battle-art");
            Directory.CreateDirectory(folder);
            using (var bytes = new MemoryStream()) {
                using (var zip = new GZipStream(bytes, CompressionMode.Compress, true)) {
                    var json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(actor));
                    zip.Write(json,0,json.Length);
                }
                File.WriteAllText(Path.Combine(folder,file),
                    "window." + binding + " = \"" + Convert.ToBase64String(bytes.ToArray()) + "\";\n",new UTF8Encoding(false));
            }
        } finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(root); }
    }
}
