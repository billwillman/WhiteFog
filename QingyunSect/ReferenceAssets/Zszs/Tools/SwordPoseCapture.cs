using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEditor;

// Read the source Humanoid pose through the animation system, including muscle curves.
public static class SwordPoseCapture {
    [Serializable] class Sample {public string clip; public int frames; public float rate; public float[] positions;}
    [Serializable] class Sequence {public string[] bones; public Sample[] clips;}
    static readonly HumanBodyBones[] Bones={HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand};
    public static void Run(){
        try{
            var root=Directory.GetParent(Application.dataPath).FullName;
            var models=AssetBundle.LoadFromFile(Path.Combine(root,"sword-model.ab"));
            var source=AssetBundle.LoadFromFile(Path.Combine(root,"sword-clips.ab"));
            var prefab=models.LoadAllAssets<GameObject>().First(x=>x.GetComponentInChildren<Animator>(true));
            var motions=new List<Sample>();
            foreach(var clip in source.LoadAllAssets<AnimationClip>().Where(c=>c.name.EndsWith("@jian_attack_1_yj")||c.name.EndsWith("@jian_liuxing_25120")||c.name.EndsWith("@jian_pokong_25090"))) {
                var actor=UnityEngine.Object.Instantiate(prefab);
                var animator=actor.GetComponentInChildren<Animator>(true);animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var graph=PlayableGraph.Create(); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var motion=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"source",animator);output.SetSourcePlayable(motion);
                graph.Play();graph.Evaluate(0);
                var bones=Bones.Select(id=>animator.GetBoneTransform(id)).ToArray();
                var frames=Mathf.RoundToInt(clip.length*clip.frameRate)+1;var positions=new float[frames*Bones.Length*3];
                for(int i=0;i<frames;i++){
                    motion.SetTime(Math.Min(i/clip.frameRate,clip.length));graph.Evaluate(0);
                    for(int k=0;k<bones.Length;k++){var v=actor.transform.InverseTransformPoint(bones[k].position);int j=(i*bones.Length+k)*3;positions[j]=v.x;positions[j+1]=v.y;positions[j+2]=v.z;}
                }
                motions.Add(new Sample{clip=clip.name,frames=frames,rate=clip.frameRate,positions=positions});Debug.Log("SWORD_POSE "+clip.name+" "+frames);
                graph.Destroy();UnityEngine.Object.DestroyImmediate(actor);
            }
            File.WriteAllText(Path.Combine(root,"sword-poses.json"),JsonUtility.ToJson(new Sequence{bones=Bones.Select(b=>b.ToString()).ToArray(),clips=motions.ToArray()}));
            EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
