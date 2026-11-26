using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace SoftKitty
{
    // Editor-only observation of one animator layer. Blending is one time interval shared by both states.
    internal sealed class SkillMotionTimeline
    {
        internal struct Band { public string name; public bool blend; public float start; }
        internal readonly List<Band> bands = new List<Band>();
        internal float seconds;
        internal string note;
        internal bool complete;

        internal string Description => string.Join(" → ", bands.Where(b => !b.blend).Select(b => b.name)) +
            (string.IsNullOrEmpty(note) ? "" : " · " + note);

        internal void Mark(float time, string name, bool blend)
        {
            seconds = time;
            if (bands.Count > 0 && bands[bands.Count - 1].name == name && bands[bands.Count - 1].blend == blend) return;
            bands.Add(new Band { name = name, start = time, blend = blend });
        }

        internal static SkillMotionTimeline Clip(AnimationClip clip)
        {
            var result = new SkillMotionTimeline();
            if (clip && clip.legacy) result.note = "Animator 无法播放 Legacy 片段";
            else if (clip) { result.Mark(0, clip.name, false); result.seconds = clip.length; result.complete = true; }
            else result.note = "未指定动画片段";
            return result;
        }

        internal static SkillMotionTimeline Sample(RuntimeAnimatorController runtime, AnimatorController controller, SkillMotion binding,
            Dictionary<string, object> parameters)
        {
            var result = new SkillMotionTimeline();
            if (!runtime || !controller) { result.note = "未找到参考 Controller"; return result; }
            var layers = controller.layers;
            int layer = Array.FindIndex(layers, l => l.name == binding.layer);
            if (layer < 0) { result.note = "找不到层 " + binding.layer; return result; }
            var data = layers[layer];
            var machine = data.syncedLayerIndex >= 0 ? layers[data.syncedLayerIndex].stateMachine : data.stateMachine;
            var states = SkillWorkbench.MotionStates(machine, "").ToDictionary(
                s => Animator.StringToHash(data.name + "." + s.path), s => s);
            int start = Animator.StringToHash(binding.StatePath);
            if (!states.ContainsKey(start)) { result.note = "找不到状态 " + binding.StatePath; return result; }

            // A controller behaviour can invoke game code or depend on the live scene. Observe it in Play instead.
            if (controller.GetBehaviours<StateMachineBehaviour>()?.Length > 0)
            { result.note = "状态使用运行时脚本，请查看实播"; return result; }

            var scene = EditorSceneManager.NewPreviewScene();
            var graph = default(PlayableGraph);
            try
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags("技能时间轴采样", HideFlags.HideAndDontSave, typeof(Animator));
                SceneManager.MoveGameObjectToScene(go, scene);
                var animator = go.GetComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.fireEvents = false;
                graph = PlayableGraph.Create("技能工作台动画时间轴");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var player = AnimatorControllerPlayable.Create(graph, runtime);
                AnimationPlayableOutput.Create(graph, "动画", animator).SetSourcePlayable(player);
                graph.Play();
                graph.Evaluate(0);
                foreach (var param in controller.parameters)
                {
                    if (!parameters.TryGetValue(param.name, out var value)) continue;
                    switch (param.type)
                    {
                        case AnimatorControllerParameterType.Bool when value is bool flag: player.SetBool(param.nameHash, flag); break;
                        case AnimatorControllerParameterType.Int when value is int number: player.SetInteger(param.nameHash, number); break;
                        case AnimatorControllerParameterType.Float when value is float scalar: player.SetFloat(param.nameHash, scalar); break;
                        case AnimatorControllerParameterType.Trigger when value is bool armed && armed: player.SetTrigger(param.nameHash); break;
                    }
                }
                player.CrossFadeInFixedTime(start, Mathf.Max(0, binding.transitionSeconds), layer, 0f);
                graph.Evaluate(0);
                const int rate = 120, limit = rate * 60;
                int lastHash = 0;
                float waitAt = 1;
                bool hasExit = false;
                for (int frame = 0; frame <= limit; frame++)
                {
                    float t = (float)frame / rate;
                    var now = player.GetCurrentAnimatorStateInfo(layer);
                    if (!states.TryGetValue(now.fullPathHash, out var current))
                    { result.note = "状态路径需实播确认"; break; }
                    bool blend = player.IsInTransition(layer);
                    var next = blend ? player.GetNextAnimatorStateInfo(layer) : default;
                    if (frame == 0 && !blend && now.fullPathHash != start)
                    {
                        graph.Evaluate(1f / rate);
                        continue;
                    }
                    string label = current.path;
                    if (blend && states.TryGetValue(next.fullPathHash, out var target))
                        label += " → " + target.path;
                    result.Mark(t, label, blend);
                    if (lastHash != now.fullPathHash)
                    {
                        lastHash = now.fullPathHash;
                        var exits = current.state.transitions.Where(tr => !tr.mute).ToArray();
                        hasExit = exits.Length > 0;
                        waitAt = Mathf.Max(1, exits.Where(tr => tr.hasExitTime).Select(tr => tr.exitTime).DefaultIfEmpty(1).Max());
                    }
                    if (!blend && now.loop && now.fullPathHash != start && current.state == machine.defaultState)
                    { result.complete = true; break; }
                    if (!blend && now.normalizedTime >= waitAt && frame > 0)
                    {
                        if (!now.loop && !hasExit) { result.complete = true; break; }
                        // Allow an exit at normalized time 1 to be consumed on the next evaluation.
                        if (now.normalizedTime > waitAt + .002f)
                        { result.note = hasExit ? "等待参数或 Trigger，结束时间待定" : "循环状态，结束时间待定"; break; }
                    }
                    if (!blend && frame > 1 && Mathf.Approximately(now.speed * now.speedMultiplier, 0))
                    { result.note = "状态暂停，结束时间待定"; break; }
                    if (frame == limit) { result.note = "60 秒预演范围内未结束"; break; }
                    graph.Evaluate(1f / rate);
                }
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return result;
        }
    }
}
